using Microsoft.Extensions.Logging;

namespace BcReleasePlanPortal.Ingest.Learn;

/// <summary>
/// Deliberate placeholder, not a real scraper. <c>learn.microsoft.com</c> is blocked by this
/// environment's network policy, so the actual page structure of "What's new and changed in
/// update N" and the deprecated-features pages has never been observed — writing CSS
/// selectors against remembered/guessed markup would violate the "recreate from source, not
/// memory" rule and would silently break the moment the guess is wrong. This implementation
/// fails loudly (logs a warning, returns <c>Available: false</c>) instead of pretending to
/// scrape, per design doc §13's own mitigation for this exact risk ("fail loudly, degrade to
/// link-only"). Swap in a real <see cref="ILearnPageSource"/> implementation once the pages
/// can be fetched and their markup inspected.
/// </summary>
public sealed class UnavailableLearnPageSource(ILogger<UnavailableLearnPageSource> logger) : ILearnPageSource
{
    private const string Reason = "learn.microsoft.com is not reachable from this environment and its page structure has not been verified; no scraper has been implemented against guessed markup.";

    public Task<LearnPageResult> FetchWhatsNewAsync(string version, CancellationToken ct)
    {
        logger.LogWarning("Learn 'what's new' fetch for BC update {Version} skipped: {Reason}", version, Reason);
        return Task.FromResult(new LearnPageResult(Available: false, Items: [], UnavailableReason: Reason));
    }

    public Task<LearnPageResult> FetchDeprecatedFeaturesAsync(CancellationToken ct)
    {
        logger.LogWarning("Learn deprecated-features fetch skipped: {Reason}", Reason);
        return Task.FromResult(new LearnPageResult(Available: false, Items: [], UnavailableReason: Reason));
    }
}
