namespace backend.Models;

public record StartResponse(
    string ScenarioId,
    int StepNumber,
    int TotalSteps,
    string Question,
    bool IsLast,
    List<string> RealWorldUses,
    string? PassiveGrill = null
);

public record AssistResponse(string Message);

public record EvaluateRequest(
    string ScenarioId,
    int StepNumber,
    string Answer,
    int WrongCount,
    int HintCount = 0,
    int GuidedCount = 0,
    string? StudentName = null
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
    string? ParentCoachingTips = null,
    string? TeachingMomentType = null
);

public record NextStepDto(
    int StepNumber,
    int TotalSteps,
    string Question,
    bool IsLast,
    string? PassiveGrill = null
);

public record FreeTalkMessage(string Role, string Content);

public record FreeTalkRequest(
    List<FreeTalkMessage> History,
    string Message,
    string? StudentName = null,
    bool DuringLesson = false
);

public record FreeTalkResponse(
    string Message,
    bool SuggestLesson = false
);

public record ProjectBrainMessage(string Role, string Text);

public record ProjectBrainRequest(
    List<ProjectBrainMessage> History,
    string Message,
    string Phase,
    string? StudentName = null,
    string? PriorEvidenceSummary = null,
    string? TopicId = null
);

public record EvidenceItem(
    string EvidenceType,
    string UserStatement,
    string AiInterpretation,
    double Confidence
);

public record ProjectBrainResponse(
    string Message,
    string Phase,
    bool SuggestSummary = false,
    List<EvidenceItem>? Evidence = null
);

public record SaveEvidenceRequest(
    string? StudentId,
    string Topic,
    List<EvidenceItem> Items
);

public record EvidenceSummary(
    List<string> StrongEvidence,
    List<string> PartialEvidence,
    List<string> OpenQuestions,
    List<string> PossibleMisalignment
);
