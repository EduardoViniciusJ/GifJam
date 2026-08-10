function Import-EnvironmentFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing environment file: $Path"
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmedLine = $line.Trim()
        if ($trimmedLine.Length -eq 0 -or $trimmedLine.StartsWith('#')) {
            continue
        }

        $separatorIndex = $trimmedLine.IndexOf('=')
        if ($separatorIndex -le 0) {
            continue
        }

        $name = $trimmedLine.Substring(0, $separatorIndex)
        $value = $trimmedLine.Substring($separatorIndex + 1)
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}
