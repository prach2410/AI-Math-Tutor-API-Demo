using backend.Models;

namespace backend.Services;

public class LearningFlowService
{
    private static readonly string[] TriggerWords =
        ["ไม่รู้", "งง", "ไม่เข้าใจ", "ขอเฉลย", "ไม่ทราบ", "ช่วยด้วย"];

    private readonly Dictionary<string, ScenarioDefinition> _scenarios;

    public LearningFlowService()
    {
        _scenarios = BuildScenarios().ToDictionary(s => s.Id);
    }

    public ScenarioDefinition? GetScenario(string id) =>
        _scenarios.GetValueOrDefault(id);

    public AssistResponse GetAssist(string scenarioId, int stepNumber, string type)
    {
        var scenario = GetScenario(scenarioId);
        var step = scenario?.Steps.FirstOrDefault(s => s.StepNumber == stepNumber);
        if (step is null) return new AssistResponse("ไม่พบข้อมูลครับ");

        return type switch
        {
            "hint"          => new AssistResponse($"💡 {step.Hint}"),
            "guided"        => new AssistResponse(step.GuidedAssistance),
            "worked-example"=> new AssistResponse(step.WorkedExample),
            _               => new AssistResponse("ไม่รู้จัก action นี้ครับ"),
        };
    }

    public EvaluateResponse Evaluate(EvaluateRequest request)
    {
        var scenario = GetScenario(request.ScenarioId);
        if (scenario is null) return Fail("ไม่พบ scenario ครับ");

        var step = scenario.Steps.FirstOrDefault(s => s.StepNumber == request.StepNumber);
        if (step is null) return Fail("ไม่พบขั้นตอนนี้ครับ");

        bool needsGuided = HasTriggerWord(request.Answer) || request.WrongCount >= 2;
        if (needsGuided)
        {
            return new EvaluateResponse(
                Correct: false,
                Message: step.GuidedAssistance,
                Hint: null,
                NextStep: null,
                StudentNote: null,
                ParentSummary: null,
                IsGuidedAssistance: true,
                LearningReflection: null
            );
        }

        bool correct = IsCorrect(request.Answer, step.ExpectedAnswer);

        if (!correct)
        {
            return new EvaluateResponse(
                Correct: false,
                Message: "ลองคิดใหม่อีกครั้งนะครับ 🤔",
                Hint: step.Hint,
                NextStep: null,
                StudentNote: null,
                ParentSummary: null,
                IsGuidedAssistance: false,
                LearningReflection: null
            );
        }

        if (step.IsLast)
        {
            var (studentFeedback, parentCoachingTips) = BuildFeedback(request.HintCount, request.GuidedCount);
            return new EvaluateResponse(
                Correct: true,
                Message: "ยอดเยี่ยมมากเลยครับ! 🎉 ทำครบทุกขั้นตอนแล้ว",
                Hint: null,
                NextStep: null,
                StudentNote: scenario.StudentNote,
                ParentSummary: scenario.ParentSummary,
                IsGuidedAssistance: false,
                LearningReflection: scenario.LearningReflection,
                StudentFeedback: studentFeedback,
                ParentCoachingTips: parentCoachingTips
            );
        }

        var next = scenario.Steps.First(s => s.StepNumber == request.StepNumber + 1);
        return new EvaluateResponse(
            Correct: true,
            Message: "ยอดเยี่ยมครับ! 🎉",
            Hint: null,
            NextStep: new NextStepDto(next.StepNumber, step.TotalSteps, next.Question, next.IsLast),
            StudentNote: null,
            ParentSummary: null,
            IsGuidedAssistance: false,
            LearningReflection: null
        );
    }

    private static bool HasTriggerWord(string answer)
    {
        var lower = answer.Trim().ToLower();
        return TriggerWords.Any(w => lower.Contains(w));
    }

    private static bool IsCorrect(string answer, string expected)
        => Normalize(answer) == Normalize(expected);

    private static string Normalize(string s) =>
        s.Trim().ToLower()
         .Replace(",", "").Replace(" ", "")
         .Replace("×", "x").Replace("*", "x");

    private static EvaluateResponse Fail(string msg) =>
        new(false, msg, null, null, null, null, false, null);

    private static (string studentFeedback, string parentCoachingTips) BuildFeedback(int hintCount, int guidedCount)
    {
        string level;
        string studentStrengths;
        string studentImprove;

        if (hintCount == 0 && guidedCount == 0)
        {
            level = "ยอดเยี่ยม";
            studentStrengths = "✅ เข้าใจแนวคิดได้ทันที\n✅ คำนวณทีละขั้นได้ถูกต้อง\n✅ แก้ปัญหาด้วยตนเองได้ทั้งหมด";
            studentImprove = "ลองท้าทายตัวเองด้วยโจทย์ยากขึ้นได้เลยครับ!";
        }
        else if (hintCount <= 2 && guidedCount == 0)
        {
            level = "ดีมาก";
            studentStrengths = "✅ เข้าใจเนื้อหาได้ดี\n✅ พยายามคิดก่อนขอความช่วยเหลือ\n✅ ทำต่อด้วยตนเองได้";
            studentImprove = "• ลองทำโจทย์เพิ่มเติมโดยไม่ดู hint ครับ\n• ฝึกตรวจคำตอบก่อนส่ง";
        }
        else
        {
            level = "กำลังพัฒนา";
            studentStrengths = "✅ พยายามได้ดีมาก\n✅ ไม่ยอมแพ้และทำจนครบทุกขั้น";
            studentImprove = "• ฝึกการคูณเลขให้คล่องขึ้นครับ\n• ลองทำซ้ำโจทย์เดิมโดยไม่ขอความช่วยเหลือ";
        }

        string needsPracticeNote = guidedCount >= 2
            ? "\n\n📌 แนะนำให้ฝึกพื้นฐานเพิ่มเติมก่อนไปโจทย์ถัดไปนะครับ"
            : "";

        string studentFeedback = $"""
            🌟 Feedback จาก AI Tutor

            ระดับ: {level}

            วันนี้หนูทำได้ดีมากเลยครับ!

            {studentStrengths}

            สิ่งที่ควรฝึกเพิ่ม
            {studentImprove}{needsPracticeNote}

            Keep Going! 🚀
            """;

        string parentCoachingTips = $"""
            👨‍👩‍👧 ข้อเสนอแนะสำหรับผู้ปกครอง

            ระดับวันนี้: {level}

            วันนี้ลูกเรียนเรื่องปริมาตรและพื้นที่ผิวทรงสี่เหลี่ยมมุมฉาก
            และทำภารกิจจนครบทุกขั้นตอนแล้วครับ

            กิจกรรมที่แนะนำ
            - ลองชวนลูกหาตัวอย่างทรงสี่เหลี่ยมในบ้าน
            - ลองถามว่าปริมาตรหรือพื้นที่ผิวใช้ทำอะไรได้บ้าง
            - ชมเชยความพยายามมากกว่าผลลัพธ์ครับ

            เป้าหมายคือการเชื่อมโยงคณิตศาสตร์กับชีวิตจริง 🏠
            """;

        return (studentFeedback, parentCoachingTips);
    }

    private static List<ScenarioDefinition> BuildScenarios() =>
    [
        new ScenarioDefinition(
            Id: "fish-tank",
            Title: "ตู้ปลา — ปริมาตรทรงสี่เหลี่ยมมุมฉาก",
            StudentNote: """
                บันทึกการเรียน: ปริมาตรทรงสี่เหลี่ยมมุมฉาก

                สูตร
                  ปริมาตร = กว้าง × ยาว × สูง

                ตัวอย่าง: ตู้ปลา
                  กว้าง  20 ซม.  ยาว  40 ซม.  สูง  30 ซม.

                คำตอบ
                  20 × 40 × 30 = 24,000 ลบ.ซม. = 24 ลิตร

                สิ่งที่ต้องจำ
                  ปริมาตร = พื้นที่ภายในที่บรรจุของได้
                """,
            ParentSummary: """
                สรุปการเรียนสำหรับผู้ปกครอง

                บทเรียนวันนี้
                  ปริมาตรทรงสี่เหลี่ยมมุมฉาก ผ่านโจทย์ตู้ปลา

                ผลการเรียน
                  ✅ เข้าใจความหมายของปริมาตร
                  ✅ ใช้สูตร กว้าง × ยาว × สูง ได้
                  ✅ คำนวณได้ถูกต้องทุกขั้นตอน

                โจทย์ที่ทำ
                  ตู้ปลา 20×40×30 ซม. = 24,000 ลบ.ซม.

                ระดับความเข้าใจ  ⭐⭐⭐⭐ ดีมาก
                """,
            RealWorldUses: ["ตู้ปลา", "ถังเก็บน้ำ", "ห้องเก็บของ", "กล่องของขวัญ"],
            LearningReflection:
            [
                "ปริมาตรคืออะไร",
                "สูตร กว้าง × ยาว × สูง",
                "คำนวณปริมาตรตู้ปลาได้",
                "แปลง ลบ.ซม. เป็นลิตร"
            ],
            Steps:
            [
                new(1, 4,
                    Question: "ปริมาตรของทรงสี่เหลี่ยมมุมฉากคำนวณด้วยสูตรอะไรครับ?",
                    ExpectedAnswer: "กว้างxยาวxสูง",
                    Hint: "ลองนึกถึงการหาพื้นที่ฐาน แล้วคูณความสูงครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪 มาเริ่มด้วยกันนะครับ\n\nปริมาตรคือพื้นที่ภายในที่บรรจุของได้\nเราหาได้จากการคูณ 3 ด้านเข้าด้วยกัน\n\nตัวอย่าง: กล่องเล็กกว้าง 2 × ยาว 3 × สูง 4 = 24 ลบ.ซม.\n\nทีนี้ลองเขียนสูตรทั่วไปดูครับ",
                    WorkedExample: "ตัวอย่างการหาปริมาตร\n\nปริมาตร\n  = กว้าง × ยาว × สูง\n  = 20 × 40 × 30\n  = 24,000 ลบ.ซม.\n\nลองพิมพ์สูตรให้ถูกต้องดูครับ",
                    IsLast: false),
                new(2, 4,
                    Question: "20 × 40 ได้เท่าไรครับ?",
                    ExpectedAnswer: "800",
                    Hint: "ลองคูณ 2 × 4 ก่อน แล้วเติมศูนย์ครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪\n\n2 × 4 = 8\nแล้ว 20 × 40 ก็แค่เติมศูนย์ให้ครบ\nได้ 8__ ครับ (เติมศูนย์กี่ตัวดีครับ?)",
                    WorkedExample: "วิธีคูณ 20 × 40\n\n2 × 4 = 8\nเติมศูนย์อีก 2 ตัว (จาก 20 และ 40)\n= 800\n\nคำตอบคือ 800 ครับ",
                    IsLast: false),
                new(3, 4,
                    Question: "800 × 30 ได้เท่าไรครับ?",
                    ExpectedAnswer: "24000",
                    Hint: "ลองคูณ 8 × 3 ก่อน แล้วเติมศูนย์ครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪\n\n8 × 3 = 24\nแล้ว 800 × 30 ก็เติมศูนย์ให้ครบ\n24___ ครับ (เติมศูนย์กี่ตัวดีครับ?)",
                    WorkedExample: "วิธีคูณ 800 × 30\n\n8 × 3 = 24\nเติมศูนย์อีก 3 ตัว (จาก 800 และ 30)\n= 24,000\n\nคำตอบคือ 24,000 ครับ",
                    IsLast: false),
                new(4, 4,
                    Question: "24,000 ลบ.ซม. เท่ากับกี่ลิตรครับ? (1,000 ลบ.ซม. = 1 ลิตร)",
                    ExpectedAnswer: "24",
                    Hint: "หารด้วย 1,000 ครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪\n\n1,000 ลบ.ซม. = 1 ลิตร\n2,000 ลบ.ซม. = 2 ลิตร\n24,000 ลบ.ซม. = __ ลิตรครับ?",
                    WorkedExample: "การแปลงหน่วย\n\n24,000 ลบ.ซม. ÷ 1,000 = 24 ลิตร\n\nตู้ปลานี้จุน้ำได้ 24 ลิตรครับ",
                    IsLast: true),
            ]
        ),

        new ScenarioDefinition(
            Id: "parcel",
            Title: "กล่องพัสดุ — พื้นที่ผิวทรงสี่เหลี่ยมมุมฉาก",
            StudentNote: """
                บันทึกการเรียน: พื้นที่ผิวทรงสี่เหลี่ยมมุมฉาก

                สูตร
                  พื้นที่ผิว = 2(กว้าง×ยาว + กว้าง×สูง + ยาว×สูง)

                ตัวอย่าง: กล่องพัสดุ
                  กว้าง  20 ซม.  ยาว  30 ซม.  สูง  10 ซม.

                คำตอบ
                  2(600 + 200 + 300) = 2,200 ตร.ซม.

                สิ่งที่ต้องจำ
                  กล่องมี 6 หน้า แบ่งเป็น 3 คู่
                """,
            ParentSummary: """
                สรุปการเรียนสำหรับผู้ปกครอง

                บทเรียนวันนี้
                  พื้นที่ผิวทรงสี่เหลี่ยมมุมฉาก ผ่านโจทย์กล่องพัสดุ

                ผลการเรียน
                  ✅ เข้าใจความหมายของพื้นที่ผิว
                  ✅ คิดเชิงพื้นที่ (6 หน้า 3 คู่)
                  ✅ ใช้สูตรและคำนวณได้ถูกต้อง

                โจทย์ที่ทำ
                  กล่องพัสดุ 20×30×10 ซม. = 2,200 ตร.ซม.

                ระดับความเข้าใจ  ⭐⭐⭐⭐ ดีมาก
                """,
            RealWorldUses: ["การห่อของขวัญ", "กล่องพัสดุ", "งานบรรจุภัณฑ์", "การออกแบบสินค้า"],
            LearningReflection:
            [
                "พื้นที่ผิวคืออะไร",
                "กล่องมี 6 หน้า แบ่งเป็น 3 คู่",
                "สูตร 2(กว้าง×ยาว + กว้าง×สูง + ยาว×สูง)",
                "คำนวณพื้นที่ผิวกล่องพัสดุได้"
            ],
            Steps:
            [
                new(1, 4,
                    Question: "พื้นที่ผิวทรงสี่เหลี่ยมมุมฉากคำนวณด้วยสูตรอะไรครับ?",
                    ExpectedAnswer: "2(กว้างxยาว+กว้างxสูง+ยาวxสูง)",
                    Hint: "กล่องมี 6 หน้า แบ่งเป็น 3 คู่ครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪 มาดูด้วยกันนะครับ\n\nกล่องมี 6 หน้า แบ่งเป็น 3 คู่\n• บน+ล่าง = 2(กว้าง×ยาว)\n• หน้า+หลัง = 2(กว้าง×สูง)\n• ซ้าย+ขวา = 2(ยาว×สูง)\n\nรวม = 2(กว้าง×ยาว + กว้าง×สูง + ยาว×สูง)\n\nลองพิมพ์สูตรนี้ดูครับ",
                    WorkedExample: "ตัวอย่างการหาพื้นที่ผิว\n\nพื้นที่ผิว\n  = (20×30×2)\n  + (20×10×2)\n  + (30×10×2)\n  = 1,200 + 400 + 600\n  = 2,200 ตร.ซม.\n\nลองพิมพ์สูตรทั่วไปดูครับ",
                    IsLast: false),
                new(2, 4,
                    Question: "20 × 30 ได้เท่าไรครับ? (พื้นที่หน้าบน-ล่าง)",
                    ExpectedAnswer: "600",
                    Hint: "2 × 3 = 6 แล้วเติมศูนย์ครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪\n\n2 × 3 = 6\nแล้ว 20 × 30 ก็แค่เติมศูนย์ให้ครบ\n6__ ครับ (เติมศูนย์กี่ตัวดีครับ?)",
                    WorkedExample: "วิธีคูณ 20 × 30\n\n2 × 3 = 6\nเติมศูนย์อีก 2 ตัว\n= 600\n\nครูช่วยเริ่มให้ก่อนนะ\n20 × 30 = 600\nลองทำขั้นถัดไปดูครับ",
                    IsLast: false),
                new(3, 4,
                    Question: "600 + 200 + 300 ได้เท่าไรครับ?",
                    ExpectedAnswer: "1100",
                    Hint: "รวมทีละคู่ครับ 600+200 ก่อน",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪\n\nลองทีละขั้น\n600 + 200 = ?\nแล้วค่อยบวก 300 ครับ",
                    WorkedExample: "วิธีบวก 600 + 200 + 300\n\n600 + 200 = 800\n800 + 300 = 1,100\n\nคำตอบคือ 1,100 ครับ",
                    IsLast: false),
                new(4, 4,
                    Question: "1,100 × 2 ได้เท่าไรครับ? (คูณ 2 เพราะมี 3 คู่)",
                    ExpectedAnswer: "2200",
                    Hint: "1,100 + 1,100 ครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪\n\n1,100 × 2 แปลว่า 1,100 + 1,100\n= 2,___ ครับ",
                    WorkedExample: "วิธีคูณ 1,100 × 2\n\n1,100 + 1,100 = 2,200\n\nพื้นที่ผิวกล่องพัสดุ = 2,200 ตร.ซม.\nต้องใช้กระดาษห่ออย่างน้อย 2,200 ตร.ซม. ครับ",
                    IsLast: true),
            ]
        ),

        new ScenarioDefinition(
            Id: "water-tank",
            Title: "ถังน้ำ — ปริมาตรและการแปลงหน่วย",
            StudentNote: """
                บันทึกการเรียน: ปริมาตรและการแปลงหน่วย

                สูตร
                  ปริมาตร = กว้าง × ยาว × สูง
                  1,000 ลบ.ซม. = 1 ลิตร

                ตัวอย่าง: ถังน้ำ
                  กว้าง 50 ซม.  ยาว 100 ซม.  สูง 80 ซม.

                คำตอบ
                  50 × 100 × 80 = 400,000 ลบ.ซม. = 400 ลิตร

                สิ่งที่ต้องจำ
                  หาร ลบ.ซม. ด้วย 1,000 เพื่อได้ลิตร
                """,
            ParentSummary: """
                สรุปการเรียนสำหรับผู้ปกครอง

                บทเรียนวันนี้
                  ปริมาตรและการแปลงหน่วย ผ่านโจทย์ถังน้ำ

                ผลการเรียน
                  ✅ คำนวณปริมาตรได้
                  ✅ แปลง ลบ.ซม. เป็นลิตรได้
                  ✅ เชื่อมโยงกับการใช้น้ำในชีวิตจริง

                โจทย์ที่ทำ
                  ถังน้ำ 50×100×80 ซม. = 400 ลิตร

                ระดับความเข้าใจ  ⭐⭐⭐⭐ ดีมาก
                """,
            RealWorldUses: ["ระบบเกษตร", "ถังเก็บน้ำ", "ระบบชลประทาน", "การวางแผนใช้น้ำ"],
            LearningReflection:
            [
                "ปริมาตรทรงสี่เหลี่ยมมุมฉาก",
                "การแปลง ลบ.ซม. เป็นลิตร",
                "1,000 ลบ.ซม. = 1 ลิตร",
                "การใช้งานในระบบน้ำจริง"
            ],
            Steps:
            [
                new(1, 4,
                    Question: "ปริมาตรของทรงสี่เหลี่ยมมุมฉากคำนวณด้วยสูตรอะไรครับ?",
                    ExpectedAnswer: "กว้างxยาวxสูง",
                    Hint: "ลองนึกถึงการหาพื้นที่ฐาน แล้วคูณความสูงครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪 มาเริ่มด้วยกันนะครับ\n\nปริมาตรคือพื้นที่ภายในที่บรรจุของได้\nเราหาได้จากการคูณ 3 ด้านเข้าด้วยกัน\n\nตัวอย่าง: กล่องเล็กกว้าง 2 × ยาว 3 × สูง 4 = 24 ลบ.ซม.\n\nทีนี้ลองเขียนสูตรทั่วไปดูครับ",
                    WorkedExample: "ตัวอย่างการหาปริมาตร\n\nปริมาตร\n  = กว้าง × ยาว × สูง\n  = 50 × 100 × 80\n  = 400,000 ลบ.ซม.\n\nลองพิมพ์สูตรให้ถูกต้องดูครับ",
                    IsLast: false),
                new(2, 4,
                    Question: "50 × 100 ได้เท่าไรครับ?",
                    ExpectedAnswer: "5000",
                    Hint: "5 × 1 ก่อน แล้วเติมศูนย์ 3 ตัวครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪\n\n5 × 1 = 5\nแล้ว 50 × 100 ก็แค่เติมศูนย์ให้ครบ\n5___ ครับ (เติมศูนย์กี่ตัวดีครับ?)",
                    WorkedExample: "วิธีคูณ 50 × 100\n\n5 × 1 = 5\nเติมศูนย์อีก 3 ตัว (จาก 50 และ 100)\n= 5,000\n\nคำตอบคือ 5,000 ครับ",
                    IsLast: false),
                new(3, 4,
                    Question: "5,000 × 80 ได้เท่าไรครับ?",
                    ExpectedAnswer: "400000",
                    Hint: "5 × 8 = 40 แล้วเติมศูนย์ 3 ตัวครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪\n\n5 × 8 = 40\nแล้ว 5,000 × 80 ก็เติมศูนย์ให้ครบ\n40____ ครับ (เติมศูนย์กี่ตัวดีครับ?)",
                    WorkedExample: "วิธีคูณ 5,000 × 80\n\n5 × 8 = 40\nเติมศูนย์อีก 4 ตัว (จาก 5,000 และ 80)\n= 400,000\n\nคำตอบคือ 400,000 ครับ",
                    IsLast: false),
                new(4, 4,
                    Question: "400,000 ลบ.ซม. เท่ากับกี่ลิตรครับ? (1,000 ลบ.ซม. = 1 ลิตร)",
                    ExpectedAnswer: "400",
                    Hint: "หารด้วย 1,000 ครับ",
                    GuidedAssistance: "ไม่เป็นไรเลยครับ! 💪\n\n1,000 ลบ.ซม. = 1 ลิตร\n10,000 ลบ.ซม. = 10 ลิตร\n400,000 ลบ.ซม. = __ ลิตรครับ?",
                    WorkedExample: "การแปลงหน่วย\n\n400,000 ลบ.ซม. ÷ 1,000 = 400 ลิตร\n\nถังน้ำนี้จุน้ำได้ 400 ลิตร\nถ้าใช้น้ำวันละ 10 ลิตร จะอยู่ได้นาน 40 วันครับ",
                    IsLast: true),
            ]
        ),
    ];
}
