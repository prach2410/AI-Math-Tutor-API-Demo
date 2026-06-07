namespace backend.Services;

public record CurriculumTopic(
    string Id,
    string Title,
    string Emoji,
    string Subtitle,
    string KnowledgeContent,
    string[] GuidedExamples,
    string[] GrillQuestions,
    string[] ExplainWords,
    string[] ReasonWords,
    string[] ApplyWords
);

public static class KnowledgeCurriculum
{
    public static readonly CurriculumTopic[] Topics =
    [
        new CurriculumTopic(
            Id: "vision",
            Title: "Vision & Teaching Philosophy",
            Emoji: "🧭",
            Subtitle: "ทำไมเราถึงสร้างสิ่งนี้",
            KnowledgeContent:
                "📌 Product Vision\n\n" +
                "เราไม่ได้สร้าง AI ที่ทำการบ้านแทนเด็ก\n" +
                "เราสร้าง AI ที่ช่วยให้นักเรียนเรียนรู้ได้ด้วยตัวเอง\n\n" +
                "───────────────────────\n" +
                "💡 Teaching Philosophy — 8 หลักการ\n\n" +
                "1. Teach, Don't Answer\n" +
                "   นำทางด้วยคำถาม ไม่ใช่คำตอบ\n\n" +
                "2. Thinking Before Answer\n" +
                "   ให้พื้นที่นักเรียนคิดก่อนเสมอ\n\n" +
                "3. Step-by-Step Learning\n" +
                "   แบ่งปัญหาให้เล็กลงจนทำได้\n\n" +
                "4. Real-world Learning\n" +
                "   เชื่อมทุกบทเรียนกับชีวิตจริง\n\n" +
                "5. Growth Mindset\n" +
                "   ความผิดพลาดคือการเติบโต ไม่ใช่ความล้มเหลว\n\n" +
                "6. Learning Asset Ownership\n" +
                "   นักเรียนเก็บโน้ตไว้แม้ AI หายไป\n\n" +
                "7. Parent Partnership\n" +
                "   Parent Summary ช่วยให้พ่อแม่เข้าใจ ไม่ใช่จับผิด\n\n" +
                "8. Simplicity First\n" +
                "   สร้างน้อย สังเกตมาก ก่อนขยาย\n\n" +
                "───────────────────────\n" +
                "🎯 Core Design Principle\n\n" +
                "Teaching Flow > AI Capability\n\n" +
                "คุณค่าของ product มาจากวิธีสอน ไม่ใช่ AI ที่แรงกว่า\n" +
                "โครงสร้างการสอนที่ดีกว่า LLM อัจฉริยะที่ไม่มีโครงสร้าง\n\n" +
                "───────────────────────\n" +
                "✅ Success Criteria\n\n" +
                "• นักเรียน: \"ฉันเข้าใจมากขึ้น\" — อธิบายสิ่งที่เรียนได้\n" +
                "• ผู้ปกครอง: \"ฉันรู้ว่าลูกเรียนอะไร\" — อ่าน summary ด้วยความมั่นใจ\n" +
                "• ผู้สังเกต: \"นี่ไม่ใช่ ChatGPT ธรรมดา\" — เห็นการออกแบบการสอนที่ตั้งใจ",

            GuidedExamples:
            [
                "ลองนึกภาพครูสองคนนะคะ\n\n" +
                "ครูคนแรก: \"ปริมาตร = กว้าง × ยาว × สูง นะ คำนวณได้เลย\"\n" +
                "ครูคนที่สอง: \"ถ้าต้องเติมน้ำในถังปลา คุณคิดว่าต้องรู้อะไรบ้าง?\"\n\n" +
                "ครูคนไหนช่วยให้เด็กเข้าใจมากกว่ากัน? เพราะอะไรคะ?"
            ],

            GrillQuestions:
            [
                "ลองนึกภาพผู้ปกครองถามว่า\n" +
                "\"AI ตัวนี้ต่างจาก Google ยังไง?\"\n\n" +
                "คุณจะตอบว่าอะไร? และอะไรคือสิ่งที่ทำให้มันแตกต่างจริงๆ?",

                "ถ้ามีนักเรียนถาม AI ว่า \"ช่วยทำการบ้านหน้า 42 ให้หน่อย\"\n" +
                "AI ควรตอบยังไงถึงจะสอดคล้องกับ Teaching Philosophy?\n" +
                "และทำไมถึงตอบแบบนั้น?",

                "หลักการ Teaching Flow > AI Capability หมายความว่าอะไรในทางปฏิบัติ?\n" +
                "ยกตัวอย่างสถานการณ์ที่หลักการนี้สำคัญกว่าการมี AI ที่ฉลาดกว่า"
            ],

            ExplainWords:  ["หมายความว่า", "ก็คือ", "คือว่า", "หมายถึง", "นั่นก็คือ", "แปลว่า"],
            ReasonWords:   ["เพราะ", "เหตุผล", "เนื่องจาก", "เพราะว่า", "จึง"],
            ApplyWords:    ["เช่น", "ตัวอย่าง", "ลูก", "ในชีวิต", "นักเรียน", "ผู้ปกครอง", "ถ้า"]
        ),

        new CurriculumTopic(
            Id: "understanding-engine",
            Title: "Understanding Engine",
            Emoji: "🧠",
            Subtitle: "ความเข้าใจที่ซ่อนอยู่ → หลักฐานที่มองเห็นได้",
            KnowledgeContent:
                "📌 Discovery สำคัญ\n\n" +
                "Correct Answer ≠ Understanding\n\n" +
                "ถ้าเด็กตอบถูกทุกข้อ แต่บอกไม่ได้ว่า \"ทำไมถึงเลือกวิธีนี้\"\n" +
                "ถือว่าเข้าใจหรือไม่?\n\n" +
                "คำตอบ: ไม่เข้าใจ\n\n" +
                "───────────────────────\n" +
                "💡 Understanding Engine คืออะไร\n\n" +
                "ระบบที่ช่วยเปลี่ยน\n" +
                "\"ความเข้าใจที่ซ่อนอยู่\"\n" +
                "ให้กลายเป็น\n" +
                "\"หลักฐานความเข้าใจที่สังเกตได้\"\n\n" +
                "ผ่านการสนทนา การสะท้อนความคิด และการให้เหตุผล\n\n" +
                "───────────────────────\n" +
                "🔄 Core Flow\n\n" +
                "Knowledge\n" +
                "↓ Guided Understanding\n" +
                "↓ Comprehension Check\n" +
                "↓ Reflection\n" +
                "↓ Grill\n" +
                "↓ Evidence\n\n" +
                "───────────────────────\n" +
                "📋 5 ประเภทหลักฐาน\n\n" +
                "• Explain — อธิบายด้วยคำของตัวเองได้\n" +
                "• Reason — บอกเหตุผลเบื้องหลังได้\n" +
                "• Apply — นำไปใช้กับสถานการณ์ใหม่ได้\n" +
                "• Connect — เชื่อมกับความรู้เดิมได้\n" +
                "• Reflect — สะท้อนความไม่แน่ใจออกมาได้\n\n" +
                "───────────────────────\n" +
                "⚠️ สิ่งสำคัญ\n\n" +
                "Understanding ≠ Agreement\n" +
                "Visible Reasoning > Correct Answers",

            GuidedExamples:
            [
                "ลองนึกภาพแบบนี้นะคะ\n\n" +
                "นักเรียนจำสูตรปริมาตรได้ ทำข้อสอบผ่าน\n" +
                "แต่พอถามว่า \"ทำไมถึงต้องคูณสามจำนวน?\" — ตอบไม่ได้\n\n" +
                "เทียบกับนักเรียนที่คำนวณผิด แต่อธิบายได้ว่า\n" +
                "\"เพราะความยาว กว้าง สูง แต่ละด้านบอกว่าของวางได้กี่ชั้น\"\n\n" +
                "คนไหนเข้าใจมากกว่ากัน? เพราะอะไรคะ?"
            ],

            GrillQuestions:
            [
                "ลองนึกภาพดูนะคะ — เด็กตอบถูกทุกข้อในข้อสอบ\n" +
                "แต่พอถามว่า \"ทำไมถึงเลือกวิธีนี้?\" เขาตอบไม่ได้\n" +
                "คุณคิดว่าเขาเข้าใจจริงๆ ไหม เพราะอะไร?",

                "ถ้าลูกบอกว่า \"อ่านหนังสือจบแล้ว เข้าใจแล้ว\"\n" +
                "คุณจะรู้ได้ยังไงว่าเขาเข้าใจจริง ไม่ใช่แค่จำได้?",

                "Understanding ≠ Agreement หมายความว่าอะไร?\n" +
                "ยกตัวอย่างสถานการณ์ที่คนเห็นต่างกัน แต่ทั้งคู่ \"เข้าใจ\" แล้ว"
            ],

            ExplainWords:  ["หมายความว่า", "ก็คือ", "คือว่า", "หมายถึง", "นั่นก็คือ"],
            ReasonWords:   ["เพราะ", "เหตุผล", "เนื่องจาก", "เพราะว่า"],
            ApplyWords:    ["เช่น", "ตัวอย่าง", "ลูก", "ในชีวิต", "ใช้กับ"]
        ),

        new CurriculumTopic(
            Id: "learning-flow-engine",
            Title: "Learning Flow Engine",
            Emoji: "⚙️",
            Subtitle: "โครงสร้างที่เปลี่ยน LLM ให้เป็นครู",
            KnowledgeContent:
                "📌 ปัญหาที่ต้องแก้\n\n" +
                "LLM ตอบเร็วเกินไป — ข้ามขั้นตอนการสอน\n" +
                "ไม่รู้ว่านักเรียนอยู่ขั้นไหน\n" +
                "ประเมินไม่ได้ว่าคำตอบถูกในบริบทนั้นหรือเปล่า\n\n" +
                "───────────────────────\n" +
                "⚙️ Learning Flow Engine คืออะไร\n\n" +
                "ระบบที่ห่อหุ้ม LLM ด้วยโครงสร้างการสอน\n\n" +
                "สิ่งที่ Engine ควบคุม:\n" +
                "• Step tracking — นักเรียนอยู่ขั้นไหน?\n" +
                "• Answer evaluation — ถูก / ถูกบางส่วน / ผิด\n" +
                "• Hint selection — ให้ความช่วยเหลือระดับไหน?\n" +
                "• Next step — ไปขั้นต่อไปหรือต้องลองใหม่?\n\n" +
                "สิ่งที่ LLM ทำ:\n" +
                "• อธิบายด้วยภาษาธรรมชาติ\n" +
                "• ปรับโทนตามสถานการณ์\n" +
                "• สร้าง Student Notes\n" +
                "• สร้าง Parent Summary\n\n" +
                "───────────────────────\n" +
                "📋 Guided Assistance Ladder\n\n" +
                "4 ระดับความช่วยเหลือ (ต้องไต่ขึ้นทีละขั้น):\n\n" +
                "💡 Hint — แค่ชี้แนวทาง\n" +
                "🆘 Help Me Start — ทำขั้นแรกให้เป็นตัวอย่าง\n" +
                "👀 Worked Example — โจทย์คล้ายกันทั้งข้อ\n" +
                "📖 Show Solution — ทางออกสุดท้าย\n\n" +
                "───────────────────────\n" +
                "🎯 หลักการสำคัญ\n\n" +
                "Learning Flow Engine ควบคุมการสอน\n" +
                "LLM สร้างภาษา\n\n" +
                "Teaching Flow > AI Capability",

            GuidedExamples:
            [
                "ลองเปรียบเทียบนะคะ\n\n" +
                "ถ้าถาม ChatGPT ว่า \"ปริมาตรของถังปลา 20×40×30 เท่าไหร่?\"\n" +
                "มันจะตอบ: \"ปริมาตร = 20×40×30 = 24,000 cm³\"\n\n" +
                "แต่ถ้าใช้ Learning Flow Engine:\n" +
                "ระบบจะถามว่า \"ก่อนคำนวณ ช่วยบอกหน่อยได้ไหมว่าปริมาตรคืออะไร?\"\n" +
                "และรอให้นักเรียนตอบ ก่อนไปขั้นต่อไป\n\n" +
                "ความแตกต่างนี้ทำให้เกิดอะไรขึ้นกับการเรียนรู้?"
            ],

            GrillQuestions:
            [
                "ถ้าไม่มี Learning Flow Engine แต่มีแค่ system prompt ที่เขียนดีมาก\n" +
                "คุณคิดว่ามันจะสอนได้ผลเท่ากันไหม? เพราะอะไร?",

                "Guided Assistance Ladder มี 4 ระดับ\n" +
                "ทำไมถึงออกแบบให้ต้องไต่ขึ้นทีละขั้น แทนที่จะให้เลือกได้เลย?\n" +
                "มีความเสี่ยงอะไรถ้าเปิดให้กด Show Solution ได้ทันที?",

                "คุณคิดว่า feature ไหนใน Learning Flow Engine สำคัญที่สุด?\n" +
                "และถ้าต้องตัด 1 feature ออก ควรตัดอะไร? เพราะอะไร?"
            ],

            ExplainWords:  ["หมายความว่า", "ก็คือ", "คือว่า", "หมายถึง", "engine", "ระบบ"],
            ReasonWords:   ["เพราะ", "เหตุผล", "เนื่องจาก", "เพราะว่า", "จึง"],
            ApplyWords:    ["เช่น", "ตัวอย่าง", "ถ้า", "สมมติ", "นักเรียน"]
        ),

        new CurriculumTopic(
            Id: "discoveries",
            Title: "Key Discoveries",
            Emoji: "🔍",
            Subtitle: "Insight สำคัญที่นำทาง product",
            KnowledgeContent:
                "📌 Discovery คืออะไร\n\n" +
                "Discovery ไม่ใช่ความคิดเห็น\n" +
                "Discovery คือสิ่งที่สังเกตเห็นจากหลักฐานจริง\n" +
                "และนำไปสู่การตัดสินใจที่สำคัญ\n\n" +
                "───────────────────────\n" +
                "🔍 D001 — Correct Answer Is Not Understanding\n\n" +
                "สังเกต: นักเรียนตอบถูก แต่อธิบายเหตุผลไม่ได้\n" +
                "นำไปสู่: Understanding Engine\n\n" +
                "───────────────────────\n" +
                "🔍 D002 — Understanding Is Not Agreement\n\n" +
                "สังเกต: คน 2 คนสรุปต่างกัน แต่ทั้งคู่เข้าใจหลักการ\n" +
                "นำไปสู่: Alignment Model — วัด reasoning ไม่ใช่ consensus\n\n" +
                "───────────────────────\n" +
                "🔍 D003 — Comprehension Before Grill\n\n" +
                "สังเกต: หลังสอนเสร็จ ผู้เรียนยังตอบคำถามเชิงลึกไม่ได้\n" +
                "นำไปสู่: เพิ่ม Comprehension Check Stage ก่อน Grill\n\n" +
                "───────────────────────\n" +
                "🔍 D004 — Knowledge Growth Creates Alignment Risk\n\n" +
                "สังเกต: เอกสารเพิ่มขึ้นทุกสัปดาห์\n" +
                "ยิ่งมีเอกสารมาก ยิ่งเสี่ยง misalignment ถ้าพึ่งแค่การอ่าน\n" +
                "นำไปสู่: Understanding Layer บน Knowledge Base\n\n" +
                "───────────────────────\n" +
                "🔍 D006 — Understanding Requires Sufficient Context\n\n" +
                "สังเกต: ผู้เรียนบอก \"ยังไม่เข้าใจเลย แล้วมาเจอคำถามอีก\"\n" +
                "นำไปสู่: เพิ่ม Guided Understanding ก่อน Comprehension Check\n\n" +
                "───────────────────────\n" +
                "💡 Pattern ที่เห็นร่วมกัน\n\n" +
                "Knowledge ไม่ได้นำไปสู่ Understanding โดยอัตโนมัติ\n" +
                "ต้องมีเวลา ตัวอย่าง การสนทนา และการสะท้อน\n" +
                "ก่อนที่ความเข้าใจจะเกิดขึ้น",

            GuidedExamples:
            [
                "ลองดู D004 นะคะ\n\n" +
                "\"ยิ่งมีเอกสารมาก ยิ่งเสี่ยง misalignment\"\n\n" +
                "ฟังดูขัดแย้งกัน แต่มันเป็นความจริง\n" +
                "คุณเคยเจอสถานการณ์แบบนี้ไหม — " +
                "มีข้อมูลเยอะแต่คนยังเข้าใจไม่ตรงกัน? เพราะอะไร?"
            ],

            GrillQuestions:
            [
                "จาก 5 Discoveries ที่เล่าให้ฟัง\n" +
                "คุณคิดว่าอันไหนสำคัญที่สุดสำหรับ product ตัวนี้?\n" +
                "และ Discovery นั้นเปลี่ยนทิศทางของ product ยังไง?",

                "D006 บอกว่า \"You cannot assess understanding\n" +
                "before understanding has had a chance to form.\"\n\n" +
                "คุณเห็นด้วยไหม? และถ้าเห็นด้วย — \n" +
                "มันหมายความว่าอะไรในการออกแบบ onboarding?",

                "ถ้าต้องอธิบาย D001 ให้ผู้ปกครองฟัง\n" +
                "ในแบบที่เขาจะเข้าใจและเห็นว่า AI Tutor ตัวนี้แตกต่างจาก app อื่น\n" +
                "คุณจะพูดว่าอะไร?"
            ],

            ExplainWords:  ["หมายความว่า", "ก็คือ", "คือว่า", "discovery", "พบว่า", "สังเกต"],
            ReasonWords:   ["เพราะ", "เหตุผล", "เนื่องจาก", "นำไปสู่", "จึง"],
            ApplyWords:    ["เช่น", "ตัวอย่าง", "ถ้า", "ในการออกแบบ", "สถานการณ์"]
        ),

        new CurriculumTopic(
            Id: "decisions",
            Title: "Key Decisions",
            Emoji: "✅",
            Subtitle: "ทำไมถึงตัดสินใจแบบนี้",
            KnowledgeContent:
                "📌 หลักการตัดสินใจ\n\n" +
                "ทุก decision ต้องมาจากหลักฐาน ไม่ใช่ความรู้สึก\n" +
                "Evidence Before Decisions\n\n" +
                "───────────────────────\n" +
                "✅ DEC-001 — ไม่ใช้ Understanding Score\n\n" +
                "เหตุผล: ยังไม่มีหลักฐานว่า score วัดความเข้าใจได้จริง\n" +
                "แทน: เก็บ Evidence ก่อน สร้าง Scoring ทีหลัง\n\n" +
                "───────────────────────\n" +
                "✅ DEC-002 — เพิ่ม Comprehension Check\n\n" +
                "เหตุผล: ผู้ใช้จริงบอกว่า AI เริ่ม Grill เร็วเกินไป\n" +
                "แทน: ต้องผ่าน Comprehension ก่อนเข้า Grill\n\n" +
                "───────────────────────\n" +
                "✅ DEC-003 — Prioritize Real Co-Founder Onboarding\n\n" +
                "เหตุผล: ต้องการหลักฐานจริงก่อนสร้าง advanced features\n" +
                "แทน: ทำ onboarding จริงก่อน ค่อยสร้าง Retrieval/Dashboard\n\n" +
                "───────────────────────\n" +
                "✅ ไม่สร้าง Authentication (ในช่วง demo)\n\n" +
                "เหตุผล: Login เพิ่ม friction โดยไม่ validate การสอน\n" +
                "แทน: Anonymous ID, ไม่บังคับ login ในช่วงทดสอบ\n\n" +
                "───────────────────────\n" +
                "✅ ไม่ทำ Gamification\n\n" +
                "เหตุผล: XP, badges, leaderboard ขัดกับ learning-not-scoring philosophy\n" +
                "หลักการ: ถ้า feature ไม่ช่วยให้นักเรียนเรียนรู้ด้วยตัวเอง — อย่าสร้าง\n\n" +
                "───────────────────────\n" +
                "📋 MVP Decision Rule\n\n" +
                "ก่อนเพิ่ม feature ทุกครั้ง:\n" +
                "\"Feature นี้ช่วยให้นักเรียนเรียนรู้ได้ด้วยตัวเองไหม?\"\n" +
                "ถ้าไม่ — อย่าสร้าง",

            GuidedExamples:
            [
                "ดู DEC-001 นะคะ — ไม่สร้าง Understanding Score\n\n" +
                "ฟังดูง่าย แต่มัน counterintuitive มาก\n" +
                "เพราะปกติ app ทุกตัวมี score, progress bar, badge\n\n" +
                "ทำไมเราถึงเลือกไม่ทำ? และ trade-off คืออะไร?"
            ],

            GrillQuestions:
            [
                "ลูกค้ารายใหม่ถามว่า \"app นี้มีระบบ reward ไหม เด็กจะได้แรงจูงใจ\"\n\n" +
                "คุณจะตอบยังไง?\n" +
                "และอธิบาย decision ที่อยู่เบื้องหลังคำตอบนั้นได้ไหม?",

                "ถ้าวันนี้มี investor บอกว่า\n" +
                "\"ถ้าไม่มี analytics dashboard ฉันไม่ลงทุน\"\n\n" +
                "คุณจะตอบว่าอะไร?\n" +
                "และ decision นี้ขัดกับ MVP Scope ยังไง?",

                "จาก decisions ที่เล่าให้ฟัง\n" +
                "decision ไหนที่คุณเห็นด้วยน้อยที่สุด?\n" +
                "และถ้าจะเถียง — เถียงด้วยเหตุผลอะไร?"
            ],

            ExplainWords:  ["หมายความว่า", "ก็คือ", "decision", "ตัดสินใจ", "เลือก"],
            ReasonWords:   ["เพราะ", "เหตุผล", "เนื่องจาก", "เพราะว่า", "trade-off"],
            ApplyWords:    ["เช่น", "ตัวอย่าง", "ถ้า", "investor", "ลูกค้า"]
        ),
    ];

    public static CurriculumTopic? Get(string topicId) =>
        Topics.FirstOrDefault(t => t.Id == topicId);
}
