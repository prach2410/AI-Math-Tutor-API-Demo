using System.Text;
using System.Text.Json;

namespace backend.Services;

public record LearningJournalAnalysis(
    bool Readable,
    string Message,
    string DocumentType,
    string Topic,
    string Summary,
    List<string> Highlights,
    List<string> Keywords,
    string VisionModel = "",
    string StartedAt = "",
    string EndedAt = ""
);

public class LearningJournalService
{
    private static readonly ILearningJournalAnalyzer Analyzer = CreateAnalyzer();

    private static ILearningJournalAnalyzer CreateAnalyzer()
    {
        var provider = Environment.GetEnvironmentVariable("LLM__JournalVisionProvider") ?? "Claude";
        if (provider == "LocalAI")
        {
            var key   = Environment.GetEnvironmentVariable("LLM__LocalAI__ApiKey") ?? "";
            var model = Environment.GetEnvironmentVariable("LLM__LocalAI__VisionModel") ?? "gemma4:26b";
            return new OllamaLearningJournalAnalyzer(key, model);
        }

        if (provider == "OpenRouter")
        {
            var key   = Environment.GetEnvironmentVariable("LLM__OpenRouter__ApiKey") ?? "";
            var model = Environment.GetEnvironmentVariable("LLM__OpenRouter__VisionModel") ?? "google/gemini-2.5-flash";
            return new OpenRouterLearningJournalAnalyzer(key, model);
        }

        if (provider == "OpenAI")
        {
            var key   = Environment.GetEnvironmentVariable("LLM__OpenAI__ApiKey") ?? "";
            var model = Environment.GetEnvironmentVariable("LLM__OpenAI__VisionModel") ?? "gpt-4o-mini";
            return new OpenAiLearningJournalAnalyzer(key, model);
        }

        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";
        return new ClaudeLearningJournalAnalyzer(anthropicKey);
    }

    public async Task<LearningJournalAnalysis> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images)
    {
        var startedAt = DateTime.UtcNow.ToString("O");
        var result    = await Analyzer.AnalyzeAsync(images);
        var endedAt   = DateTime.UtcNow.ToString("O");
        return result with { VisionModel = Analyzer.ModelName, StartedAt = startedAt, EndedAt = endedAt };
    }
}

// ── shared ──────────────────────────────────────────────────────────────────

internal interface ILearningJournalAnalyzer
{
    string ModelName { get; }
    Task<LearningJournalAnalysis> AnalyzeAsync(IReadOnlyList<(byte[] Bytes, string MediaType)> images);
}

internal static class JournalPrompt
{
    internal const string Text = """
        วิเคราะห์ภาพเอกสารการเรียนและตอบเป็น JSON เท่านั้น ห้ามเพิ่มข้อความนอกเหนือจาก JSON
        ถ้ามีหลายภาพ = ภาพต่อเนื่องกัน ให้วิเคราะห์รวมกัน

        กฎความถูกต้องของคำ (สำคัญมาก — ใช้กับทุก field: topic, summary, highlights, keywords):
        - ใช้เฉพาะคำและศัพท์ที่ปรากฏจริงในภาพ ห้ามแทนคำในภาพด้วยศัพท์เทคนิคที่สูงกว่า หรือเพิ่มศัพท์ที่ไม่มีในภาพ
        - เนื้อหาระดับ ม.2 — ห้ามยกระดับเป็นศัพท์ฟิสิกส์/เวกเตอร์ระดับสูง เช่น ห้ามเปลี่ยน "ระยะห่างจากจุดเริ่มต้น" หรือ "ระยะทาง" เป็น "การกระจัด" (displacement) ให้คงคำตามที่โจทย์ใช้จริง
        - ถ้าอ่านคำหรือตัวเลขไม่ชัด อย่าเดาเป็นศัพท์เทคนิค ให้ใช้คำกลาง ๆ ตามที่เห็นจริงในภาพ

        กฎอธิบายศัพท์ (inline gloss) — ใช้กับ field "summary" เท่านั้น:
        - ถ้าใน summary มีศัพท์เทคนิค/ศัพท์บัญญัติที่นักเรียน ม.2 อาจไม่รู้จัก (เช่น "สัญกรณ์วิทยาศาสตร์")
          ให้เติมคำอธิบายสั้นในวงเล็บ "ครั้งแรก" ที่พบ เช่น "สัญกรณ์วิทยาศาสตร์ (วิธีเขียนเลขในรูป a × 10 ยกกำลัง n)"
        - คำอธิบายต้องเป็นภาษาง่าย สั้น (ไม่เกิน 1 วลี) และ "ไม่เพิ่มเนื้อหาคณิตใหม่" ที่ไม่มีในภาพ
        - เป็นการ "อธิบาย" คำเดิม ไม่ใช่ "แทน" หรือ "ยกระดับ" คำ — คำหลักต้องคงเดิมตามภาพ
        - ใส่ gloss เฉพาะใน summary ห้ามใส่ใน topic, highlights, keywords
        - ศัพท์ทั่วไปที่เด็กรู้อยู่แล้ว (เช่น บวก ลบ เศษส่วน) ไม่ต้องอธิบาย

        จำแนกประเภทเอกสาร (เลือกหนึ่งอย่างเท่านั้น):
        - "Whiteboard" : กระดานในห้องเรียน ครูเขียน
        - "Notebook"   : สมุดจดของนักเรียน
        - "Textbook"   : หนังสือเรียน/ตำราเรียน
        - "Worksheet"  : ใบงาน/แบบฝึกหัดในห้องเรียน (แม้จะเป็นโจทย์คณิตก็ตาม)
        - "Homework"   : การบ้านที่ครูสั่งให้ทำที่บ้าน (ระบุชัดว่า "การบ้าน" หรือ "ส่งพรุ่งนี้")
        กฎ: ถ้าไม่แน่ใจ → เลือก Worksheet (ใบงานมีโอกาสสูงกว่าในบริบทห้องเรียน)

        รูปแบบที่ต้องการ:
        {
          "readable": true,
          "message": "วิเคราะห์ได้",
          "documentType": "Whiteboard",
          "topic": "หัวข้อที่เรียน เช่น พหุนาม",
          "summary": "สรุปเนื้อหาที่เห็นในภาพ 2-4 ประโยค",
          "highlights": ["ประเด็นสำคัญ 1", "ประเด็นสำคัญ 2", "ประเด็นสำคัญ 3"],
          "keywords": ["คำสำคัญ1", "คำสำคัญ2", "คำสำคัญ3"]
        }

        กฎ highlights (3-5 ข้อ) — ต่างกันตามประเภทเอกสาร:
        - Whiteboard : สิ่งที่ครูเขียน/อธิบาย/เน้นในกระดาน
        - Notebook   : สิ่งสำคัญที่นักเรียนจด + สิ่งที่ดูไม่สมบูรณ์หรืออาจเข้าใจผิด
        - Textbook   : แนวคิดหลักและตัวอย่างที่แสดงในหน้านี้
        - Worksheet  : ประเภทโจทย์และทักษะที่ฝึก
        - Homework   : โจทย์ที่ได้รับและทักษะที่ต้องใช้

        ถ้าภาพไม่ชัด ไม่ใช่เอกสารการเรียน หรือวิเคราะห์ไม่ออก ให้ตอบ:
        {
          "readable": false,
          "message": "กรุณาถ่ายภาพให้ชัดขึ้น",
          "documentType": "", "topic": "", "summary": "", "highlights": [], "keywords": []
        }
        """;
}

internal static class JournalParser
{
    internal static LearningJournalAnalysis Parse(string text)
    {
        try
        {
            var json = ExtractJson(text);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var readable = root.GetProperty("readable").GetBoolean();
            var message  = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

            if (!readable) return new LearningJournalAnalysis(false, message, "", "", "", [], []);

            var docType    = root.TryGetProperty("documentType", out var dt) ? dt.GetString() ?? "" : "";
            var topic      = root.TryGetProperty("topic",        out var t)  ? t.GetString()  ?? "" : "";
            var summary    = root.TryGetProperty("summary",      out var s)  ? s.GetString()  ?? "" : "";
            var highlights = new List<string>();
            if (root.TryGetProperty("highlights", out var hl) && hl.ValueKind == JsonValueKind.Array)
                foreach (var h in hl.EnumerateArray())
                    highlights.Add(h.GetString() ?? "");
            var keywords = new List<string>();
            if (root.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array)
                foreach (var k in kw.EnumerateArray())
                    keywords.Add(k.GetString() ?? "");

            return new LearningJournalAnalysis(true, message, docType, topic, summary, highlights, keywords);
        }
        catch
        {
            return new LearningJournalAnalysis(false, "วิเคราะห์ไม่ออก กรุณาลองใหม่", "", "", "", [], []);
        }
    }

    private static string ExtractJson(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```"))
        {
            var nl  = t.IndexOf('\n') + 1;
            var end = t.LastIndexOf("```");
            if (end > nl) t = t[nl..end].Trim();
        }
        var s = t.IndexOf('{');
        var e = t.LastIndexOf('}');
        return (s >= 0 && e > s) ? t[s..(e + 1)] : t;
    }
}

// ── Claude ───────────────────────────────────────────────────────────────────

internal class ClaudeLearningJournalAnalyzer(string apiKey) : ILearningJournalAnalyzer
{
    private static readonly HttpClient Http = new();
    public string ModelName => "claude-opus-4-8";

    public async Task<LearningJournalAnalysis> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new LearningJournalAnalysis(false, "ไม่พบ ANTHROPIC_API_KEY", "", "", "", [], []);

        var imageBlocks = images.Select(img => (object)new
        {
            type   = "image",
            source = new { type = "base64", media_type = img.MediaType, data = Convert.ToBase64String(img.Bytes) }
        });
        var content = imageBlocks.Append((object)new { type = "text", text = JournalPrompt.Text }).ToArray();

        var body = new
        {
            model      = "claude-opus-4-8",
            max_tokens = 4096,
            messages   = new[] { new { role = "user", content } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var response = await Http.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", [], []);

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            return JournalParser.Parse(text);
        }
        catch
        {
            return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", [], []);
        }
    }
}

// ── OpenAI ───────────────────────────────────────────────────────────────────

internal class OpenAiLearningJournalAnalyzer(string apiKey, string model) : ILearningJournalAnalyzer
{
    private static readonly HttpClient Http = new();
    public string ModelName => model;

    public async Task<LearningJournalAnalysis> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new LearningJournalAnalysis(false, "ไม่พบ LLM__OpenAI__ApiKey", "", "", "", [], []);

        var contentBlocks = new List<object>();
        foreach (var img in images)
        {
            var base64 = Convert.ToBase64String(img.Bytes);
            contentBlocks.Add(new
            {
                type      = "image_url",
                image_url = new { url = $"data:{img.MediaType};base64,{base64}" }
            });
        }
        contentBlocks.Add(new { type = "text", text = JournalPrompt.Text });

        var body = new
        {
            model,
            max_tokens = 4096,
            messages   = new[] { new { role = "user", content = contentBlocks } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var response = await Http.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", [], []);

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
            return JournalParser.Parse(text);
        }
        catch
        {
            return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", [], []);
        }
    }
}

// ── OpenRouter (Gemini) ───────────────────────────────────────────────────────

internal class OpenRouterLearningJournalAnalyzer(string apiKey, string model) : ILearningJournalAnalyzer
{
    private static readonly HttpClient Http = new();
    public string ModelName => model;

    public async Task<LearningJournalAnalysis> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new LearningJournalAnalysis(false, "ไม่พบ LLM__OpenRouter__ApiKey", "", "", "", [], []);

        var contentBlocks = new List<object>();
        foreach (var img in images)
        {
            var base64 = Convert.ToBase64String(img.Bytes);
            contentBlocks.Add(new
            {
                type      = "image_url",
                image_url = new { url = $"data:{img.MediaType};base64,{base64}" }
            });
        }
        contentBlocks.Add(new { type = "text", text = JournalPrompt.Text });

        var body = new
        {
            model,
            max_tokens = 4096,
            messages   = new[] { new { role = "user", content = contentBlocks } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var response = await Http.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", [], []);

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
            return JournalParser.Parse(text);
        }
        catch
        {
            return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", [], []);
        }
    }
}

// ── Ollama ───────────────────────────────────────────────────────────────────

internal class OllamaLearningJournalAnalyzer(string apiKey, string model) : ILearningJournalAnalyzer
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(300) };
    private readonly string _endpoint = "https://dgx.toptier.co.th/ollama/api/chat";
    public string ModelName => model;

    public async Task<LearningJournalAnalysis> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images)
    {
        var base64Images = images.Select(img => Convert.ToBase64String(img.Bytes)).ToArray();
        var body = new
        {
            model    = model,
            messages = new[] { new { role = "user", content = JournalPrompt.Text, images = base64Images } },
            stream   = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", [], []);

            var sb = new StringBuilder();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var lineDoc = JsonDocument.Parse(line);
                var root = lineDoc.RootElement;
                if (root.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var chunk))
                    sb.Append(chunk.GetString());
                if (root.TryGetProperty("done", out var done) && done.GetBoolean()) break;
            }

            return JournalParser.Parse(sb.ToString());
        }
        catch
        {
            return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", [], []);
        }
    }
}
