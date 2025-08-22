namespace WebServicesApi.Middleware;
public class GlobalErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalErrorHandlingMiddleware> _logger;

    public GlobalErrorHandlingMiddleware(RequestDelegate next, ILogger<GlobalErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context); // Call the next middleware
        }
        catch (Exception ex)
        {
            // Log the exception
            _logger.LogError(ex, "Unhandled exception occurred");

            // Return a custom error response
            context.Response.StatusCode = 500; // Internal Server Error
            context.Response.ContentType = "application/json";

            var response = new
            {
                message = "An unexpected error occurred.",
                StatusCode = 500,
                Status = "Failed"
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}

