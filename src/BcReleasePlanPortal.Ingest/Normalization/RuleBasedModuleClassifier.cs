namespace BcReleasePlanPortal.Ingest.Normalization;

/// <summary>
/// Keyword-per-module lookup against the BC module taxonomy from design doc §5.1. ("Localisation-NL"
/// is one of Microsoft's own BC modules — the Dutch localisation of the product — not a statement
/// about this tool's language, which is English throughout.) Multiple
/// modules can match the same item. When nothing matches, the item comes back with an empty
/// module list and <c>Confident: false</c> — the normalizer flags it <c>NeedsConfirmation</c>
/// rather than guessing a module. Keyword sets are intentionally conservative (BC domain terms,
/// not generic English words) since a false module tag is worse than none: it would route an
/// item to the wrong customers' triage queues.
/// </summary>
public sealed class RuleBasedModuleClassifier : IModuleClassifier
{
    private static readonly Dictionary<string, string[]> ModuleKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Finance"] = ["general ledger", "bank reconciliation", "bank rec", "chart of accounts", "financial report", "currency", "vat", "fixed asset", "cash flow", "budget", "audit trail"],
        ["Sales"] = ["sales order", "sales quote", "sales invoice", "customer", "crm", "sales price"],
        ["Purchasing"] = ["purchase order", "purchase invoice", "vendor", "requisition", "approval workflow"],
        ["Warehouse"] = ["warehouse", "license plate", "bin", "put-away", "pick", "inventory"],
        ["Manufacturing"] = ["manufactur", "production order", "routing", "bill of material", "bom", "capacity planning"],
        ["Projects"] = ["project", "job planning", "job ledger", "time sheet"],
        ["Service"] = ["service order", "service item", "service contract", "field service"],
        ["Reporting"] = ["report", "statistics", "power bi", "analytics"],
        ["Dev/API"] = ["api", "odata", "soap", "extension", "al language", "codeunit", "web service", "webhook"],
        ["Admin"] = ["admin center", "tenant", "environment", "feature management", "feature key", "user management"],
        ["Localisation-NL"] = ["netherlands", "dutch", "nl localiz", "sepa", "btw"],
    };

    public ModuleClassification Classify(string title, string description, IReadOnlyCollection<string> microsoftProductTags)
    {
        var haystack = $"{title}\n{description}\n{string.Join('\n', microsoftProductTags)}";

        var matches = ModuleKeywords
            .Where(kv => kv.Value.Any(keyword => haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .Select(kv => kv.Key)
            .ToList();

        return new ModuleClassification(matches, Confident: matches.Count > 0);
    }
}
