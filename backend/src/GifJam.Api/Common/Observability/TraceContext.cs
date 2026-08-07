using System.Diagnostics;

namespace GifJam.Api.Common.Observability;

public static class TraceContext
{
    public static string GetTraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
}
