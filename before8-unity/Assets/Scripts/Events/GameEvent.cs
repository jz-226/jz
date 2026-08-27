namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件 ID（规格书 15 事件，数据驱动）。
    /// 枚举顺序仅用于目录索引；图鉴位在 CollectionSystem 另行分配（不依赖此顺序）。
    /// </summary>
    public enum GameEvent
    {
        // ---- 已有（[0.4.4]）----
        TimeRift,           // 时间裂缝：碰触 +15s（摆点）
        LureChest,          // 诱饵宝箱：开箱报警引开守卫（摆点）
        Blackout,           // 黑屏广播：全校熄灯守卫失明（时间触发）
        // ---- [0.8.0] 新增 12 ----
        // 摆点型（地图实体，碰触/区域触发）
        MidnightMerchant,   // 午夜商人：花本局金币买随机道具
        MysteryVending,     // 神秘售货机：投币 20 随机出一个道具
        LateNightCanteen,   // 深夜食堂：宵夜补给 +15s
        OldStudentCard,     // 旧学生卡：守卫无视 6s
        LuckyRoom,          // 幸运房间：随机大奖（金币/道具/时间）
        TempSafeHouse,      // 临时安全屋：区域内守卫抓不到
        PortalEvent,        // 传送门：一对互传
        LostAndFound,       // 遗失物招领处：寻回失物 +40 金币
        // 时间触发型（开局后随机时刻全图触发）
        InfiniteClassroom,  // 无限教室：倒计时 +20s
        TimeStopZone,       // 时间停滞区：倒计时冻结 5s
        ReverseStaircase,   // 逆流楼梯：守卫感知增强 5s
        TimeStorm,          // 时间风暴：倒计时 -15s
    }
}
