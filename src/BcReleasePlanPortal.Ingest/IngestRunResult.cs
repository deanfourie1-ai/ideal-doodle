namespace BcReleasePlanPortal.Ingest;

public sealed class IngestRunResult
{
    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset FinishedAt { get; set; }

    public List<ProductIngestResult> Products { get; } = [];

    public int TotalItemsSeen => Products.Sum(p => p.ItemsSeen);

    public int TotalItemsNew => Products.Sum(p => p.ItemsNew);

    public int TotalItemsUpdated => Products.Sum(p => p.ItemsUpdated);

    public int TotalChangeEvents => Products.Sum(p => p.ChangeEventsEmitted);

    public int TotalAlertsSent => Products.Sum(p => p.AlertsSent);
}

public sealed class ProductIngestResult
{
    public required string InternalProduct { get; init; }

    public int ItemsSeen { get; set; }

    public int ItemsNew { get; set; }

    public int ItemsUpdated { get; set; }

    public int ItemsUnchanged { get; set; }

    public int ChangeEventsEmitted { get; set; }

    public int AlertsSent { get; set; }
}
