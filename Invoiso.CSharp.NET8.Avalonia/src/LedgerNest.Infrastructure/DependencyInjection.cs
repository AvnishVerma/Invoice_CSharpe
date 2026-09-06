using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerNest.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string databasePath)
    {
        services.AddDbContextFactory<LedgerNestDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        return services;
    }
}
