namespace BcReleasePlanPortal.Domain;

/// <summary>
/// The BC module taxonomy from design doc §5.1 — the values <see cref="RoadmapItem.Modules"/> may
/// hold. The rule-based classifier's keyword table and the triage editor both draw from this list.
/// ("Localisation-NL" is Microsoft's own module name for the Dutch localisation.)
/// </summary>
public static class BcModules
{
    public static IReadOnlyList<string> All { get; } =
    [
        "Finance",
        "Sales",
        "Purchasing",
        "Warehouse",
        "Manufacturing",
        "Projects",
        "Service",
        "Reporting",
        "Dev/API",
        "Admin",
        "Localisation-NL",
    ];
}
