namespace backend.Models;

public record StartResponse(
    string ScenarioId,
    int StepNumber,
    int TotalSteps,
    string Question,
    bool IsLast,
    List<string> RealWorldUses
);

public record AssistResponse(string Message);

public record EvaluateRequest(
    string ScenarioId,
    int StepNumber,
    string Answer,
    int WrongCount,
    int HintCount = 0,
    int GuidedCount = 0
);

public record EvaluateResponse(
    bool Correct,
    string Message,
    string? Hint,
    NextStepDto? NextStep,
    string? StudentNote,
    string? ParentSummary,
    bool IsGuidedAssistance,
    List<string>? LearningReflection,
    string? StudentFeedback = null,
    string? ParentCoachingTips = null
);

public record NextStepDto(
    int StepNumber,
    int TotalSteps,
    string Question,
    bool IsLast
);
