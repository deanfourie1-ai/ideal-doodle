using BcReleasePlanPortal.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BcReleasePlanPortal.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBcReleasePlanData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BcReleasePlan") ?? "Data Source=bcreleaseplan.db";

        // Options are singleton so the factory (used by interactive UI components, which outlive a
        // request) and the scoped context (used by ingest) can share them.
        services.AddDbContext<BcReleasePlanDbContext>(options => options.UseSqlite(connectionString), optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<BcReleasePlanDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IRoadmapItemStore, RoadmapItemStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RoadmapTriageService>();
        services.AddSingleton<CustomerBoardService>();
        services.AddSingleton<ImpactNoteService>();
        services.AddSingleton<CustomerProfileService>();

        return services;
    }
}
