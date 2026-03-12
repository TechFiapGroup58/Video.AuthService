using System.Net;
using System.Text.Json;
using AuthService.Domain.Exceptions;

namespace AuthService.API.Middlewares;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate             _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next   = next;
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
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await WriteErrorAsync(context, ex);
        }
    }

    private static Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            UserAlreadyExistsException  => (HttpStatusCode.Conflict,           "Conflito"),
            InvalidCredentialsException => (HttpStatusCode.Unauthorized,       "Não autorizado"),
            DomainException             => (HttpStatusCode.BadRequest,         "Requisição inválida"),
            _                           => (HttpStatusCode.InternalServerError, "Erro interno")
        };

        context.Response.StatusCode  = (int)status;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type    = $"https://httpstatuses.com/{(int)status}",
            title,
            status  = (int)status,
            detail  = exception.Message,
            traceId = context.TraceIdentifier
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
