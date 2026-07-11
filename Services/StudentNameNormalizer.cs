namespace backend.Services;

public static class StudentNameNormalizer
{
    // ชื่อว่าง หรือไม่มีตัวอักษรจริงเลย (เช่น combining mark ลอยตัวเดียว) → ถือเป็น blank → default "คนเก่ง"
    public static string Normalize(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Any(char.IsLetter) ? trimmed : "คนเก่ง";
    }
}
