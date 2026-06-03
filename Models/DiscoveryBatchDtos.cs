namespace backend.Models;

public record CreateBatchRequest(int MaxSessions = 10);

public record UpdateNotesRequest(
    string? KeyObservations,
    string? ValidatedDiscoveries,
    string? UnconfirmedSignals,
    string? ProductDecisions,
    string? NextQuestions
);

public class DiscoveryNotes
{
    public string KeyObservations { get; set; } = string.Empty;
    public string ValidatedDiscoveries { get; set; } = string.Empty;
    public string UnconfirmedSignals { get; set; } = string.Empty;
    public string ProductDecisions { get; set; } = string.Empty;
    public string NextQuestions { get; set; } = string.Empty;
}

public record ImportBatchRequest(string Json);

public class ImportBatchResponse
{
    public string BatchId { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public string DuplicateStatus { get; set; } = string.Empty; // "NewBatch" | "PartiallyImported" | "AlreadyReviewed"
    public int DuplicateCount { get; set; }
}

public class BatchSummaryDto
{
    public string BatchId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BatchType { get; set; } = "Normal";
    public int SessionCount { get; set; }
    public DiscoveryNotes Notes { get; set; } = new();
}

public class BatchDetailDto : BatchSummaryDto
{
    public List<string> SessionIds { get; set; } = [];
}

public class UnreviewedCountDto
{
    public int UnreviewedSessions { get; set; }
}

public class CreateBatchResponseDto
{
    public string BatchId { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public string Status { get; set; } = string.Empty;
}
