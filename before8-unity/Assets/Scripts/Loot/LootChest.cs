using UnityEngine;
using Before8AM.Run;
using Before8AM.World;
using Before8AM.Player;
using Before8AM.Collection;   // [0.8.0] MidnightRelic 图鉴解锁
using Before8AM.Mission;   // [0.8.0] 每日任务"搜刮 10 次"进度
using Before8AM.Reward;   // [0.8.6] GameProgress.CoinRankMultiplier（段位金币系数）
using Before8AM.Audio;    // [0.8.9] 开箱音

namespace Before8AM.Loot
{
    /// <summary>
    /// 宝箱：搜索 1 秒开出奖励（规格书 46/47/48）。
    /// [0.4.0] 品质驱动产出：Common 保底 / Rare 高价值 / Epic 高价值 + 50% 概率直接获得随机 buff（加速/隐身/加时）。
    /// 价值受 RunManager.LootMultiplier 影响（随时间提高）。Legendary/MidnightRelic 为后续午夜遗物系统保留，暂用保底。
    /// Vertical Slice：开箱 = 金币 + 概率出碎片；Juice 简化为变色上浮。
    /// </summary>
    public class LootChest : Interactable
    {
        public enum ChestQuality { Common, Rare, Epic, Legendary, MidnightRelic }

        [Header("宝箱")]
        public ChestQuality Quality = ChestQuality.Common;
        [Tooltip("Legacy：Common 基础金币（Rare/Epic 由品质系数覆盖）")]
        public int BaseCoins = 100;
        [Tooltip("Legacy：Common 碎片概率（Rare/Epic 由品质系数覆盖）")]
        public float FragmentChance = 0.3f;

        bool opened;
        Transform _t;
        Vector3 baseScale;

        public override string PromptText => opened ? "（已开启）" : "搜索";

        public override bool RequiresHold => !opened;
        public override float HoldDuration => 1f;

        // [0.8.0] 品质配置（数据驱动，规格书"战利品价值"：普通100/稀有500/史诗1200/传奇3000/午夜遗物10000）。
        // coins = 本局资源（金币进背包）；frag = 碎片概率；lootValue = 结算 RankScore 的战利品价值；
        // epicBuff = Epic 及以上附赠随机 buff。MidnightRelic 不直接给 10000 金币（防通胀）——
        // 价值全压在午夜遗物物品上（RelicCatalog.Value，规格书 10000 级）。
        static (int coins, float frag, int lootValue, bool epicBuff) ConfigFor(ChestQuality q) => q switch
        {
            // [0.9.2+] 金币产出整体 ×0.7（用户反馈一局入账 916 太快）：70/130/210/420/210；碎片概率与战利品价值分不动
            ChestQuality.Rare => (130, 0.45f, 500, false),
            ChestQuality.Epic => (210, 0.6f, 1200, true),
            ChestQuality.Legendary => (420, 0.85f, 3000, true),
            ChestQuality.MidnightRelic => (210, 1f, 0, true),   // 碎片必出；价值由遗物补足
            _ => (70, 0.3f, 100, false),   // Common
        };

        void Awake()
        {
            _t = transform;
            baseScale = _t.localScale;
        }

        public override void OnHoldProgress(float progress01)
        {
            if (opened) return;
            // 搜索中轻微抖动（Juice 简化版）
            _t.localRotation = Quaternion.Euler(0f, progress01 * 40f, Mathf.Sin(progress01 * 30f) * 2f);
        }

        public override void Interact()
        {
            if (opened) return;
            opened = true;
            _t.localRotation = Quaternion.identity;

            RunManager run = RunManager.Instance;
            if (run == null) return;

            var cfg = ConfigFor(Quality);
            // [0.8.6] 段位金币系数：段位越高开箱金币略多（×1.0~1.18），但收益增幅远低于难度增幅（增援守卫）
            int coins = Mathf.RoundToInt(cfg.coins * run.LootMultiplier * GameProgress.CoinRankMultiplier);
            bool hasFragment = Random.value < cfg.frag;
            run.AddCoins(coins);
            run.AddXP(20);   // [0.4.4] 开宝箱 +20 XP
            SFXManager.Instance.Play("handleCoins", 0.9f);   // [0.8.9] 开箱金币声
            if (hasFragment) run.AddFragment();
            run.AddLootValue(cfg.lootValue);   // [0.8.0] 战利品价值入账
            MissionSystem.OnLootCollected();   // [0.8.0] 每日任务"搜刮 10 次"进度

            // [0.4.0] Epic 附赠：50% 概率直接获得随机 buff（加速 / 隐身 / 加时）
            string bonus = cfg.epicBuff && Random.value < 0.5f ? GrantRandomBuff() : null;

            // [0.8.0] MidnightRelic：开出午夜遗物（记 RelicIndex 供结算 + 图鉴解锁 + 战利品价值）
            string relicName = null;
            if (Quality == ChestQuality.MidnightRelic)
            {
                int ridx = Random.Range(0, RelicCatalog.All.Length);
                RelicInfo rel = RelicCatalog.All[ridx];
                run.RelicIndex = ridx;
                run.AddLootValue(rel.Value);
                CollectionSystem.Unlock(rel.Entry);
                relicName = rel.Name;
            }

            // [0.8.0] Juice：飞散 + 回弹 + 上浮文字（ChestJuice 组件）
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = hasFragment ? new Color(1f, 0.85f, 0.3f) : Color.yellow;
            var juice = GetComponent<ChestJuice>() ?? gameObject.AddComponent<ChestJuice>();
            string floatText = relicName != null
                ? $"【{relicName}】"
                : $"+{coins} 金币{(hasFragment ? " · 时间碎片!" : "")}";
            juice.Burst(floatText, r != null ? r.material : null);

            Debug.Log($"[宝箱]({Quality}) 开出 {coins} 金币{(hasFragment ? " + 时间碎片!" : "")}{(bonus != null ? " + " + bonus : "")}{(relicName != null ? " + 遗物[" + relicName + "]" : "")}");
        }

        /// <summary>[0.4.0] Epic 附赠 buff 三选一；返回描述字符串（无玩家/不在运行返回 null）。</summary>
        string GrantRandomBuff()
        {
            RunManager run = RunManager.Instance;
            if (run == null) return null;

            if (Random.value < 0.33f)
            {
                run.AddTime(20f);
                return "加时 +20s";
            }

            PlayerController pc = Object.FindObjectOfType<PlayerController>();
            if (pc == null) return null;

            if (Random.value < 0.5f)
            {
                pc.AddSpeedBoost(8f, 1.4f);
                return "加速饮料 ×1.4 (8s)";
            }
            pc.AddInvisibility(6f);
            return "隐身 (6s)";
        }
    }
}
