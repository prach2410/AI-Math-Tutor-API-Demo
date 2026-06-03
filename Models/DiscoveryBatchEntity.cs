namespace backend.Models;

public class DiscoveryBatchEntity
{
    public string BatchId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Status { get; set; } = "draft";
    public string BatchType { get; set; } = "Normal";   // "Normal" | "Imported"
    public string? SourceJson { get; set; }              // original uploaded JSON for Imported batches
    public string SessionIdsJson { get; set; } = "[]";
    public string NotesJson { get; set; } = "{}";
}
