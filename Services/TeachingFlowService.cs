using System.Text.Json;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public record TeachingStep(int Step, string Goal, string GuidingQuestion, string ConceptHint);

public record StartTeachingResult(string SessionId, TeachingStep CurrentStep, int TotalSteps);

public record AnswerResult(
    string Verdict,        // "correct" (S2a stubbed — S2b ใส่ judge จริง)
    string Encouragement,
    TeachingStep? NextStep,
    bool Done
);

public class TeachingFlowService(AppDbContext db, IChatProvider chat)
{
    private const string StepPlanPrompt = """
        คุณคือติวเตอร์คณิตศาสตร์ ม.2 ที่ช่วยให้นักเรียน "คิดเองเป็น" ไม่ใช่เฉลยให้

        หน้าที่: วางแผนการสอนโจทย์นี้เป็นขั้นๆ โดยแต่ละขั้นเป็นการ "ชวนคิด" หนึ่งก้าวเล็กๆ
        นักเรียนจะค่อยๆ แก้โจทย์ด้วยตัวเองผ่านคำถามของคุณ

        โจทย์: {problemText}
        สมการ (ถ้ามี): {latex}
        หัวข้อ: {topic}

        กฎ:
        - วาง 3–6 ขั้น แต่ละขั้น = การคิดหนึ่งก้าว (เล็ก ไม่กระโดด)
        - guidingQuestion ต้องเป็นคำถามชวนคิดแบบเชิญชวน เช่น "ลองดูว่า..." "คิดว่าอะไร..."
          ห้ามใช้โทนสอบปากเปล่า เช่น "จงอธิบาย" "บอกมาว่า"
        - ห้ามใส่คำตอบตัวเลขสุดท้ายในทุกขั้น
        - ขั้นสุดท้าย = นักเรียนสรุป/คำนวณคำตอบเอง
        - ตอบ JSON เท่านั้น ห้ามมีข้อความนอก JSON

        รูปแบบ:
        {
          "steps": [
            { "step": 1, "goal": "เป้าหมายของขั้นนี้", "guidingQuestion": "คำถามชวนคิด", "conceptHint": "แนวคิด/สูตรที่เกี่ยวข้อง (ย่อ)" }
          ]
        }
        """;

    public async Task<StartTeachingResult> StartAsync(
        string problemText, string latex, string topic, bool hasFigure)
    {
        var prompt = StepPlanPrompt
            .Replace("{problemText}", problemText)
            .Replace("{latex}", latex)
            .Replace("{topic}", topic);

        var raw = await chat.CompleteAsync(prompt);
        var steps = ParseSteps(raw);

        var session = new TeachingSessionEntity
        {
            Id          = Guid.NewGuid().ToString(),
            ProblemText = problemText,
            Latex       = latex,
            Topic       = topic,
            HasFigure   = hasFigure,
            StepsJson   = JsonSerializer.Serialize(steps),
            CurrentStep = 1,
            Status      = "in_progress",
            CreatedAt   = DateTime.UtcNow.ToString("O"),
        };
        db.TeachingSessions.Add(session);
        await db.SaveChangesAsync();

        return new StartTeachingResult(session.Id, steps[0], steps.Count);
    }

    public async Task<AnswerResult> AnswerAsync(string sessionId, string answer)
    {
        var session = await db.TeachingSessions.FindAsync(sessionId)
            ?? throw new KeyNotFoundException($"session {sessionId} not found");

        if (session.Status == "done")
            return new AnswerResult("correct", "โจทย์นี้เสร็จแล้วนะ!", null, true);

        var steps = JsonSerializer.Deserialize<List<TeachingStep>>(session.StepsJson)!;
        var currentIdx = session.CurrentStep - 1;

        // S2a: judge ยัง stubbed = always correct — S2b ใส่ LLM judge ตรงนี้
        var nextIdx = currentIdx + 1;
        if (nextIdx >= steps.Count)
        {
            session.CurrentStep = steps.Count;
            session.Status = "done";
            await db.SaveChangesAsync();
            return new AnswerResult("correct", "เยี่ยมมาก! ทำโจทย์ครบทุกขั้นแล้ว!", null, true);
        }

        session.CurrentStep = nextIdx + 1;
        await db.SaveChangesAsync();

        return new AnswerResult("correct", "ถูกต้อง! ไปขั้นต่อไปกันเลย", steps[nextIdx], false);
    }

    public async Task<(TeachingSessionEntity Session, List<TeachingStep> Steps)> GetSessionAsync(string sessionId)
    {
        var session = await db.TeachingSessions.FindAsync(sessionId)
            ?? throw new KeyNotFoundException($"session {sessionId} not found");
        var steps = JsonSerializer.Deserialize<List<TeachingStep>>(session.StepsJson)!;
        return (session, steps);
    }

    private static List<TeachingStep> ParseSteps(string raw)
    {
        try
        {
            var json = ExtractJson(raw);
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement.GetProperty("steps");
            return arr.EnumerateArray().Select(s => new TeachingStep(
                Step:            s.GetProperty("step").GetInt32(),
                Goal:            s.GetProperty("goal").GetString() ?? "",
                GuidingQuestion: s.GetProperty("guidingQuestion").GetString() ?? "",
                ConceptHint:     s.TryGetProperty("conceptHint", out var h) ? h.GetString() ?? "" : ""
            )).ToList();
        }
        catch
        {
            return
            [
                new TeachingStep(1, "วิเคราะห์โจทย์", "ลองอ่านโจทย์อีกครั้ง แล้วบอกว่าโจทย์ถามหาอะไร?", "อ่านโจทย์ให้ครบก่อน"),
                new TeachingStep(2, "วางแผนแก้โจทย์", "เรามีวิธีไหนบ้างที่จะใช้แก้โจทย์นี้ได้?", "คิดถึงสูตรที่เรียนมา"),
                new TeachingStep(3, "คำนวณและสรุป", "ลองแทนค่าและหาคำตอบดูนะ", "ทำทีละขั้น"),
            ];
        }
    }

    private static string ExtractJson(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```")) { var s = t.IndexOf('\n') + 1; var e = t.LastIndexOf("```"); if (e > s) t = t[s..e].Trim(); }
        var start = t.IndexOf('{'); var end = t.LastIndexOf('}');
        return (start >= 0 && end > start) ? t[start..(end + 1)] : t;
    }
}
