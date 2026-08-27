using System;
using UnityEngine;
using Before8AM.Reward;

namespace Before8AM.Mission
{
    /// <summary>
    /// [0.8.0] 每日任务 + 7 日挑战（规格书 82/83）。
    /// 每日任务 5 项固定：逃脱 1 次 / 搜刮 10 次 / 5 碎片 / 单局带出 3000 价值 / 无被发现撤离。
    /// 奖励 = XP + 永久金币（Cosmetics 后续系统）。
    /// 7 日挑战：连续 7 天各自特殊规则 → 第 7 天完成解锁限定外观（Cosmetic 占位）。
    /// 跨天自动重置（每日任务按自然日；挑战断签重置回第 1 天）。全部 PlayerPrefs 持久化。
    /// 游戏内事件挂钩：RunManager（碎片/道具）/ RewardSystem（撤离）/ 守卫感知（无发现标志）/ 宝箱与拾取物（搜刮）。
    /// </summary>
    public static class MissionSystem
    {
        // ---------- PlayerPrefs 键 ----------
        const string DateKey = "Before8AM.Missions.Date";
        const string ProgKey = "Before8AM.Missions.Progress";      // "a,b,c,d,e"（每日任务 5 进度）
        const string ClaimKey = "Before8AM.Missions.Claimed";      // 位掩码（已领奖）
        const string ChalDateKey = "Before8AM.Challenge.Date";
        const string ChalDayKey = "Before8AM.Challenge.Day";       // 1~7（当前第几天）
        const string ChalDoneKey = "Before8AM.Challenge.Done";     // 今天规则是否已完成
        const string ChalProgKey = "Before8AM.Challenge.Progress"; // 今天累计（计数型规则）
        const string ChalClaimKey = "Before8AM.Challenge.RewardClaimed";
        const string ChalQualKey = "Before8AM.Challenge.Qualified";   // [审查] 历史达成过第 7 天（领取资格，独立于断签重置：完成后再断签不没收已到手资格）

        static bool dayChecked;   // 进程内一天只查一次跨天

        public enum DailyTaskId { Escape, Loot, Fragments, Value, Stealth }
        public enum ChallengeRule { Escape, Stealth, Loot, Fragments, Value, ItemUse }
        public const int TaskCount = 5;

        public struct TaskDef
        {
            public string Name; public string Desc; public int Target;
            public int RewardXp; public int RewardCoins;
        }

        /// <summary>每日任务定义（规格书 82 五项）。Value 目标 3000 为单局；碎片/搜刮跨局累计。</summary>
        static readonly TaskDef[] Tasks =
        {
            new TaskDef { Name = "逃出校园",   Desc = "成功撤离 1 次",              Target = 1,  RewardXp = 100, RewardCoins = 150 },
            new TaskDef { Name = "搜刮大师",   Desc = "搜刮宝箱 / 拾取物 10 次",    Target = 10, RewardXp = 50,  RewardCoins = 80 },
            new TaskDef { Name = "时间碎片",   Desc = "累计拾取 5 个时间碎片",      Target = 5,  RewardXp = 60,  RewardCoins = 100 },
            new TaskDef { Name = "满载而归",   Desc = "单局带出 3000 战利品价值",  Target = 1,  RewardXp = 150, RewardCoins = 300 },
            new TaskDef { Name = "无声夜行",   Desc = "无被发现撤离 1 次",          Target = 1,  RewardXp = 120, RewardCoins = 200 },
        };

        public static TaskDef GetTask(int i) { EnsureDay(); return Tasks[i]; }

        // ---------- 本局"被发现"标志（守卫 EnterState Alert/Chase → MarkDetected；RunManager.StartRun 重置） ----------
        public static bool RunDetected;
        public static void ResetRunFlag() => RunDetected = false;
        public static void MarkDetected() => RunDetected = true;

        // ---------- 每日任务进度 / 领取 ----------
        public static int GetProgress(int i)
        {
            EnsureDay();
            int[] p = ReadProg();
            return i >= 0 && i < p.Length ? p[i] : 0;
        }

        public static bool IsDone(int i) { EnsureDay(); return GetProgress(i) >= Tasks[i].Target; }
        public static bool IsClaimed(int i) { EnsureDay(); return (PlayerPrefs.GetInt(ClaimKey, 0) & (1 << i)) != 0; }

        /// <summary>领取每日任务奖励（完成且未领 → XP + 永久金币入账，返回 (xp, coins)；否则 (0,0)）。</summary>
        public static (int xp, int coins) ClaimDaily(int i)
        {
            EnsureDay();
            if (i < 0 || i >= Tasks.Length || IsClaimed(i) || !IsDone(i)) return (0, 0);
            PlayerPrefs.SetInt(ClaimKey, PlayerPrefs.GetInt(ClaimKey, 0) | (1 << i));
            GameProgress.AddXP(Tasks[i].RewardXp);
            GameProgress.AddPermanentCoins(Tasks[i].RewardCoins);
            return (Tasks[i].RewardXp, Tasks[i].RewardCoins);
        }

        // ---------- 7 日挑战状态 ----------
        public static int ChallengeDay { get { EnsureDay(); return PlayerPrefs.GetInt(ChalDayKey, 1); } }
        public static bool ChallengeDoneToday { get { EnsureDay(); return PlayerPrefs.GetInt(ChalDoneKey, 0) == 1; } }
        public static int ChallengeProgress { get { EnsureDay(); return PlayerPrefs.GetInt(ChalProgKey, 0); } }
        public static bool ChallengeRewardClaimed { get { EnsureDay(); return PlayerPrefs.GetInt(ChalClaimKey, 0) == 1; } }
        public static bool ChallengeComplete => ChallengeDay >= 7 && ChallengeDoneToday;

        /// <summary>当日挑战规则名（面板显示）。Day7 为终极试炼（同 Day2 规则，连续 7 天达标）。</summary>
        public static string ChallengeRuleName
        {
            get
            {
                switch (ChallengeDay)
                {
                    case 1: return "成功撤离 1 次";
                    case 2: return "无被发现撤离 1 次";
                    case 3: return "开 3 个宝箱";
                    case 4: return "拾取 5 个时间碎片";
                    case 5: return "单局带出 2000 战利品价值";
                    case 6: return "使用 5 次道具";
                    default: return "无被发现撤离 1 次";
                }
            }
        }

        /// <summary>7 日挑战计数型规则（Day3 3 箱 / Day4 5 碎片 / Day6 5 道具）的目标数。</summary>
        public static int ChallengeTarget
        {
            get
            {
                switch (ChallengeDay)
                {
                    case 3: return 3;
                    case 4: return 5;
                    case 6: return 5;
                    default: return 1;
                }
            }
        }

        /// <summary>领取 7 日完成奖励（唯一：限定外观 Cosmetic）。
        /// [审查] 资格看 ChalQualKey（历史达成第 7 天），不看 ChallengeComplete——否则完成第 7 天后没当场领、次日/断签再打开就永远领不了（断签重置把 ChalDayKey 拉回 1）。</summary>
        public static bool ClaimChallengeReward()
        {
            EnsureDay();
            if (ChallengeRewardClaimed || PlayerPrefs.GetInt(ChalQualKey, 0) != 1) return false;
            PlayerPrefs.SetInt(ChalClaimKey, 1);
            GameProgress.UnlockCosmetic(GameProgress.CosmeticSevenDay);
            return true;
        }

        // ---------- 游戏内事件挂钩（RunManager / RewardSystem / 守卫 / 宝箱 / 拾取物调用） ----------
        public static void OnEscape() { EnsureDay(); Record(DailyTaskId.Escape, 1); ChallengeTick(ChallengeRule.Escape); }
        public static void OnLootCollected() { EnsureDay(); Record(DailyTaskId.Loot, 1); ChallengeTick(ChallengeRule.Loot); }
        public static void OnFragment() { EnsureDay(); Record(DailyTaskId.Fragments, 1); ChallengeTick(ChallengeRule.Fragments); }
        public static void OnValueEscaped(int value) { EnsureDay(); if (value >= 3000) Record(DailyTaskId.Value, 1); if (value >= 2000) ChallengeTick(ChallengeRule.Value); }
        public static void OnStealthEscape() { EnsureDay(); if (RunDetected) return; Record(DailyTaskId.Stealth, 1); ChallengeTick(ChallengeRule.Stealth); }   // [审查] ChallengeTick 必须同守卫：被发现的撤离不能完成 Day2/7「无被发现」挑战
        public static void OnItemUsed() { EnsureDay(); ChallengeTick(ChallengeRule.ItemUse); }

        // ---------- 内部 ----------
        static void Record(DailyTaskId id, int amount)
        {
            int[] p = ReadProg();
            int i = (int)id;
            if (i < 0 || i >= p.Length) return;
            if (p[i] >= Tasks[i].Target) return;   // 已满不再记
            p[i] = Mathf.Min(p[i] + amount, Tasks[i].Target);
            WriteProg(p);
        }

        static void ChallengeTick(ChallengeRule rule)
        {
            EnsureDay();
            if (PlayerPrefs.GetInt(ChalClaimKey, 0) == 1) return;   // 已领过奖，挑战不再推进
            if (PlayerPrefs.GetInt(ChalDoneKey, 0) == 1) return;    // 今天已达成
            int day = PlayerPrefs.GetInt(ChalDayKey, 1);

            bool matches = day switch
            {
                1 => rule == ChallengeRule.Escape,
                2 => rule == ChallengeRule.Stealth,
                3 => rule == ChallengeRule.Loot,
                4 => rule == ChallengeRule.Fragments,
                5 => rule == ChallengeRule.Value,
                6 => rule == ChallengeRule.ItemUse,
                7 => rule == ChallengeRule.Stealth,   // 终极试炼：与 Day2 同规则，连续 7 天达标
                _ => false,
            };
            if (!matches) return;

            // 计数型规则（Day3 开 3 箱 / Day4 5 碎片 / Day6 5 道具）
            int need = day switch { 3 => 3, 4 => 5, 6 => 5, _ => 0 };
            if (need > 0)
            {
                int cur = PlayerPrefs.GetInt(ChalProgKey, 0) + 1;
                if (cur < need) { PlayerPrefs.SetInt(ChalProgKey, cur); return; }
            }
            PlayerPrefs.SetInt(ChalDoneKey, 1);
            if (day >= 7) PlayerPrefs.SetInt(ChalQualKey, 1);   // [审查] 达成第 7 天即锁定领取资格（断签重置不没收）
        }

        // ---------- 跨天检测（惰性：任何访问自动重置；进程内缓存一次） ----------
        static void EnsureDay()
        {
            if (dayChecked) return;
            dayChecked = true;
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            // 每日任务按自然日重置
            if (PlayerPrefs.GetString(DateKey, "") != today)
            {
                PlayerPrefs.SetString(DateKey, today);
                PlayerPrefs.SetString(ProgKey, "0,0,0,0,0");
                PlayerPrefs.SetInt(ClaimKey, 0);
            }

            // 7 日挑战：昨天连续且完成 → 推进一天；断签 → 回到第 1 天
            string last = PlayerPrefs.GetString(ChalDateKey, "");
            if (last != today)
            {
                int day = PlayerPrefs.GetInt(ChalDayKey, 1);
                bool doneYesterday = PlayerPrefs.GetInt(ChalDoneKey, 0) == 1;
                bool consecutive = IsYesterday(last, today);
                day = (consecutive && doneYesterday) ? Mathf.Min(day + 1, 7) : 1;
                PlayerPrefs.SetString(ChalDateKey, today);
                PlayerPrefs.SetInt(ChalDayKey, day);
                PlayerPrefs.SetInt(ChalDoneKey, 0);
                PlayerPrefs.SetInt(ChalProgKey, 0);
            }
        }

        static bool IsYesterday(string a, string b)
        {
            if (a.Length != 10 || b.Length != 10) return false;
            DateTime da, db;
            if (!DateTime.TryParse(a, out da) || !DateTime.TryParse(b, out db)) return false;
            return (db - da).Days == 1;
        }

        static int[] ReadProg()
        {
            int[] p = new int[Tasks.Length];
            string[] parts = PlayerPrefs.GetString(ProgKey, "0,0,0,0,0").Split(',');
            for (int i = 0; i < parts.Length && i < p.Length; i++) int.TryParse(parts[i], out p[i]);
            return p;
        }

        static void WriteProg(int[] p) => PlayerPrefs.SetString(ProgKey, string.Join(",", p));
    }
}
