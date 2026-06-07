namespace backend.Models;

public class ProjectBrainEvidenceEntity
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string? StudentId { get; set; }
    public string Topic { get; set; } = "understanding_engine";
    public DateTime CreatedAt { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public string SummaryJson { get; set; } = "{}";
}
