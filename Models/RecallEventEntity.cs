namespace backend.Models;

public class RecallEventEntity
{
    public int Id { get; set; }
    public string At { get; set; } = "";          // UTC ISO-8601
    public string Kind { get; set; } = "";         // shown | miss | answered
    public string Topic { get; set; } = "";        // matched/prev topic (ไม่ใช่ PII)
    public string TodayTopic { get; set; } = "";   // today's topic (สำหรับ miss = topic ที่ต่าง)
}
