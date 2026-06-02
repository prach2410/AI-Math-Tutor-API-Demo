namespace backend.Models;

public class LearningSessionEntity
{
    public string SessionId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string StudentAlias { get; set; } = "Student-001";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Completed { get; set; }
    public string SessionJson { get; set; } = "{}";
}
