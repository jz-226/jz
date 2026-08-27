using UnityEngine;

namespace Before8AM.Collection
{
    /// <summary>图鉴条目展示数据（名称/描述/色块，由 CollectionView 渲染）。</summary>
    public struct EntryInfo
    {
        public CollectionEntry Id;
        public string Name;
        public string Desc;
        public Color Swatch;
    }

    /// <summary>
    /// [0.4.5] 图鉴条目目录：12 条（道具 4 + 守卫 4 + 碎片 1 + 事件 3）。
    /// 色值照抄 VerticalSliceBuilder 材质色 / IntroRules（IntroRules 同源注释），与游戏内一致。
    /// [0.8.1] 回退：删 6 新道具 + 清全部随机事件（事件暂缓，后期完善再加）→ 道具 4 + 守卫 4 + 碎片 1 + 遗物 3。
    /// </summary>
    public static class CollectionCatalog
    {
        // 道具（IntroRules L30-33 同源）
        static readonly Color OilC       = new Color(1.00f, 0.90f, 0.60f);   // MAT_LampHead 灯油暖黄
        static readonly Color SpeedC     = new Color(0.95f, 0.20f, 0.15f);   // MAT_SpeedDrink 加速红
        static readonly Color HourglassC = new Color(1.00f, 0.85f, 0.30f);   // MAT_Hourglass 沙漏金
        static readonly Color InvisC     = new Color(0.25f, 0.80f, 1.00f);   // MAT_Invisibility 隐身青
        // 守卫（IntroRules L26-29 同源）
        static readonly Color ScoutC     = new Color(0.02f, 0.02f, 0.04f);   // MAT_Patrol Scout 深黑
        static readonly Color RunnerC    = new Color(0.45f, 0.10f, 0.08f);   // MAT_Runner Runner 暗红
        static readonly Color TrackerC   = new Color(0.36f, 0.12f, 0.44f);   // MAT_Tracker Tracker 暗紫
        static readonly Color GuardianC  = new Color(0.72f, 0.55f, 0.18f);   // MAT_Guardian Guardian 金铜
        // 碎片
        static readonly Color FragC      = new Color(1.00f, 0.90f, 0.35f);   // MAT_Fragment 时间碎片金

        public static readonly EntryInfo[] Items = new EntryInfo[]
        {
            new EntryInfo { Id = CollectionEntry.TorchItem,          Name = "灯油",       Desc = "拾取后视野光圈 +2m",               Swatch = OilC },
            new EntryInfo { Id = CollectionEntry.SpeedDrink,         Name = "加速饮料",   Desc = "8 秒速度 ×1.4",                    Swatch = SpeedC },
            new EntryInfo { Id = CollectionEntry.TimeHourglass,      Name = "时间沙漏",   Desc = "倒计时 +20 秒",                    Swatch = HourglassC },
            new EntryInfo { Id = CollectionEntry.InvisibilityPotion, Name = "隐身药水",   Desc = "6 秒完全隐身（守卫看不见听不见）", Swatch = InvisC },
        };
        public static readonly EntryInfo[] Guards = new EntryInfo[]
        {
            new EntryInfo { Id = CollectionEntry.Scout,    Name = "Scout",    Desc = "侦察·普通巡逻",     Swatch = ScoutC },
            new EntryInfo { Id = CollectionEntry.Runner,   Name = "Runner",   Desc = "追捕·跑得快",       Swatch = RunnerC },
            new EntryInfo { Id = CollectionEntry.Tracker,  Name = "Tracker",  Desc = "追踪·视野大追得久", Swatch = TrackerC },
            new EntryInfo { Id = CollectionEntry.Guardian, Name = "Guardian", Desc = "守卫者·引走回岗",   Swatch = GuardianC },
        };
        public static readonly EntryInfo[] Fragments = new EntryInfo[]
        {
            new EntryInfo { Id = CollectionEntry.TimeFragment, Name = "时间碎片", Desc = "3 枚开晨门，每枚光圈 +3m", Swatch = FragC },
        };
        // [0.8.1] 事件图鉴条目已全部移除（随机事件暂缓，后期完善再加）。
        // [0.8.0] 午夜遗物（3 条，规格书例）
        static readonly Color RelicC = new Color(0.95f, 0.55f, 0.95f);   // 午夜遗物 遗物粉紫
        public static readonly EntryInfo[] Relics = new EntryInfo[]
        {
            new EntryInfo { Id = CollectionEntry.RelicDiploma,      Name = "不存在的毕业证", Desc = "价值 8000 · +50 Rank",  Swatch = RelicC },
            new EntryInfo { Id = CollectionEntry.RelicStudentCard,  Name = "零点以前的学生证", Desc = "价值 5000 · +30 Rank", Swatch = RelicC },
            new EntryInfo { Id = CollectionEntry.RelicDormKey,      Name = "消失的寝室钥匙", Desc = "价值 12000 · +80 Rank", Swatch = RelicC },
        };
    }
}
