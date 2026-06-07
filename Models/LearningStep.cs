namespace backend.Models;

public record LearningStep(
    int StepNumber,
    int TotalSteps,
    string Question,
    string ExpectedAnswer,
    string Hint,
    string GuidedAssistance,
    string WorkedExample,
    bool IsLast,
    string? TeachingMoment = null
);
