using System;
using System.Collections.Generic;
using UnityEngine;
using Before8AM.Core;
using Before8AM.Player;
using Before8AM.Visual;   // [0.5] ApplyItemEffect 用 ExplorationFog.AddRadius
using Before8AM.Mission;  // [0.8.0] 每日任务/7日挑战挂钩（碎片/道具/无发现标志）
using Before8AM.Audio;    // [0.8.9] 拾取/道具/失败/成功/时段/晨门音效

namespace Before8AM.Run
{
    /// <summary>
    /// 单局管理器：480 秒倒计时 / 时间阶段 / 本局战利品 / 失败清空 / 成功结算。
    /// 核心规则（规格书 26/27/28）：被抓或超时 = 本局全部没收，禁止掉一部分。
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }   // 场景唯一，由 Bootstrap/AutoCreate 保证

        public RunState State { get; private set; } = RunState.Ready;
        public float ElapsedTime { get; private set; }
        public float TimeLeft => Mathf.Max(0f, MaxTime - ElapsedTime);

        [Header("Run 配置")]
        [Tooltip("一局最长秒数（规格书 22；[0.4.4] 10 分钟改为 8 分钟：480 秒）")]
        public float MaxTime = 480f;
        [Tooltip("集齐该数量时间碎片后晨门激活（规格书 23：3 个）")]
        public int TimeFragmentsRequired = 3;

        [Header("本局数据（被抓/超时清空）")]
        public int TimeFragments;
        public int TemporaryCoins;
        /// <summary>[0.5] 背包：4 种道具持有数（索引 = RunItem 枚举值）。拾取进背包、手动使用，
        /// 撤离时未用道具按 ItemCatalog 价值折金币入账；失败被 Fail 清空（本局没收）。</summary>
        readonly int[] inventory = new int[ItemCatalog.Count];
        /// <summary>[0.4.4] 本局累计 XP（碎片 +30 / 开宝箱 +20 / 成功 +200 / 失败 +30）。
        /// 局内累计，结算时由 RewardSystem 统一入账 GameProgress（不被 Fail 清空，需带到结算界面）。</summary>
        public int RunXP;

        public event Action OnRunStarted;
        public event Action OnFragmentAdded;
        public event Action<int> OnCoinsChanged;
        public event Action<RunState> OnRunEnded;
        public event Action OnInventoryChanged;   // [0.5] 背包变化（拾取/使用）→ HUD 刷新

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            GameServices.Register(this);
        }

        void Update()
        {
            if (State != RunState.Running) return;
            if (TimeFrozen)
            {
                // [0.8.0] 时间停滞区：倒计时冻结（守卫继续动，时间不流）
                if (TimeFrozenTimer > 0f)
                {
                    TimeFrozenTimer -= Time.deltaTime;
                    if (TimeFrozenTimer <= 0f) TimeFrozen = false;
                }
                return;
            }
            ElapsedTime += Time.deltaTime;
            if (ElapsedTime >= MaxTime)
                Fail(RunState.Timeout);

            // [0.8.9] 危险时段切换提示（阶段只会前进：开局 0→0 不响，1→2→3 各响一次）
            int st = CurrentStage;
            if (st > lastStage)
            {
                lastStage = st;
                SFXManager.Instance.Play("lowThreeTone", 0.9f);
            }
        }

        /// <summary>[0.8.0] 时间停滞区：冻结倒计时（TimeFrozen 期间 ElapsedTime 不增长）。</summary>
        public bool TimeFrozen;
        public float TimeFrozenTimer;

        /// <summary>[0.8.0] 启动时间冻结（事件触发用；重叠调用取最长剩余）。</summary>
        public void FreezeTime(float seconds)
        {
            TimeFrozen = true;
            TimeFrozenTimer = Mathf.Max(TimeFrozenTimer, seconds);
        }

        public void StartRun()
        {
            State = RunState.Running;
            ElapsedTime = 0f;
            RunXP = 0;   // [0.4.4] 每局 XP 从零开始
            LootValue = 0;   // [0.8.0] 战利品价值清零
            RelicIndex = -1;   // [0.8.0] 午夜遗物清零（-1 = 本局未开出）
            TimeFragments = 0;   // [审查] 防御性清零：同实例复用不残留上一局碎片/金币（重开走 LoadScene 天然归零，但 StartRun 注释承诺"同实例不残留"）
            TemporaryCoins = 0;
            lastStage = 0;   // [0.8.9] 时段缓存同步清零（同实例复用不残留上局阶段）
            Array.Clear(inventory, 0, inventory.Length);   // [0.6] 保险清零：重开/同实例复用不残留上一局背包
            LoadPurchasedItems();   // [0.6] 商店：主菜单买的下局道具开局注入背包
            MissionSystem.ResetRunFlag();   // [0.8.0] 本局"无被发现"标志清零（守卫 Alert/Chase 置位）
            OnRunStarted?.Invoke();
        }

        /// <summary>[0.8.0] 本局战利品价值（开箱/遗物/碎片累积；结算 RankScore 的战利品分）。</summary>
        public int LootValue;
        /// <summary>[0.8.0] 本局开出的午夜遗物索引（RelicCatalog.All；-1 = 无）。</summary>
        public int RelicIndex = -1;

        public void AddLootValue(int v) => LootValue += v;

        /// <summary>[0.6] 商店买的道具开局注入背包（一次性，注入后清购买记录，避免下局重复给）。</summary>
        void LoadPurchasedItems()
        {
            bool any = false;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                var item = (RunItem)i;
                int n = PurchasedItems.Count(item);
                if (n <= 0) continue;
                inventory[i] += n;
                any = true;
            }
            PurchasedItems.Clear();
            if (any) OnInventoryChanged?.Invoke();   // 有注入才刷 HUD
        }

        public void AddFragment()
        {
            bool firstFull = !AllFragmentsCollected;   // [0.8.9] 本次集齐前检查：首次集满播晨门解锁音
            TimeFragments++;
            AddXP(30);   // [0.4.4] 时间碎片 +30 XP
            MissionSystem.OnFragment();   // [0.8.0] 每日任务"时间碎片"进度
            OnFragmentAdded?.Invoke();
            SFXManager.Instance.Play("tick_001", 0.8f);   // [0.8.9] 碎片拾取
            if (firstFull && AllFragmentsCollected)
                SFXManager.Instance.Play("confirmation_003", 1f);   // [0.8.9] 晨门解锁（集齐瞬间）
        }

        // [0.8.9] 危险时段提示音：CurrentStage 是计算属性无变化事件，这里缓存上次值比对（Update 每帧）。
        int lastStage;

        /// <summary>[0.4.4] 本局 XP 记账：碎片/开宝箱/成功/失败累积，结算统一入账永久 XP。</summary>
        public void AddXP(int amount)
        {
            if (amount > 0) RunXP += amount;
        }

        public void AddCoins(int amount)
        {
            TemporaryCoins += amount;
            OnCoinsChanged?.Invoke(TemporaryCoins);
        }

        /// <summary>[0.8.0] 扣本局金币（午夜商人/售货机）。不足返回 false，不扣。</summary>
        public bool SpendCoins(int amount)
        {
            if (TemporaryCoins < amount) return false;
            TemporaryCoins -= amount;
            OnCoinsChanged?.Invoke(TemporaryCoins);
            return true;
        }

        // ---------- [0.5] 背包：拾取进背包、手动使用、撤离未用折金币 ----------

        public int GetItemCount(RunItem item)
        {
            int i = (int)item;
            if (i < 0 || i >= inventory.Length) return 0;
            return inventory[i];
        }

        /// <summary>背包未用道具的总金币价值（撤离成功时入账；失败 Fail 已清空 → 0，天然正确）。</summary>
        public int ItemValue()
        {
            int sum = 0;
            for (int i = 0; i < inventory.Length; i++)
                sum += inventory[i] * ItemCatalog.CoinValue((RunItem)i);
            return sum;
        }

        /// <summary>拾取道具进背包（不再立即生效；手动使用走 TryUseItem）。</summary>
        public void AddItem(RunItem item)
        {
            int i = (int)item;
            if (i < 0 || i >= inventory.Length) return;
            inventory[i]++;
            OnInventoryChanged?.Invoke();
            SFXManager.Instance.Play("confirmation_001", 0.7f);   // [0.8.9] 道具拾取入包
        }

        /// <summary>手动使用一个背包道具（仅 Running 态、有货才生效）。返回是否用出。</summary>
        public bool TryUseItem(RunItem item)
        {
            int i = (int)item;
            if (i < 0 || i >= inventory.Length) return false;
            if (State != RunState.Running) return false;
            if (inventory[i] <= 0) return false;
            inventory[i]--;
            ApplyItemEffect(item);
            MissionSystem.OnItemUsed();   // [0.8.0] 7 日挑战 Day6"使用 5 次道具"
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>道具效果（数值与旧"拾取即生效"一致，用户已确认均衡档）：灯油扩光圈 +2m / 加速 ×1.4 8s / 沙漏 +20s / 隐身 6s。
        /// [0.8.1] 回退：删 6 新道具效果分支（诱饵/探测/干扰/传送/夜视/假卡）。</summary>
        void ApplyItemEffect(RunItem item)
        {
            switch (item)
            {
                case RunItem.Torch:
                {
                    ExplorationFog fog = UnityEngine.Object.FindObjectOfType<ExplorationFog>();
                    if (fog != null) fog.AddRadius(2f);
                    SFXManager.Instance.Play("zap1", 0.9f);   // [0.8.9] 灯油注入
                    break;
                }
                case RunItem.SpeedDrink:
                {
                    var pc = UnityEngine.Object.FindObjectOfType<PlayerController>();
                    if (pc != null) pc.AddSpeedBoost(8f, 1.4f);
                    SFXManager.Instance.Play("powerUp1", 0.9f);   // [0.8.9] 加速生效
                    break;
                }
                case RunItem.TimeHourglass:
                    AddTime(20f);
                    SFXManager.Instance.Play("powerUp5", 0.9f);   // [0.8.9] 沙漏加时
                    break;
                case RunItem.InvisibilityPotion:
                {
                    var pc = UnityEngine.Object.FindObjectOfType<PlayerController>();
                    if (pc != null) pc.AddInvisibility(6f);
                    SFXManager.Instance.Play("phaserUp1", 0.9f);   // [0.8.9] 隐身披风
                    break;
                }
            }
        }

        /// <summary>[0.3.0] 时间沙漏：倒计时直接回退（加时）。只在游玩中生效，不会减到负数以下。</summary>
        public void AddTime(float seconds)
        {
            if (State != RunState.Running) return;
            ElapsedTime = Mathf.Max(0f, ElapsedTime - seconds);
        }

        /// <summary>当前时间阶段 0~3（规格书 25）。[0.4.4] 按 MaxTime 百分比分四段（480s 下 0/120/240/360 起），
        /// 未来再改时长无需重算阈值。</summary>
        public int CurrentStage
        {
            get
            {
                float p = MaxTime > 0f ? ElapsedTime / MaxTime : 0f;
                if (p < 0.25f) return 0;
                if (p < 0.50f) return 1;
                if (p < 0.75f) return 2;
                return 3;
            }
        }

        /// <summary>随时间上升的 Loot Multiplier（规格书 56，数值待试玩调整）。[0.4.4] 四档按 MaxTime 百分比，
        /// 480s 下 1/1.2/1.5/2（原 8-10 分钟 ×2.5/×3 档随 8 分钟移除）。
        /// [0.9.2+] 压到 1/1.1/1.25/1.5（用户反馈一局入账太快，后期档收敛）。</summary>
        public float LootMultiplier
        {
            get
            {
                float p = MaxTime > 0f ? ElapsedTime / MaxTime : 0f;
                if (p < 0.25f) return 1f;
                if (p < 0.50f) return 1.1f;
                if (p < 0.75f) return 1.25f;
                return 1.5f;
            }
        }

        public bool AllFragmentsCollected => TimeFragments >= TimeFragmentsRequired;

        /// <summary>被巡夜者抓捕 / 超时：本局全部没收（规格书 27/28）。永久进度不受影响。</summary>
        public void Fail(RunState reason)
        {
            if (State != RunState.Running) return;
            State = reason;
            TimeFragments = 0;
            TemporaryCoins = 0;
            Array.Clear(inventory, 0, inventory.Length);   // [0.5] 背包全没收（未用道具也不折价，规格书 27/28）
            AddXP(30);   // [0.4.4] 失败安慰 XP（RunXP 不被清空，带到结算显示）
            SFXManager.Instance.Play("error_005", 1f);   // [0.8.9] 被抓/超时警报
            OnRunEnded?.Invoke(reason);
            ClearPlayerBuffs();   // [0.4.1] 本局结束清空 buff（防幽灵/加速带入下一局；场景重载兜底）
        }

        /// <summary>成功进入晨门：保留本局战利品进入结算（规格书 71/72）。</summary>
        public void Escape()
        {
            if (State != RunState.Running) return;
            State = RunState.Success;
            AddXP(200);   // [0.4.4] 成功奖励 XP
            SFXManager.Instance.Play("confirmation_002", 1f);   // [0.8.9] 成功撤离
            OnRunEnded?.Invoke(RunState.Success);
            ClearPlayerBuffs();   // [0.4.1] 同上
        }

        /// <summary>[0.4.1] 本局结束还原玩家 buff 状态（清空计时 + 身体不透明）。</summary>
        void ClearPlayerBuffs()
        {
            var pc = UnityEngine.Object.FindObjectOfType<PlayerController>();   // 全限定：using System 下 Object 有歧义
            if (pc != null) pc.ClearBuffs();
        }
    }
}
