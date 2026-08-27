using Before8AM.Collection;

namespace Before8AM.Loot
{
    /// <summary>午夜遗物单条数据（规格书例：不存在的毕业证 8000/+50、零点以前的学生证 5000/+30、消失的寝室钥匙 12000/+80）。</summary>
    public struct RelicInfo
    {
        public string Name;
        /// <summary>战利品价值（结算 RankScore 的战利品分）。</summary>
        public int Value;
        /// <summary>撤离时的额外 Rank 加分（规格书"午夜遗物额外分"）。</summary>
        public int RankBonus;
        public CollectionEntry Entry;
    }

    /// <summary>
    /// [0.8.0] 午夜遗物目录（数据驱动）：MidnightRelic 品质宝箱开出的高价值遗物。
    /// 开出后记 RunManager.RelicIndex（本局战利品），结算加 RankScore + 图鉴解锁（批次4 结算落地）。
    /// </summary>
    public static class RelicCatalog
    {
        public static readonly RelicInfo[] All =
        {
            new RelicInfo { Name = "不存在的毕业证",     Value = 8000,  RankBonus = 50, Entry = CollectionEntry.RelicDiploma },
            new RelicInfo { Name = "零点以前的学生证",   Value = 5000,  RankBonus = 30, Entry = CollectionEntry.RelicStudentCard },
            new RelicInfo { Name = "消失的寝室钥匙",     Value = 12000, RankBonus = 80, Entry = CollectionEntry.RelicDormKey },
        };
    }
}
