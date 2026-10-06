using System.Text.RegularExpressions;

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
/// <para>
/// Keywords match whole words, allowing plural/verb endings ("report" matches "reports" and
/// "reporting"); a trailing <c>*</c> marks a stem that may continue into any word ("manufactur*").
/// Plain substring matching was tried first and failed on the first real BC data (2026-10-06):
/// "sepa" matched "separate" and tagged 10 of 80 items Localisation-NL. "dutch" on its own was
/// also dropped for "dutch locali*" — Microsoft lists Dutch among supported UI languages, which
/// says nothing about the Dutch localisation. Bare "customer" was dropped from Sales for the
/// same reason: Microsoft writes "customers can…" in most descriptions, and it alone tagged 11
/// of 13 Sales items, nearly all wrongly (a banking-app removal, Page Scripting, lookups).
/// </para>
/// </summary>
public sealed class RuleBasedModuleClassifier : IModuleClassifier
{
    private static readonly Dictionary<string, string[]> ModuleKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Finance"] = ["general ledger", "bank reconciliation", "bank rec*", "chart of accounts", "financial report", "currency", "vat", "fixed asset", "cash flow", "budget", "audit trail"],
        ["Sales"] = ["sales order", "sales quote", "sales invoice", "sales document", "sales return", "customer card", "customer ledger", "crm", "sales price"],
        ["Purchasing"] = ["purchase order", "purchase invoice", "vendor", "requisition", "approval workflow"],
        ["Warehouse"] = ["warehouse", "license plate", "bin", "put-away", "pick", "inventory"],
        ["Manufacturing"] = ["manufactur*", "production order", "routing", "bill of material", "bom", "capacity planning"],
        ["Projects"] = ["project", "job planning", "job ledger", "time sheet"],
        ["Service"] = ["service order", "service item", "service contract", "field service"],
        ["Reporting"] = ["report", "statistics", "power bi", "analytics"],
        ["Dev/API"] = ["api", "odata", "soap", "extension", "al language", "codeunit", "web service", "webhook"],
        ["Admin"] = ["admin center", "tenant", "environment", "feature management", "feature key", "user management"],
        ["Localisation-NL"] = ["netherlands", "dutch locali*", "nl locali*", "sepa", "btw"],
    };

    private static readonly Dictionary<string, Regex[]> ModulePatterns = ModuleKeywords.ToDictionary(
        kv => kv.Key,
        kv => kv.Value.Select(ToPattern).ToArray(),
        StringComparer.OrdinalIgnoreCase);

    private static Regex ToPattern(string keyword)
    {
        var pattern = keyword.EndsWith('*')
            ? $@"\b{Regex.Escape(keyword.TrimEnd('*'))}"
            : $@"\b{Regex.Escape(keyword)}(?:s|es|ed|ing)?\b";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    public ModuleClassification Classify(string title, string description, IReadOnlyCollection<string> microsoftProductTags)
    {
        var haystack = $"{title}\n{description}\n{string.Join('\n', microsoftProductTags)}";

        var matches = ModulePatterns
            .Where(kv => kv.Value.Any(pattern => pattern.IsMatch(haystack)))
            .Select(kv => kv.Key)
            .ToList();

        return new ModuleClassification(matches, Confident: matches.Count > 0);
    }
}
