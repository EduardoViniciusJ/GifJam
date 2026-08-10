using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using GifJam.Api.Common.Observability;

namespace GifJam.Api.Common.Errors;

public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var apiException = exception as ApiException;
        var statusCode = apiException?.StatusCode ?? StatusCodes.Status500InternalServerError;
        var code = apiException?.Code ?? "internal_error";
        const string detail = "The request could not be completed.";

        var traceId = TraceContext.GetTraceId(httpContext);
        if (apiException is null)
        {
            LogUnhandledRequest(logger, exception, traceId);
        }
        else
        {
            LogRejectedRequest(logger, code, traceId);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new()
            {
                Status = statusCode,
                Title = ReasonPhrases.GetReasonPhrase(statusCode),
                Detail = detail,
                Extensions =
                {
                    ["code"] = code,
                    ["traceId"] = traceId
                }
            }
        });
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled request failure with trace {TraceId}")]
    private static partial void LogUnhandledRequest(ILogger logger, Exception exception, string traceId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Request rejected with code {Code} and trace {TraceId}")]
    private static partial void LogRejectedRequest(ILogger logger, string code, string traceId);
}
