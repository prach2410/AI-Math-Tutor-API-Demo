using backend.Models;

namespace backend.Services;

public class ProjectBrainTutorService
{
    // ── Grill questions ──────────────────────────────────────────────────────
    private static readonly string[] GrillQuestions =
    [
        "ลองนึกภาพดูนะคะ — เด็กตอบถูกทุกข้อในข้อสอบ\nแต่พอถามว่า \"ทำไมถึงเลือกวิธีนี้?\" เขาตอบไม่ได้\nคุณคิดว่าเขาเข้าใจจริงๆ ไหม เพราะอะไร?",
        "ถ้าลูกบอกว่า \"อ่านหนังสือจบแล้ว เข้าใจแล้ว\"\nคุณจะรู้ได้ยังไงว่าเขาเข้าใจจริง ไม่ใช่แค่จำได้?",
        "ถ้าคุณเห็นว่าลูกเอาความรู้จากในห้องเรียน\nไปใช้อธิบายเรื่องในชีวิตจริงได้เอง\nนั่นบอกอะไรเกี่ยวกับความเข้าใจของเขา?",
    ];

    // ── Evidence keyword patterns ────────────────────────────────────────────
    private static readonly string[] ExplainWords  = ["หมายความว่า", "ก็คือ", "คือว่า", "หมายถึง", "นั่นก็คือ"];
    private static readonly string[] ReasonWords   = ["เพราะ", "เหตุผล", "เนื่องจาก", "เพราะว่า"];
    private static readonly string[] ApplyWords    = ["เช่น", "ตัวอย่าง", "ลูก", "ในชีวิต", "ใช้กับ"];
    private static readonly string[] ConnectWords  = ["เหมือน", "คล้าย", "เชื่อมโยง", "เกี่ยวกับ", "ก็เหมือน"];
    private static readonly string[] ReflectWords  = ["ไม่แน่ใจ", "คิดว่า", "น่าจะ", "อาจจะ", "สงสัย", "ยังไม่"];
    private static readonly string[] QuestionWords = ["?", "？", "อย่างไร", "ทำไม", "ได้ไหม", "ใช่ไหม"];

    public ProjectBrainResponse Chat(ProjectBrainRequest req)
    {
        var n         = string.IsNullOrWhiteSpace(req.StudentName) ? "คุณ" : req.StudentName;
        var msg       = req.Message.Trim();
        var phase     = req.Phase;
        var userTurns = req.History.Count(h => h.Role == "user");

        // Extract evidence from user message (skip teach/retrieval/guided/ready — still context-building)
        List<EvidenceItem>? evidence = null;
        if (phase != "teach" && phase != "retrieval" && phase != "guided" && phase != "ready" && msg != "เริ่ม")
            evidence = ExtractEvidence(msg);

        // Explicit summary request any time
        if (phase == "summary" || msg.Contains("สรุป") || msg.Contains("ขอสรุป"))
            return new ProjectBrainResponse(BuildSummary(req.History, n), "summary", Evidence: evidence);

        var baseResponse = phase switch
        {
            "teach"     => HandleTeach(n),
            "retrieval" => HandleRetrieval(n, req.PriorEvidenceSummary ?? ""),
            "guided"    => HandleGuided(n, msg, userTurns),
            "ready"     => HandleReady(n, msg),
            "check"     => HandleCheck(n, msg, evidence, userTurns),
            "reflect"   => HandleReflect(n, msg),
            "grill"     => HandleGrill(n, msg, userTurns),
            _           => HandleTeach(n),
        };

        return baseResponse with { Evidence = evidence };
    }

    // ── Phase handlers ───────────────────────────────────────────────────────

    private static ProjectBrainResponse HandleTeach(string n)
    {
        var text =
            $"สวัสดีนะคะ {n} 😊\n\n" +
            "วันนี้เราจะคุยกันเรื่อง **Understanding Engine** —\n" +
            "แนวคิดหลักที่อยู่เบื้องหลัง AI Tutor ตัวนี้\n\n" +
            "───────────────────────\n" +
            "💡 แนวคิดสำคัญ\n\n" +
            "ความเข้าใจไม่ได้เกิดขึ้นตอนที่เราตอบถูก\n" +
            "แต่เกิดขึ้นตอนที่เราสามารถ อธิบายเหตุผล ได้\n\n" +
            "Correct Answer ≠ Understanding\n\n" +
            "Understanding Engine คือระบบที่ช่วยเปลี่ยน\n" +
            "\"ความเข้าใจที่ซ่อนอยู่\"\n" +
            "ให้กลายเป็น\n" +
            "\"หลักฐานความเข้าใจที่สังเกตได้\"\n\n" +
            "ผ่านการสนทนา การสะท้อนความคิด และการให้เหตุผล\n" +
            "───────────────────────\n\n" +
            "อ่านจบแล้วนะคะ 🙂\n" +
            "ลองอธิบายให้ฟังได้เลยค่ะ —\n" +
            "Understanding Engine คืออะไร ในความเข้าใจของคุณ?";

        // ← advances to "guided" (Guided Understanding) before Comprehension Check
        return new ProjectBrainResponse(text, "guided");
    }

    private static ProjectBrainResponse HandleRetrieval(string n, string priorSummary)
    {
        var summaryBlock = string.IsNullOrWhiteSpace(priorSummary)
            ? "— (ยังไม่มีข้อมูลจากครั้งก่อน)"
            : priorSummary;

        var text =
            $"ยินดีต้อนรับกลับนะคะ {n} 😊\n\n" +
            "ครั้งที่แล้วเราคุยกันเรื่อง Understanding Engine\n" +
            "นี่คือสิ่งที่เหลืออยู่จากการสนทนาครั้งก่อน:\n\n" +
            $"{summaryBlock}\n\n" +
            "วันนี้ยังเห็นด้วยกับสิ่งเหล่านี้อยู่ไหมคะ?\n" +
            "หรืออยากเพิ่มเติมหรือเปลี่ยนใจอะไรก็ได้เลย 😊";

        // Skip teach for returning users — go straight to check
        return new ProjectBrainResponse(text, "check");
    }

    private static ProjectBrainResponse HandleGuided(string n, string msg, int userTurns)
    {
        // After 2+ user turns in guided → advance to ready check
        if (userTurns >= 2)
        {
            var readyText =
                $"ขอบคุณที่แชร์ความคิดนะคะ {n} 😊\n\n" +
                "ตอนนี้รู้สึกว่าพร้อมลองอธิบายแนวคิดนี้ด้วยคำของตัวเองแล้วไหมคะ?\n" +
                "หรืออยากให้ยกตัวอย่างเพิ่มเติมก่อนก็ได้นะคะ";
            return new ProjectBrainResponse(readyText, "ready");
        }

        // Turn 1: concrete examples + analogy to build mental model
        var guidedText =
            $"ให้ลองนึกภาพแบบนี้นะคะ {n} 🤔\n\n" +
            "**ตัวอย่างที่ 1 — ห้องเรียนทั่วไป**\n" +
            "นักเรียนอ่านสูตรคณิตศาสตร์ได้ จำขั้นตอนได้ ทำข้อสอบผ่าน\n" +
            "แต่พอถามว่า 'ทำไมถึงต้องคูณตรงนี้?' — ตอบไม่ได้\n\n" +
            "**ตัวอย่างที่ 2 — AI Tutor ตัวนี้**\n" +
            "แทนที่จะแค่ให้คำตอบถูก\n" +
            "ระบบถามว่า 'คิดว่าทำไมถึงได้คำตอบนี้?'\n" +
            "เพราะการอธิบายเหตุผลคือหลักฐานว่าเข้าใจจริง — ไม่ใช่แค่จำได้\n\n" +
            "───────────────────────\n" +
            "ลองนึกดูนะคะ — ในประสบการณ์ของคุณ\n" +
            "เคยเจอสถานการณ์ที่ 'รู้' แต่ 'ไม่เข้าใจจริง' ไหมคะ?";

        return new ProjectBrainResponse(guidedText, "guided");
    }

    private static ProjectBrainResponse HandleReady(string n, string msg)
    {
        // Always advance to check — 1 turn only, don't block the flow
        var text =
            $"เยี่ยมเลยค่ะ {n} 😊\n\n" +
            "ตอนนี้ลองอธิบายด้วยคำของตัวเองได้เลยนะคะ —\n" +
            "**Understanding Engine คืออะไร ในความเข้าใจของคุณ?**\n\n" +
            "ไม่ต้องสมบูรณ์แบบค่ะ แค่บอกตามที่เข้าใจตอนนี้ได้เลย";

        return new ProjectBrainResponse(text, "check");
    }

    private static ProjectBrainResponse HandleCheck(string n, string msg,
        List<EvidenceItem>? evidence, int userTurns)
    {
        bool hasExplain = evidence?.Any(e => e.EvidenceType == "Explain") ?? false;

        // Success: Explain evidence found → advance to reflect
        if (hasExplain)
        {
            var successText =
                $"ดีมากเลยค่ะ {n} 👏\n" +
                "คุณอธิบายได้ชัดเจนมาก — นั่นแสดงว่าเข้าใจแนวคิดแล้ว\n\n" +
                "มาลองคิดให้ลึกขึ้นอีกหน่อยนะคะ\n\n" +
                GrillQuestions[0].Replace("ลองนึกภาพดูนะคะ",
                    "ถาม Reflection สักข้อนะคะ\n\nลองนึกภาพดูนะคะ");

            return new ProjectBrainResponse(successText, "reflect");
        }

        // Graceful degradation: 2+ turns without Explain → advance anyway
        if (userTurns >= 2)
        {
            var fallbackText =
                $"ขอบคุณนะคะ {n} 😊\n" +
                "ไม่เป็นไรเลย — บางแนวคิดต้องใช้เวลาในการตกผลึก\n\n" +
                "มาลองดูกันต่อนะคะ ขอถามสถานการณ์จริงสักข้อ\n\n" +
                GrillQuestions[0];

            return new ProjectBrainResponse(fallbackText, "reflect");
        }

        // Stay in check: provide clarification + re-ask
        var clarifyText =
            $"ไม่เป็นไรเลยนะคะ 🙂\n\n" +
            "ลองคิดแบบนี้ก็ได้ —\n" +
            "ถ้าเพื่อนถามว่า 'AI Tutor นี้ต่างจาก Google ยังไง?'\n" +
            "คุณจะตอบว่าอะไร?\n\n" +
            "ไม่ต้องสมบูรณ์แบบค่ะ แค่บอกตามที่คิดได้เลย";

        return new ProjectBrainResponse(clarifyText, "check");
    }

    private static ProjectBrainResponse HandleReflect(string n, string msg)
    {
        bool hasReason  = Contains(msg, ReasonWords);
        bool hasExample = Contains(msg, ApplyWords);

        string ack;
        if (hasReason && hasExample)
            ack = $"เยี่ยมมากเลยค่ะ {n} 👏\nคุณให้ทั้งเหตุผลและตัวอย่าง — นั่นคือหลักฐานความเข้าใจที่ดีมาก\n\n";
        else if (hasReason)
            ack = $"ขอบคุณนะคะ {n} 😊 คุณให้เหตุผลได้ดีเลย\n\n";
        else if (hasExample)
            ack = $"ขอบคุณค่ะ {n} การยกตัวอย่างแบบนี้ช่วยให้เห็นภาพได้ชัดเจนมาก\n\n";
        else
            ack = $"ขอบคุณที่แชร์ความคิดนะคะ {n} 😊\n\n";

        var text = ack +
            "มาลองดูกันต่อนะคะ — ขอถามสถานการณ์จริงสักข้อ\n\n" +
            GrillQuestions[0];

        return new ProjectBrainResponse(text, "grill");
    }

    private static ProjectBrainResponse HandleGrill(string n, string msg, int userTurns)
    {
        int grillTurn = Math.Max(0, userTurns - 1);

        bool hasReason  = Contains(msg, ReasonWords);
        bool hasReflect = Contains(msg, ReflectWords);

        string ack;
        if (hasReason)
            ack = $"ชอบเหตุผลที่ให้มากเลยค่ะ {n} 💡\n\n";
        else if (hasReflect)
            ack = $"การที่คุณบอกว่าไม่แน่ใจ — นั่นคือการ Reflect ที่ดีมากเลยนะคะ 😊\n\n";
        else
            ack = $"ขอบคุณสำหรับคำตอบนะคะ {n}\n\n";

        if (grillTurn >= 2)
        {
            var closingText = ack +
                "คุณได้แสดงความคิดออกมาได้ดีมากเลยค่ะ 🎉\n\n" +
                "ตอนนี้พร้อมดูสรุปความเข้าใจแล้วไหมคะ?\n" +
                "กดปุ่ม \"ดูสรุปความเข้าใจ\" ได้เลยนะคะ";

            return new ProjectBrainResponse(closingText, "grill", SuggestSummary: true);
        }

        int nextIdx = Math.Min(grillTurn + 1, GrillQuestions.Length - 1);
        var text = ack + GrillQuestions[nextIdx];

        return new ProjectBrainResponse(text, "grill");
    }

    // ── Evidence extraction ──────────────────────────────────────────────────

    private static List<EvidenceItem> ExtractEvidence(string msg)
    {
        var items = new List<EvidenceItem>();
        TryAdd(items, msg, "Explain", ExplainWords,  "ผู้ใช้อธิบายแนวคิดด้วยคำของตัวเอง");
        TryAdd(items, msg, "Reason",  ReasonWords,   "ผู้ใช้ให้เหตุผลหรืออธิบายที่มาของความคิด");
        TryAdd(items, msg, "Apply",   ApplyWords,    "ผู้ใช้นำแนวคิดไปใช้กับตัวอย่างหรือสถานการณ์จริง");
        TryAdd(items, msg, "Connect", ConnectWords,  "ผู้ใช้เชื่อมโยงแนวคิดกับความรู้หรือประสบการณ์อื่น");
        TryAdd(items, msg, "Reflect", ReflectWords,  "ผู้ใช้สะท้อนความคิดหรือแสดงความไม่แน่ใจ");
        TryAdd(items, msg, "Question", QuestionWords, "ผู้ใช้ตั้งคำถามเพิ่มเติม");
        return items;
    }

    private static void TryAdd(List<EvidenceItem> items, string msg,
        string evidenceType, string[] keywords, string interpretation)
    {
        var matchCount = keywords.Count(k => msg.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (matchCount == 0) return;
        double confidence = matchCount >= 2 ? 0.9 : 0.7;
        items.Add(new EvidenceItem(evidenceType, msg, interpretation, confidence));
    }

    // ── Summary ──────────────────────────────────────────────────────────────

    private static string BuildSummary(List<ProjectBrainMessage> history, string n)
    {
        var allText = string.Join(" ", history.Where(h => h.Role == "user").Select(h => h.Text));

        var evidence = new List<string>();
        if (Contains(allText, ExplainWords))  evidence.Add("✓ Explain — อธิบายแนวคิดด้วยคำของตัวเองได้");
        if (Contains(allText, ReasonWords))   evidence.Add("✓ Reason — ให้เหตุผลของ decision ได้");
        if (Contains(allText, ApplyWords))    evidence.Add("✓ Apply — เชื่อมกับตัวอย่างจากชีวิตจริงได้");
        if (Contains(allText, ConnectWords))  evidence.Add("✓ Connect — เชื่อมโยงกับสิ่งที่รู้แล้วได้");
        if (Contains(allText, ReflectWords))  evidence.Add("✓ Reflect — สะท้อนความไม่แน่ใจออกมาได้");

        var evidenceBlock = evidence.Count > 0
            ? string.Join("\n", evidence)
            : "ยังไม่พบหลักฐานความเข้าใจที่ชัดเจน — ลองคุยต่อได้นะคะ";

        bool hasOpenQ = Contains(allText, ReflectWords) ||
                        Contains(allText, ["ยังไม่", "ไม่แน่", "อาจ", "สงสัย"]);

        var openQ = hasOpenQ
            ? "? วิธีสังเกตความเข้าใจในระยะยาว"
            : "? ยังไม่มีคำถามที่ชัดเจน — ลองคุยต่อได้";

        return
            $"📋 สรุปความเข้าใจ — {n}\n" +
            "═══════════════════════════\n\n" +
            "Strong Understanding\n" +
            $"{evidenceBlock}\n\n" +
            "Open Questions\n" +
            $"{openQ}\n\n" +
            "→ Next Conversation\n" +
            "   Visible Reasoning vs Correct Answers\n" +
            "   (ทำไมเหตุผลจึงสำคัญกว่าคำตอบ)\n\n" +
            "───────────────────────\n" +
            "ขอบคุณที่ใช้เวลาคุยด้วยนะคะ 😊\n" +
            "ความเข้าใจของคุณจะช่วยให้ Project Brain ดีขึ้นด้วยค่ะ";
    }

    private static bool Contains(string text, string[] words) =>
        words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
}
