using System.Text;
using System.Text.Json;

namespace backend.Services;

public record HomeworkAnalysisResult(
    string ProblemText,
    string Latex,
    string Topic,
    bool Readable,
    string Message
);

public interface IHomeworkAnalyzer
{
    Task<HomeworkAnalysisResult> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images,
        string fileName = "");
}

public class HomeworkAnalysisService
{
    private readonly IHomeworkAnalyzer _analyzer;

    public HomeworkAnalysisService()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _analyzer = string.IsNullOrWhiteSpace(apiKey)
            ? new MockHomeworkAnalyzer()
            : new ClaudeHomeworkAnalyzer(apiKey);
    }

    public Task<HomeworkAnalysisResult> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images,
        string fileName = "")
        => _analyzer.AnalyzeAsync(images, fileName);
}

internal class ClaudeHomeworkAnalyzer : IHomeworkAnalyzer
{
    private static readonly HttpClient Http = new();
    private readonly string _apiKey;

    private const string Prompt = """
        อ่านโจทย์คณิตศาสตร์จากภาพ แล้วตอบเป็น JSON เท่านั้น ห้ามเพิ่มข้อความนอกเหนือจาก JSON
        (ถ้ามีหลายภาพ = โจทย์ข้อเดียวต่อเนื่อง ให้รวมเนื้อหาจากทุกภาพเป็นโจทย์เดียว)

        กฎสำคัญ — รูปทางเรขาคณิต:
        - บรรยายเฉพาะค่าที่มี label กำกับจริงในรูป ห้ามสรุปค่าที่ไม่ได้เขียนไว้
        - ด้านที่ไม่มีตัวเลขกำกับ = ตัวไม่ทราบค่า ให้ใช้ตัวแปร เช่น x
        - ระบุมุมฉากเฉพาะเมื่อรูปมีเครื่องหมายมุมฉาก □ กำกับชัดเจน
        - ห้ามสรุปว่าด้านไหนเป็น hypotenuse เว้นแต่รูประบุชัดเจน
        - ถ้าไม่มั่นใจโครงสร้างรูป ให้ระบุใน problemText ว่า "โปรดดูรูปประกอบ" แทนการเดา

        รูปแบบที่ต้องการ:
        {
          "problemText": "ข้อความโจทย์ที่อ่านได้ทั้งหมด",
          "latex": "สมการในรูปแบบ LaTeX (ถ้าไม่มีใส่ string ว่าง)",
          "topic": "หัวข้อคณิตศาสตร์ เช่น สมการเชิงเส้นตัวแปรเดียว",
          "readable": true,
          "message": "อ่านโจทย์ได้"
        }

        ถ้าภาพไม่ชัด ไม่ใช่โจทย์คณิตศาสตร์ หรืออ่านไม่ออก ให้ตอบ:
        {
          "problemText": "",
          "latex": "",
          "topic": "",
          "readable": false,
          "message": "กรุณาถ่ายภาพโจทย์ให้ชัดขึ้น"
        }
        """;

    public ClaudeHomeworkAnalyzer(string apiKey) => _apiKey = apiKey;

    public async Task<HomeworkAnalysisResult> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images,
        string fileName = "")
    {
        var imageBlocks = images.Select(img => (object)new
        {
            type = "image",
            source = new
            {
                type = "base64",
                media_type = img.MediaType,
                data = Convert.ToBase64String(img.Bytes)
            }
        });

        var content = imageBlocks.Append((object)new { type = "text", text = Prompt }).ToArray();

        var body = new
        {
            model = "claude-opus-4-8",
            max_tokens = 2048,
            messages = new[] { new { role = "user", content } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var raw = "";
        try
        {
            using var response = await Http.SendAsync(request);
            raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                HomeworkDebugLog.Add(new HomeworkDebugEntry(
                    BkkNow(), false, $"api_error_{(int)response.StatusCode}", "", raw, fileName));
                return new HomeworkAnalysisResult("", "", "", false, "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
            }

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";

            var (result, reason) = ParseResult(text);
            HomeworkDebugLog.Add(new HomeworkDebugEntry(
                BkkNow(), result.Readable, reason, result.ProblemText, text, fileName));
            return result;
        }
        catch (Exception ex)
        {
            HomeworkDebugLog.Add(new HomeworkDebugEntry(
                BkkNow(), false, "exception: " + ex.Message, "", raw, fileName));
            return new HomeworkAnalysisResult("", "", "", false, "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
        }
    }

    private static (HomeworkAnalysisResult result, string reason) ParseResult(string text)
    {
        try
        {
            var json = ExtractJson(text);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new HomeworkAnalysisResult(
                ProblemText: root.GetProperty("problemText").GetString() ?? "",
                Latex:       root.GetProperty("latex").GetString() ?? "",
                Topic:       root.GetProperty("topic").GetString() ?? "",
                Readable:    root.GetProperty("readable").GetBoolean(),
                Message:     root.GetProperty("message").GetString() ?? ""
            );
            return (result, result.Readable ? "ok" : "model_unreadable");
        }
        catch
        {
            return (new HomeworkAnalysisResult("", "", "", false, "อ่านโจทย์ไม่ออก กรุณาลองใหม่"), "parse_error");
        }
    }

    private static string ExtractJson(string text)
    {
        var t = StripCodeFence(text.Trim());
        var start = t.IndexOf('{');
        var end   = t.LastIndexOf('}');
        return (start >= 0 && end > start) ? t[start..(end + 1)] : t;
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```")) return text;
        var start = text.IndexOf('\n') + 1;
        var end   = text.LastIndexOf("```");
        return end > start ? text[start..end].Trim() : text;
    }

    private static DateTimeOffset BkkNow()
        => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
}

internal class MockHomeworkAnalyzer : IHomeworkAnalyzer
{
    public Task<HomeworkAnalysisResult> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images,
        string fileName = "")
    {
        var label = images.Count > 1 ? $"mock ({images.Count} รูป)" : "mock";
        var result = new HomeworkAnalysisResult(
            ProblemText: "กล่องพัสดุใบหนึ่งมีความกว้าง x เซนติเมตร ยาวกว่าความกว้าง 5 เซนติเมตร และสูง 3x เซนติเมตร ถ้าปริมาตรของกล่องเท่ากับ 1,200 ลูกบาศก์เซนติเมตร จงหาค่า x",
            Latex:       @"x(x+5)(3x) = 1200",
            Topic:       "สมการพหุนาม",
            Readable:    true,
            Message:     "อ่านโจทย์ได้"
        );
        HomeworkDebugLog.Add(new HomeworkDebugEntry(
            DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)),
            true, label, result.ProblemText, "(mock provider — ไม่ได้ยิง API จริง)", fileName));
        return Task.FromResult(result);
    }
}

// เก็บผลการอ่านล่าสุดไว้ใน memory (ล่าสุด 20 รายการ) — demo/test เท่านั้น
public record HomeworkDebugEntry(
    DateTimeOffset At, bool Readable, string Reason,
    string ProblemText, string RawResponse, string FileName = "")
{
    public int Seq { get; init; }
}

public static class HomeworkDebugLog
{
    private static readonly LinkedList<HomeworkDebugEntry> Entries = new();
    private static readonly object Lock = new();
    private static int _nextSeq = 1;
    private const int Max = 20;

    public static void Add(HomeworkDebugEntry entry)
    {
        lock (Lock)
        {
            Entries.AddFirst(entry with { Seq = _nextSeq++ });
            while (Entries.Count > Max) Entries.RemoveLast();
        }
    }

    public static IReadOnlyList<HomeworkDebugEntry> Recent()
    {
        lock (Lock) { return Entries.ToList(); }
    }
}
