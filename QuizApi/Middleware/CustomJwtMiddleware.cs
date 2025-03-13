using Jose;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text;

public class CustomJwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CustomJwtMiddleware> _logger;
    private readonly string _secretKey;

    public CustomJwtMiddleware(RequestDelegate next, ILogger<CustomJwtMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _secretKey = configuration["Jwt:Key"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var secretKeyBytes = Encoding.UTF8.GetBytes(_secretKey);
                var claims = JWT.Decode<Dictionary<string, object>>(token, secretKeyBytes, JwsAlgorithm.HS256);

                var claimsIdentity = new ClaimsIdentity(claims.Select(c => new Claim(c.Key, c.Value.ToString())), "jwt");
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                context.User = claimsPrincipal;

                _logger.LogInformation("JWT token validated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"JWT validation failed: {ex.Message}");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid or expired JWT token.");
                return;
            }
        }
        else
        {
            _logger.LogWarning("No JWT token found in the request.");
        }

        await _next(context);
    }
}

public static class CustomJwtMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomJwtMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CustomJwtMiddleware>();
    }
}
