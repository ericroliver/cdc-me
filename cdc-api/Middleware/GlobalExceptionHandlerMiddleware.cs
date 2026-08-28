using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Softbase.Cdc.Factory.Engine;
using Microsoft.Data.SqlClient;

namespace cdc_api.Middleware;

/// <summary>
/// Global exception handler middleware.
/// Catches unhandled exceptions and returns clean error responses
/// without leaking internal details (stack traces, DB schema, file paths).
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NpgsqlException ex)
        {
            _logger.LogError(ex, "Database error during {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteError(context, HttpStatusCode.BadRequest,
                "A database error occurred. Please verify your request parameters.");
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL Server error during {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteError(context, HttpStatusCode.BadRequest,
                "A database error occurred. Please verify your request parameters.");
        }
        catch (ArgumentException ex)
        {
            // Argument exceptions are expected validation errors — safe to return the message
            _logger.LogWarning(ex, "Invalid argument during {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteError(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found during {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteError(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (ReferencedByOrdersException ex)
        {
            _logger.LogWarning(ex, "Referenced entity conflict during {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteError(context, HttpStatusCode.Conflict, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // These are often business-logic errors (e.g., "Connection not found")
            // but can also indicate bugs — log as warning and return 400
            _logger.LogWarning(ex, "Invalid operation during {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteError(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteError(context, HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later.");
        }
    }

    private async Task WriteError(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        // Only include exception details in Development — never in Production
        var error = new { error = message };
        await context.Response.WriteAsJsonAsync(error);
    }
}
