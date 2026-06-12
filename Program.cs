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
