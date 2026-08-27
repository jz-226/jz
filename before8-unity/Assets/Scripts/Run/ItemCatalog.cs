using UnityEngine;

namespace Before8AM.Run
{
    /// <summary>
    /// [0.5] 可持有道具：捡到进背包（RunManager.Inventory 计数），手动使用（PC 数字键 1-4 / 手游右按钮）。
    /// 撤离时背包未用道具按 ItemCatalog.CoinValue 折金币入账；失败全没收。
    /// 使用效果数值在 RunManager.ApplyItemEffect（与旧"拾取即生效"一致）。
    /// [0.8.1] 回退：删 6 新道具（声学诱饵/探测器/干扰器/传送器/夜视仪/假学生卡）→ 恢复 4 旧道具。
    /// 枚举顺序 == 背包索引，只追加不插队（防旧存档错位）；本次回退删的是尾部 6 值，旧 4 值不变。
    /// </summary>
    public enum RunItem
    {
        Torch = 0,              // 灯油：使用时扩探索光圈 +2m
        SpeedDrink = 1,         // 加速饮料：使用时移速 ×1.4 持续 8s
        TimeHourglass = 2,      // 时间沙漏：使用时倒计时回退 +20s
        InvisibilityPotion = 3, // 隐身药水：使用时 6s 守卫看不见
    }

    /// <summary>[0.5] 道具静态表：显示名 + 金币价值（用户确认均衡档：30/40/80/100）。
    /// [0.8.1] 回退：删 6 新道具折价，恢复 4 旧道具（30/40/80/100）。
    /// [0.9.2+] 撤离折价 ×0.65（20/30/50/65），配合开箱产出/撤离奖励一起压通胀。</summary>
    public static class ItemCatalog
    {
        public const int Count = 4;

        /// <summary>全名（HUD 背包栏用）。</summary>
        public static string DisplayName(RunItem item) => item switch
        {
            RunItem.Torch => "灯油",
            RunItem.SpeedDrink => "加速饮料",
            RunItem.TimeHourglass => "时间沙漏",
            RunItem.InvisibilityPotion => "隐身药水",
            _ => "?",
        };

        /// <summary>短名（手游按钮/商店行用小空间用）。</summary>
        public static string ShortName(RunItem item) => item switch
        {
            RunItem.Torch => "灯油",
            RunItem.SpeedDrink => "加速",
            RunItem.TimeHourglass => "沙漏",
            RunItem.InvisibilityPotion => "隐身",
            _ => "?",
        };

        /// <summary>撤离折价（未用道具 × 价值入账永久金币）。</summary>
        public static int CoinValue(RunItem item) => item switch
        {
            RunItem.Torch => 20,
            RunItem.SpeedDrink => 30,
            RunItem.TimeHourglass => 50,
            RunItem.InvisibilityPotion => 65,
            _ => 0,
        };

        /// <summary>[0.6] 商店购买价 = 折价 × 2（60/80/160/200）。比撤离折价高，杜绝"低价买→撤离高价折"套利；
        /// 买了下局开局自带，能多拆几口更好的箱子，多赚的远超差价，玩家才愿意买。</summary>
        public static int ShopPrice(RunItem item) => CoinValue(item) * 2;
    }

    /// <summary>[0.6] 商店购买记录（一次性待注入）：主菜单 ShopController 买 → PlayerPrefs 记账 →
    /// 开局 RunManager.LoadPurchasedItems 注入背包后清除。独立键每道具一个，读写两处共用本类（防键名漂移）。</summary>
    public static class PurchasedItems
    {
        static string Key(RunItem item) => "Before8AM.Purchase." + item;

        public static int Count(RunItem item) => PlayerPrefs.GetInt(Key(item), 0);
        public static void Set(RunItem item, int n) => PlayerPrefs.SetInt(Key(item), Mathf.Max(0, n));
        public static void Clear()
        {
            for (int i = 0; i < ItemCatalog.Count; i++)
                PlayerPrefs.DeleteKey(Key((RunItem)i));
        }
    }
}
