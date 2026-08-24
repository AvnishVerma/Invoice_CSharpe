using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Invoiso.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string databasePath)
    {
        services.AddDbContextFactory<InvoisoDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        return services;
    }
}
