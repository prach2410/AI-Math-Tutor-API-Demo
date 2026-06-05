using backend.Models;

namespace backend.Services;

public class FreeTalkService
{
    private static readonly string[] TiredWords    = ["เหนื่อย", "อ่อนล้า", "ง่วง", "ล้า"];
    private static readonly string[] StressWords   = ["เครียด", "กดดัน", "กังวล", "กลัว"];
    private static readonly string[] SadWords      = ["เศร้า", "เสียใจ", "ร้องไห้", "หงุดสี", "ไม่สบายใจ"];
    private static readonly string[] HwWords       = ["การบ้าน", "ข้อสอบ", "สอบ", "เรียน", "วิชา"];
    private static readonly string[] FriendWords   = ["เพื่อน", "ทะเลาะ", "โดนแกล้ง", "โดนด่า"];
    private static readonly string[] HappyWords    = ["สนุก", "ดีใจ", "มีความสุข", "เฮฮา", "ชอบ"];
    private static readonly string[] ReadyWords    = ["พร้อม", "เรียนได้", "เริ่มได้", "ไปเลย", "โอเค", "ได้เลย"];

    private static readonly Random Rng = new();

    public FreeTalkResponse Reply(FreeTalkRequest req)
    {
        var msg     = req.Message.Trim();
        var name    = string.IsNullOrWhiteSpace(req.StudentName) ? "" : req.StudentName;
        var n       = string.IsNullOrWhiteSpace(name) ? "น้อง" : name;
        var turns   = req.History.Count(h => h.Role == "user");
        var suggest = turns >= 3;

        if (Contains(msg, ReadyWords))
            return new FreeTalkResponse(ReadyReply(n, req.DuringLesson), SuggestLesson: true);

        if (Contains(msg, TiredWords))
            return new FreeTalkResponse(TiredReply(n, suggest, req.DuringLesson), SuggestLesson: suggest);

        if (Contains(msg, StressWords))
            return new FreeTalkResponse(StressReply(n, suggest), SuggestLesson: suggest);

        if (Contains(msg, SadWords))
            return new FreeTalkResponse(SadReply(n, suggest), SuggestLesson: suggest);

        if (Contains(msg, FriendWords))
            return new FreeTalkResponse(FriendReply(n, suggest), SuggestLesson: suggest);

        if (Contains(msg, HwWords))
            return new FreeTalkResponse(HwReply(n, suggest, req.DuringLesson), SuggestLesson: suggest);

        if (Contains(msg, HappyWords))
            return new FreeTalkResponse(HappyReply(n, suggest), SuggestLesson: suggest);

        return new FreeTalkResponse(DefaultReply(n, turns, req.DuringLesson), SuggestLesson: suggest);
    }

    private static bool Contains(string msg, string[] words) =>
        words.Any(w => msg.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static string TiredReply(string n, bool suggest, bool duringLesson) =>
        Pick(suggest,
            duringLesson
                ? $"ฟังดูเหนื่อยเลยนะ {n} 😊\n\nพักคุยก่อนได้เลยนะ ไม่ต้องรีบ\n\nถ้าพร้อมแล้ว กลับไปทำต่อด้วยกันได้เลย"
                : $"ฟังดูเป็นวันที่หนักพอสมควรเลยนะ {n}\n\nมีอะไรเกิดขึ้นที่โรงเรียนหรือเปล่า?\nอยากเล่าให้พี่ฟังไหม",
            $"เข้าใจเลย {n} บางวันมันก็เหนื่อยได้\n\nพักสักครู่แล้วค่อยไปต่อด้วยกันนะ\nวันนี้เรียนแค่ข้อเดียวก็พอ 😊");

    private static string StressReply(string n, bool suggest) =>
        Pick(suggest,
            $"เครียดแล้วให้บอกพี่นะ {n}\n\nเรื่องอะไรที่ทำให้รู้สึกกดดันล่ะ?\nเล่าให้ฟังได้เลย",
            $"เป็นธรรมดานะที่จะเครียดบ้าง {n}\n\nถ้าพร้อมแล้ว เราค่อย ๆ ทำด้วยกันได้เลย\nพี่อยู่ตรงนี้ 😊");

    private static string SadReply(string n, bool suggest) =>
        Pick(suggest,
            $"ไม่เป็นไรเลย {n} เศร้าบ้างก็ได้\n\nอยากเล่าให้พี่ฟังไหมว่าเกิดอะไรขึ้น?",
            $"พี่อยู่ตรงนี้นะ {n} ไม่ต้องอยู่คนเดียว 😊\n\nถ้าพร้อมแล้ว เราไปทำโจทย์ง่าย ๆ ด้วยกันก็ได้\nบางทีการทำอะไรไปเรื่อย ๆ ช่วยให้รู้สึกดีขึ้นนะ");

    private static string FriendReply(string n, bool suggest) =>
        Pick(suggest,
            $"โอ้ เรื่องเพื่อนนี่ยากเลยนะ {n}\n\nเล่าให้พี่ฟังได้นะ เกิดอะไรขึ้น?",
            $"ขอบคุณที่เล่าให้ฟังนะ {n} 😊\n\nถ้าพร้อมแล้ว เราลองทำโจทย์ง่าย ๆ ด้วยกันก็ได้\nพี่ช่วยเริ่มให้ได้เลย");

    private static string HwReply(string n, bool suggest, bool duringLesson) =>
        Pick(suggest,
            duringLesson
                ? $"เข้าใจเลย {n} การบ้านเยอะก็เหนื่อยได้\n\nพักแป๊บนึงก่อนได้นะ แล้วค่อยกลับมาทำต่อด้วยกัน"
                : $"การบ้านเยอะแล้วเหนื่อยได้เลยนะ {n}\n\nวันนี้มีวิชาไหนที่ต้องส่งก่อนไหม?\nพี่ช่วยเริ่มได้นะ",
            $"เข้าใจเลย {n} 😊\n\nวันนี้เรียนแค่ข้อเดียวก็พอนะ\nพี่ช่วยเริ่มให้เลยถ้าพร้อม");

    private static string HappyReply(string n, bool suggest) =>
        Pick(suggest,
            $"เยี่ยมเลย {n}! 🎉\n\nวันนี้มีอะไรดี ๆ เกิดขึ้นเหรอ?\nเล่าให้พี่ฟังได้เลย",
            $"ดีใจด้วยนะ {n} 😊\n\nอารมณ์ดีแบบนี้เหมาะมากเลยสำหรับการเรียน\nลองทำโจทย์ด้วยกันไหม?");

    private static string ReadyReply(string n, bool duringLesson) =>
        duringLesson
            ? $"เยี่ยมเลย {n}! 🚀\n\nกลับไปทำต่อด้วยกันได้เลยนะ\nพี่รอ 😊"
            : $"เยี่ยมเลย {n}! 🚀\n\nเราไปเรียนด้วยกันได้เลยนะ\nพี่พร้อมแล้ว 😊";

    private static string DefaultReply(string n, int turns, bool duringLesson)
    {
        if (turns == 0)
            return $"สวัสดี {n} 😊\n\nมีอะไรอยู่ในใจอยากเล่าให้พี่ฟังไหม?\nหรือวันนี้เป็นยังไงบ้าง?";

        if (turns == 1)
            return $"โอ้ เป็นยังไงบ้าง {n}?\n\nเล่าต่อได้เลยนะ พี่ฟังอยู่ 😊";

        if (turns >= 3)
            return duringLesson
                ? $"ขอบคุณที่เล่าให้ฟังนะ {n} 😊\n\nถ้าพร้อมแล้ว กลับไปทำต่อด้วยกันได้เลย\nพี่ช่วยเริ่มจากขั้นแรกให้ได้"
                : $"ขอบคุณที่คุยกับพี่นะ {n} 😊\n\nถ้าพร้อมแล้ว ลองทำโจทย์ง่าย ๆ ด้วยกันไหม?\nวันนี้เรียนแค่ข้อเดียวก็พอ";

        return $"พี่ฟังอยู่นะ {n} เล่าต่อได้เลย 😊";
    }

    private static string Pick(bool useLatter, string first, string latter) =>
        useLatter ? latter : first;
}
