using System.Text;
using System.Text.Json;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public record ProblemItem(int Index, string ProblemText, string Latex, string Topic, bool HasFigure);

public record HomeworkAnalysisResult(
    List<ProblemItem> Problems,
    bool Readable,
    string Message,
    string VisionModel = "",
    string StartedAt = "",
    string EndedAt = ""
);

internal interface IHomeworkAnalyzer
{
    string ModelName { get; }
    Task<(HomeworkAnalysisResult Result, string Reason, string RawResponse)> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images,
        string fileName = "");
}

public class HomeworkAnalysisService(AppDbContext db)
{
    // Static so one instance is shared across scoped service instances (HttpClient reuse, one-time env check)
    private static readonly IHomeworkAnalyzer Analyzer = CreateAnalyzer();

    private static IHomeworkAnalyzer CreateAnalyzer()
    {
        var visionProvider = Environment.GetEnvironmentVariable("LLM__VisionProvider") ?? "Claude";

        if (visionProvider == "OpenAI")
        {
            var key   = Environment.GetEnvironmentVariable("LLM__OpenAI__ApiKey") ?? "";
            var model = Environment.GetEnvironmentVariable("LLM__OpenAI__VisionModel") ?? "gpt-4o-mini";
            return new OpenAiHomeworkAnalyzer(key, model);
        }

        if (visionProvider == "LocalAI")
        {
            var key   = Environment.GetEnvironmentVariable("LLM__LocalAI__ApiKey") ?? "";
            var model = Environment.GetEnvironmentVariable("LLM__LocalAI__VisionModel") ?? "gemma4:26b";
            return new OllamaHomeworkAnalyzer(key, model);
        }

        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(anthropicKey))
            return new MockHomeworkAnalyzer();
        return new ClaudeHomeworkAnalyzer(anthropicKey);
    }

    public async Task<HomeworkAnalysisResult> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images,
        string fileName = "")
    {
        var startedAt = DateTime.UtcNow.ToString("O");
        var (result, reason, rawResponse) = await Analyzer.AnalyzeAsync(images, fileName);
        var endedAt = DateTime.UtcNow.ToString("O");

        var first = result.Problems.FirstOrDefault();
        db.HomeworkReads.Add(new HomeworkReadEntity
        {
            Filename          = fileName,
            CreatedAt         = DateTime.UtcNow.ToString("O"),
            Readable          = result.Readable,
            Reason            = reason,
            ProblemText       = JsonSerializer.Serialize(result.Problems),
            Latex             = first?.Latex ?? "",
            Topic             = first?.Topic ?? "",
            RawResponse       = rawResponse,
            VisionModel       = Analyzer.ModelName,
            AnalysisStartedAt = startedAt,
            AnalysisEndedAt   = endedAt,
        });
        await db.SaveChangesAsync();

        return result with { VisionModel = Analyzer.ModelName, StartedAt = startedAt, EndedAt = endedAt };
    }

    public Task<List<HomeworkReadEntity>> GetRecentAsync(int limit = 50)
        => db.HomeworkReads.OrderByDescending(e => e.Id).Take(limit).ToListAsync();

    public Task<List<HomeworkReadEntity>> GetAllAsync()
        => db.HomeworkReads.OrderBy(e => e.Id).ToListAsync();
}

internal class ClaudeHomeworkAnalyzer : IHomeworkAnalyzer
{
    private static readonly HttpClient Http = new();
    private readonly string _apiKey;
    public string ModelName => "claude-opus-4-8";

    internal const string Prompt = """
        อ่านโจทย์คณิตศาสตร์จากภาพ แล้วตอบเป็น JSON เท่านั้น ห้ามเพิ่มข้อความนอกเหนือจาก JSON

        ให้แยกโจทย์ทุกข้อที่พบในภาพออกจากกัน (แยกตามเลขข้อ เช่น 1. 2. 3.)
        ถ้ามีหลายภาพ = ภาพต่อเนื่องกัน ให้รวมโจทย์จากทุกภาพเป็น list เดียวกัน

        รูปแบบที่ต้องการ:
        {
          "readable": true,
          "message": "อ่านโจทย์ได้",
          "problems": [
            {
              "index": 1,
              "problemText": "ข้อความโจทย์ข้อที่ 1 ครบถ้วน",
              "latex": "สมการ LaTeX (ถ้าไม่มีใส่ string ว่าง)",
              "topic": "หัวข้อคณิตศาสตร์ เช่น สมการเชิงเส้นตัวแปรเดียว",
              "hasFigure": false
            }
          ]
        }

        กฎ hasFigure:
        - hasFigure: true เมื่อโจทย์ข้อนั้นอ้างถึง "รูป" หรือมีรูปเรขาคณิต/แผนภาพ/กราฟประกอบ
        - hasFigure: false ถ้าเป็นข้อความล้วนๆ ไม่มีรูปประกอบ

        กฎสำคัญ — รูปทางเรขาคณิต:
        - บรรยายเฉพาะค่าที่มี label กำกับจริงในรูป ห้ามสรุปค่าที่ไม่ได้เขียนไว้
        - ด้านที่ไม่มีตัวเลขกำกับ = ตัวไม่ทราบค่า ให้ใช้ตัวแปร เช่น x
        - ระบุมุมฉากเฉพาะเมื่อรูปมีเครื่องหมายมุมฉาก □ กำกับชัดเจน
        - ห้ามสรุปว่าด้านไหนเป็น hypotenuse เว้นแต่รูประบุชัดเจน
        - ถ้าไม่มั่นใจโครงสร้างรูป ให้ระบุใน problemText ว่า "โปรดดูรูปประกอบ" แทนการเดา

        ถ้าภาพไม่ชัด ไม่ใช่โจทย์คณิตศาสตร์ หรืออ่านไม่ออก ให้ตอบ:
        {
          "readable": false,
          "message": "กรุณาถ่ายภาพโจทย์ให้ชัดขึ้น",
          "problems": []
        }
        """;

    public ClaudeHomeworkAnalyzer(string apiKey) => _apiKey = apiKey;

    public async Task<(HomeworkAnalysisResult Result, string Reason, string RawResponse)> AnalyzeAsync(
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
            max_tokens = 8192,
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
                var errResult = new HomeworkAnalysisResult([], false, "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
                return (errResult, $"api_error_{(int)response.StatusCode}", raw);
            }

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";

            var (result, reason) = HomeworkResponseParser.ParseResult(text);
            return (result, reason, text);
        }
        catch (Exception ex)
        {
            var errResult = new HomeworkAnalysisResult([], false, "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
            return (errResult, "exception: " + ex.Message, raw);
        }
    }

}

internal static class HomeworkResponseParser
{
    internal static (HomeworkAnalysisResult result, string reason) ParseResult(string text)
    {
        try
        {
            var json = ExtractJson(text);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var readable = root.GetProperty("readable").GetBoolean();
            var message  = root.GetProperty("message").GetString() ?? "";

            if (!readable)
                return (new HomeworkAnalysisResult([], false, message), "model_unreadable");

            var problems = new List<ProblemItem>();
            if (root.TryGetProperty("problems", out var problemsEl) && problemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in problemsEl.EnumerateArray())
                {
                    problems.Add(new ProblemItem(
                        Index:       p.TryGetProperty("index", out var idx) ? idx.GetInt32() : problems.Count + 1,
                        ProblemText: p.GetProperty("problemText").GetString() ?? "",
                        Latex:       p.TryGetProperty("latex", out var lat) ? lat.GetString() ?? "" : "",
                        Topic:       p.TryGetProperty("topic", out var top) ? top.GetString() ?? "" : "",
                        HasFigure:   p.TryGetProperty("hasFigure", out var hf) && hf.GetBoolean()
                    ));
                }
            }

            if (problems.Count == 0)
                return (new HomeworkAnalysisResult([], false, "อ่านโจทย์ไม่ออก กรุณาลองใหม่"), "parse_no_problems");

            return (new HomeworkAnalysisResult(problems, true, message), "ok");
        }
        catch
        {
            return (new HomeworkAnalysisResult([], false, "อ่านโจทย์ไม่ออก กรุณาลองใหม่"), "parse_error");
        }
    }

    internal static string ExtractJson(string text)
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

internal class OllamaHomeworkAnalyzer : IHomeworkAnalyzer
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(300) };
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    public string ModelName => _model;

    public OllamaHomeworkAnalyzer(string apiKey, string model = "gemma4:26b",
        string endpoint = "https://dgx.toptier.co.th/ollama/api/chat")
    {
        _apiKey   = apiKey;
        _model    = model;
        _endpoint = endpoint;
    }

    public async Task<(HomeworkAnalysisResult Result, string Reason, string RawResponse)> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images,
        string fileName = "")
    {
        var base64Images = images.Select(img => Convert.ToBase64String(img.Bytes)).ToArray();

        var body = new
        {
            model    = _model,
            messages = new[]
            {
                new { role = "user", content = ClaudeHomeworkAnalyzer.Prompt, images = base64Images }
            },
            stream = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var raw = "";
        try
        {
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                raw = await response.Content.ReadAsStringAsync();
                var errResult = new HomeworkAnalysisResult([], false, "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
                return (errResult, $"ollama_error_{(int)response.StatusCode}", raw);
            }

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

            raw = sb.ToString();
            var (result, reason) = HomeworkResponseParser.ParseResult(raw);
            return (result, reason, raw);
        }
        catch (Exception ex)
        {
            var errResult = new HomeworkAnalysisResult([], false, "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
            return (errResult, "exception: " + ex.Message, raw);
        }
    }
}

internal class OpenAiHomeworkAnalyzer(string apiKey, string model = "gpt-4o-mini") : IHomeworkAnalyzer
{
    private static readonly HttpClient Http = new();
    public string ModelName => model;

    public async Task<(HomeworkAnalysisResult Result, string Reason, string RawResponse)> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images,
        string fileName = "")
    {
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
        contentBlocks.Add(new { type = "text", text = ClaudeHomeworkAnalyzer.Prompt });

        var body = new
        {
            model,
            max_tokens = 4096,
            messages   = new[] { new { role = "user", content = contentBlocks } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var raw = "";
        try
        {
            using var response = await Http.SendAsync(request);
            raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errResult = new HomeworkAnalysisResult([], false, "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
                return (errResult, $"openai_error_{(int)response.StatusCode}", raw);
            }

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            var (result, reason) = HomeworkResponseParser.ParseResult(text);
            return (result, reason, text);
        }
        catch (Exception ex)
        {
            var errResult = new HomeworkAnalysisResult([], false, "ระบบอ่านโจทย์ขัดข้องชั่วคราว กรุณาลองใหม่");
            return (errResult, "exception: " + ex.Message, raw);
        }
    }
}

internal class MockHomeworkAnalyzer : IHomeworkAnalyzer
{
    public string ModelName => "mock";

    public Task<(HomeworkAnalysisResult Result, string Reason, string RawResponse)> AnalyzeAsync(
        IReadOnlyList<(byte[] Bytes, string MediaType)> images,
        string fileName = "")
    {
        var label = images.Count > 1 ? $"mock ({images.Count} รูป)" : "mock";
        var result = new HomeworkAnalysisResult(
            Problems: [
                new ProblemItem(1,
                    "กล่องพัสดุใบหนึ่งมีความกว้าง x เซนติเมตร ยาวกว่าความกว้าง 5 เซนติเมตร และสูง 3x เซนติเมตร ถ้าปริมาตรของกล่องเท่ากับ 1,200 ลูกบาศก์เซนติเมตร จงหาค่า x",
                    @"x(x+5)(3x) = 1200",
                    "สมการพหุนาม",
                    false),
                new ProblemItem(2,
                    "ถังน้ำทรงกระบอกรัศมี r เซนติเมตร สูง 40 เซนติเมตร มีปริมาตร 5,024 ลูกบาศก์เซนติเมตร จงหาค่า r",
                    @"\pi r^2 \times 40 = 5024",
                    "ปริมาตรทรงกระบอก",
                    false),
            ],
            Readable: true,
            Message:  "อ่านโจทย์ได้"
        );
        return Task.FromResult((result, label, "(mock provider — ไม่ได้ยิง API จริง)"));
    }
}
