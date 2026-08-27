using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Before8AM.Reward
{
    /// <summary>
    /// [0.8.0] 午夜榜单条成绩（本地积分排行榜）：一局成功撤离的段位分 + 日期 + 地图 + 遗物徽标。
    /// </summary>
    public struct RankEntry
    {
        public int Score;      // 单局段位分（RankDetail.Total，[0.8.1] 基础 100 + 战利品/100 + 遗物 + 极限）
        public string Date;    // MM-dd（结算当日）
        public int MapIndex;   // 0=午夜校园 1=午夜超市
        public bool Relic;     // 本局带出午夜遗物
    }

    /// <summary>
    /// [0.8.0] 午夜榜（本地积分排行榜，规格书 75「午夜王者=积分排行榜」）。
    /// 无后端 → 本地排行：每局成功结算后 Add 插入 → 降序排序 → 截断 Top 8 → PlayerPrefs 持久化。
    /// 展示：主菜单「午夜榜」面板（MidnightRankView）。段位名映射复用 GameProgress.RankNameFor。
    /// </summary>
    public static class RankBoard
    {
        const string Key = "Before8AM.RankBoard";
        public const int MaxEntries = 8;

        static RankEntry[] cache;

        public static int Count => Entries().Length;

        /// <summary>历史最高单局分（午夜榜第一名的分数）。</summary>
        public static int BestScore
        {
            get
            {
                var arr = Entries();
                return arr.Length > 0 ? arr[0].Score : 0;
            }
        }

        /// <summary>第 i 名成绩（0=最高分，已按分数降序）。</summary>
        public static RankEntry Get(int i)
        {
            var arr = Entries();
            return (i >= 0 && i < arr.Length) ? arr[i] : default(RankEntry);
        }

        /// <summary>[0.8.0] 新增成绩：成功撤离后插入 → 降序排序 → 截断 Top 8 → 持久化。</summary>
        public static void Add(int score, int mapIndex, bool relic)
        {
            if (score <= 0) return;
            var list = new List<RankEntry>(Entries());
            list.Add(new RankEntry
            {
                Score = score,
                Date = System.DateTime.Now.ToString("MM-dd"),
                MapIndex = mapIndex,
                Relic = relic,
            });
            list.Sort((a, b) => b.Score.CompareTo(a.Score));   // 降序：最高分第一

            int n = list.Count > MaxEntries ? MaxEntries : list.Count;
            var keep = new RankEntry[n];
            for (int i = 0; i < n; i++) keep[i] = list[i];

            var sb = new StringBuilder();
            for (int i = 0; i < keep.Length; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(keep[i].Score).Append('|').Append(keep[i].Date)
                  .Append('|').Append(keep[i].MapIndex).Append('|').Append(keep[i].Relic ? 1 : 0);
            }
            PlayerPrefs.SetString(Key, sb.ToString());
            cache = keep;
        }

        /// <summary>[0.8.0] 清空午夜榜（调试/重置）。</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            cache = new RankEntry[0];
        }

        static RankEntry[] Entries()
        {
            if (cache != null) return cache;
            string raw = PlayerPrefs.GetString(Key, "");
            var list = new List<RankEntry>();
            if (!string.IsNullOrEmpty(raw))
            {
                string[] parts = raw.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string[] f = parts[i].Split('|');
                    int score, map;
                    if (f.Length < 4 || !int.TryParse(f[0], out score) || !int.TryParse(f[2], out map)) continue;
                    list.Add(new RankEntry { Score = score, Date = f[1], MapIndex = map, Relic = f[3] == "1" });
                }
            }
            cache = list.ToArray();
            return cache;
        }
    }
}
