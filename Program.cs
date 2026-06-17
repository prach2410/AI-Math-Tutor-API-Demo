using backend.Data;
using backend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<LearningFlowService>();
builder.Services.AddSingleton<FreeTalkService>();
builder.Services.AddSingleton<ProjectBrainTutorService>();
builder.Services.AddScoped<HomeworkAnalysisService>();
builder.Services.AddScoped<ProjectBrainEvidenceService>();
builder.Services.AddSingleton<LearningJournalService>();
builder.Services.AddSingleton<LearningRecordsService>();

var textProvider = builder.Configuration.GetValue<string>("LLM__TextProvider")
    ?? Environment.GetEnvironmentVariable("LLM__TextProvider")
    ?? "Claude";

if (textProvider == "LocalAI")
{
    var localAiKey = builder.Configuration.GetValue<string>("LLM__LocalAI__ApiKey")
        ?? Environment.GetEnvironmentVariable("LLM__LocalAI__ApiKey")
        ?? "";
    builder.Services.AddSingleton<IChatProvider>(_ => new OllamaChatProvider(localAiKey));
}
else
{
    var anthropicKey = builder.Configuration.GetValue<string>("ANTHROPIC_API_KEY")
        ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? "";
    if (string.IsNullOrWhiteSpace(anthropicKey))
        builder.Services.AddSingleton<IChatProvider, MockChatProvider>();
    else
        builder.Services.AddSingleton<IChatProvider>(_ => new ClaudeChatProvider(anthropicKey, "claude-sonnet-4-6"));
}

builder.Services.AddScoped<TeachingFlowService>();

var dbPath = builder.Configuration.GetValue<string>("DatabasePath") ?? "learning_sessions.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<LearningSessionService>();
builder.Services.AddScoped<DiscoveryBatchService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Ensure new tables added after initial DB creation exist
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS ProjectBrainEvidence (
            Id          TEXT NOT NULL PRIMARY KEY,
            SessionId   TEXT NOT NULL,
            StudentId   TEXT,
            Topic       TEXT NOT NULL,
            CreatedAt   TEXT NOT NULL,
            EvidenceJson TEXT NOT NULL,
            SummaryJson  TEXT NOT NULL
        );
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS TeachingSessions (
            Id                 TEXT NOT NULL PRIMARY KEY,
            ProblemText        TEXT NOT NULL DEFAULT '',
            Latex              TEXT NOT NULL DEFAULT '',
            Topic              TEXT NOT NULL DEFAULT '',
            HasFigure          INTEGER NOT NULL DEFAULT 0,
            StepsJson          TEXT NOT NULL DEFAULT '[]',
            CurrentStep        INTEGER NOT NULL DEFAULT 1,
            Status             TEXT NOT NULL DEFAULT 'in_progress',
            SolutionShownCount INTEGER NOT NULL DEFAULT 0,
            FigureDescription  TEXT NOT NULL DEFAULT '',
            FigureCorrection   TEXT NOT NULL DEFAULT '',
            CreatedAt          TEXT NOT NULL
        );
        """);

    // Add columns that may be missing from older DB instances
    try { db.Database.ExecuteSqlRaw("ALTER TABLE LearningRecords ADD COLUMN ImageHash TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE LearningRecords ADD COLUMN DownloadedAt TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE TeachingSessions ADD COLUMN DownloadedAt TEXT NOT NULL DEFAULT ''"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE TeachingSessions ADD COLUMN FigureDescription TEXT NOT NULL DEFAULT ''"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE TeachingSessions ADD COLUMN FigureCorrection  TEXT NOT NULL DEFAULT ''"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE TeachingSessions ADD COLUMN Mode             TEXT NOT NULL DEFAULT 'guide_first'"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE TeachingSessions ADD COLUMN SolveFirstCount  INTEGER NOT NULL DEFAULT 0"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE LearningRecords ADD COLUMN Reflection TEXT"); } catch { }

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS LearningRecords (
            Id             TEXT NOT NULL PRIMARY KEY,
            Date           TEXT NOT NULL,
            DocumentType   TEXT NOT NULL,
            Topic          TEXT NOT NULL,
            Summary        TEXT NOT NULL,
            HighlightsJson TEXT NOT NULL DEFAULT '[]',
            KeywordsJson   TEXT NOT NULL DEFAULT '[]',
            CreatedAt      TEXT NOT NULL
        );
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS HomeworkReads (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            Filename    TEXT NOT NULL DEFAULT '',
            CreatedAt   TEXT NOT NULL,
            Readable    INTEGER NOT NULL DEFAULT 0,
            Reason      TEXT NOT NULL DEFAULT '',
            ProblemText TEXT NOT NULL DEFAULT '',
            Latex       TEXT NOT NULL DEFAULT '',
            Topic       TEXT NOT NULL DEFAULT '',
            RawResponse TEXT NOT NULL DEFAULT ''
        );
        """);
}

app.UseCors();
app.MapControllers();
app.Run();
