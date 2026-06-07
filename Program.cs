using backend.Data;
using backend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<LearningFlowService>();
builder.Services.AddSingleton<FreeTalkService>();
builder.Services.AddSingleton<ProjectBrainTutorService>();
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
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseCors();
app.MapControllers();
app.Run();
