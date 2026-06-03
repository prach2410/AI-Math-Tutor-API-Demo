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

public class BatchMetrics
{
    // Session
    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int IncompleteSessions { get; set; }
    public double CompletionRate { get; set; }
    // Help
    public int HintCount { get; set; }
    public double HintRate { get; set; }
    public int HelpMeStartCount { get; set; }
    public double HelpMeStartRate { get; set; }
    public int WorkedExampleCount { get; set; }
    public double WorkedExampleRate { get; set; }
    // Friction
    public int AbandonedCount { get; set; }
    public double AbandonmentRate { get; set; }
    public string MostAbandonedLesson { get; set; } = string.Empty;
    public string MostAbandonedStep { get; set; } = string.Empty;
    // Students
    public int UniqueStudents { get; set; }
    public int ReturningStudents { get; set; }
    public double AvgSessionsPerStudent { get; set; }
    // Devices
    public int UniqueDevices { get; set; }
    public int ReturningDevices { get; set; }
    public double AvgSessionsPerDevice { get; set; }
}

public class ImportBatchResponse
{
    public string BatchId { get; set; } = string.Empty;
    public string BatchType { get; set; } = "Imported";
    public string Source { get; set; } = "Upload";
    public DateTime ImportedAt { get; set; }
    public int SessionCount { get; set; }
    public string DuplicateStatus { get; set; } = string.Empty; // "NewBatch" | "PartiallyImported" | "AlreadyReviewed"
    public int DuplicateCount { get; set; }
    public string DuplicateBatchRef { get; set; } = string.Empty;
    public BatchMetrics? Summary { get; set; }
}

public class BatchSummaryDto
{
    public string BatchId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DiscoveryStatus { get; set; } = "not_analyzed"; // "not_analyzed" | "discovery_draft" | "reviewed"
    public string BatchType { get; set; } = "Normal";
    public string AnalysisStatus { get; set; } = "not_analyzed";
    public int SessionCount { get; set; }
    public DiscoveryNotes Notes { get; set; } = new();
    public BatchMetrics? Summary { get; set; }
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
