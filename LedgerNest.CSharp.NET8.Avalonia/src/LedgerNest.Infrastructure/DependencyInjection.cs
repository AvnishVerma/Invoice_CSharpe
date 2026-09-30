using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerNest.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string databasePath) =>
        services.AddInfrastructure(DatabaseProfile.LocalSqlite(databasePath));

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, DatabaseProfile profile)
    {
        services.AddDbContextFactory<LedgerNestDbContext>(options =>
        {
            switch (profile.Provider)
            {
                case DatabaseProvider.Sqlite:
                    options.UseSqlite(profile.ConnectionString);
                    break;
                case DatabaseProvider.MySql:
                    options.UseMySQL(profile.ConnectionString);
                    break;
                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(profile.ConnectionString, sql => sql.EnableRetryOnFailure());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile.Provider));
            }
        });
        services.AddSingleton<NumberSeriesService>();
        services.AddSingleton<UnitMasterService>();
        services.AddSingleton<ProductPricingService>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<InvoiceRefundService>();
        services.AddSingleton<AuthorizationService>();
        services.AddSingleton<QuotationService>();
        return services;
    }
}
