using UnityEngine;

namespace Before8AM.Collection
{
    /// <summary>
    /// [0.4.5] 图鉴条目（位掩码，一位一条目）。
    /// [0.8.0] int → long：30 条后再加遗物 3 条会顶到 32 位符号位（1<<31 溢出为负，位运算/统计死循环），
    /// 故底层改 long（1L&lt;&lt;30 起），持久化从 int 改 string（兼容读旧 int 存档）。
    /// 名称对应已实现实体；位值 = 1&lt;&lt;n 便于位运算与收集统计。
    /// </summary>
    public enum CollectionEntry : long
    {
        TorchItem = 1L << 0,         // 道具·灯油
        SpeedDrink = 1L << 1,        // 道具·加速饮料
        TimeHourglass = 1L << 2,     // 道具·时间沙漏
        InvisibilityPotion = 1L << 3,// 道具·隐身药水
        Scout = 1L << 4,             // 守卫·Scout（侦察）
        Runner = 1L << 5,            // 守卫·Runner（追捕）
        Tracker = 1L << 6,           // 守卫·Tracker（追踪）
        Guardian = 1L << 7,          // 守卫·Guardian（守卫者）
        TimeFragment = 1L << 8,      // 碎片·时间碎片
        TimeRift = 1L << 9,          // 事件·时间裂缝
        LureChest = 1L << 10,        // 事件·诱饵宝箱
        Blackout = 1L << 11,         // 事件·黑屏广播
        // [0.8.0] 新增六道具（独立高位，不插进旧位——持久化标志不可移动）
        SoundDecoy = 1L << 12,       // 道具·声学诱饵
        Detector = 1L << 13,         // 道具·探测器
        Jammer = 1L << 14,           // 道具·干扰器
        Teleporter = 1L << 15,       // 道具·传送器
        NightVision = 1L << 16,      // 道具·夜视仪
        FakeStudentCard = 1L << 17,  // 道具·假学生卡
        // [0.8.0] 新增 12 随机事件（规格书 15 事件 → 3 已有 + 12 新增；1<<18~1<<29，不插旧位）
        MidnightMerchant = 1L << 18, // 事件·午夜商人
        MysteryVending = 1L << 19,   // 事件·神秘售货机
        LateNightCanteen = 1L << 20, // 事件·深夜食堂
        OldStudentCard = 1L << 21,   // 事件·旧学生卡
        LuckyRoom = 1L << 22,        // 事件·幸运房间
        TempSafeHouse = 1L << 23,    // 事件·临时安全屋
        PortalEvent = 1L << 24,      // 事件·传送门
        LostAndFound = 1L << 25,     // 事件·遗失物招领处
        InfiniteClassroom = 1L << 26,// 事件·无限教室
        TimeStopZone = 1L << 27,     // 事件·时间停滞区
        ReverseStaircase = 1L << 28, // 事件·逆流楼梯
        TimeStorm = 1L << 29,        // 事件·时间风暴
        // [0.8.0] 午夜遗物（3 条，规格书例：不存在的毕业证/零点以前的学生证/消失的寝室钥匙）
        RelicDiploma = 1L << 30,     // 遗物·不存在的毕业证
        RelicStudentCard = 1L << 31, // 遗物·零点以前的学生证
        RelicDormKey = 1L << 32,     // 遗物·消失的寝室钥匙
    }

    /// <summary>
    /// 图鉴进度（PlayerPrefs 持久化，单 int 位掩码键）。
    /// 照 GameProgress 范式：`Before8AM.` 前缀 + 改动即 Set（无显式 Save）。
    /// **无静态缓存**：场景重载（重开）直接读 PlayerPrefs，进度天然跨局保留。
    /// 扩容：条目 &lt;31 继续加 bit；超出再改 long / 拆分类键。
    /// </summary>
    public static class CollectionSystem
    {
        const string FlagsKey = "Before8AM.CollectionFlags";
        // [0.8.1] 回退：TotalCount 33 → 12（道具 4 + 守卫 4 + 碎片 1 + 遗物 3；事件/新道具条目已从 CollectionCatalog 移除）。
        // CollectionEntry 枚举值全部保留（位掩码不可移动，删值会毁老存档位）；暂停条目收集到后仍入存档，只是图鉴不显示。
        public const int TotalCount = 12;

        // [0.8.0] long 位掩码：存 string；读时优先 string，兼容旧 int 存档（空串 → GetInt 迁移）
        public static long Flags
        {
            get
            {
                string s = PlayerPrefs.GetString(FlagsKey, "");
                if (s.Length > 0) return long.Parse(s);
                int legacy = PlayerPrefs.GetInt(FlagsKey, 0);
                // [审查] 迁移：PlayerPrefs 同键 int/string 互斥，SetString 已覆盖旧 int 存储——不得再 DeleteKey（会把刚写的 string 连键删掉，老玩家图鉴进度丢失）
                if (legacy != 0) PlayerPrefs.SetString(FlagsKey, legacy.ToString());
                return legacy;
            }
            set => PlayerPrefs.SetString(FlagsKey, value.ToString());
        }

        public static bool Has(CollectionEntry e) => (Flags & (long)e) != 0;

        public static int CollectedCount
        {
            get
            {
                long f = Flags, n = 0;
                while (f != 0) { n += f & 1L; f >>= 1; }
                return (int)n;
            }
        }

        /// <summary>收录一条（幂等：已收录返回 false，不重复计）。</summary>
        public static bool Unlock(CollectionEntry e)
        {
            long bit = (long)e;
            if ((Flags & bit) != 0) return false;
            Flags |= bit;
            Debug.Log($"[图鉴] 新收录：{e}（{CollectedCount}/{TotalCount}）");
            return true;
        }
    }
}
