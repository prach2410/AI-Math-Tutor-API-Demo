namespace backend.Models;

public record ScenarioDefinition(
    string Id,
    string Title,
    string StudentNote,
    string ParentSummary,
    List<string> RealWorldUses,
    List<string> LearningReflection,
    List<LearningStep> Steps
);
