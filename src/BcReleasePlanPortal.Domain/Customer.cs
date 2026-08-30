namespace BcReleasePlanPortal.Domain;

/// <summary>
/// Design doc §5.4. The profile fields that drive match scoring (modules in use, AppSource
/// apps, custom extensions, integrations, flags) are stored as JSON-serialized collections
/// via <c>BcReleasePlanDbContext</c> value converters rather than normalized tables — this data
/// is maintained by hand for the MVP (doc §5.4) and read as a whole per customer, so a
/// document column is the right shape until phase 7 (profile automation) needs to query into it.
/// Not exercised by the ingest pipeline yet — schema only, for the match engine and curation UI phases.
/// </summary>
public class Customer
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? TenantId { get; set; }

    public List<CustomerEnvironment> Environments { get; set; } = [];

    public List<string> ModulesInUse { get; set; } = [];

    public List<CustomerAppSourceApp> AppSourceApps { get; set; } = [];

    public List<CustomerExtension> CustomExtensions { get; set; } = [];

    public CustomerIntegrations Integrations { get; set; } = new();

    public CustomerFlags Flags { get; set; } = new();

    public List<CustomerContact> Contacts { get; set; } = [];

    public string ReviewCadence { get; set; } = "quarterly";

    public List<CustomerItem> Items { get; set; } = [];
}

public class CustomerEnvironment
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateOnly? ScheduledUpdate { get; set; }
    public DateOnly? UpdateWindowEnds { get; set; }
    public string Country { get; set; } = string.Empty;
}

public class CustomerAppSourceApp
{
    public string Publisher { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
}

public class CustomerExtension
{
    public string Name { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public List<string> ExtendsObjects { get; set; } = [];
}

public class CustomerIntegrations
{
    public bool ODataV4 { get; set; }
    public bool Soap { get; set; }
    public List<string> SoapEndpoints { get; set; } = [];
    public bool ApiV2 { get; set; }
    public bool Webhooks { get; set; }
    public bool PowerAutomate { get; set; }
}

public class CustomerFlags
{
    public bool UsesCopilot { get; set; }
    public bool MultiCompany { get; set; }
    public bool HasTestEnvironment { get; set; }
}

public class CustomerContact
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// The contact's own preferred language, as real-world data about that person — independent of
    /// the tool's working language, which is English throughout. Nothing reads this yet; it exists
    /// so the information isn't lost if a translated document is ever offered.
    /// </summary>
    public string Language { get; set; } = "en";
}
