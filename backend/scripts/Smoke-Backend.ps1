param(
    [Parameter(Mandatory = $true)]
    [uri]$BaseUrl,

    [string]$AccessToken
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$apiBaseUrl = $BaseUrl.AbsoluteUri.TrimEnd('/')
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.UseProxy = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(10)

function Assert-SuccessResponse {
    param(
        [Parameter(Mandatory = $true)]
        [System.Net.Http.HttpResponseMessage]$Response,

        [Parameter(Mandatory = $true)]
        [string]$Check
    )

    if (-not $Response.IsSuccessStatusCode) {
        throw "$Check failed with HTTP $([int]$Response.StatusCode)."
    }
}

try {
    $live = $client.GetAsync("$apiBaseUrl/health/live").GetAwaiter().GetResult()
    Assert-SuccessResponse -Response $live -Check 'Liveness check'
    $ready = $client.GetAsync("$apiBaseUrl/health/ready").GetAwaiter().GetResult()
    Assert-SuccessResponse -Response $ready -Check 'Readiness check'

    $oauth = $client.GetAsync("$apiBaseUrl/api/auth/discord/start").GetAwaiter().GetResult()
    if ([int]$oauth.StatusCode -notin 302, 303, 307) {
        throw "Discord OAuth start failed with HTTP $([int]$oauth.StatusCode)."
    }
    if ($oauth.Headers.Location.Host -ne 'discord.com') {
        throw 'Discord OAuth start did not redirect to discord.com.'
    }

    $rooms = $client.GetAsync("$apiBaseUrl/api/rooms/public?pageSize=5").GetAwaiter().GetResult()
    Assert-SuccessResponse -Response $rooms -Check 'Public room directory check'

    $directoryNegotiateContent = [System.Net.Http.StringContent]::new('')
    $directoryNegotiate = $client.PostAsync(
        "$apiBaseUrl/hubs/rooms/negotiate?negotiateVersion=1",
        $directoryNegotiateContent).GetAwaiter().GetResult()
    Assert-SuccessResponse -Response $directoryNegotiate -Check 'Public room directory SignalR negotiate check'

    if ([string]::IsNullOrWhiteSpace($AccessToken)) {
        Write-Output 'Public smoke checks passed. Provide -AccessToken to test authentication, room creation and game SignalR.'
        return
    }

    $client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $AccessToken)
    $me = $client.GetAsync("$apiBaseUrl/api/auth/me").GetAwaiter().GetResult()
    Assert-SuccessResponse -Response $me -Check 'Authenticated profile check'

    $gameContent = [System.Net.Http.StringContent]::new(
        '{"totalRounds":3}',
        [System.Text.Encoding]::UTF8,
        'application/json')
    $gameResponse = $client.PostAsync("$apiBaseUrl/api/games", $gameContent).GetAwaiter().GetResult()
    Assert-SuccessResponse -Response $gameResponse -Check 'Room creation check'
    $game = $gameResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json

    $negotiateContent = [System.Net.Http.StringContent]::new('')
    $negotiate = $client.PostAsync(
        "$apiBaseUrl/hubs/game/negotiate?negotiateVersion=1",
        $negotiateContent).GetAwaiter().GetResult()
    Assert-SuccessResponse -Response $negotiate -Check 'SignalR negotiate check'
    $connection = $negotiate.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json

    $hubUri = [uri]$apiBaseUrl
    $webSocketScheme = if ($hubUri.Scheme -eq 'https') { 'wss' } else { 'ws' }
    $connectionToken = [uri]::EscapeDataString($connection.connectionToken)
    $encodedAccessToken = [uri]::EscapeDataString($AccessToken)
    $webSocketUri = [uri]"${webSocketScheme}://$($hubUri.Authority)/hubs/game?id=$connectionToken&access_token=$encodedAccessToken"
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $socketTimeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
    try {
        $socket.ConnectAsync($webSocketUri, $socketTimeout.Token).GetAwaiter().GetResult()
        $handshake = [System.Text.Encoding]::UTF8.GetBytes("{`"protocol`":`"json`",`"version`":1}$([char]0x1e)")
        $socket.SendAsync(
            [ArraySegment[byte]]::new($handshake),
            [System.Net.WebSockets.WebSocketMessageType]::Text,
            $true,
            $socketTimeout.Token).GetAwaiter().GetResult()
        $buffer = [byte[]]::new(1024)
        $received = $socket.ReceiveAsync(
            [ArraySegment[byte]]::new($buffer),
            $socketTimeout.Token).GetAwaiter().GetResult()
        $handshakeResponse = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $received.Count)
        if (-not $handshakeResponse.EndsWith([char]0x1e)) {
            throw 'SignalR handshake returned an invalid frame.'
        }
    }
    finally {
        $socketTimeout.Dispose()
        $socket.Dispose()
    }

    Write-Output "Authenticated smoke checks passed; room $($game.lobby.code) was created."
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
