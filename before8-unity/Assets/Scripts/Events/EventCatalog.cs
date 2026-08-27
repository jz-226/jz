using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>事件类型：场景摆点（玩家发现/碰触）vs 时间触发（开局后随机时刻全图）。</summary>
    public enum EventType { World, Timed }

    /// <summary>单条随机事件数据（数据驱动目录：ID → 展示名 / 图鉴位 / 类型 / 触发权重）。</summary>
    public struct EventInfo
    {
        public GameEvent Id;
        public string DisplayName;
        public CollectionEntry Entry;
        public EventType Type;
        /// <summary>每局相对出现权重（World=地图出现的倾向，Timed=被选中的倾向）。</summary>
        public int Weight;
    }

    /// <summary>
    /// [0.8.0] 随机事件目录（数据驱动，规格书 15 事件 ≥15）。
    /// [0.8.1] 回退：All 清空 → 随机事件全部暂缓（场景生成器不再创建事件实体、RandomEventSystem 不再排程）。
    /// 事件脚本/GameEvent 枚举/EventType 保留为死代码，后期完善后恢复本目录 + 生成器创建即可。
    /// </summary>
    public static class EventCatalog
    {
        public static readonly EventInfo[] All = System.Array.Empty<EventInfo>();

        public static string DisplayName(GameEvent e)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Id == e) return All[i].DisplayName;
            return e.ToString();
        }

        public static CollectionEntry Entry(GameEvent e)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Id == e) return All[i].Entry;
            return CollectionEntry.Blackout;   // 兜底（正常不会到）
        }
    }
}
