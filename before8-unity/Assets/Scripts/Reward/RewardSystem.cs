using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Before8AM.Run;
using Before8AM.Collection;
using Before8AM.Core;   // [0.5] SceneNames
using Before8AM.Loot;   // [0.8.0] RelicCatalog（午夜遗物结算）
using Before8AM.Mission;   // [0.8.0] 每日任务/7日挑战撤离挂钩
using Before8AM.UI;   // [0.8.9i] InGameSettings（游玩中设置面板，get-or-add 挂接）

namespace Before8AM.Reward
{
    /// <summary>
    /// 结算系统：
    /// 成功 → ESCAPED + 战利品 + 本局金币入账永久金币（[0.4.0] 真实入账，不再写死示例数值）。
    /// 失败 → LOST + 本局已清空（本局没收，永久金币不变）。按 R 重开（再次翻窗）。
    /// </summary>
    public class RewardSystem : MonoBehaviour
    {
        RunManager run;
        float displayTimer;
        bool showing;
        bool pendingRestart;   // [0.4.4] 按钮不在 OnGUI 里直接 LoadScene（渲染阶段被拒），置位后由 Update 下一帧执行
        bool showCatalog;      // [0.4.5] 图鉴视图开关（同一 OnGUI 状态切换，纯 bool）
        bool pendingMenu;      // [0.5] 返回主菜单（同 flag 模式：OnGUI 置位 → Update 执行 LoadScene）
        CollectionView collection;   // [0.4.5] 图鉴面板（builder 挂组件，Start get-or-add 兜底）
        const int BaseEscapeReward = 70;   // [0.5] 成功撤离基础金币（用户确认均衡档）；[0.9.2+] 100→70（用户反馈一局入账太快）

        void Start()
        {
            run = RunManager.Instance;
            if (run != null) run.OnRunEnded += OnRunEnded;

            // [0.4.5] 图鉴面板懒接线：builder 已挂则复用，否则自建（老场景兜底）；返回回调回结算
            collection = GetComponent<CollectionView>() ?? gameObject.AddComponent<CollectionView>();
            collection.OnBack = () => { showCatalog = false; collection.SetVisible(false); };

            // [0.8.9i] 游玩中设置面板（Esc 暂停/调灵敏度/退出本局）：get-or-add 不动场景文件，校园/停车场两图自动生效
            _ = GetComponent<InGameSettings>() ?? gameObject.AddComponent<InGameSettings>();
        }

        void OnDestroy()
        {
            if (run != null) run.OnRunEnded -= OnRunEnded;
        }

        void OnRunEnded(RunState state)
        {
            showing = true;
            displayTimer = 0f;

            // [0.4.0] 永久进度入账：成功 → 本局金币转永久；失败 → 金币已被 Fail 清空（本局没收），只记局数。
            bool escaped = state == RunState.Success;
            GameProgress.RecordRun(escaped);
            if (escaped && run != null)
            {
                // [0.5] 成功入账 = 基础撤离奖励 100 + 本局金币 + 未用道具折价（ItemValue，背包剩余按价值折金币）
                int itemValue = run.ItemValue();
                int total = BaseEscapeReward + run.TemporaryCoins + itemValue;
                GameProgress.AddPermanentCoins(total);
                // [0.8.0] 每日任务/7日挑战：报告成功撤离（逃脱计数 / 战利品价值 / 无发现撤离）
                MissionSystem.OnEscape();
                MissionSystem.OnValueEscaped(run.LootValue);
                MissionSystem.OnStealthEscape();
                Debug.Log($"[奖励] 逃出成功：基础+{BaseEscapeReward} + 金币+{run.TemporaryCoins} + 道具折价+{itemValue} = +{total}（累计 {GameProgress.PermanentCoins}）");
            }
            else
            {
                Debug.Log($"[奖励] 本局没收：永久金币不变（累计 {GameProgress.PermanentCoins}，第 {GameProgress.TotalRuns} 局）");
            }

            // [0.4.4] Meta 入账：本局 XP（碎片/开箱/成功/失败已由 RunManager 记账）→ 永久 XP；
            // 段位分成功 = RankDetail 公式；失败不加不减（规格书"失败不掉分"）。
            GameProgress.AddXP(run != null ? run.RunXP : 0);
            // [0.8.0] 段位分 = 规格书 74 公式：基础撤离分 + 战利品价值分 + 稀有物品分 + 午夜遗物额外分 + 极限撤离奖励
            // [0.8.1] 压到百级：基础 100、战利品 ÷100、遗物额外 30/50/80、极限 40/20（RankDetail 实现）
            //（稀有物品分并入战利品分：Epic+ 箱 lootValue 已含稀有度；午夜遗物额外分 = RelicCatalog.RankBonus）
            int rankGain = escaped && run != null ? RankDetail().Total : 0;
            GameProgress.AddRankScore(rankGain);
            // [0.8.0] 午夜榜：成功撤离记入本地积分排行榜（地图 0=校园/1=停车场 + 遗物徽标）
            if (escaped && run != null)
                RankBoard.Add(rankGain,
                    SceneManager.GetActiveScene().name == SceneNames.Parking ? 1 : 0,
                    run.RelicIndex >= 0);
            Debug.Log($"[奖励] Lv.{GameProgress.Level} · {GameProgress.RankName} · 本局 XP +{(run != null ? run.RunXP : 0)} · 段位分 +{rankGain}");
        }

        /// <summary>[0.8.0][0.8.1] 本局段位分明细（规格书 74）：(基础, 战利品, 遗物额外, 极限撤离, 总和)。
        /// [0.8.1] 分值整体压到百级（用户反馈几千分太吓人）：基础 1000→100；战利品价值分 = LootValue / 100
        /// （普通一局 ≈110 分，带遗物 200~300）；午夜王者门槛 130000→8000，成长节奏不变。
        /// 午夜遗物额外分 = RelicCatalog.RankBonus（30/50/80 本就小，保留）。
        /// [0.9.2+] 基础 100→70、极限奖励 40/20→30/15（用户反馈段位提升太快）：普通一局 ≈75 分，带遗物 150~200。</summary>
        (int Base, int Loot, int Relic, int Escape, int Total) RankDetail()
        {
            int baseScore = 70;   // 基础撤离分（[0.8.1] 1000→100；[0.9.2+] →70）
            int loot = run != null ? run.LootValue / 100 : 0;   // [0.8.1] 战利品价值 ÷100（不再原样堆分）
            int relic = run != null && run.RelicIndex >= 0 && run.RelicIndex < RelicCatalog.All.Length
                ? RelicCatalog.All[run.RelicIndex].RankBonus : 0;
            int escape = run != null ? EscapeBonus() : 0;   // 极限撤离奖励
            return (baseScore, loot, relic, escape, baseScore + loot + relic + escape);
        }

        /// <summary>[0.8.0][0.8.1] 极限撤离奖励：剩余时间越少越极限（搜刮到最后一刻才走）。[0.8.1] 400/200→40/20；[0.9.2+] →30/15。</summary>
        int EscapeBonus()
        {
            float p = run != null && run.MaxTime > 0f ? run.TimeLeft / run.MaxTime : 0f;
            if (p <= 0.25f) return 30;
            if (p <= 0.4f) return 15;
            return 0;
        }

        void Update()
        {
            if (showing) displayTimer += Time.deltaTime;

            // [0.4.4] 结算面板按钮的延迟重开：OnGUI 只置位，这里真正执行
            // （在 OnGUI 渲染阶段直接调 LoadScene 会被 Unity 吞掉 → 用户点"重新开始"无反应）
            if (pendingRestart)
            {
                pendingRestart = false;
                RestartRun();
                return;
            }

            // [0.5] 返回主菜单（同 flag 模式：OnGUI 置位 → 这里执行 LoadScene）
            if (pendingMenu)
            {
                pendingMenu = false;
                SceneManager.LoadScene(SceneNames.MainMenu);
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && showing && !showCatalog)
                RestartRun();   // [0.4.4] 去掉 1.5s 门槛：失败后立即可按 R 重开；[0.4.5] 图鉴打开时 R 不重开
        }

        void OnGUI()
        {
            if (!showing || run == null) return;
            if (showCatalog) return;   // [0.4.5] 图鉴面板：CollectionView 自己渲染（visible 门控），本帧不画结算（必须在遮罩前）

            // 暗色遮罩（[0.4.4] 0.6→0.35：不再像黑屏，结算文字清晰可读）
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            var big = new GUIStyle(GUI.skin.label) { fontSize = 64, alignment = TextAnchor.MiddleCenter };
            var mid = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };

            float cx = Screen.width / 2f;

            if (run.State == RunState.Success)
            {
                // [0.8.0] 入账明细 + 段位分明细（规格书 74 公式）——y 变量累加，遗物行条件显示防叠字
                int itemValue = run.ItemValue();
                int gained = BaseEscapeReward + run.TemporaryCoins + itemValue;
                var rank = RankDetail();
                GUI.Label(new Rect(cx - 400, 120, 800, 80), "ESCAPED", big);
                GUI.Label(new Rect(cx - 400, 225, 800, 50), "你带着战利品回到了宿舍", small);

                int y = 290;
                GUI.Label(new Rect(cx - 400, y, 800, 50), $"时间碎片 × {run.TimeFragments}  金币 × {run.TemporaryCoins}", mid);
                y += 55;
                GUI.Label(new Rect(cx - 400, y, 800, 40), $"入账 = 撤离奖励 +{BaseEscapeReward} + 金币 +{run.TemporaryCoins} + 道具折价 +{itemValue}", small);
                y += 42;
                GUI.Label(new Rect(cx - 400, y, 800, 40), $"永久金币 +{gained}（已入账）· 累计 {GameProgress.PermanentCoins}", small);
                y += 42;
                GUI.Label(new Rect(cx - 400, y, 800, 40), $"第 {GameProgress.TotalRuns} 局 · 已逃出 {GameProgress.EscapeCount} 次 · 本局 XP +{run.RunXP}", small);
                y += 42;
                GUI.Label(new Rect(cx - 400, y, 800, 40), $"段位分 +{rank.Total}（基础 {rank.Base} + 战利品 {rank.Loot} + 遗物 {rank.Relic} + 极限 {rank.Escape}）", small);
                y += 42;
                if (run.RelicIndex >= 0 && run.RelicIndex < RelicCatalog.All.Length)
                {
                    RelicInfo rel = RelicCatalog.All[run.RelicIndex];
                    GUI.Label(new Rect(cx - 400, y, 800, 40), $"【午夜遗物】{rel.Name} · 战利品价值 {rel.Value} · 段位分 +{rel.RankBonus}", small);
                    y += 42;
                }
                GUI.Label(new Rect(cx - 400, y, 800, 40), $"Lv.{GameProgress.Level} · {GameProgress.RankName}", small);
            }
            else
            {
                string msg = run.State == RunState.Caught ? "你被午夜校园留下了。" : "黎明已至，晨门关闭。";
                GUI.Label(new Rect(cx - 400, 200, 800, 80), "LOST", big);
                GUI.Label(new Rect(cx - 400, 300, 800, 50), msg, mid);
                GUI.Label(new Rect(cx - 400, 380, 800, 40), "本局战利品已全部清空 · 永久金币 +0（本局没收）", small);
                GUI.Label(new Rect(cx - 400, 430, 800, 40), $"累计永久金币 {GameProgress.PermanentCoins}", small);
                GUI.Label(new Rect(cx - 400, 480, 800, 40), $"Lv.{GameProgress.Level} · {GameProgress.RankName} · 本局 XP +{run.RunXP}", small);
                // [0.4.4] 失败：立即显示重开提示（成功分支按钮就在下方，无需额外提示）
                GUI.Label(new Rect(cx - 400, 540, 800, 50), "按 R 重新开始", mid);
            }
            var btn = new GUIStyle(GUI.skin.button);
            btn.fontSize = 26;
            btn.alignment = TextAnchor.MiddleCenter;
            // [0.4.5] 双按钮 + [0.5] 返回主菜单：三按钮都只置位/切视图，不走 LoadScene
            // 三按钮宽 220 间距 20，总宽 700，居中 (cx-350..cx+350)，720 屏不溢出
            if (GUI.Button(new Rect(cx - 350, 620, 220, 62), "重新开始", btn))
                pendingRestart = true;   // [0.4.4] 只置位，不在此 LoadScene
            if (GUI.Button(new Rect(cx - 110, 620, 220, 62), "图鉴", btn))
            {
                showCatalog = true;
                collection?.SetVisible(true);
            }
            if (GUI.Button(new Rect(cx + 130, 620, 220, 62), "返回主菜单", btn))
                pendingMenu = true;   // [0.5] 只置位，Update 下一帧 LoadScene(主菜单)
        }

        void RestartRun()
        {
            // [0.5] 固定重载游戏场景（不再用 buildIndex——主菜单加入后游戏场景降到 buildIndex 1）
            // [0.8.0] 第二张地图（停车场）：重开按当前场景返回，校园/停车场各自重跑自己的关卡
            string cur = SceneManager.GetActiveScene().name;
            Debug.Log($"[结算] 重开：LoadScene {cur}");
            SceneManager.LoadScene(cur == SceneNames.Parking ? SceneNames.Parking : SceneNames.Game);
        }
    }
}
