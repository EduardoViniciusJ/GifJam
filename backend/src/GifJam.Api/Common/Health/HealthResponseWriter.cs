using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GifJam.Api.Common.Health;

public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString()
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }
}
