using System.IdentityModel.Tokens.Jwt;

namespace ArticleService.Middleware;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtMiddleware> _logger;

    public JwtMiddleware(RequestDelegate next, ILogger<JwtMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (authHeader?.StartsWith("Bearer ") == true)
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

                if (jwtToken != null)
                {
                    var customerNumberClaim = jwtToken.Claims
                        .FirstOrDefault(c => c.Type == "Kundennummer");

                    if (customerNumberClaim != null &&
                        int.TryParse(customerNumberClaim.Value, out var customerId))
                    {
                        context.Items["CustomerId"] = customerId;
                        _logger.LogInformation("JWT validated for customer {CustomerId}", customerId);
                    }
                    else
                    {
                        _logger.LogWarning("Kundennummer claim not found or invalid in JWT");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing JWT token");
            }
        }

        await _next(context);
    }
}
