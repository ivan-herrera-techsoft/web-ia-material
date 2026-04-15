using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Techsoft.Sima.Web.Api.Resources;
using Techsoft.Sima.Web.Shared.Exceptions;
using Techsoft.Sima.Web.Shared.Logging;

namespace Techsoft.Sima.Web.Api.Middleware;

public class ErrorHandlerMiddleware
{
    private readonly LoggerWrapper<ErrorHandlerMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly IStringLocalizer _localizer;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ErrorHandlerMiddleware(
        LoggerWrapper<ErrorHandlerMiddleware> logger,
        RequestDelegate next,
        IStringLocalizerFactory factory)
    {
        _logger = logger;
        _next = next;
        _localizer = factory.Create(typeof(SharedResources));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("response has already started"))
        {
            _logger.LogDebug("Response has already started, ignoring: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            await HandleException(context, ex);
        }
    }

    private Task HandleException(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var error = new ErrorObjectResponse();
        IEnumerable<object?> additionalData = [];

        switch (exception)
        {
            case TInvalidOperationException ex:
                _logger.LogWarning(ex, "Operación no permitida [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;
            case TArgumentException ex:
                _logger.LogWarning(ex, "Error de argumento [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;
            case TNotFoundException ex:
                _logger.LogWarning(ex, "No encontrado [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                break;
            case TUnauthorizedAccessException ex when ex.Code.Contains("UA0002"):
                _logger.LogWarning(ex, "Permisos insuficientes [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                break;
            case TUnauthorizedAccessException ex:
                _logger.LogWarning(ex, "No autorizado [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                break;
            case BadHttpRequestException ex:
                _logger.LogWarning(ex, "Solicitud incorrecta: {Message}.", ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;
            default:
                _logger.LogError(exception, "Error no controlado: {Message}.", exception.Message);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        if (exception is TException customException)
        {
            error.Code = customException.Code;
            additionalData = customException.Args?.Values.Cast<object?>() ?? [];
            var template = _localizer[customException.Code];
            var detail = template.Value;
            if (customException.Args != null)
                foreach (var kv in customException.Args)
                    detail = detail.Replace($"{{{kv.Key}}}", kv.Value?.ToString());
            error.Message = $"WS: {detail}";
        }
        else
        {
            error.Code = "ErrorInterno";
            error.Message = $"WS: {_localizer["ErrorInterno"].Value}";
        }

        error.Data = additionalData;

        var response = new ExceptionResponse
        {
            StatusCode = context.Response.StatusCode,
            IsError = true,
            Error = error
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
    }
}

public class ErrorObjectResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IEnumerable<object?> Data { get; set; } = [];
}

public class ExceptionResponse
{
    public int StatusCode { get; set; } = 500;
    public bool IsError { get; set; } = true;
    public required ErrorObjectResponse Error { get; set; }
}
