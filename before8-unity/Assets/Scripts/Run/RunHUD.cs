using UnityEngine;
using Before8AM.Patrol;   // [0.8.6] ReinforceDirector 增援倒计时
using Before8AM.UI;

namespace Before8AM.Run
{
    /// <summary>
    /// 横屏运行 HUD：左上本局状态、中上倒计时、右上等级段位。
    /// 订阅 RunManager 事件驱动刷新（金币/碎片），时间每帧轮询；仅在 Running 态显示。
    /// </summary>
    public class RunHUD : MonoBehaviour
    {
        RunManager run;
        int coins;
        int fragments;
        readonly int[] itemCounts = new int[ItemCatalog.Count];   // [0.5] 背包数量缓存（OnInventoryChanged 刷新）
        ReinforceDirector reinforce;   // [0.8.6] 段位增援倒计时（停车场无该组件 → null 不显示）

        // GUIStyle 只能在 OnGUI 内创建（GUI.skin 访问限制），按屏幕高度缓存。
        GUIStyle sectionStyle;
        GUIStyle valueStyle;
        GUIStyle detailStyle;
        GUIStyle inventoryStyle;
        GUIStyle timerSectionStyle;
        GUIStyle timerValueStyle;
        GUIStyle stageStyle;
        int styleHeight = -1;

        /// <summary>左上卡底缘 y（每帧 OnGUI 更新）。PlayerController 的 buff 提示据此避让，防止低高度屏被卡片盖住。</summary>
        public static float LeftCardBottomY { get; private set; }

        void Start()
        {
            run = RunManager.Instance;
            if (run == null) return;
            reinforce = Object.FindObjectOfType<ReinforceDirector>();   // [0.8.6]
            coins = run.TemporaryCoins;
            fragments = run.TimeFragments;
            run.OnRunStarted += OnRunStarted;
            run.OnCoinsChanged += OnCoinsChanged;
            run.OnFragmentAdded += OnFragmentAdded;
            run.OnInventoryChanged += OnInventoryChanged;   // [0.5] 背包栏刷新
            run.OnRunEnded += OnRunEnded;
        }

        void OnDestroy()
        {
            if (run == null) return;
            run.OnRunStarted -= OnRunStarted;
            run.OnCoinsChanged -= OnCoinsChanged;
            run.OnFragmentAdded -= OnFragmentAdded;
            run.OnInventoryChanged -= OnInventoryChanged;   // [0.5]
            run.OnRunEnded -= OnRunEnded;
        }

        void OnRunStarted() { coins = 0; fragments = 0; OnInventoryChanged(); }
        void OnCoinsChanged(int c) => coins = c;
        void OnFragmentAdded() => fragments = run != null ? run.TimeFragments : fragments;

        // [0.5] 背包数量同步（拾取/使用/开局）
        void OnInventoryChanged()
        {
            if (run == null) return;
            for (int i = 0; i < itemCounts.Length; i++)
                itemCounts[i] = run.GetItemCount((RunItem)i);
        }

        // 结算界面由 RewardSystem 画，HUD 在非 Running 态自然消失
        void OnRunEnded(RunState state) { }

        void OnGUI()
        {
            if (Before8AM.UI.InGameSettings.AnyOpen) return;   // [0.9.3] 设置面板打开时隐藏顶部 HUD
            if (run == null || run.State != RunState.Running) return;

            EnsureStyles();
            float screenW = Screen.width;
            float screenH = Screen.height;
            float margin = Mathf.Clamp(screenH * 0.028f, 24f, 34f);   // [0.9.4] HUD 整体内收（用户反馈太贴屏幕边缘；原 10-18px）
            float gap = Mathf.Clamp(screenH * 0.009f, 5f, 10f);
            float leftW = Mathf.Clamp(screenW * 0.23f, 190f, 330f);
            float centerW = Mathf.Clamp(screenW * 0.16f, 154f, 250f);
            float rightW = Mathf.Clamp(screenW * 0.23f, 200f, 340f);
            // [0.9.4+] 行高按字号×1.35 撑开（同 PlayerController buff 提示的成熟方案）：原 headerH/
            // valueH/detailH 是固定比例，高分屏上比字体实际行高（≈1.15~1.25×fontSize）还矮，
            // 中文数字上下各被裁掉一点（用户反馈「字的上和下被切掉」）。字号也整体上调
            // （用户反馈字太小/挤），行距拉开。面板高度仍由内容反推，任何分辨率行间不叠。
            float titleRowH = Mathf.RoundToInt(sectionStyle.fontSize * 1.35f);
            float valueRowH = Mathf.RoundToInt(valueStyle.fontSize * 1.35f);
            float detailRowH = Mathf.RoundToInt(detailStyle.fontSize * 1.35f);
            float panelH = 6f + titleRowH + 6f + valueRowH + 6f + detailRowH + 8f;   // 上下留白 6/8，行距 6
            int mm = Mathf.FloorToInt(run.TimeLeft / 60f);
            int ss = Mathf.FloorToInt(run.TimeLeft % 60f);
            float nextIn = reinforce != null ? reinforce.NextReinforceIn : -1f;
            Color dangerColor = StageColor(run.CurrentStage);
            Rect left = new Rect(margin, margin, leftW, panelH);
            Rect center = new Rect(screenW * 0.5f - centerW * 0.5f, margin, centerW, panelH);
            LeftCardBottomY = left.yMax;   // 供 PlayerController buff 提示避让（本帧/上帧值相同，读晚一帧也安全）

            // 左上：随身通行卡式状态条，紧凑呈现本局资源与风险。
            UiStyle.DrawStatusPlate(left, new Color(0.44f, 0.66f, 1.00f));
            GUI.Label(new Rect(left.x + 14f, left.y + 6f, left.width - 28f, titleRowH), "本局状态", sectionStyle);
            GUI.Label(new Rect(left.x + 14f, left.y + 6f + titleRowH + 6f, left.width * 0.56f, valueRowH),
                $"碎片 {fragments}/{run.TimeFragmentsRequired}", valueStyle);
            GUI.Label(new Rect(left.x + left.width * 0.56f + 6f, left.y + 6f + titleRowH + 6f + (valueRowH - detailRowH) * 0.5f,
                left.width * 0.44f - 20f, detailRowH), $"金币 {coins}", detailStyle);
            stageStyle.normal.textColor = dangerColor;
            string reinforceText = nextIn >= 0f ? $"    增援 {nextIn:0}s" : "";   // [0.9.4] 与「危险时段 X/4」拉开间距（用户反馈挤）
            GUI.Label(new Rect(left.x + 14f, left.yMax - detailRowH - 8f, left.width - 28f, detailRowH),
                $"危险时段 {run.CurrentStage + 1}/4{reinforceText}", stageStyle);

            // 中上：悬浮刻度框只强调时间，不再占用一整块蓝色面板。
            UiStyle.DrawTimerFrame(center, new Color(1.00f, 0.78f, 0.45f));
            // [0.9.4+] 计时内容同样按字号×1.35 撑行高，垂直居中（面板加高后避免下方空一截）
            float timerTitleRowH = Mathf.RoundToInt(timerSectionStyle.fontSize * 1.35f);
            float timerValueRowH = Mathf.RoundToInt(timerValueStyle.fontSize * 1.35f);
            float timerContentH = timerTitleRowH + 6f + timerValueRowH;
            float timerTop = center.y + Mathf.Max(4f, (panelH - timerContentH) * 0.5f);
            GUI.Label(new Rect(center.x + 8f, timerTop, center.width - 16f, timerTitleRowH), "距离早八", timerSectionStyle);
            GUI.Label(new Rect(center.x + 8f, timerTop + timerTitleRowH + 6f, center.width - 16f, timerValueRowH),
                $"{mm:00}:{ss:00}", timerValueStyle);

            // 右上：正式 Logo 替换旧的个人档案面板，避免 HUD 出现两套品牌图形。
            // [0.9.4] 尺寸独立于面板高：面板随内容加高后若仍 ×1.65 会在 1024×768 顶到下方道具格（126 > 118 顶线）。
            float logoSize = Mathf.Clamp(screenH * 0.117f, 100f, 126f);
            Rect logo = new Rect(screenW - margin - logoSize, margin, logoSize, logoSize);
            UiStyle.DrawOfficialLogo(logo);

            // PC 背包只保留总件数和键位提示，避免中文道具名挤出面板；手游由右侧道具按钮显示数量。
            if (!Application.isMobilePlatform)
            {
                int itemTotal = 0;
                for (int i = 0; i < itemCounts.Length; i++)
                    itemTotal += itemCounts[i];
                if (itemTotal > 0)
                {
                    Rect inventory = new Rect(screenW - margin - rightW, logo.yMax + gap, rightW, detailRowH + 6f);
                    UiStyle.DrawStatusPlate(inventory, new Color(0.33f, 0.54f, 0.90f));
                    GUI.Label(new Rect(inventory.x + 8f, inventory.y + 2f, inventory.width - 16f, detailRowH),
                        $"背包 {itemTotal}件   1-4 使用", inventoryStyle);
                }
            }
        }

        void EnsureStyles()
        {
            if (styleHeight == Screen.height) return;
            styleHeight = Screen.height;
            // [0.9.4+] 字号整体上调（用户反馈字太小）+ 提高上限（旧上限 32 在 1080p 已顶满会裁切）
            sectionStyle = MakeStyle(0.019f, 13, 27, TextAnchor.MiddleLeft, FontStyle.Bold, new Color(0.66f, 0.76f, 0.92f));
            valueStyle = MakeStyle(0.033f, 17, 36, TextAnchor.MiddleLeft, FontStyle.Bold, new Color(0.96f, 0.98f, 1f));
            detailStyle = MakeStyle(0.022f, 14, 28, TextAnchor.MiddleLeft, FontStyle.Normal, new Color(0.82f, 0.88f, 0.98f));
            inventoryStyle = MakeStyle(0.014f, 11, 32, TextAnchor.MiddleRight, FontStyle.Normal, new Color(0.88f, 0.93f, 1f));
            timerSectionStyle = MakeStyle(0.016f, 12, 23, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.95f, 0.84f, 0.62f));
            timerValueStyle = MakeStyle(0.033f, 17, 40, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.94f, 0.78f));
            stageStyle = MakeStyle(0.022f, 14, 28, TextAnchor.MiddleLeft, FontStyle.Normal, Color.white);
        }

        static GUIStyle MakeStyle(float screenScale, int minSize, int maxSize, TextAnchor alignment, FontStyle fontStyle, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * screenScale), minSize, maxSize);
            style.alignment = alignment;
            style.fontStyle = fontStyle;
            style.normal.textColor = color;
            return style;
        }

        static Color StageColor(int stage)
        {
            switch (stage)
            {
                case 0: return new Color(0.50f, 0.72f, 1.00f);
                case 1: return new Color(1.00f, 0.78f, 0.42f);
                case 2: return new Color(1.00f, 0.54f, 0.36f);
                default: return new Color(0.93f, 0.32f, 0.38f);
            }
        }
    }
}
