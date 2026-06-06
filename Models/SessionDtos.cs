namespace backend.Models;

public record CreateSessionRequest(
    string SessionId,
    string Topic,
    string StudentAlias,
    DateTime StartedAt,
    string? StudentId = null,
    string? DeviceId = null,
    string? DisplayName = null
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

public record ReflectionRequest(
    string? WhatILearned,
    string? MostDifficultPart,
    string? WhatIWantToRemember,
    DateTime SubmittedAt
);

public record SessionMessage(
    string Role,
    string Type,
    string Text,
    DateTime Timestamp,
    string? InputMode = null
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
    int DurationSeconds,
    int? VoiceMessages = null,
    int? TextMessages = null
);
