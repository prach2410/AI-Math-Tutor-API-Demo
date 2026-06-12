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
    Task<HomeworkAnalysisResult> AnalyzeAsync(byte[] imageBytes, string mediaType);
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

    public Task<HomeworkAnalysisResult> AnalyzeAsync(byte[] imageBytes, string mediaType)
        => _analyzer.AnalyzeAsync(imageBytes, mediaType);
}

internal class ClaudeHomeworkAnalyzer : IHomeworkAnalyzer
{
    private static readonly HttpClient Http = new();
    private readonly string _apiKey;

    private const string Prompt = """
        อ่านโจทย์คณิตศาสตร์จากภาพนี้ แล้วตอบเป็น JSON เท่านั้น ห้ามเพิ่มข้อความนอกเหนือจาก JSON

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

    public async Task<HomeworkAnalysisResult> AnalyzeAsync(byte[] imageBytes, string mediaType)
    {
        var base64 = Convert.ToBase64String(imageBytes);

        var body = new
        {
            model = "claude-opus-4-8",
            max_tokens = 2048,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = mediaType,
                                data = base64
                            }
                        },
                        new { type = "text", text = Prompt }
                    }
                }
            }
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
                    DateTime.Now, false, $"api_error_{(int)response.StatusCode}", "", raw));
                return new HomeworkAnalysisResult("", "", "", false,
                    "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
            }

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "";

            var (result, reason) = ParseResult(text);
            HomeworkDebugLog.Add(new HomeworkDebugEntry(
                DateTime.Now, result.Readable, reason, result.ProblemText, text));
            return result;
        }
        catch (Exception ex)
        {
            HomeworkDebugLog.Add(new HomeworkDebugEntry(
                DateTime.Now, false, "exception: " + ex.Message, "", raw));
            return new HomeworkAnalysisResult("", "", "", false,
                "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
        }
    }

    // คืน reason ด้วย เพื่อแยกว่า model บอกอ่านไม่ออก (model_unreadable) หรือ parse JSON พัง (parse_error)
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
            return (new HomeworkAnalysisResult("", "", "", false, "อ่านโจทย์ไม่ออก กรุณาลองใหม่"),
                "parse_error");
        }
    }

    // ทนทานขึ้น: ดึงตั้งแต่ { แรกถึง } สุดท้าย เผื่อ model แทรกข้อความนำหน้า/ต่อท้าย JSON
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
}

internal class MockHomeworkAnalyzer : IHomeworkAnalyzer
{
    public Task<HomeworkAnalysisResult> AnalyzeAsync(byte[] imageBytes, string mediaType)
    {
        var result = new HomeworkAnalysisResult(
            ProblemText: "กล่องพัสดุใบหนึ่งมีความกว้าง x เซนติเมตร ยาวกว่าความกว้าง 5 เซนติเมตร และสูง 3x เซนติเมตร ถ้าปริมาตรของกล่องเท่ากับ 1,200 ลูกบาศก์เซนติเมตร จงหาค่า x",
            Latex:       @"x(x+5)(3x) = 1200",
            Topic:       "สมการพหุนาม",
            Readable:    true,
            Message:     "อ่านโจทย์ได้"
        );
        HomeworkDebugLog.Add(new HomeworkDebugEntry(
            DateTime.Now, true, "mock", result.ProblemText, "(mock provider — ไม่ได้ยิง API จริง)"));
        return Task.FromResult(result);
    }
}

// เก็บผลการอ่านล่าสุดไว้ใน memory (ล่าสุด 20 รายการ) สำหรับหน้า debug — demo/test เท่านั้น
public record HomeworkDebugEntry(
    DateTime At, bool Readable, string Reason, string ProblemText, string RawResponse);

public static class HomeworkDebugLog
{
    private static readonly LinkedList<HomeworkDebugEntry> Entries = new();
    private static readonly object Lock = new();
    private const int Max = 20;

    public static void Add(HomeworkDebugEntry entry)
    {
        lock (Lock)
        {
            Entries.AddFirst(entry);
            while (Entries.Count > Max) Entries.RemoveLast();
        }
    }

    public static IReadOnlyList<HomeworkDebugEntry> Recent()
    {
        lock (Lock) { return Entries.ToList(); }
    }
}
