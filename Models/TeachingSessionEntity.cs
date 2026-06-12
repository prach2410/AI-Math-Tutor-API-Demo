namespace backend.Models;

public class TeachingSessionEntity
{
    public string Id { get; set; } = "";
    public string ProblemText { get; set; } = "";
    public string Latex { get; set; } = "";
    public string Topic { get; set; } = "";
    public bool HasFigure { get; set; }
    public string StepsJson { get; set; } = "";   // JSON array of step objects
    public int CurrentStep { get; set; } = 1;     // 1-indexed
    public string Status { get; set; } = "in_progress";
    public int SolutionShownCount { get; set; }
    public string CreatedAt { get; set; } = "";   // UTC ISO-8601
}
