namespace backend.Services;

public sealed class FeatureFlags
{
    public bool PersistStructuredSummary { get; init; }
    public bool EnableSessionContinuityReview { get; init; }
}
