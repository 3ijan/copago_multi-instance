using ArticleService.APIs;
using System.Reflection;

namespace ArticleService.Extensions;

/// <summary>
/// Extension methods for registering endpoints
/// Provides automatic discovery and registration of IEndpointExtension implementations
/// </summary>
public static class EndpointRegistrationExtension
{
    /// <summary>
    /// Registers all endpoints that implement IEndpointExtension
    /// Uses reflection to automatically discover endpoint classes
    /// </summary>
    /// <param name="app">WebApplication instance</param>
    /// <returns>WebApplication for method chaining</returns>
    /// <example>
    /// Usage in Program.cs:
    /// <code>
    /// app.RegisterEndpoints();
    /// </code>
    /// </example>
    public static WebApplication RegisterEndpoints(this WebApplication app)
    {
        var endpointTypes = DiscoverEndpointTypes();

        if (endpointTypes.Count == 0)
        {
            Console.WriteLine("⚠️ Warning: No endpoint types found implementing IEndpointExtension");
            return app;
        }

        Console.WriteLine($"✅ Registering {endpointTypes.Count} endpoint group(s):\n");

        foreach (var type in endpointTypes)
        {
            MapEndpointType(app, type);
        }

        Console.WriteLine($"✅ All endpoints registered successfully\n");

        return app;
    }

    /// <summary>
    /// Discovers all types in the current assembly that implement IEndpointExtension
    /// </summary>
    /// <returns>List of types implementing IEndpointExtension</returns>
    private static List<Type> DiscoverEndpointTypes()
    {
        var assembly = Assembly.GetExecutingAssembly();

        return assembly.GetTypes()
            .Where(t => t.IsClass &&
                       !t.IsAbstract &&
                       typeof(IEndpointExtension).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();
    }

    /// <summary>
    /// Maps endpoints for a specific endpoint type
    /// Creates an instance and invokes MapEndpoints method
    /// </summary>
    /// <param name="app">WebApplication instance</param>
    /// <param name="type">Type implementing IEndpointExtension</param>
    private static void MapEndpointType(WebApplication app, Type type)
    {
        try
        {
            // Create instance of the endpoint class
            var instance = Activator.CreateInstance(type) as IEndpointExtension;

            if (instance == null)
            {
                Console.WriteLine($"✗ {type.Name} - Failed to create instance");
                return;
            }

            // Call MapEndpoints method
            instance.MapEndpoints(app);
            Console.WriteLine($"✓ {type.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ {type.Name} - Error: {ex.Message}");
        }
    }
}
