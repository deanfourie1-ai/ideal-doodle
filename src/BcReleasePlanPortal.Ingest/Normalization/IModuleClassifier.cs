namespace BcReleasePlanPortal.Ingest.Normalization;

public readonly record struct ModuleClassification(IReadOnlyList<string> Modules, bool Confident);

/// <summary>
/// Derives <see cref="Domain.RoadmapItem.Modules"/> — the BC module taxonomy from design doc
/// §5.1 (Finance, Sales, Purchasing, Warehouse, Manufacturing, Projects, Service, Reporting,
/// Dev/API, Admin, Localisation-NL) — from an item's title/description/Microsoft product tags.
/// </summary>
public interface IModuleClassifier
{
    ModuleClassification Classify(string title, string description, IReadOnlyCollection<string> microsoftProductTags);
}
