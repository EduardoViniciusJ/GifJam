using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GifJam.Api.Tests.Infrastructure;

public sealed class GifJamApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discord:ClientId"] = "test-client-id",
                ["Discord:ClientSecret"] = "test-client-secret",
                ["Discord:CallbackUrl"] = "https://localhost/api/auth/discord/callback",
                ["Jwt:SigningKey"] = new string('t', 64),
                ["Klipy:ApiKey"] = "test-klipy-key",
                ["ApplicationUrls:FrontendUrl"] = "https://frontend.test"
            }));
    }
}
