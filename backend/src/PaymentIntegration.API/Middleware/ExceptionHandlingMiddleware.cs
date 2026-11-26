using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Domain.Exceptions;

namespace PaymentIntegration.API.Middleware;

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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled error processing request: {Path}", context.Request.Path);

        var statusCode = HttpStatusCode.InternalServerError;
        var title = "An unexpected error occurred.";
        var detail = exception.Message;
        IDictionary<string, string[]>? errors = null;

        switch (exception)
        {
            case ValidationException validationEx:
                statusCode = HttpStatusCode.BadRequest;
                title = "Validation Failed";
                detail = "One or more validation errors occurred.";
                errors = validationEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                break;

            case ArgumentException argEx:
                statusCode = HttpStatusCode.BadRequest;
                title = "Bad Request";
                detail = argEx.Message;
                break;

            case CustomerNotFoundException or ProductNotFoundException or CustomerCardNotFoundException or TransactionNotFoundException:
                statusCode = HttpStatusCode.NotFound;
                title = "Resource Not Found";
                detail = exception.Message;
                break;

            case IdempotencyConflictException conflictEx:
                statusCode = HttpStatusCode.Conflict;
                title = "Checkout In Progress";
                detail = conflictEx.Message;
                break;

            case InvalidPaymentOperationException invalidEx:
                statusCode = HttpStatusCode.UnprocessableEntity;
                title = "Invalid Payment Operation";
                detail = invalidEx.Message;
                break;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        if (errors != null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
