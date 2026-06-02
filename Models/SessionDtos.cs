namespace backend.Models;

public record CreateSessionRequest(
    string SessionId,
    string Topic,
    string StudentAlias,
    DateTime StartedAt
);

public record CompleteSessionRequest(
    DateTime CompletedAt,
    List<SessionMessage> Messages,
    List<SessionEvent> Events,
    SessionSummary Summary
);

public record ParentFeedbackRequest(
    string UnderstandingLevel,
    string? MostValuableSection,
    string? Comment
);

public record SessionMessage(
    string Role,
    string Type,
    string Text,
    DateTime Timestamp
);

public record SessionEvent(
    string Type,
    DateTime Timestamp
);

public record SessionSummary(
    int HintUsed,
    int HelpMeStartUsed,
    int ExampleUsed,
    bool Completed,
    int DurationSeconds
);
