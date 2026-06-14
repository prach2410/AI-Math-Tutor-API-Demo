using System.Text.Json;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public record TeachingStep(int Step, string Goal, string GuidingQuestion, string ConceptHint);

public record StartTeachingResult(string SessionId, TeachingStep CurrentStep, int TotalSteps);

public record AnswerResult(
    string Verdict,        // correct | partial | wrong
    string Reason,
    string Missing,
    string Encouragement,
    TeachingStep? NextStep,   // non-null เฉพาะตอนผ่านขั้นแล้วไปขั้นถัดไป
    bool Done
);

public record HintResult(int Level, string Help);

public class TeachingFlowService(AppDbContext db, IChatProvider chat)
{
    private const string StepPlanPrompt = """
        คุณคือติวเตอร์คณิตศาสตร์ ม.2 ที่ช่วยให้นักเรียน "คิดเองเป็น" ไม่ใช่เฉลยให้

        หน้าที่: วางแผนการสอนโจทย์นี้เป็นขั้นๆ โดยแต่ละขั้นเป็นการ "ชวนคิด" หนึ่งก้าวเล็กๆ
        นักเรียนจะค่อยๆ แก้โจทย์ด้วยตัวเองผ่านคำถามของคุณ

        โจทย์: {problemText}
        สมการ (ถ้ามี): {latex}
        หัวข้อ: {topic}

        กฎจำนวนขั้น — เลือกให้เหมาะกับโจทย์ (ไม่ต้องครบ 6 ขั้นทุกครั้ง):
        - โจทย์ตรงๆ แค่ 1 แนวคิด เช่น แก้สมการ x²=c หรือหาค่า x จากนิพจน์ง่ายๆ → 1–2 ขั้น
        - โจทย์กลาง มี 2–3 แนวคิดต่อกัน → 2–4 ขั้น
        - โจทย์ซับซ้อน หลายขั้นตอน/หลายแนวคิด → 4–6 ขั้น

        กฎการสร้างขั้น:
        - ห้ามตั้งขั้นสำหรับข้อมูลที่ปรากฏในโจทย์อยู่แล้ว (เช่น ถ้าโจทย์มีสมการ x²=225 ให้แล้ว อย่าถามให้ "เขียนสมการ" หรือ "ระบุค่า")
        - เริ่มจากขั้นแรกที่นักเรียน "ต้องคิดเอง" จริงๆ ไม่ใช่ทวนสิ่งที่โจทย์บอกแล้ว
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

    private const string JudgePrompt = """
        คุณคือติวเตอร์ที่กำลังประเมินคำตอบของนักเรียนสำหรับ "ขั้นนี้เท่านั้น"

        โจทย์รวม: {problemText}
        เป้าหมายขั้นนี้: {goal}
        คำถามที่ถามไป: {guidingQuestion}
        คำตอบนักเรียน: {answer}

        กฎการตัดสิน:
        - ประเมินเฉพาะว่านักเรียนทำ "ขั้นนี้" ถูกไหม ไม่ใช่ทั้งโจทย์
        - ใจกว้างกับการสะกด/พิมพ์ผิด/ภาษาพูด โฟกัสที่การคิดทางคณิตศาสตร์
        - ถูกแนวคิดแต่ยังไม่ครบ = partial
        - ห้ามเฉลยคำตอบของขั้นถัดไป
        - feedback เป็นไทย โทน growth mindset (ชมความพยายาม/การคิด ไม่ใช่ตัวบุคคล)
        - ตอบ JSON เท่านั้น ห้ามมีข้อความนอก JSON

        รูปแบบ:
        { "verdict": "correct | partial | wrong", "reason": "เหตุผลสั้นๆ", "missing": "สิ่งที่ยังขาด ไม่บอกคำตอบตรงๆ", "encouragement": "คำให้กำลังใจสั้นๆ" }
        """;

    private const string HintPrompt = """
        นักเรียนกำลังติดในขั้นนี้ ช่วยตามระดับที่กำหนด โดยค่อยๆ เพิ่มความช่วยเหลือ ไม่เฉลยทันที

        โจทย์: {problemText}
        เป้าหมายขั้นนี้: {goal}
        คำถามขั้นนี้: {guidingQuestion}
        ระดับความช่วยเหลือ: {level}

        ความหมายแต่ละระดับ (ทำเฉพาะระดับที่ขอ):
        - 1 (Hint): ใบ้สั้นๆ ชี้ทิศ ไม่บอกคำตอบ
        - 2 (Help Me Start): ทำก้าวแรกให้ดูเป็นตัวอย่าง แล้วให้นักเรียนทำต่อ
        - 3 (Worked Example): ยกตัวอย่างโจทย์คล้ายกันแต่เลขต่าง แล้วแสดงวิธีคิดเต็ม (ไม่ใช่โจทย์เดิม)
        - 4 (Show Solution): เฉลยขั้นนี้ของโจทย์เดิม (ทางเลือกสุดท้าย)

        กฎ: ระดับ 1–3 ห้ามเฉลยคำตอบตัวเลขของโจทย์เดิม โทนให้กำลังใจ ภาษาไทย ตอบ JSON เท่านั้น
        รูปแบบ: { "level": {level}, "help": "เนื้อหาความช่วยเหลือ" }
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
            return new AnswerResult("correct", "", "", "โจทย์นี้เสร็จแล้วนะ!", null, true);

        var steps = JsonSerializer.Deserialize<List<TeachingStep>>(session.StepsJson)!;
        var currentIdx = session.CurrentStep - 1;
        var step = steps[currentIdx];

        // S2b: LLM judge ตัดสินคำตอบของขั้นนี้
        var judge = await JudgeAsync(session.ProblemText, step, answer);

        if (judge.Verdict != "correct")
        {
            // ยังไม่ผ่านขั้นนี้ → อยู่ step เดิม ให้ feedback (ไม่เฉลย ไม่ข้ามขั้น)
            return new AnswerResult(
                judge.Verdict, judge.Reason, judge.Missing,
                judge.Encouragement.Length > 0 ? judge.Encouragement : "ลองอีกครั้งนะ ใกล้แล้ว",
                null, false);
        }

        // correct → ไปขั้นถัดไป
        var nextIdx = currentIdx + 1;
        if (nextIdx >= steps.Count)
        {
            session.CurrentStep = steps.Count;
            session.Status = "done";
            await db.SaveChangesAsync();
            return new AnswerResult("correct", judge.Reason, "",
                judge.Encouragement.Length > 0 ? judge.Encouragement : "เยี่ยมมาก! ทำโจทย์ครบทุกขั้นแล้ว!",
                null, true);
        }

        session.CurrentStep = nextIdx + 1;
        await db.SaveChangesAsync();
        return new AnswerResult("correct", judge.Reason, "",
            judge.Encouragement.Length > 0 ? judge.Encouragement : "ถูกต้อง! ไปขั้นต่อไปกันเลย",
            steps[nextIdx], false);
    }

    public async Task<HintResult> HintAsync(string sessionId, int level)
    {
        level = Math.Clamp(level, 1, 4);
        var session = await db.TeachingSessions.FindAsync(sessionId)
            ?? throw new KeyNotFoundException($"session {sessionId} not found");

        var steps = JsonSerializer.Deserialize<List<TeachingStep>>(session.StepsJson)!;
        var step = steps[session.CurrentStep - 1];

        var prompt = HintPrompt
            .Replace("{problemText}", session.ProblemText)
            .Replace("{goal}", step.Goal)
            .Replace("{guidingQuestion}", step.GuidingQuestion)
            .Replace("{level}", level.ToString());

        var raw = await chat.CompleteAsync(prompt);
        var help = ParseHint(raw);

        if (level == 4)   // guardrail: นับการเฉลย (Show Solution) → solution_shown_rate
        {
            session.SolutionShownCount += 1;
            await db.SaveChangesAsync();
        }

        return new HintResult(level, help);
    }

    private record Judgement(string Verdict, string Reason, string Missing, string Encouragement);

    private async Task<Judgement> JudgeAsync(string problemText, TeachingStep step, string answer)
    {
        var prompt = JudgePrompt
            .Replace("{problemText}", problemText)
            .Replace("{goal}", step.Goal)
            .Replace("{guidingQuestion}", step.GuidingQuestion)
            .Replace("{answer}", answer);

        var raw = await chat.CompleteAsync(prompt);
        try
        {
            var json = ExtractJson(raw);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var verdict = (root.GetProperty("verdict").GetString() ?? "partial").Trim().ToLowerInvariant();
            if (verdict is not ("correct" or "partial" or "wrong")) verdict = "partial";
            return new Judgement(
                verdict,
                root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "",
                root.TryGetProperty("missing", out var m) ? m.GetString() ?? "" : "",
                root.TryGetProperty("encouragement", out var e) ? e.GetString() ?? "" : ""
            );
        }
        catch
        {
            // parse fail → ไม่ตัดสินว่าถูก (กันข้ามขั้นพลาด) ให้ลองใหม่
            return new Judgement("partial", "", "ลองอธิบายแนวคิดเพิ่มอีกนิดนะ", "พยายามได้ดีแล้ว ลองอีกหน่อย");
        }
    }

    private static string ParseHint(string raw)
    {
        try
        {
            var json = ExtractJson(raw);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("help", out var h))
                return h.GetString() ?? raw.Trim();
        }
        catch { }
        return raw.Trim();
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
