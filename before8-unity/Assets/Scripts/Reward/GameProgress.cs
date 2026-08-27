using UnityEngine;

namespace Before8AM.Reward
{
    /// <summary>
    /// 跨局永久进度（PlayerPrefs 持久化）。
    /// 照 ViewToggle 范式：`Before8AM.` 前缀键 + GetX 带默认 + 改动即 SetX（无显式 Save）。
    /// [0.4.0] 奖励循环地基：永久金币 / 累计局数 / 累计成功次数——段位、图鉴、皮肤等后续系统都挂在这上面。
    /// 规则（规格书 27/28）：成功逃出才入账永久金币；被抓/超时本局没收，永久进度不受影响。
    /// </summary>
    public static class GameProgress
    {
        const string CoinKey = "Before8AM.PermanentCoins";
        const string RunsKey = "Before8AM.TotalRuns";
        const string EscapeKey = "Before8AM.EscapeCount";
        const string XpKey = "Before8AM.XP";
        const string RankKey = "Before8AM.RankScore";

        /// <summary>累计永久金币（成功逃出才入账）。</summary>
        public static int PermanentCoins
        {
            get => PlayerPrefs.GetInt(CoinKey, 0);
            set => PlayerPrefs.SetInt(CoinKey, value);
        }

        /// <summary>累计跑局数（含成功与失败，段位/统计地基）。</summary>
        public static int TotalRuns
        {
            get => PlayerPrefs.GetInt(RunsKey, 0);
            set => PlayerPrefs.SetInt(RunsKey, value);
        }

        /// <summary>累计成功逃出次数。</summary>
        public static int EscapeCount
        {
            get => PlayerPrefs.GetInt(EscapeKey, 0);
            set => PlayerPrefs.SetInt(EscapeKey, value);
        }

        /// <summary>成功结算：本局金币入账永久金币（本局未被没收的部分）。</summary>
        public static void AddPermanentCoins(int amount)
        {
            if (amount <= 0) return;
            PermanentCoins += amount;
        }

        /// <summary>记录一局结束（成功/失败都记局数，成功另记逃出次数）。</summary>
        public static void RecordRun(bool escaped)
        {
            TotalRuns++;
            if (escaped) EscapeCount++;
        }

        // ---------- [0.5] 地图解锁（金币消费端，用户核心系统） ----------

        const string UnlockMapsKey = "Before8AM.UnlockedMaps";

        /// <summary>地图索引（位掩码 bit）：0=午夜校园（默认解锁），1=午夜超市（¥5000 金币解锁）。</summary>
        public const int MapCampusIndex = 0;
        public const int MapParkingIndex = 1;

        /// <summary>已解锁地图位掩码（默认 1 = 校园已解锁）。</summary>
        public static int UnlockedMaps
        {
            get => PlayerPrefs.GetInt(UnlockMapsKey, 1);
            set => PlayerPrefs.SetInt(UnlockMapsKey, value);
        }

        public static bool IsMapUnlocked(int mapIndex) => (UnlockedMaps & (1 << mapIndex)) != 0;

        /// <summary>消费永久金币（够才扣，返回是否成功）。地图解锁/未来商店共用。</summary>
        public static bool TrySpendCoins(int amount)
        {
            if (amount < 0) return false;
            int cur = PermanentCoins;
            if (cur < amount) return false;
            PermanentCoins = cur - amount;
            return true;
        }

        /// <summary>金币解锁地图（扣款成功才置位）。</summary>
        public static bool TryUnlockMap(int mapIndex, int price)
        {
            if (IsMapUnlocked(mapIndex)) return false;
            if (!TrySpendCoins(price)) return false;
            UnlockedMaps |= 1 << mapIndex;
            return true;
        }

        // ---------- [0.4.4] Meta：等级（XP 制）+ 段位（RankScore 制） ----------

        /// <summary>累计经验值（等级来源：碎片 +30 / 开宝箱 +20 / 成功 +200 / 失败 +30 安慰）。</summary>
        public static int XP
        {
            get => PlayerPrefs.GetInt(XpKey, 0);
            set => PlayerPrefs.SetInt(XpKey, value);
        }

        /// <summary>累计段位分（成功 = RankDetail 公式：[0.8.1] 百级；失败不加不减，规格书"失败不掉分"）。</summary>
        public static int RankScore
        {
            get => PlayerPrefs.GetInt(RankKey, 0);
            set => PlayerPrefs.SetInt(RankKey, value);
        }

        public static void AddXP(int amount)
        {
            if (amount <= 0) return;
            XP += amount;
        }

        public static void AddRankScore(int amount)
        {
            if (amount <= 0) return;
            RankScore += amount;
        }

        /// <summary>等级：每级需 100 XP，从累计 XP 反推（Lv.1 起，100→Lv.2，200→Lv.3…）。</summary>
        public static int Level
        {
            get
            {
                int xp = XP, level = 1;
                while (xp >= 100) { xp -= 100; level++; }
                return level;
            }
        }

        /// <summary>段位表（累计 RankScore 阈值；GAME_DESIGN：新生→夜行者→熟夜生→深夜探险家→午夜大师→午夜宗师→午夜王者）。
        /// [0.8.1] 分值压到百级（与 RankDetail 公式同步：成功一局 ≈110 分，带遗物 200~300），王者 8000 ≈ 60~70 把登顶。
        /// [0.9.2+] 基础分降到 70：成功一局 ≈75 分，带遗物 150~200，王者 8000 ≈ 100 把登顶。</summary>
        static readonly (int Threshold, string Name)[] RankTiers =
        {
            (0, "新生"),
            (100, "夜行者 III"), (250, "夜行者 II"), (450, "夜行者 I"),
            (700, "熟夜生 III"), (1000, "熟夜生 II"), (1400, "熟夜生 I"),
            (1800, "深夜探险家 III"), (2300, "深夜探险家 II"), (2900, "深夜探险家 I"),
            (3600, "午夜大师 III"), (4300, "午夜大师 II"), (5100, "午夜大师 I"),
            (6000, "午夜宗师 III"), (7000, "午夜宗师 II"), (7800, "午夜宗师 I"),
            (8000, "午夜王者"),
        };

        /// <summary>[0.8.0] 按积分映射段位名：累计 RankScore 与午夜榜单局分共用同一阈值表（午夜榜按单局分显示档位）。</summary>
        public static string RankNameFor(int score)
        {
            string best = "新生";
            for (int i = 0; i < RankTiers.Length; i++)
                if (score >= RankTiers[i].Threshold) best = RankTiers[i].Name;
            return best;
        }

        /// <summary>[0.8.0] 午夜王者门槛（段位表顶档阈值）——午夜榜/宿舍进度条归一化基准，单一出处防硬编码漂移。</summary>
        public static int RankKingThreshold => RankTiers[RankTiers.Length - 1].Threshold;

        /// <summary>[0.9.2] 段位表只读访问（「段位一览」面板用）：只给下标取值，不暴露数组引用防外部改动。</summary>
        public static int RankTierCount => RankTiers.Length;
        public static int RankTierThresholdAt(int i) => RankTiers[i].Threshold;
        public static string RankTierNameAt(int i) => RankTiers[i].Name;

        /// <summary>[0.9.2] 段位进度查询：当前段位档的起点阈值、下一档阈值与名称（供主菜单进度条/「距下一段位还差 N 分」标注）。
        /// 王者已登顶 → nextThreshold=-1、nextName=null。不直接暴露 RankTiers 表，防外部改动。</summary>
        public static void RankTierBounds(out int curThreshold, out int nextThreshold, out string nextName)
        {
            for (int i = RankTiers.Length - 1; i >= 0; i--)
            {
                if (RankScore >= RankTiers[i].Threshold)
                {
                    curThreshold = RankTiers[i].Threshold;
                    if (i + 1 < RankTiers.Length)
                    {
                        nextThreshold = RankTiers[i + 1].Threshold;
                        nextName = RankTiers[i + 1].Name;
                    }
                    else
                    {
                        nextThreshold = -1;
                        nextName = null;
                    }
                    return;
                }
            }
            // 不可达兜底：第 0 档阈值 0 必命中
            curThreshold = 0;
            nextThreshold = RankTiers[1].Threshold;
            nextName = RankTiers[1].Name;
        }

        /// <summary>当前段位名（按 RankScore 命中最高档）。</summary>
        public static string RankName => RankNameFor(RankScore);

        /// <summary>[0.8.6] 段位族 index（0~6：新生/夜行者/熟夜生/探险家/大师/宗师/王者）——按各族族首阈值映射，
        /// 局内难度（增援计划）与金币系数共用。子档（III/II/I）不单独开档，族内强度一致。</summary>
        public static int RankFamilyIndex
        {
            get
            {
                int s = RankScore;
                if (s >= 8000) return 6;
                if (s >= 6000) return 5;
                if (s >= 3600) return 4;
                if (s >= 1800) return 3;
                if (s >= 700) return 2;
                if (s >= 100) return 1;
                return 0;
            }
        }

        /// <summary>[0.8.6] 段位金币系数：每族 +3%（新生 1.0 → 王者 1.18）。
        /// 收益增幅（≤18%）远低于难度增幅（增援守卫 0→5，基数 7 只最多 +71%）——"收益比难度涨得慢"。</summary>
        public static float CoinRankMultiplier => 1f + RankFamilyIndex * 0.03f;

        // ---------- [0.8.0] Cosmetics（限定外观：7 日挑战奖励占位，美术后期做效果） ----------

        const string CosmeticsKey = "Before8AM.Cosmetics";

        /// <summary>7 日挑战限定外观（连续 7 天完成解锁；占位标志，效果待美术）。</summary>
        public const int CosmeticSevenDay = 0;

        /// <summary>已解锁外观位掩码。</summary>
        public static int Cosmetics
        {
            get => PlayerPrefs.GetInt(CosmeticsKey, 0);
            set => PlayerPrefs.SetInt(CosmeticsKey, value);
        }

        public static bool HasCosmetic(int id) => (Cosmetics & (1 << id)) != 0;

        public static void UnlockCosmetic(int id) => Cosmetics |= 1 << id;

        // ---------- [0.9.0] 角色皮肤系统：已装备皮肤 ----------

        const string EquippedSkinKey = "Before8AM.EquippedSkin";

        /// <summary>已装备皮肤 id（默认 1=夜海蓝原皮；只存键不校验，SkinCatalog.ValidatedEquipped 兜底回退）。</summary>
        public static int EquippedSkin
        {
            get => PlayerPrefs.GetInt(EquippedSkinKey, 1);
            set => PlayerPrefs.SetInt(EquippedSkinKey, value);
        }
    }
}
