using UnityEngine;
using Before8AM.Patrol;
using Before8AM.Run;
using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件·时间触发调度器（规格书 15 事件中 5 个 Timed 型：黑屏广播/无限教室/时间停滞区/逆流楼梯/时间风暴）。
    /// [0.8.1] 回退：随机事件全部暂缓 → OnRunStarted 不再排程（schedule 恒空），本组件为休眠死代码。
    /// 保留守卫感知基准捕获/恢复逻辑：无活跃事件时 RecomputeGuardSense 乘数恒 1，不扰动守卫。
    /// </summary>
    public class RandomEventSystem : MonoBehaviour
    {
        [Header("黑屏广播")]
        [Tooltip("开局后随机触发窗口（秒）：默认 90~270s（480s 局内 2~5 分钟）")]
        public float MinDelay = 90f;
        public float MaxDelay = 270f;
        [Tooltip("熄灯持续秒数")]
        public float BlackoutDuration = 5f;
        [Tooltip("守卫感知范围缩放（0.25 = 视野/听力/驻守圈缩到 1/4）")]
        public float GuardPerceptionScale = 0.25f;

        [Header("逆流楼梯")]
        [Tooltip("守卫感知增强倍数")]
        public float ReverseScale = 1.4f;
        public float ReverseDuration = 5f;

        [Header("引用（VerticalSliceBuilder 接线）")]
        public PatrolController[] Guards;

        RunManager run;

        // 统一守卫感知乘数（基础 × 各活跃事件乘数，Update 重算应用）。
        // [审查] 基准按守卫逐只存数组（守卫感知类型不同：Scout 11/7 · Runner 6.5/4.5 · Tracker 18/8 · Guardian 12/8/12），
        // 不能用单值覆盖全部——否则任何一次黑屏/逆流事件后全被同化成同一套。
        float[] baseVision, baseHearing, baseGuardRadius;
        bool basesCaptured;
        float appliedSenseScale = 1f;

        // 黑屏（触发时刻由 schedule 列表管理）
        bool blackoutActive;
        float blackoutTimer;

        // 逆流楼梯
        bool reverseActive;
        float reverseTimer;

        // 通用 Timed 调度（每局排程：各事件独立触发时刻）
        struct ScheduledEvent { public GameEvent Ev; public float FireAt; public bool Fired; }
        readonly System.Collections.Generic.List<ScheduledEvent> schedule = new System.Collections.Generic.List<ScheduledEvent>();

        // 当前横幅（一次只显示一条：最近触发的活跃事件）
        string bannerText;
        float bannerTimer;
        Color bannerColor;

        void Start()
        {
            run = RunManager.Instance;
            if (run != null) run.OnRunStarted += OnRunStarted;
        }

        void OnDestroy()
        {
            if (run != null) run.OnRunStarted -= OnRunStarted;
        }

        void OnRunStarted()
        {
            // 每次开局先重置全部状态（重开不残留）
            blackoutActive = false;
            reverseActive = false;
            bannerTimer = 0f;
            basesCaptured = false;   // 本局守卫是新场景实例，重捕基准
            appliedSenseScale = 1f;
            EnsureBases();           // [审查] 必须先捕获基准再恢复：Restore 写的是 base 值，未捕获时 base=0 → 覆盖守卫 = 每局开场全场失明失聪
            RestoreGuardSense();     // 保险：上一局若中途退出先恢复

            // [0.8.1] 回退：随机事件全部暂缓 → 不再排 Timed 事件（schedule 恒空，Update 无事可做）。
            // 保留恢复逻辑：守卫感知永不被本组件改写（无活跃事件 → RecomputeGuardSense 只乘 1）。
            schedule.Clear();
        }

        /// <summary>按权重加入一条时间触发事件（黑屏必加，其余按 EventCatalog.Weight 概率抽签）。</summary>
        void AddScheduled(GameEvent ev, float min, float max)
        {
            if (ev != GameEvent.Blackout)
            {
                int weight = WeightOf(ev);
                if (Random.Range(0, 100) >= weight) return;   // 概率=weight%
            }
            schedule.Add(new ScheduledEvent { Ev = ev, FireAt = Random.Range(min, max), Fired = false });
        }

        int WeightOf(GameEvent ev)
        {
            for (int i = 0; i < EventCatalog.All.Length; i++)
                if (EventCatalog.All[i].Id == ev) return EventCatalog.All[i].Weight;
            return 50;
        }

        void Update()
        {
            if (run == null) return;

            if (run.State == RunState.Running)
            {
                // 到点触发
                for (int i = 0; i < schedule.Count; i++)
                {
                    var s = schedule[i];
                    if (!s.Fired && run.ElapsedTime >= s.FireAt)
                    {
                        s.Fired = true;
                        schedule[i] = s;
                        TriggerTimed(s.Ev);
                    }
                }

                // 活跃事件计时
                if (blackoutActive)
                {
                    blackoutTimer -= Time.deltaTime;
                    if (blackoutTimer <= 0f) { blackoutActive = false; RecomputeGuardSense(); }
                }
                if (reverseActive)
                {
                    reverseTimer -= Time.deltaTime;
                    if (reverseTimer <= 0f) { reverseActive = false; RecomputeGuardSense(); }
                }
                if (bannerTimer > 0f) bannerTimer -= Time.deltaTime;
            }
            else
            {
                // 本局结束/被抓/超时强制恢复，防守卫永久失明/感知
                if (blackoutActive || reverseActive)
                {
                    blackoutActive = false;
                    reverseActive = false;
                    RecomputeGuardSense();
                }
            }
        }

        void TriggerTimed(GameEvent ev)
        {
            switch (ev)
            {
                case GameEvent.Blackout: TriggerBlackout(); break;
                case GameEvent.InfiniteClassroom: TriggerInfiniteClassroom(); break;
                case GameEvent.TimeStopZone: TriggerTimeStop(); break;
                case GameEvent.ReverseStaircase: TriggerReverseStaircase(); break;
                case GameEvent.TimeStorm: TriggerTimeStorm(); break;
            }
        }

        // ---------- 统一守卫感知乘数管理 ----------

        void EnsureBases()
        {
            if (basesCaptured) return;
            int n = Guards != null ? Guards.Length : 0;
            baseVision = new float[n];
            baseHearing = new float[n];
            baseGuardRadius = new float[n];
            for (int i = 0; i < n; i++)
            {
                var g = Guards[i];
                if (g == null) continue;
                baseVision[i] = g.VisionRange;
                baseHearing[i] = g.HearingRange;
                baseGuardRadius[i] = g.GuardRadius;
            }
            basesCaptured = true;
        }

        /// <summary>重算并应用守卫感知（各自基准 × 活跃事件乘数）。</summary>
        void RecomputeGuardSense()
        {
            EnsureBases();
            float scale = 1f;
            if (blackoutActive) scale *= GuardPerceptionScale;
            if (reverseActive) scale *= ReverseScale;
            int n = Guards != null ? Guards.Length : 0;
            for (int i = 0; i < n; i++)
            {
                var g = Guards[i];
                if (g == null || i >= baseVision.Length) continue;
                g.VisionRange = baseVision[i] * scale;
                g.HearingRange = baseHearing[i] * scale;
                g.GuardRadius = baseGuardRadius[i] * scale;
            }
            appliedSenseScale = scale;
        }

        void RestoreGuardSense()
        {
            // [审查] 基准未捕获（数组 null）时跳过——防止把 0 基准写进守卫造成永久失明失聪
            if (baseVision == null) return;
            int n = Guards != null ? Guards.Length : 0;
            for (int i = 0; i < n; i++)
            {
                var g = Guards[i];
                if (g == null || i >= baseVision.Length) continue;
                g.VisionRange = baseVision[i];
                g.HearingRange = baseHearing[i];
                g.GuardRadius = baseGuardRadius[i];
            }
        }

        void ShowBanner(string text, Color color)
        {
            bannerText = text;
            bannerColor = color;
            bannerTimer = 5f;
        }

        // ---------- 各事件效果 ----------

        void TriggerBlackout()
        {
            blackoutActive = true;
            blackoutTimer = BlackoutDuration;
            CollectionSystem.Unlock(CollectionEntry.Blackout);
            RecomputeGuardSense();   // 此刻守卫必已激活（LayoutRandomizer 已跑完）
            ShowBanner($"【广播】全校熄灯：守卫暂时失明 {Mathf.CeilToInt(blackoutTimer)} 秒", new Color(1f, 0.9f, 0.4f));
            Debug.Log($"[黑屏广播] 全校熄灯 {BlackoutDuration}s：守卫感知 ×{GuardPerceptionScale}");
        }

        void TriggerInfiniteClassroom()
        {
            CollectionSystem.Unlock(CollectionEntry.InfiniteClassroom);
            run.AddTime(20f);
            ShowBanner("你误入无限教室，听课到深夜——倒计时 +20 秒", new Color(0.7f, 0.85f, 1f));
            Debug.Log("[无限教室] 倒计时 +20 秒");
        }

        void TriggerTimeStop()
        {
            CollectionSystem.Unlock(CollectionEntry.TimeStopZone);
            run.FreezeTime(5f);
            ShowBanner("时间停滞区扩散——倒计时冻结 5 秒", new Color(0.5f, 0.5f, 0.9f));
            Debug.Log("[时间停滞区] 倒计时冻结 5 秒");
        }

        void TriggerReverseStaircase()
        {
            CollectionSystem.Unlock(CollectionEntry.ReverseStaircase);
            reverseActive = true;
            reverseTimer = ReverseDuration;
            RecomputeGuardSense();
            ShowBanner($"楼梯逆流，守卫感知暴增 ×{ReverseScale}（{ReverseDuration} 秒）", new Color(1f, 0.5f, 0.4f));
            Debug.Log($"[逆流楼梯] 守卫感知 ×{ReverseScale} 持续 {ReverseDuration}s");
        }

        void TriggerTimeStorm()
        {
            CollectionSystem.Unlock(CollectionEntry.TimeStorm);
            run.AddTime(-15f);   // AddTime 有 Mathf.Max(0) 保护，不会减负
            ShowBanner("时间风暴卷过校园——倒计时 -15 秒", new Color(0.55f, 0.6f, 0.8f));
            Debug.Log("[时间风暴] 倒计时 -15 秒");
        }

        void OnGUI()
        {
            // 全屏暗幕（仅黑屏时；氛围，不影响迷雾光圈）
            if (blackoutActive)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.3f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // 顶部横幅（y≈20% 屏幕高，避开 RunHUD 顶部时间区 11%~17% 与首视提示 5%~10%）
            if (bannerTimer > 0f)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.034f),
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                style.normal.textColor = bannerColor;
                GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, Screen.height * 0.05f), bannerText, style);
            }
        }
    }
}
