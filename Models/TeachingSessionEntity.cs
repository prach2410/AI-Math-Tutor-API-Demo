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
    public string FigureDescription { get; set; } = "";  // what AI read from the figure (shown to student)
    public string FigureCorrection { get; set; } = "";   // student's confirmed/corrected description
    public string CreatedAt { get; set; } = "";   // UTC ISO-8601
    public string Mode { get; set; } = "guide_first";  // guide_first | solve_first
    public int SolveFirstCount { get; set; }
    public string DownloadedAt { get; set; } = "";
}
