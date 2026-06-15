using System.Text;
using System.Text.Json;

namespace backend.Services;

public record LearningJournalAnalysis(
    bool Readable,
    string Message,
    string DocumentType,
    string Topic,
    string Summary,
    List<string> Keywords
);

public class LearningJournalService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(300) };
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;

    internal const string Prompt = """
        วิเคราะห์ภาพเอกสารการเรียนและตอบเป็น JSON เท่านั้น ห้ามเพิ่มข้อความนอกเหนือจาก JSON
        ถ้ามีหลายภาพ = ภาพต่อเนื่องกัน ให้วิเคราะห์รวมกัน

        จำแนกประเภทเอกสาร (เลือกหนึ่งอย่างเท่านั้น):
        - "Whiteboard" : กระดานในห้องเรียน ครูเขียน
        - "Notebook"   : สมุดจดของนักเรียน
        - "Textbook"   : หนังสือเรียน/ตำราเรียน
        - "Worksheet"  : ใบงาน/แบบฝึกหัด
        - "Homework"   : การบ้านหรือโจทย์ที่ได้รับมา

        รูปแบบที่ต้องการ:
        {
          "readable": true,
          "message": "วิเคราะห์ได้",
          "documentType": "Whiteboard",
          "topic": "หัวข้อที่เรียน เช่น พหุนาม",
          "summary": "สรุปเนื้อหาที่เห็นในภาพ 2-4 ประโยค",
          "keywords": ["คำสำคัญ1", "คำสำคัญ2", "คำสำคัญ3"]
        }

        ถ้าภาพไม่ชัด ไม่ใช่เอกสารการเรียน หรือวิเคราะห์ไม่ออก ให้ตอบ:
        {
          "readable": false,
          "message": "กรุณาถ่ายภาพให้ชัดขึ้น",
          "documentType": "", "topic": "", "summary": "", "keywords": []
        }
        """;

    public LearningJournalService()
    {
        _apiKey   = Environment.GetEnvironmentVariable("LLM__LocalAI__ApiKey") ?? "";
        _model    = Environment.GetEnvironmentVariable("LLM__LocalAI__VisionModel") ?? "gemma4:26b";
        _endpoint = "https://dgx.toptier.co.th/ollama/api/chat";
    }

    public async Task<LearningJournalAnalysis> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return new LearningJournalAnalysis(false, "ไม่พบ API key สำหรับระบบวิเคราะห์", "", "", "", []);

        var base64Images = images.Select(img => Convert.ToBase64String(img.Bytes)).ToArray();

        var body = new
        {
            model    = _model,
            messages = new[] { new { role = "user", content = Prompt, images = base64Images } },
            stream   = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
                return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", []);

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

                if (root.TryGetProperty("done", out var done) && done.GetBoolean())
                    break;
            }

            return ParseResult(sb.ToString());
        }
        catch
        {
            return new LearningJournalAnalysis(false, "ระบบวิเคราะห์ขัดข้องชั่วคราว กรุณาลองใหม่", "", "", "", []);
        }
    }

    private static LearningJournalAnalysis ParseResult(string text)
    {
        try
        {
            var json = ExtractJson(text);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var readable = root.GetProperty("readable").GetBoolean();
            var message  = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

            if (!readable) return new LearningJournalAnalysis(false, message, "", "", "", []);

            var docType  = root.TryGetProperty("documentType", out var dt) ? dt.GetString() ?? "" : "";
            var topic    = root.TryGetProperty("topic",        out var t)  ? t.GetString()  ?? "" : "";
            var summary  = root.TryGetProperty("summary",      out var s)  ? s.GetString()  ?? "" : "";
            var keywords = new List<string>();
            if (root.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array)
                foreach (var k in kw.EnumerateArray())
                    keywords.Add(k.GetString() ?? "");

            return new LearningJournalAnalysis(true, message, docType, topic, summary, keywords);
        }
        catch
        {
            return new LearningJournalAnalysis(false, "วิเคราะห์ไม่ออก กรุณาลองใหม่", "", "", "", []);
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
