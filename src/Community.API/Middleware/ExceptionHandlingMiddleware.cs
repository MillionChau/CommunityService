using System.Net;
using System.Text.Json;
using Community.Application.Common.Exceptions;
using Community.Application.Common.Models;

namespace Community.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception has occurred.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var responseCode = HttpStatusCode.InternalServerError;
        var message = exception.Message;
        object? errors = null;

        switch (exception)
        {
            case ValidationException valEx:
                responseCode = HttpStatusCode.BadRequest;
                message = "Validation failures occurred.";
                errors = valEx.Errors;
                break;
            case NotFoundException:
                responseCode = HttpStatusCode.NotFound;
                break;
            case ForbiddenException:
                responseCode = HttpStatusCode.Forbidden;
                break;
            case UnauthorizedAccessException:
                responseCode = HttpStatusCode.Unauthorized;
                break;
        }

        context.Response.StatusCode = (int)responseCode;

        var resultModel = new ResponseModel<object>
        {
            IsSuccess = false,
            Code = (int)responseCode,
            Message = message,
            Data = errors
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return context.Response.WriteAsync(JsonSerializer.Serialize(resultModel, options));
    }
}
