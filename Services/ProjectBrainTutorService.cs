using backend.Models;

namespace backend.Services;

public class ProjectBrainTutorService
{
    // ── Evidence keyword patterns (fallback when topic has no keywords) ──────
    private static readonly string[] DefaultExplainWords  = ["หมายความว่า", "ก็คือ", "คือว่า", "หมายถึง", "นั่นก็คือ"];
    private static readonly string[] DefaultReasonWords   = ["เพราะ", "เหตุผล", "เนื่องจาก", "เพราะว่า"];
    private static readonly string[] DefaultApplyWords    = ["เช่น", "ตัวอย่าง", "ลูก", "ในชีวิต", "ใช้กับ"];
    private static readonly string[] DefaultConnectWords  = ["เหมือน", "คล้าย", "เชื่อมโยง", "เกี่ยวกับ", "ก็เหมือน"];
    private static readonly string[] DefaultReflectWords  = ["ไม่แน่ใจ", "คิดว่า", "น่าจะ", "อาจจะ", "สงสัย", "ยังไม่"];
    private static readonly string[] DefaultQuestionWords = ["?", "？", "อย่างไร", "ทำไม", "ได้ไหม", "ใช่ไหม"];

    public ProjectBrainResponse Chat(ProjectBrainRequest req)
    {
        var n         = string.IsNullOrWhiteSpace(req.StudentName) ? "คุณ" : req.StudentName;
        var msg       = req.Message.Trim();
        var phase     = req.Phase;
        var userTurns = req.History.Count(h => h.Role == "user");
        var topicId   = req.TopicId ?? "understanding-engine";
        var topic     = KnowledgeCurriculum.Get(topicId)
                        ?? KnowledgeCurriculum.Get("understanding-engine")!;

        // Extract evidence from user message (skip teach/retrieval/guided/ready — still context-building)
        List<EvidenceItem>? evidence = null;
        if (phase != "teach" && phase != "retrieval" && phase != "guided" && phase != "ready" && msg != "เริ่ม")
            evidence = ExtractEvidence(msg, topic);

        // Explicit summary request any time
        if (phase == "summary" || msg.Contains("สรุป") || msg.Contains("ขอสรุป"))
            return new ProjectBrainResponse(BuildSummary(req.History, n, topic), "summary", Evidence: evidence);

        var baseResponse = phase switch
        {
            "teach"     => HandleTeach(n, topic),
            "retrieval" => HandleRetrieval(n, req.PriorEvidenceSummary ?? "", topic),
            "guided"    => HandleGuided(n, msg, userTurns, topic),
            "ready"     => HandleReady(n, topic),
            "check"     => HandleCheck(n, msg, evidence, userTurns, topic),
            "reflect"   => HandleReflect(n, msg, topic),
            "grill"     => HandleGrill(n, msg, userTurns, topic),
            _           => HandleTeach(n, topic),
        };

        return baseResponse with { Evidence = evidence };
    }

    // ── Phase handlers ───────────────────────────────────────────────────────

    private static ProjectBrainResponse HandleTeach(string n, CurriculumTopic topic)
    {
        var passiveGrillSuffix = !string.IsNullOrWhiteSpace(topic.PassiveGrill)
            ? $"\n\n───────────────────────\n{topic.PassiveGrill}\n\nพอเริ่มเห็นภาพไหมคะ 😊"
            : "\n\nพอเริ่มเห็นภาพไหมคะ 😊";

        var text =
            $"สวัสดีนะคะ {n} 😊\n\n" +
            $"วันนี้เราจะคุยกันเรื่อง **{topic.Title}**\n\n" +
            "───────────────────────\n" +
            $"{topic.KnowledgeContent}\n" +
            "───────────────────────" +
            passiveGrillSuffix;

        return new ProjectBrainResponse(text, "guided");
    }

    private static ProjectBrainResponse HandleRetrieval(string n, string priorSummary, CurriculumTopic topic)
    {
        var summaryBlock = string.IsNullOrWhiteSpace(priorSummary)
            ? "— (ยังไม่มีข้อมูลจากครั้งก่อน)"
            : priorSummary;

        var text =
            $"ยินดีต้อนรับกลับนะคะ {n} 😊\n\n" +
            $"ครั้งที่แล้วเราคุยกันเรื่อง **{topic.Title}**\n" +
            "นี่คือสิ่งที่เหลืออยู่จากการสนทนาครั้งก่อน:\n\n" +
            $"{summaryBlock}\n\n" +
            "วันนี้ยังเห็นด้วยกับสิ่งเหล่านี้อยู่ไหมคะ?\n" +
            "หรืออยากเพิ่มเติมหรือเปลี่ยนใจอะไรก็ได้เลย 😊";

        return new ProjectBrainResponse(text, "check");
    }

    private static ProjectBrainResponse HandleGuided(string n, string msg, int userTurns, CurriculumTopic topic)
    {
        string[] readyKeywords    = ["เริ่มเห็น", "เห็นภาพ", "โอเค", "พร้อม"];
        string[] confusedKeywords = ["ยังงง", "งง", "ไม่เข้าใจ", "ไม่เห็นภาพ"];

        if (Contains(msg, readyKeywords))
        {
            return new ProjectBrainResponse(
                $"ดีมากเลยค่ะ {n} 😊\n\n" +
                "ลองอธิบายด้วยคำของตัวเองได้เลยนะคะ —\n" +
                $"**{topic.Title} คืออะไร ในความเข้าใจของคุณ?**\n\n" +
                "ไม่ต้องสมบูรณ์แบบค่ะ แค่บอกตามที่เข้าใจตอนนี้ได้เลย",
                "check"
            );
        }

        if (Contains(msg, confusedKeywords))
        {
            var pg = !string.IsNullOrWhiteSpace(topic.PassiveGrill)
                ? topic.PassiveGrill
                : (topic.GuidedExamples.Length > 0 ? topic.GuidedExamples[0] : "");
            return new ProjectBrainResponse(
                $"ไม่เป็นไรเลยนะคะ {n} 😊\n\n" +
                (pg.Length > 0 ? $"{pg}\n\n" : "") +
                "พอเริ่มเห็นภาพไหมคะ 😊",
                "guided"
            );
        }

        if (userTurns >= 2)
        {
            return new ProjectBrainResponse(
                $"ขอบคุณที่แชร์ความคิดนะคะ {n} 😊\n\n" +
                "ตอนนี้รู้สึกว่าพร้อมลองอธิบายแนวคิดนี้ด้วยคำของตัวเองแล้วไหมคะ?\n" +
                "หรืออยากให้ยกตัวอย่างเพิ่มเติมก่อนก็ได้นะคะ",
                "ready"
            );
        }

        var guidedText = topic.GuidedExamples.Length > 0
            ? $"ให้ลองนึกภาพแบบนี้นะคะ {n} 🤔\n\n{topic.GuidedExamples[0]}"
            : $"ลองนึกภาพตัวอย่างจริงนะคะ {n} 🤔\n\n" +
              "จากที่อ่านมา — มีส่วนไหนที่ทำให้นึกถึงประสบการณ์ของคุณบ้างไหมคะ?";

        return new ProjectBrainResponse(guidedText, "guided");
    }

    private static ProjectBrainResponse HandleReady(string n, CurriculumTopic topic)
    {
        var text =
            $"เยี่ยมเลยค่ะ {n} 😊\n\n" +
            "ตอนนี้ลองอธิบายด้วยคำของตัวเองได้เลยนะคะ —\n" +
            $"**{topic.Title} คืออะไร ในความเข้าใจของคุณ?**\n\n" +
            "ไม่ต้องสมบูรณ์แบบค่ะ แค่บอกตามที่เข้าใจตอนนี้ได้เลย";

        return new ProjectBrainResponse(text, "check");
    }

    private static ProjectBrainResponse HandleCheck(string n, string msg,
        List<EvidenceItem>? evidence, int userTurns, CurriculumTopic topic)
    {
        bool hasExplain = evidence?.Any(e => e.EvidenceType == "Explain") ?? false;

        if (hasExplain)
        {
            var grillQ = topic.GrillQuestions.Length > 0 ? topic.GrillQuestions[0] : DefaultGrillQ;
            var successText =
                $"ดีมากเลยค่ะ {n} 👏\n" +
                "คุณอธิบายได้ชัดเจนมาก — นั่นแสดงว่าเข้าใจแนวคิดแล้ว\n\n" +
                "มาลองคิดให้ลึกขึ้นอีกหน่อยนะคะ\n\n" +
                grillQ;

            return new ProjectBrainResponse(successText, "reflect");
        }

        if (userTurns >= 2)
        {
            var grillQ = topic.GrillQuestions.Length > 0 ? topic.GrillQuestions[0] : DefaultGrillQ;
            var fallbackText =
                $"ขอบคุณนะคะ {n} 😊\n" +
                "ไม่เป็นไรเลย — บางแนวคิดต้องใช้เวลาในการตกผลึก\n\n" +
                "มาลองดูกันต่อนะคะ ขอถามสถานการณ์จริงสักข้อ\n\n" +
                grillQ;

            return new ProjectBrainResponse(fallbackText, "reflect");
        }

        var clarifyText =
            $"ไม่เป็นไรเลยนะคะ 🙂\n\n" +
            "ลองคิดแบบนี้ก็ได้ —\n" +
            $"ถ้าเพื่อนถามว่า \"{topic.Title} คืออะไร?\"\n" +
            "คุณจะตอบว่าอะไร?\n\n" +
            "ไม่ต้องสมบูรณ์แบบค่ะ แค่บอกตามที่คิดได้เลย";

        return new ProjectBrainResponse(clarifyText, "check");
    }

    private static ProjectBrainResponse HandleReflect(string n, string msg, CurriculumTopic topic)
    {
        bool hasReason  = Contains(msg, topic.ReasonWords.Length > 0 ? topic.ReasonWords : DefaultReasonWords);
        bool hasExample = Contains(msg, topic.ApplyWords.Length > 0  ? topic.ApplyWords  : DefaultApplyWords);

        string ack;
        if (hasReason && hasExample)
            ack = $"เยี่ยมมากเลยค่ะ {n} 👏\nคุณให้ทั้งเหตุผลและตัวอย่าง — นั่นคือหลักฐานความเข้าใจที่ดีมาก\n\n";
        else if (hasReason)
            ack = $"ขอบคุณนะคะ {n} 😊 คุณให้เหตุผลได้ดีเลย\n\n";
        else if (hasExample)
            ack = $"ขอบคุณค่ะ {n} การยกตัวอย่างแบบนี้ช่วยให้เห็นภาพได้ชัดเจนมาก\n\n";
        else
            ack = $"ขอบคุณที่แชร์ความคิดนะคะ {n} 😊\n\n";

        var grillQ = topic.GrillQuestions.Length > 0 ? topic.GrillQuestions[0] : DefaultGrillQ;
        var text = ack + "มาลองดูกันต่อนะคะ — ขอถามสถานการณ์จริงสักข้อ\n\n" + grillQ;

        return new ProjectBrainResponse(text, "grill");
    }

    private static ProjectBrainResponse HandleGrill(string n, string msg, int userTurns, CurriculumTopic topic)
    {
        int grillTurn = Math.Max(0, userTurns - 1);

        bool hasReason  = Contains(msg, topic.ReasonWords.Length > 0 ? topic.ReasonWords : DefaultReasonWords);
        bool hasReflect = Contains(msg, DefaultReflectWords);

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

        int nextIdx = Math.Min(grillTurn + 1, topic.GrillQuestions.Length - 1);
        var nextQ   = topic.GrillQuestions.Length > 0 ? topic.GrillQuestions[nextIdx] : DefaultGrillQ;
        var text    = ack + nextQ;

        return new ProjectBrainResponse(text, "grill");
    }

    // ── Evidence extraction ──────────────────────────────────────────────────

    private static List<EvidenceItem> ExtractEvidence(string msg, CurriculumTopic topic)
    {
        var explainWords  = topic.ExplainWords.Length > 0 ? topic.ExplainWords  : DefaultExplainWords;
        var reasonWords   = topic.ReasonWords.Length  > 0 ? topic.ReasonWords   : DefaultReasonWords;
        var applyWords    = topic.ApplyWords.Length   > 0 ? topic.ApplyWords    : DefaultApplyWords;

        var items = new List<EvidenceItem>();
        TryAdd(items, msg, "Explain",  explainWords,        "ผู้ใช้อธิบายแนวคิดด้วยคำของตัวเอง");
        TryAdd(items, msg, "Reason",   reasonWords,         "ผู้ใช้ให้เหตุผลหรืออธิบายที่มาของความคิด");
        TryAdd(items, msg, "Apply",    applyWords,          "ผู้ใช้นำแนวคิดไปใช้กับตัวอย่างหรือสถานการณ์จริง");
        TryAdd(items, msg, "Connect",  DefaultConnectWords, "ผู้ใช้เชื่อมโยงแนวคิดกับความรู้หรือประสบการณ์อื่น");
        TryAdd(items, msg, "Reflect",  DefaultReflectWords, "ผู้ใช้สะท้อนความคิดหรือแสดงความไม่แน่ใจ");
        TryAdd(items, msg, "Question", DefaultQuestionWords,"ผู้ใช้ตั้งคำถามเพิ่มเติม");
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

    private static string BuildSummary(List<ProjectBrainMessage> history, string n, CurriculumTopic topic)
    {
        var explainWords = topic.ExplainWords.Length > 0 ? topic.ExplainWords : DefaultExplainWords;
        var reasonWords  = topic.ReasonWords.Length  > 0 ? topic.ReasonWords  : DefaultReasonWords;
        var applyWords   = topic.ApplyWords.Length   > 0 ? topic.ApplyWords   : DefaultApplyWords;

        var allText = string.Join(" ", history.Where(h => h.Role == "user").Select(h => h.Text));

        var evidence = new List<string>();
        if (Contains(allText, explainWords))       evidence.Add("✓ Explain — อธิบายแนวคิดด้วยคำของตัวเองได้");
        if (Contains(allText, reasonWords))         evidence.Add("✓ Reason — ให้เหตุผลของ decision ได้");
        if (Contains(allText, applyWords))          evidence.Add("✓ Apply — เชื่อมกับตัวอย่างจากชีวิตจริงได้");
        if (Contains(allText, DefaultConnectWords)) evidence.Add("✓ Connect — เชื่อมโยงกับสิ่งที่รู้แล้วได้");
        if (Contains(allText, DefaultReflectWords)) evidence.Add("✓ Reflect — สะท้อนความไม่แน่ใจออกมาได้");

        var evidenceBlock = evidence.Count > 0
            ? string.Join("\n", evidence)
            : "ยังไม่พบหลักฐานความเข้าใจที่ชัดเจน — ลองคุยต่อได้นะคะ";

        bool hasOpenQ = Contains(allText, DefaultReflectWords) ||
                        Contains(allText, ["ยังไม่", "ไม่แน่", "อาจ", "สงสัย"]);

        var openQ = hasOpenQ
            ? $"? คำถามเปิดเกี่ยวกับ {topic.Title}"
            : "? ยังไม่มีคำถามที่ชัดเจน — ลองคุยต่อได้";

        return
            $"📋 สรุปความเข้าใจ — {n}\n" +
            $"Topic: {topic.Emoji} {topic.Title}\n" +
            "═══════════════════════════\n\n" +
            "Strong Understanding\n" +
            $"{evidenceBlock}\n\n" +
            "Open Questions\n" +
            $"{openQ}\n\n" +
            "→ Next Conversation\n" +
            $"   {GetNextTopic(topic.Id)}\n\n" +
            "───────────────────────\n" +
            "ขอบคุณที่ใช้เวลาคุยด้วยนะคะ 😊\n" +
            "ความเข้าใจของคุณจะช่วยให้ Project Brain ดีขึ้นด้วยค่ะ";
    }

    private static string GetNextTopic(string currentId) => currentId switch
    {
        "vision"              => "Understanding Engine — ระบบสร้างหลักฐานความเข้าใจ",
        "understanding-engine"=> "Learning Flow Engine — โครงสร้างที่เปลี่ยน LLM ให้เป็นครู",
        "learning-flow-engine"=> "Key Discoveries — Insight สำคัญที่นำทาง product",
        "discoveries"         => "Key Decisions — ทำไมถึงตัดสินใจแบบนี้",
        _                     => "กลับมาทบทวน topic ที่ยังสงสัยได้เลยนะคะ"
    };

    private static bool Contains(string text, string[] words) =>
        words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

    private const string DefaultGrillQ =
        "ลองนึกภาพสถานการณ์จริงสักสถานการณ์\n" +
        "ที่แนวคิดนี้จะช่วยให้คุณตัดสินใจได้ดีขึ้น\n" +
        "และอธิบายว่าทำไมถึงนึกถึงสถานการณ์นั้น";
}
