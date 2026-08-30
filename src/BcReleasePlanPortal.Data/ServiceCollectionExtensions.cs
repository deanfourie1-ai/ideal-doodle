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

        services.AddDbContext<BcReleasePlanDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IRoadmapItemStore, RoadmapItemStore>();

        return services;
    }
}
