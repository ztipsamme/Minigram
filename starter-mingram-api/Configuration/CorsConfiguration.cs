using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

static class CorsConfiguration
{
    internal static IServiceCollection AddMinGramCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("MinGramPolicy", policy =>
            {
                var origins = configuration
                    .GetSection("AllowedOrigins")
                    .Get<string[]>() ?? [];

                policy.WithOrigins(origins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
