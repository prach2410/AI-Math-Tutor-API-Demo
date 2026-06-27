using System.Text;
using System.Text.Json;

namespace backend.Services;

public interface IChatProvider
{
    Task<string> CompleteAsync(string prompt);
}

public class ClaudeChatProvider : IChatProvider
{
    private static readonly HttpClient Http = new();
    private readonly string _apiKey;
    private readonly string _model;

    public ClaudeChatProvider(string apiKey, string model = "claude-sonnet-4-6")
    {
        _apiKey = apiKey;
        _model  = model;
    }

    public async Task<string> CompleteAsync(string prompt)
    {
        var body = new
        {
            model = _model,
            max_tokens = 2048,
            messages = new[] { new { role = "user", content = prompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Claude API error {(int)response.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }
}

public class OllamaChatProvider : IChatProvider
{
    private static readonly HttpClient Http = new();
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;

    public OllamaChatProvider(string apiKey, string model = "qwen3.6:latest",
        string endpoint = "https://dgx.toptier.co.th/ollama/api/chat")
    {
        _apiKey   = apiKey;
        _model    = model;
        _endpoint = endpoint;
    }

    public async Task<string> CompleteAsync(string prompt)
    {
        var body = new
        {
            model    = _model,
            messages = new[] { new { role = "user", content = prompt } },
            stream   = false,
            options  = new { num_predict = 16384 }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama API error {(int)response.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}

public class OpenRouterChatProvider : IChatProvider
{
    private static readonly HttpClient Http = new();
    private readonly string _apiKey;
    private readonly string _model;

    public OpenRouterChatProvider(string apiKey, string model = "google/gemini-2.5-flash")
    {
        _apiKey = apiKey;
        _model  = model;
    }

    public async Task<string> CompleteAsync(string prompt)
    {
        var body = new
        {
            model      = _model,
            max_tokens = 16384,
            messages   = new[] { new { role = "user", content = prompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenRouter API error {(int)response.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}

public class MockChatProvider : IChatProvider
{
    public Task<string> CompleteAsync(string prompt)
    {
        // mock แยกชนิด prompt จาก keyword เพื่อให้ flow ทำงานครบโดยไม่ยิง API
        if (prompt.Contains("\"verdict\""))
            return Task.FromResult("""
                { "verdict": "correct", "reason": "คำตอบสอดคล้องกับเป้าหมายของขั้นนี้", "missing": "", "encouragement": "เยี่ยมมาก คิดได้ดีเลย!" }
                """);

        if (prompt.Contains("ระดับความช่วยเหลือ"))
            return Task.FromResult("""
                { "level": 1, "help": "ลองดูว่าโจทย์ให้ข้อมูลอะไรมาบ้าง แล้วเราอยากรู้ค่าอะไร?" }
                """);

        return Task.FromResult("""
            {
              "steps": [
                { "step": 1, "goal": "ระบุสิ่งที่โจทย์กำหนดให้", "guidingQuestion": "ลองดูสิว่าโจทย์บอกข้อมูลอะไรมาให้เราบ้าง?", "conceptHint": "อ่านโจทย์ให้ครบก่อนแก้" },
                { "step": 2, "goal": "เลือกวิธีการแก้ที่เหมาะสม", "guidingQuestion": "เรารู้จักวิธีไหนบ้างที่ใช้แก้โจทย์แบบนี้ได้?", "conceptHint": "คิดถึงสูตรหรือแนวคิดที่เรียนมา" },
                { "step": 3, "goal": "แทนค่าและคำนวณคำตอบ", "guidingQuestion": "ลองแทนตัวเลขลงในวิธีที่เลือก แล้วดูว่าได้อะไร?", "conceptHint": "ทำทีละขั้น ไม่ต้องรีบ" }
              ]
            }
            """);
    }
}
