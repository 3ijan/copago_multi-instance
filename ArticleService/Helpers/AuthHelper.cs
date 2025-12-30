namespace ArticleService.Helpers;

public static class AuthHelper
{
    /// <summary>
    /// Extracts customer ID from JWT token in HttpContext
    /// </summary>
    public static int GetCustomerId(HttpContext context)
    {
        if (context.Items.TryGetValue("CustomerId", out var customerId))
            return (int)customerId;

        throw new UnauthorizedAccessException("Invalid or missing JWT token");
    }

    /// <summary>
    /// Validates if customer ID exists in context
    /// </summary>
    public static bool TryGetCustomerId(HttpContext context, out int customerId)
    {
        customerId = 0;
        if (context.Items.TryGetValue("CustomerId", out var id))
        {
            customerId = (int)id;
            return true;
        }
        return false;
    }
}
