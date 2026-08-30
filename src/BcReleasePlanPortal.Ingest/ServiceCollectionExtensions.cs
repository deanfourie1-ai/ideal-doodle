using BcReleasePlanPortal.Ingest.Alerts;
using BcReleasePlanPortal.Ingest.Configuration;
using BcReleasePlanPortal.Ingest.Learn;
using BcReleasePlanPortal.Ingest.Mcp;
using BcReleasePlanPortal.Ingest.Normalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BcReleasePlanPortal.Ingest;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoadmapIngest(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RoadmapIngestOptions>()
            .Bind(configuration.GetSection(RoadmapIngestOptions.SectionName));

        services.AddHttpClient<McpJsonRpcClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<RoadmapIngestOptions>>().Value;
            client.BaseAddress = new Uri(options.McpEndpoint);
        });

        services.AddHttpClient<TeamsWebhookAlertSink>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<RoadmapIngestOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.TeamsWebhookUrl))
            {
                client.BaseAddress = new Uri(options.TeamsWebhookUrl);
            }
        });

        services.AddSingleton<IRoadmapMcpClient, RoadmapMcpClient>();
        services.AddSingleton<IChangeClassifier, RuleBasedChangeClassifier>();
        services.AddSingleton<IModuleClassifier, RuleBasedModuleClassifier>();
        services.AddSingleton<RoadmapItemNormalizer>();
        services.AddSingleton<ILearnPageSource, UnavailableLearnPageSource>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        services.AddSingleton<IIngestAlertSink>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RoadmapIngestOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.TeamsWebhookUrl)
                ? sp.GetRequiredService<NullAlertSink>()
                : sp.GetRequiredService<TeamsWebhookAlertSink>();
        });
        services.AddSingleton<NullAlertSink>();

        // Scoped, not Singleton: it depends on IRoadmapItemStore, which is backed by a
        // per-operation EF Core DbContext (BcReleasePlanPortal.Data registers it Scoped).
        // The daily background service creates a DI scope for each ingest run.
        services.AddScoped<RoadmapIngestService>();

        return services;
    }
}
