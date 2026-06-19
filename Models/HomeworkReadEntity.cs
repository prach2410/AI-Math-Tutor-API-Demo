namespace backend.Models;

public class HomeworkReadEntity
{
    public int Id { get; set; }
    public string Filename { get; set; } = "";
    public string CreatedAt { get; set; } = "";  // UTC ISO-8601
    public bool Readable { get; set; }
    public string Reason { get; set; } = "";
    public string ProblemText { get; set; } = "";
    public string Latex { get; set; } = "";
    public string Topic { get; set; } = "";
    public string RawResponse { get; set; } = "";
    public string VisionModel { get; set; } = "";
    public string AnalysisStartedAt { get; set; } = "";
    public string AnalysisEndedAt { get; set; } = "";
}
