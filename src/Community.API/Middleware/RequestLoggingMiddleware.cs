using System.Diagnostics;

namespace Community.API.Middleware;

/// <summary>
/// Ghi log console mỗi request: method, path, status, thời gian phản hồi (ms),
/// định danh người gọi và trace id — format đồng bộ với GatewayService
/// để soi chéo một request xuyên qua hệ thống bằng trace id.
///
/// Đặt ĐẦU pipeline (trước exception handler) để bắt mọi request.
/// Mức log: >= 500 Error · >= 400 Warning · còn lại Information.
/// Bỏ qua /healthz và /swagger* để không làm nhiễu console.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSkipped(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        var caller = DescribeCaller(context);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            _logger.LogError(ex,
                "[community] {Method} {Path} -> 500 ({Elapsed:F1} ms) {Caller} trace={TraceId} UNHANDLED_EXCEPTION",
                context.Request.Method,
                context.Request.Path,
                elapsedMs,
                caller,
                GetTraceId(context));
            throw;
        }

        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var statusCode = context.Response.StatusCode;

        var message =
            $"[community] {context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> " +
            $"{statusCode} ({elapsed:F1} ms) {caller} trace={GetTraceId(context)}";

        if (statusCode >= 500)
        {
            _logger.LogError("{Message}", message);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning("{Message}", message);
        }
        else
        {
            _logger.LogInformation("{Message}", message);
        }
    }

    private static bool IsSkipped(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Equals("/healthz", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ai đang gọi: sub từ JWT claims nếu có, fallback về IP cho guest.</summary>
    private static string DescribeCaller(HttpContext context)
    {
        var sub = context.User.FindFirst("sub")?.Value
               ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(sub))
        {
            return $"user={sub}";
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip={ip}";
    }

    private static string GetTraceId(HttpContext context)
        => Activity.Current?.Id ?? context.TraceIdentifier;
}
