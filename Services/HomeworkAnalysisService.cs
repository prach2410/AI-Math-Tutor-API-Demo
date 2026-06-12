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
            max_tokens = 1024,
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

        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "";

        return ParseResult(text);
    }

    private static HomeworkAnalysisResult ParseResult(string text)
    {
        try
        {
            var json = StripCodeFence(text.Trim());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new HomeworkAnalysisResult(
                ProblemText: root.GetProperty("problemText").GetString() ?? "",
                Latex:       root.GetProperty("latex").GetString() ?? "",
                Topic:       root.GetProperty("topic").GetString() ?? "",
                Readable:    root.GetProperty("readable").GetBoolean(),
                Message:     root.GetProperty("message").GetString() ?? ""
            );
        }
        catch
        {
            return new HomeworkAnalysisResult("", "", "", false, "อ่านโจทย์ไม่ออก กรุณาลองใหม่");
        }
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
        => Task.FromResult(new HomeworkAnalysisResult(
            ProblemText: "กล่องพัสดุใบหนึ่งมีความกว้าง x เซนติเมตร ยาวกว่าความกว้าง 5 เซนติเมตร และสูง 3x เซนติเมตร ถ้าปริมาตรของกล่องเท่ากับ 1,200 ลูกบาศก์เซนติเมตร จงหาค่า x",
            Latex:       @"x(x+5)(3x) = 1200",
            Topic:       "สมการพหุนาม",
            Readable:    true,
            Message:     "อ่านโจทย์ได้"
        ));
}
