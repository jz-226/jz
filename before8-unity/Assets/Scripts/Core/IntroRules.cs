using UnityEngine;

namespace Before8AM.Core
{
    /// <summary>
    /// [0.4.2] 开场规则解说面板：每次进入游戏（场景加载）黑屏先弹规则，看完点开始才播翻窗过场。
    /// 挂在独立 GameObject "IntroRulesPanel"（纯 OnGUI，不依赖相机/Transform/碰撞），
    /// WindowIntro.RulesGate 等它 Dismissed 才播 PlayIntro。
    /// 三个按钮：✕ / [开始游戏] = 本次关闭（下次还弹）；[不再显示] = 持久化 Before8AM.SkipIntroRules（永不弹）。
    /// 内容教"哪个房间什么颜色有什么效果、道具什么效果"，让玩家靠颜色区分房间，不需要房间内文字提示。
    /// 色块色值直接照抄 VerticalSliceBuilder 材质色，保证与实际游戏一致。GUIStyle 只能在 OnGUI 内懒初始化。
    /// </summary>
    public class IntroRules : MonoBehaviour
    {
        public const string SkipKey = "Before8AM.SkipIntroRules";

        /// <summary>门控源：置 true 后 WindowIntro.IntroFlow 才继续播过场。</summary>
        public bool Dismissed { get; private set; }

        // ---- 行数据（色值 = VerticalSliceBuilder 材质色，照抄；不用 emoji，靠色块精确对应实际颜色）----
        struct LegendRow { public string Name; public string Desc; public Color Swatch; }

        static readonly Color SafeFloor  = new Color(0.12f, 0.72f, 0.34f);   // MAT_SafeZone    安全屋绿发光地板
        static readonly Color BldFloor   = new Color(0.42f, 0.16f, 0.14f);   // MAT_BuildingFloor 普通建筑暗红地板
        static readonly Color GoldRing   = new Color(1.00f, 0.75f, 0.20f);   // MAT_GuardRing   守卫领地金色光圈
        static readonly Color ScoutC     = new Color(0.02f, 0.02f, 0.04f);   // MAT_Patrol      Scout 深黑
        static readonly Color RunnerC    = new Color(0.45f, 0.10f, 0.08f);   // MAT_Runner      Runner 暗红
        static readonly Color TrackerC   = new Color(0.36f, 0.12f, 0.44f);   // MAT_Tracker     Tracker 暗紫
        static readonly Color GuardianC  = new Color(0.72f, 0.55f, 0.18f);   // MAT_Guardian    Guardian 金铜
        static readonly Color OilC       = new Color(1.00f, 0.90f, 0.60f);   // MAT_LampHead    灯油罐暖黄
        static readonly Color SpeedC     = new Color(0.95f, 0.20f, 0.15f);   // MAT_SpeedDrink  加速饮料红
        static readonly Color HourglassC = new Color(1.00f, 0.85f, 0.30f);   // MAT_Hourglass   时间沙漏金
        static readonly Color InvisC     = new Color(0.25f, 0.80f, 1.00f);   // MAT_Invisibility 隐身药水青
        static readonly Color FragC      = new Color(1.00f, 0.90f, 0.35f);   // MAT_Fragment    时间碎片金
        static readonly Color ChestC     = new Color(0.50f, 0.30f, 0.15f);   // MAT_Chest       宝箱棕
        static readonly Color ChestRareC = new Color(0.28f, 0.48f, 0.95f);   // MAT_ChestRare   宝箱蓝
        static readonly Color ChestEpicC = new Color(1.00f, 0.78f, 0.20f);   // MAT_ChestEpic   宝箱金

        // 左栏 A：房间图例（哪个房间什么颜色什么效果）
        static readonly LegendRow[] LeftRooms =
        {
            new LegendRow { Name = "安全屋",   Desc = "绿发光地板，守卫进不来，躲入脱战",       Swatch = SafeFloor },
            new LegendRow { Name = "普通建筑", Desc = "暗红地板，掩体，可穿堂溜走",             Swatch = BldFloor },
            new LegendRow { Name = "守卫领地", Desc = "金色光圈，守卫驻守，可引走",             Swatch = GoldRing },
        };
        // 左栏 B：守卫颜色
        static readonly LegendRow[] LeftGuards =
        {
            new LegendRow { Name = "Scout",    Desc = "深黑·侦察，普通巡逻",                   Swatch = ScoutC },
            new LegendRow { Name = "Runner",   Desc = "暗红·追捕，跑得快",                     Swatch = RunnerC },
            new LegendRow { Name = "Tracker",  Desc = "暗紫·追踪，视野大追得久",               Swatch = TrackerC },
            new LegendRow { Name = "Guardian", Desc = "金铜·守卫者，引走回岗",                 Swatch = GuardianC },
        };
        // 右栏 C：道具效果
        static readonly LegendRow[] RightItems =
        {
            new LegendRow { Name = "灯油罐",   Desc = "暖黄，视野光圈+2m",                     Swatch = OilC },
            new LegendRow { Name = "加速饮料", Desc = "红，8秒速度×1.4",                       Swatch = SpeedC },
            new LegendRow { Name = "时间沙漏", Desc = "金，倒计时+20秒",                        Swatch = HourglassC },
            new LegendRow { Name = "隐身药水", Desc = "青，6秒完全隐身",                        Swatch = InvisC },
            new LegendRow { Name = "时间碎片", Desc = "金，3枚开晨门，每枚光圈+3m",             Swatch = FragC },
            new LegendRow { Name = "宝箱",     Desc = "棕蓝金，搜索1秒得金币概率碎片",          Swatch = ChestC },
        };
        // 右栏 D：目标 / 失败
        static readonly LegendRow[] RightGoal =
        {
            new LegendRow { Name = "目标",     Desc = "480秒内集齐3碎片，北门晨门逃出",         Swatch = GoldRing },
            new LegendRow { Name = "成功",     Desc = "本局金币转入永久金币",                   Swatch = ChestEpicC },
            new LegendRow { Name = "失败",     Desc = "被抓或超时，本局战利品全没收",           Swatch = RunnerC },
        };

        GUIStyle titleStyle, headStyle, rowStyle, btnStyle;
        bool stylesReady;

        void Start()
        {
            // 已持久化"不再显示"：直接放行（门控立即通过）+ 销毁面板，不再绘制
            if (PlayerPrefs.GetInt(SkipKey, 0) == 1)
            {
                Dismissed = true;
                Destroy(gameObject);
            }
        }

        void OnGUI()
        {
            if (Dismissed) return;                 // 已关闭（含持久化跳过）不再绘制
            EnsureStyles();
            DrawPanel();
        }

        // ---- 懒初始化：GUIStyle 只能在 OnGUI 内创建（GUI.skin 访问限制）----
        void EnsureStyles()
        {
            if (stylesReady) return;
            float h = Screen.height;
            titleStyle = MakeLabel(0.045f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.93f, 0.7f));
            headStyle  = MakeLabel(0.026f, TextAnchor.MiddleLeft,  FontStyle.Bold, new Color(1f, 0.85f, 0.5f));
            rowStyle   = MakeLabel(0.022f, TextAnchor.MiddleLeft,  FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f));
            btnStyle   = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = Mathf.RoundToInt(h * 0.026f);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            stylesReady = true;
        }

        static GUIStyle MakeLabel(float fontScale, TextAnchor anchor, FontStyle fs, Color c)
        {
            var s = new GUIStyle(GUI.skin.label);
            s.fontSize = Mathf.RoundToInt(Screen.height * fontScale);
            s.alignment = anchor;
            s.fontStyle = fs;
            s.normal.textColor = c;
            return s;
        }

        // ---- 布局：全屏黑底 + 居中面板 + 两栏；字号/行高全部按 Screen.height 等比（720p/1080p 自适应）----
        void DrawPanel()
        {
            float w = Screen.width, h = Screen.height;

            // 全屏不透明黑底遮罩（盖住场景；场景相机已被 WindowIntro 禁用，本层是唯一可见层）
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 居中深色面板
            float panelW = Mathf.Min(w * 0.86f, 1600f);
            float panelH = h * 0.88f;
            float px = (w - panelW) * 0.5f;
            float py = (h - panelH) * 0.5f;
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.96f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 标题 + 副题
            GUI.Label(new Rect(px, py + h * 0.015f, panelW, h * 0.055f), "早八在逃 · 前情提要", titleStyle);
            GUI.Label(new Rect(px, py + h * 0.070f, panelW, h * 0.030f), "夜半校园 · 天亮前集齐碎片，逃出晨门", headStyle);

            // ✕ 右上角 = 本次关闭（下次还弹）
            float xSize = h * 0.05f;
            if (GUI.Button(new Rect(px + panelW - xSize - h * 0.008f, py + h * 0.012f, xSize, xSize), "×", btnStyle))
                Close(false);

            // 两栏内容区
            float rowH = h * 0.042f;
            float top  = py + h * 0.115f;
            float colW = (panelW - h * 0.06f) * 0.5f;
            float leftX  = px + h * 0.02f;
            float rightX = px + panelW * 0.5f + h * 0.01f;
            DrawColumn(leftX,  top, colW, rowH, "房间", LeftRooms, "守卫", LeftGuards);
            DrawColumn(rightX, top, colW, rowH, "道具", RightItems, "目标 / 失败", RightGoal);

            // 底部按钮：开始游戏（本次）/ 不再显示（持久化）
            float bw = panelW * 0.32f;
            float bh = h * 0.055f;
            float by = py + panelH - bh - h * 0.015f;
            if (GUI.Button(new Rect(px + panelW * 0.5f - bw - h * 0.015f, by, bw, bh), "开始游戏", btnStyle)) Close(false);
            if (GUI.Button(new Rect(px + panelW * 0.5f + h * 0.015f, by, bw, bh), "不再显示", btnStyle)) Close(true);
        }

        /// <summary>一栏画两个 section（节标题 + 若干行），行 = 色块 + 名称 + 描述，单行。</summary>
        void DrawColumn(float x, float y, float colW, float rowH, string head1, LegendRow[] rows1, string head2, LegendRow[] rows2)
        {
            y = DrawSection(x, y, colW, rowH, head1, rows1);
            DrawSection(x, y, colW, rowH, head2, rows2);
        }

        float DrawSection(float x, float y, float colW, float rowH, string head, LegendRow[] rows)
        {
            GUI.Label(new Rect(x, y, colW, rowH), head, headStyle);
            y += rowH;

            float sw = rowH * 0.62f;                       // 色块边长
            float nameW = rowH * 2.4f;                     // 名称列宽（Guardian/普通建筑 均单行放下）
            float gap = rowH * 0.2f;
            foreach (var r in rows)
            {
                // 色块描边（深黑色块在深面板上可读）+ 填实际材质色
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(x - 1f, y + rowH * 0.14f - 1f, sw + 2f, sw + 2f), Texture2D.whiteTexture);
                GUI.color = r.Swatch;
                GUI.DrawTexture(new Rect(x, y + rowH * 0.14f, sw, sw), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + sw + gap, y, nameW, rowH), r.Name, rowStyle);
                GUI.Label(new Rect(x + sw + gap + nameW, y, colW - sw - gap - nameW, rowH), r.Desc, rowStyle);
                y += rowH;
            }
            return y;
        }

        /// <summary>关闭面板。remember=true 持久化"不再显示"。标志位 + 销毁双保险。</summary>
        void Close(bool remember)
        {
            if (remember) PlayerPrefs.SetInt(SkipKey, 1);   // 改动即 SetX，同 ViewToggle/GameProgress 范式
            Dismissed = true;
            Destroy(gameObject);
        }
    }
}
