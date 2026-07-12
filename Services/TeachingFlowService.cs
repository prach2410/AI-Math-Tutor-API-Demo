using System.Text.Json;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public record TeachingStep(int Step, string Goal, string GuidingQuestion, string ConceptHint);

public record StartTeachingResult(
    string SessionId,
    bool NeedsConfirm,
    string FigureDescription,
    TeachingStep? CurrentStep,
    int TotalSteps
);
public record ConfirmFigureResult(TeachingStep CurrentStep, int TotalSteps);

public record AnswerResult(
    string Verdict,        // correct | partial | wrong
    string Reason,
    string Missing,
    string Encouragement,
    TeachingStep? NextStep,   // non-null เฉพาะตอนผ่านขั้นแล้วไปขั้นถัดไป
    bool Done
);

public record HintResult(int Level, string Help);
public record SolveResult(string SessionId, string[] SolutionSteps, string UnderstandingStep, int[] KeyStepIndices);

public class TeachingFlowService(AppDbContext db, IChatProvider chat)
{
    private const string FigureAnalysisPrompt = """
        คุณคือติวเตอร์คณิตศาสตร์ ม.2 กำลังอ่านโจทย์ที่มีรูปประกอบ

        โจทย์ที่อ่านได้: {problemText}
        ข้อมูล/สมการจากโจทย์: {latex}
        หัวข้อ: {topic}

        สรุปสั้นๆ ว่าคุณเข้าใจโครงสร้างของโจทย์นี้อย่างไร (รูปทรง, ค่าที่ทราบ, สิ่งที่ต้องหา)
        แล้วถามนักเรียนว่าถูกต้องไหม หรือมีอะไรที่ต่างจากรูปจริงบ้าง
        โทนเป็นกันเอง อย่าสมมติสิ่งที่ไม่มีในโจทย์

        ตอบ JSON เท่านั้น: { "figureDescription": "..." }
        """;

    private const string StepPlanPrompt = """
        คุณคือติวเตอร์คณิตศาสตร์ ม.2 ที่ช่วยให้นักเรียน "คิดเองเป็น" ไม่ใช่เฉลยให้

        หน้าที่: วางแผนการสอนโจทย์นี้เป็นขั้นๆ โดยแต่ละขั้นเป็นการ "ชวนคิด" หนึ่งก้าวเล็กๆ
        นักเรียนจะค่อยๆ แก้โจทย์ด้วยตัวเองผ่านคำถามของคุณ

        โจทย์: {problemText}
        สมการ (ถ้ามี): {latex}
        หัวข้อ: {topic}
        {figureContext}
        กฎจำนวนขั้น — เลือกให้เหมาะกับโจทย์ (ไม่ต้องครบ 6 ขั้นทุกครั้ง):
        - โจทย์ตรงๆ แค่ 1 แนวคิด เช่น แก้สมการ x²=c หรือหาค่า x จากนิพจน์ง่ายๆ → 1–2 ขั้น
        - โจทย์กลาง มี 2–3 แนวคิดต่อกัน → 2–4 ขั้น
        - โจทย์ซับซ้อน หลายขั้นตอน/หลายแนวคิด → 4–6 ขั้น

        กฎการสร้างขั้น:
        - ห้ามตั้งขั้นสำหรับข้อมูลที่ปรากฏในโจทย์อยู่แล้ว (เช่น ถ้าโจทย์มีสมการ x²=225 ให้แล้ว อย่าถามให้ "เขียนสมการ" หรือ "ระบุค่า")
        - guidingQuestion โทนเป็นกันเองชวนคิด แต่ต้อง "เจาะจง มีคำตอบเดียว" — นำด้วยการกระทำที่รูปธรรม
          เช่น "ลองหาร 8 ÷ 4 ดูสิ ได้เท่าไร?" · "1.331 มีทศนิยมกี่ตำแหน่ง?" · "n⁵ ÷ n⁻⁷ ใช้สมบัติข้อไหน?"
          ห้ามใช้โทนสอบปากเปล่า เช่น "จงอธิบาย" "บอกมาว่า"
        - ❌ ห้ามลงท้ายคำถามด้วยรูปเปิดกว้างเด็ดขาด — ทุกคำถามที่ลงท้าย "...อย่างไรบ้าง" "...ได้อย่างไร"
          "...มีอะไรบ้าง" "...วิธีไหนบ้าง" "...จะเริ่มยังไง" ถือว่าผิดกฎทันที ไม่ว่าใช้กริยาอะไร (แบ่ง/เขียน/จัดการ/หา)
          เพราะเด็กต้องรู้วิธีแก้ก่อนถึงตอบได้ = floor สูงเกินไปสำหรับเด็กที่กำลังงง
          ✅ เปลี่ยนเป็นคำถามที่มี "คำตอบเดียว" ชี้หรือคำนวณได้ในก้าวเดียวเสมอ
        - กฎขั้นแรก (สำคัญสุด): ขั้นที่ 1 ต้องเป็น "ก้าวลงมือทำก้าวแรก" ที่เป็นรูปธรรม มีคำตอบเดียว ทำได้ทันที
          โดยไม่ต้องมองภาพรวมทั้งโจทย์ก่อน — เด็กที่กำลังงงต้องตอบขั้นนี้ได้ เพื่อเป็นจุดเริ่ม (on-ramp)
          ❌ ขั้นแรกห้ามเป็นคำถามให้ "วางแผน / มองภาพรวม / แบ่งส่วนประกอบ / คิดว่าจะเริ่มยังไง"
        - ห้ามใส่คำตอบตัวเลขสุดท้ายในทุกขั้น
        - หารากที่สาม/สองของจำนวนมาก: นำทางให้นักเรียนใช้ "หารสั้น" (หารด้วยจำนวนเฉพาะตัวเล็กซ้ำๆ แล้วจับกลุ่มทีละ 3 หรือ 2 ตัว) แทนการเดาตัวประกอบ — guidingQuestion ควรชวนให้ลองหารด้วย 2, 3, 5, … ก่อน (เดายากเมื่อเลขใหญ่)
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
        - เลขยกกำลังพิมพ์บนมือถือยาก — ยอมรับทุกรูปแบบว่าเทียบเท่ากัน: n^12, n12, n**12, n¹², "n ยกกำลัง 12", "n กำลัง 12"
          ประเมินที่ "ค่าทางคณิตศาสตร์" ไม่ใช่รูปแบบการพิมพ์ ห้ามตัดสิน wrong เพราะรูปแบบสัญกรณ์
        - ถูกแนวคิดแต่ยังไม่ครบ = partial
        - ห้ามเฉลยคำตอบของขั้นถัดไป
        - feedback เป็นไทย โทน growth mindset (ชมความพยายาม/การคิด ไม่ใช่ตัวบุคคล)
        - ตอบ JSON เท่านั้น ห้ามมีข้อความนอก JSON

        รูปแบบ:
        { "verdict": "correct | partial | wrong", "reason": "เหตุผลสั้นๆ", "missing": "สิ่งที่ยังขาด ไม่บอกคำตอบตรงๆ", "encouragement": "คำให้กำลังใจสั้นๆ" }
        """;

    private const string NotesPrompt = """
        คุณคือติวเตอร์คณิตศาสตร์ ม.2

        นักเรียนเพิ่งทำโจทย์นี้จนจบ:
        โจทย์: {problemText}
        หัวข้อ: {topic}

        ขั้นตอนที่ฝึก:
        {stepsText}

        สร้างสรุป 2 ส่วน:

        "studentNotes" — บันทึกสำหรับนักเรียน:
        - สิ่งที่ได้เรียนจากโจทย์นี้ (แนวคิดหลัก + วิธีคิด)
        - โทนเป็นกันเอง เหมือนคุยกับเพื่อน ไม่เป็นทางการ
        - 2–3 ประโยค

        "parentSummary" — สรุปสำหรับผู้ปกครอง:
        - นักเรียนฝึกอะไรวันนี้ (หัวข้อ + แนวคิด)
        - 1–2 ประโยค กระชับ

        ตอบ JSON เท่านั้น ห้ามมีข้อความนอก JSON:
        { "studentNotes": "...", "parentSummary": "..." }
        """;

    private const string SolvePrompt = """
        คุณคือติวเตอร์คณิตศาสตร์ ม.2 กำลังเขียนวิธีทำบนกระดาน

        โจทย์: {problemText}
        สมการ (ถ้ามี): {latex}
        หัวข้อ: {topic}

        งาน 2 อย่าง:

        1. "solutionSteps" — วิธีทำทีละขั้น สไตล์ครูเขียนกระดาน (array of strings)

        รูปแบบแต่ละ element: ป้ายกำกับสั้น + \n + สมการ/ตัวเลข
        - ป้ายกำกับ = วลี 2-5 คำ บอกว่ากำลังทำอะไร ❌ ไม่ใช่ประโยคยาว
          ("จับกลุ่มทีละ 3" ✅  /  "ขั้นต่อไปเราจะนำตัวประกอบมาจับเป็นกลุ่ม" ❌)
        - สมการ: 1 การกระทำ/บรรทัด · จัด "=" ให้อยู่แนวเดียวกันด้วยช่องว่างนำ
        - กฎ/เหตุผลสั้น: ใส่ใน (...) ท้ายบรรทัดที่ apply — ❌ ไม่ใช่บรรทัดแยก ไม่ใช่ประโยคอิสระ
          ตัวอย่าง: "= 8    (∛(a³) = a · รากที่สามหักล้างกำลังสาม)"  /  "= 9    (3² = 9)"
        - เด็กอ่านแล้วเห็นโครง 3-6 ป้าย ไม่ใช่ 10+ บรรทัดยาว

        กฎ:
        - สัญลักษณ์: Unicode เท่านั้น — ∛ √ ² ³ × ÷ − ∴ ≈  ❌ ห้าม LaTeX (\times \sqrt \frac $...$) เด็ดขาด
        - ขั้นแรกเสมอ: "💡 เทคนิคสังเกต" — รวม**ทุก shortcut** ที่เกี่ยวกับโจทย์นั้น ใน element เดียว:
          · กฎหาร 2: เลขคู่ · กฎหาร 3: บวกทุกหลักหาร 3 ลงตัว · กฎหาร 5: ลงท้าย 0 หรือ 5 · กฎหาร 11: ผลต่างหลักคู่-คี่ = 0
          · สำนวน: "หาร 2 ลงตัว" / "หาร 2 ไม่ลงตัว"  ❌ "ไม่หาร 2 ลงตัว" (ผิด — negation ต้องอยู่ที่ "ลงตัว" ไม่ใช่ที่ "หาร")
          · shortcut ทั้งหมดต้องอยู่ใน "💡 เทคนิคสังเกต" เท่านั้น ❌ ห้ามซ่อนไว้ใน step อื่น
          · เครื่องหมายลบ (ถ้ามี): ∛(−n) = −∛n → ทำงานกับ n ก่อน
          ❌ ห้ามเริ่มหารสั้นทันทีโดยไม่บอกเหตุผลที่เลือก p
        - หารากที่สาม/สองของเลขใหญ่: หารสั้น → โชว์ทีละบรรทัด (n ÷ p = m) จนได้ 1 แล้วสรุป  ❌ ห้าม assert ตัวประกอบเลยโดยไม่โชว์กระบวนการ
        - ถ้าไม่ใช่กำลังสาม/สองสมบูรณ์: ประมาณหาคู่กำลังที่ใกล้ที่สุด (เช่น ∛35 อยู่ระหว่าง 3³=27 กับ 4³=64)
        - โจทย์ปัญหา: ขั้นแรกตั้งตัวแปร/สิ่งที่รู้ก่อน → สร้างสมการ → แก้ทีละบรรทัด

        ตัวอย่าง 1 — ∛512:
        solutionSteps: [
          "💡 เทคนิคสังเกต\n512 เลขคู่ → หาร 2 ได้เสมอ · เริ่มหาร 2\n(ตรวจง่าย: เลขสุดท้ายเป็นเลขคู่ = หาร 2 ลงตัว)",
          "แยกตัวประกอบ (หารสั้น)\n512 ÷ 2 = 256\n256 ÷ 2 = 128\n128 ÷ 2 = 64\n 64 ÷ 2 = 32\n 32 ÷ 2 = 16\n 16 ÷ 2 = 8\n  8 ÷ 2 = 4\n  4 ÷ 2 = 2\n  2 ÷ 2 = 1\n→ 512 = 2⁹",
          "จับกลุ่มทีละ 3  (รากที่สาม)\n512 = (2×2×2) × (2×2×2) × (2×2×2)\n    = 8 × 8 × 8\n    = 8³",
          "ถอดราก\n∛512 = ∛(8³)\n     = 8    (∛(a³) = a · รากที่สามหักล้างกำลังสาม)"
        ]
        keyStepIndices: [1]   ← ขั้น 1 "เทคนิคสังเกต" เท่านั้น · ถอดราก/จัดกลุ่ม = กลไกตรงไปตรงมา ไม่ต้องอธิบายเพิ่ม

        ตัวอย่าง 2 — ∛1,331 (ต้องหาร 2, 3, 5 ไม่ลงตัว จึงลอง 11):
        solutionSteps: [
          "💡 เทคนิคสังเกต\nเลขคี่ → หาร 2 ไม่ลงตัว\nผลบวกหลัก 1+3+3+1=8 → หาร 3 ไม่ลงตัว\nลงท้าย 1 → หาร 5 ไม่ลงตัว\nผลต่างหลักคู่-คี่: (3+1)−(1+3) = 0 → หาร 11 ลงตัว → เริ่มหาร 11",
          "แยกตัวประกอบ (หารสั้น)\n1,331 ÷ 11 = 121\n  121 ÷ 11 = 11\n   11 ÷ 11 = 1\n→ 1,331 = 11 × 11 × 11",
          "จับกลุ่มทีละ 3  (รากที่สาม)\n1,331 = (11×11×11)\n      = 11³",
          "ถอดราก\n∛1,331 = ∛(11³)\n       = 11    (∛(a³) = a · รากที่สามหักล้างกำลังสาม)"
        ]
        keyStepIndices: [1]   ← ขั้น 1 "เทคนิคสังเกต" (กฎหาร 11) · ขั้น 2-4 คำนวณตรงไปตรงมา

        ตัวอย่าง 3 — สมการ 2 ตัวแปร: x + y = 20, x − y = 2:
        solutionSteps: [
          "ตั้งสมการ\n① x + y = 20\n② x − y = 2",
          "① + ② กำจัด y\n2x = 22\n x = 11",
          "แทน x = 11 ใน ①\n11 + y = 20\n      y = 9",
          "∴ x = 11, y = 9"
        ]
        keyStepIndices: [2]   ← ขั้น 2 "กำจัด y" คือกลวิธีสำคัญ · ขั้น 1/3/4 คำนวณตรงไปตรงมา

        2. "understandingStep" — คำถาม/งานสั้น 1 อย่างให้แน่ใจว่าเข้าใจจริง (เลือก 1):
           - โจทย์คล้ายกันแต่เลขต่าง ให้ลองทำ
           - ถามว่าขั้นใดขั้นหนึ่งทำไมถึงทำแบบนั้น
           - คำถามตรวจสอบความเข้าใจ 1 ข้อ
           โทนเป็นกันเอง ภาษาไทย

        3. "keyStepIndices" — เลขขั้น (เริ่มที่ 1) ที่มีเทคนิค/กฎ/เหตุผลที่เด็กอาจไม่เคยรู้ (array of int):
           · "💡 เทคนิคสังเกต" = key เสมอ
           · ขั้นที่มีกลวิธี เช่น การกำจัดตัวแปร, ทำไมจับกลุ่มทีละ 3, การประมาณราก = key
           · ❌ ไม่ใช่ key: ขั้นคำนวณ/แทนค่า/ถอดรากตรงไปตรงมา (∛(11³)=11), การหารซ้ำที่เห็นชัดแล้ว
           · เลือกเฉพาะ 1-2 ขั้นที่ทรงคุณค่าที่สุด ไม่ต้องครบทุกขั้น

        ตอบ JSON เท่านั้น ห้ามมีข้อความนอก JSON:
        { "solutionSteps": ["...", "...", "..."], "understandingStep": "...", "keyStepIndices": [1] }
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
        string problemText, string latex, string topic, bool hasFigure,
        string visionModel = "", string analysisStartedAt = "", string analysisEndedAt = "", string studentName = "")
    {
        var session = new TeachingSessionEntity
        {
            Id                = Guid.NewGuid().ToString(),
            ProblemText       = problemText,
            Latex             = latex,
            Topic             = topic,
            HasFigure         = hasFigure,
            StepsJson         = "[]",
            CurrentStep       = 1,
            Status            = "in_progress",
            CreatedAt         = DateTime.UtcNow.ToString("O"),
            VisionModel       = visionModel,
            AnalysisStartedAt = analysisStartedAt,
            AnalysisEndedAt   = analysisEndedAt,
            StudentName       = StudentNameNormalizer.Normalize(studentName),
        };

        if (hasFigure)
        {
            // Phase 1: analyse figure → ask student to confirm before generating step plan
            var figPrompt = FigureAnalysisPrompt
                .Replace("{problemText}", problemText)
                .Replace("{latex}", latex)
                .Replace("{topic}", topic);

            var figRaw = await chat.CompleteAsync(figPrompt);
            session.FigureDescription = ParseFigureDescription(figRaw);

            db.TeachingSessions.Add(session);
            await db.SaveChangesAsync();

            return new StartTeachingResult(session.Id, true, session.FigureDescription, null, 0);
        }

        // No figure → generate step plan immediately
        var steps = await GenerateStepPlanAsync(problemText, latex, topic, "");
        session.StepsJson = JsonSerializer.Serialize(steps);
        db.TeachingSessions.Add(session);
        await db.SaveChangesAsync();

        return new StartTeachingResult(session.Id, false, "", steps[0], steps.Count);
    }

    public async Task<ConfirmFigureResult> ConfirmFigureAsync(string sessionId, string studentNote)
    {
        var session = await db.TeachingSessions.FindAsync(sessionId)
            ?? throw new KeyNotFoundException($"session {sessionId} not found");

        session.FigureCorrection = string.IsNullOrWhiteSpace(studentNote) ? "ยืนยันถูกต้อง" : studentNote;

        var steps = await GenerateStepPlanAsync(
            session.ProblemText, session.Latex, session.Topic, session.FigureCorrection);

        session.StepsJson   = JsonSerializer.Serialize(steps);
        session.CurrentStep = 1;
        await db.SaveChangesAsync();

        return new ConfirmFigureResult(steps[0], steps.Count);
    }

    private async Task<List<TeachingStep>> GenerateStepPlanAsync(
        string problemText, string latex, string topic, string figureCorrection)
    {
        var figureContext = string.IsNullOrWhiteSpace(figureCorrection)
            ? ""
            : $"ข้อมูลยืนยันจากนักเรียน (ใช้เป็น ground truth): {figureCorrection}";

        var prompt = StepPlanPrompt
            .Replace("{problemText}", problemText)
            .Replace("{latex}", latex)
            .Replace("{topic}", topic)
            .Replace("{figureContext}", figureContext);

        var raw = await chat.CompleteAsync(prompt);
        return ParseSteps(raw);
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

    private static string ParseFigureDescription(string raw)
    {
        try
        {
            var json = ExtractJson(raw);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("figureDescription", out var d))
                return d.GetString() ?? raw.Trim();
        }
        catch { }
        return raw.Trim();
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

    public async Task<(string StudentNotes, string ParentSummary)> NotesAndSummaryAsync(string sessionId)
    {
        var session = await db.TeachingSessions.FindAsync(sessionId)
            ?? throw new KeyNotFoundException($"session {sessionId} not found");

        var steps = JsonSerializer.Deserialize<List<TeachingStep>>(session.StepsJson)!;
        var stepsText = string.Join("\n", steps.Select(s => $"- {s.Goal}"));

        var prompt = NotesPrompt
            .Replace("{problemText}", session.ProblemText)
            .Replace("{topic}", string.IsNullOrWhiteSpace(session.Topic) ? "คณิตศาสตร์" : session.Topic)
            .Replace("{stepsText}", stepsText);

        var raw = await chat.CompleteAsync(prompt);
        try
        {
            var json = ExtractJson(raw);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var notes   = root.TryGetProperty("studentNotes",  out var n) ? n.GetString() ?? "" : "";
            var summary = root.TryGetProperty("parentSummary", out var p) ? p.GetString() ?? "" : "";
            return (notes, summary);
        }
        catch
        {
            return ("ทำโจทย์นี้เสร็จแล้ว เก่งมากเลย!", "นักเรียนฝึกทำโจทย์คณิตศาสตร์จนจบ");
        }
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

    public async Task<SolveResult> SolveAsync(string problemText, string latex, string topic,
        string visionModel = "", string analysisStartedAt = "", string analysisEndedAt = "", string studentName = "")
    {
        var prompt = SolvePrompt
            .Replace("{problemText}", problemText)
            .Replace("{latex}", latex)
            .Replace("{topic}", topic);

        List<string> steps = [];
        string understanding = "";
        int[] keyStepIndices = [];
        const int maxAttempts = 3;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var raw = await chat.CompleteAsync(prompt);
            var (ok, parsedSteps, parsedUnderstanding, parsedKeys) = ParseSolveResponse(raw);
            if (ok)
            {
                steps = parsedSteps;
                understanding = parsedUnderstanding;
                keyStepIndices = parsedKeys;
                break;
            }
            if (attempt == maxAttempts - 1)
                steps = ["ดูวิธีทำเต็มไม่สำเร็จ กรุณาลองใหม่"];
        }

        var session = new TeachingSessionEntity
        {
            Id                = Guid.NewGuid().ToString(),
            ProblemText       = problemText,
            Latex             = latex,
            Topic             = topic,
            Mode              = "solve_first",
            SolveFirstCount   = 1,
            StepsJson         = "[]",
            Status            = "done",
            CreatedAt         = DateTime.UtcNow.ToString("O"),
            VisionModel       = visionModel,
            AnalysisStartedAt = analysisStartedAt,
            AnalysisEndedAt   = analysisEndedAt,
            StudentName       = StudentNameNormalizer.Normalize(studentName),
        };
        db.TeachingSessions.Add(session);
        await db.SaveChangesAsync();

        return new SolveResult(session.Id, [.. steps], understanding, keyStepIndices);
    }

    private static (bool Ok, List<string> Steps, string Understanding, int[] KeyStepIndices) ParseSolveResponse(string raw)
    {
        try
        {
            var json = ExtractJson(raw);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var steps = root.GetProperty("solutionSteps")
                .EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => s.Length > 0)
                .ToList();

            var understanding = root.TryGetProperty("understandingStep", out var u)
                ? u.GetString() ?? ""
                : "";

            var keyStepIndices = root.TryGetProperty("keyStepIndices", out var k)
                ? k.EnumerateArray().Select(e => e.GetInt32()).ToArray()
                : [];

            return steps.Count > 0
                ? (true, steps, understanding, keyStepIndices)
                : (false, [], "", []);
        }
        catch
        {
            return (false, [], "", []);
        }
    }

    private const string ExplainPrompt = """
        คุณคือติวเตอร์คณิตศาสตร์ ม.2 นักเรียนเพิ่งเห็นวิธีทำ แล้วกด "อธิบายเพิ่ม" ที่ขั้นนี้เพราะยังไม่เข้าใจ

        โจทย์: {problemText}
        หัวข้อ: {topic}
        วิธีทำทั้งหมด (บริบท): {fullSolution}
        ขั้นที่นักเรียนอยากให้อธิบายเพิ่ม: {stepText}

        อธิบายขั้น/เทคนิคนี้ให้เด็กที่ "ไม่เคยเห็นเทคนิคนี้มาก่อน" เข้าใจ:
        - เน้น "ทำไมถึงทำแบบนี้" / "ทำไมเทคนิคนี้ใช้ได้" ไม่ใช่แค่ทำซ้ำ
        - ยกตัวอย่างเล็กๆ 1 ตัว (เลขอื่นที่ง่ายกว่า) ให้เห็นว่ากฎทำงานยังไง
        - ภาษาไทยเป็นกันเอง · สั้น 3-5 ประโยค · ไม่ใช้ศัพท์ยาก
        - สัญลักษณ์ Unicode เท่านั้น (∛ √ ² ³ × ÷ − ∴) ❌ ห้าม LaTeX

        ตอบ JSON เท่านั้น ห้ามมีข้อความนอก JSON:
        { "explanation": "..." }
        """;

    public async Task<string> ExplainAsync(string problemText, string topic, string stepText, string fullSolution)
    {
        var prompt = ExplainPrompt
            .Replace("{problemText}", problemText)
            .Replace("{topic}", topic)
            .Replace("{stepText}", stepText)
            .Replace("{fullSolution}", fullSolution);

        var raw = await chat.CompleteAsync(prompt);
        try
        {
            var json = ExtractJson(raw);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("explanation").GetString() ?? "";
        }
        catch
        {
            return "ไม่สามารถอธิบายเพิ่มได้ กรุณาลองใหม่";
        }
    }

    private static string ExtractJson(string text)
    {
        var t = text.Trim();
        // strip qwen3 <think>...</think> before finding JSON
        var thinkEnd = t.LastIndexOf("</think>");
        if (thinkEnd >= 0) t = t[(thinkEnd + 8)..].Trim();
        if (t.StartsWith("```")) { var s = t.IndexOf('\n') + 1; var e = t.LastIndexOf("```"); if (e > s) t = t[s..e].Trim(); }
        var start = t.IndexOf('{'); var end = t.LastIndexOf('}');
        return (start >= 0 && end > start) ? t[start..(end + 1)] : t;
    }
}
