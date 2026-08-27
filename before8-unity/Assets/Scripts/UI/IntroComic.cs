using UnityEngine;
using UnityEngine.InputSystem;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.9.1] 首次启动开场漫画：**只有第一次打开游戏才播放**（PlayerPrefs 标志位），
    /// 逐页翻页讲开场故事（程序绘制漫画页：校门/时钟/主角/守卫/晨门 + 底部旁白），
    /// 点任意处/任意键翻页，最后一页后写标志并放行主菜单；之后启动直接进主菜单。
    /// 挂载：MainMenuController.Start get-or-add（不动场景文件）；BootSplash 播完触发 Begin()。
    /// 调试：编辑器下按住 Shift 启动 → 重播漫画（开发期看效果，发布版不生效）。
    /// 遮挡：static Active=true 期间 MainMenuController.OnGUI 早退（同 BootSplash 机制）。
    /// 计时用 unscaledDeltaTime：不依赖 Time.timeScale。
    /// </summary>
    public class IntroComic : MonoBehaviour
    {
        const string SEEN_KEY = "Before8AM.SeenIntroComicV2"; // 1=已看过本次正式漫画；V2 使旧版观看记录自动播放一次新版
        const float PAGE_FADE = 0.18f;   // 每页进入淡入时长（漫画翻页感）

        /// <summary>漫画播放中（MainMenuController.OnGUI 据此早退）。</summary>
        public static bool Active { get; private set; }

        int page;          // 当前页（0 起）
        int total;
        float fadeT;       // 本页淡入计时
        Texture2D skyTex;  // 夜空渐变（每页共用同一张，翻页背景无缝）
        Texture2D[] comicPages;
        GUIStyle narrationStyle, pageStyle, hintStyle;
        bool stylesReady;

        static readonly string[] ComicPageResources =
        {
            "Prologue/Comic_01", "Prologue/Comic_02", "Prologue/Comic_03", "Prologue/Comic_04", "Prologue/Comic_05"
        };

        /// <summary>各页旁白（世界观串联：午夜永夜降临 → 钟停 → 碎片 → 守卫 → 晨门）。
        /// 时间设定：0:00 永夜降临，倒计时 8 分钟 = 一夜（1 游戏分钟 = 60 现实分钟），归零 = 早八 8:00。</summary>
        static readonly string[] Narration =
        {
            "午夜零点。钟声敲完最后一响，\n校门口的路灯，是今夜最后一盏亮着的灯。",
            "然后，钟停了。整座校园停在了这一夜，\n天，永远不会亮了。",
            "晨门之外，藏着黎明。\n集齐三块时间碎片，才能在天亮前离开。",   // 3 块 = RunManager.TimeFragmentsRequired（图鉴："3 枚开晨门"）
            "走廊深处，有什么在徘徊。\n被抓住的人，会永远留在这片夜色里。",
            "在早八以前，离开这里。\n你，已经迟到了。",
        };

        /// <summary>是否应播放：从未看过；或编辑器下按住 Shift 启动（开发期重播调试）。</summary>
        public static bool ShouldShow()
        {
            if (PlayerPrefs.GetInt(SEEN_KEY, 0) == 0) return true;
            if (Application.isEditor)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.shiftKey.isPressed) return true;
            }
            return false;
        }

        /// <summary>开始播放（BootSplash 播完调用）。</summary>
        public void Begin()
        {
            page = 0;
            total = ComicPageResources.Length;
            comicPages = new Texture2D[total];
            for (int i = 0; i < total; i++)
                comicPages[i] = Resources.Load<Texture2D>(ComicPageResources[i]);
            fadeT = 0f;
            Active = true;
        }

        void Update()
        {
            if (!Active) return;
            fadeT += Time.unscaledDeltaTime;
            if (AnyInput())
            {
                page++;
                if (page >= total)
                {
                    PlayerPrefs.SetInt(SEEN_KEY, 1);   // 看完了：写标志，以后启动不再播
                    Active = false;                    // 放行主菜单
                }
                else
                {
                    fadeT = 0f;   // 翻页：新页重新淡入
                }
            }
        }

        /// <summary>任意输入（键盘任意键 / 鼠标左键 / 触摸按下）→ 翻页。</summary>
        static bool AnyInput()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
        }

        void OnGUI()
        {
            if (!Active) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;
            float t = Mathf.Clamp01(fadeT / PAGE_FADE);
            float a = 0.5f + 0.5f * t * t * (3f - 2f * t);   // 前景 alpha 0.5→1（背景恒亮，翻页无缝）

            // 正式序章漫画自带旁白、页码和金色版式，整页按比例展示，不再叠加旧程序绘制的框和文字。
            if (HasOfficialPages())
            {
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.blackTexture);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(0, 0, w, h), comicPages[page], ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;

                if (a > 0.6f)
                {
                    hintStyle.normal.textColor = new Color(0.78f, 0.82f, 0.92f, 0.55f * a);
                    GUI.Label(new Rect(0, h * 0.955f, w, h * 0.03f),
                        page + 1 >= total ? "点击任意处 · 进入主菜单" : "点击任意处 · 下一页", hintStyle);
                }
                return;
            }

            if (skyTex == null) BuildSky();
            GUI.DrawTexture(new Rect(0, 0, w, h), skyTex);   // 夜空背景恒画（不透明，盖住主菜单）

            // 漫画页框架：插画区（细边框）+ 页号 + 底部旁白条
            float padX = w * 0.07f;
            float artW = w - padX * 2f;
            float artH = h * 0.5f;
            float artX = padX;
            float artY = h * 0.12f;

            // 插画区边框（半透明白 1px）
            float bw = artW + 2f;
            GUI.color = new Color(1f, 1f, 1f, 0.28f * a);
            GUI.DrawTexture(new Rect(artX - 1f, artY - 1f, bw, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(artX - 1f, artY + artH, bw, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(artX - 1f, artY - 1f, 1f, artH + 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(artX + artW, artY - 1f, 1f, artH + 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawPage(artX, artY, artW, artH, w, h, a);

            // 页号（右上）
            pageStyle.normal.textColor = new Color(0.8f, 0.85f, 0.95f, 0.7f * a);
            GUI.Label(new Rect(artX + artW - w * 0.12f, artY - h * 0.036f, w * 0.12f, h * 0.03f),
                $"{page + 1} / {total}", pageStyle);

            // 旁白条（深色底 + 左侧金竖线 + 白字，wordWrap 防长句溢出）
            float nH = h * 0.16f;
            float nY = artY + artH + h * 0.03f;
            GUI.color = new Color(0.03f, 0.05f, 0.09f, 0.92f * a);
            GUI.DrawTexture(new Rect(artX, nY, artW, nH), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.82f, 0.45f, 0.85f * a);
            GUI.DrawTexture(new Rect(artX + w * 0.015f, nY + nH * 0.16f, w * 0.008f, nH * 0.68f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            narrationStyle.normal.textColor = new Color(0.95f, 0.96f, 1f, a);
            GUI.Label(new Rect(artX + w * 0.05f, nY, artW - w * 0.095f, nH), Narration[page], narrationStyle);

            // 翻页提示（底部居中，最后页换文案）
            if (a > 0.6f)
            {
                hintStyle.normal.textColor = new Color(0.75f, 0.8f, 0.9f, 0.55f * a);
                GUI.Label(new Rect(artX, h * 0.9f, artW, h * 0.035f),
                    page + 1 >= total ? "点击任意处 · 开始" : "点击任意处 · 下一页", hintStyle);
            }
        }

        bool HasOfficialPages()
        {
            if (comicPages == null || comicPages.Length != total) return false;
            for (int i = 0; i < comicPages.Length; i++)
                if (comicPages[i] == null) return false;
            return page >= 0 && page < comicPages.Length;
        }

        /// <summary>当前页插画（剪影风格，与游戏内方块人美术统一）。</summary>
        void DrawPage(float x, float y, float w, float h, float sw, float sh, float a)
        {
            float cx = x + w * 0.5f;
            switch (page)
            {
                case 0: DrawPageGate(x, y, w, h, cx, sh, a); break;      // 校门 + 路灯
                case 1: DrawPageClock(x, y, w, h, cx, sh, a); break;     // 教学楼 + 停摆时钟 + 紫雾
                case 2: DrawPageFragments(x, y, w, h, cx, sh, a); break; // 主角 + 四块碎片
                case 3: DrawPageGuard(x, y, w, h, cx, sh, a); break;     // 守卫黑影 + 红眼
                case 4: DrawPageDawn(x, y, w, h, cx, sh, a); break;      // 晨门 + 晨光
            }
        }

        // ---------- 各页插画 ----------

        /// <summary>页 1：校门剪影 + 一盏路灯（最后的灯光），星点。</summary>
        void DrawPageGate(float x, float y, float w, float h, float cx, float sh, float a)
        {
            float horizon = y + h * 0.62f;
            GUI.color = new Color(0.01f, 0.02f, 0.05f, 0.95f * a);
            GUI.DrawTexture(new Rect(x, horizon, w, h * 0.38f), Texture2D.whiteTexture);   // 地面

            float gw = w * 0.16f, gh = h * 0.34f, post = w * 0.014f;
            GUI.color = new Color(0.02f, 0.04f, 0.09f, 0.97f * a);
            GUI.DrawTexture(new Rect(cx - gw * 0.5f, horizon - gh, post, gh), Texture2D.whiteTexture);              // 左柱
            GUI.DrawTexture(new Rect(cx + gw * 0.5f - post, horizon - gh, post, gh), Texture2D.whiteTexture);       // 右柱
            GUI.DrawTexture(new Rect(cx - gw * 0.5f - post * 0.5f, horizon - gh - post * 0.7f, gw + post, post * 1.1f), Texture2D.whiteTexture); // 横梁

            // 路灯（右侧：杆 + 灯 + 暖光）
            float lx = cx + w * 0.2f;
            GUI.color = new Color(0.10f, 0.13f, 0.18f, 0.97f * a);
            GUI.DrawTexture(new Rect(lx, horizon - h * 0.36f, w * 0.008f, h * 0.36f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.9f, 0.7f, 0.9f * a);
            GUI.DrawTexture(new Rect(lx - w * 0.02f, horizon - h * 0.38f, w * 0.048f, w * 0.014f), Texture2D.whiteTexture);
            float glowR = h * 0.22f;
            GUI.color = new Color(1f, 0.85f, 0.55f, 0.20f * a);
            GUI.DrawTexture(new Rect(lx + w * 0.004f - glowR * 0.5f, horizon - h * 0.38f - glowR * 0.5f, glowR, glowR), Icon.Circle());
            GUI.color = Color.white;

            DrawStars(x, y, w, h * 0.5f, a, 10);
        }

        /// <summary>页 2：教学楼剪影 + 停摆时钟（指针定格凌晨）+ 底部紫雾。</summary>
        void DrawPageClock(float x, float y, float w, float h, float cx, float sh, float a)
        {
            float horizon = y + h * 0.55f;

            float bw = w * 0.34f, bh = h * 0.3f;
            float bx = cx - bw * 0.5f;
            GUI.color = new Color(0.015f, 0.03f, 0.07f, 0.96f * a);
            GUI.DrawTexture(new Rect(bx, horizon - bh, bw, bh), Texture2D.whiteTexture);
            // 三角屋顶（渐窄横条模拟）
            float tw = bw;
            for (int i = 0; i < 6; i++)
            {
                GUI.DrawTexture(new Rect(bx + (bw - tw) * 0.5f, horizon - bh - (i + 1) * h * 0.028f, tw, h * 0.028f), Texture2D.whiteTexture);
                tw -= bw * 0.12f;
            }
            // 亮窗（3×3，暖黄）
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    GUI.color = new Color(1f, 0.8f, 0.45f, 0.55f * a);
                    GUI.DrawTexture(new Rect(bx + bw * (0.14f + 0.26f * c), horizon - bh * (0.42f + 0.27f * r), w * 0.02f, h * 0.024f), Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;

            // 停摆时钟（楼上方：金圆 + 长针朝上 + 短针水平 = 定格凌晨）
            float cR = h * 0.11f;
            float cyy = horizon - bh - h * 0.05f;
            GUI.color = new Color(0.9f, 0.75f, 0.4f, 0.9f * a);
            GUI.DrawTexture(new Rect(cx - cR, cyy - cR, cR * 2f, cR * 2f), Icon.Circle());
            GUI.color = new Color(0.22f, 0.16f, 0.07f, 0.95f * a);
            GUI.DrawTexture(new Rect(cx - cR * 0.06f, cyy - cR * 0.55f, cR * 0.12f, cR * 0.6f), Texture2D.whiteTexture);   // 长针（竖）
            GUI.DrawTexture(new Rect(cx - cR * 0.5f, cyy - cR * 0.08f, cR * 0.9f, cR * 0.16f), Texture2D.whiteTexture);    // 短针（横）
            GUI.color = Color.white;

            // 紫雾（地面层）
            GUI.color = new Color(0.40f, 0.20f, 0.50f, 0.16f * a);
            GUI.DrawTexture(new Rect(x, horizon - h * 0.16f, w, h * 0.16f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawStars(x, y, w, h * 0.42f, a, 8);
        }

        /// <summary>页 3：主角方块小人（蓝 + 橙书包）+ 四块金色时间碎片。</summary>
        void DrawPageFragments(float x, float y, float w, float h, float cx, float sh, float a)
        {
            // 主角（居中偏左，脚落地）
            DrawPlayer(cx - w * 0.12f, y + h * 0.82f, h * 0.48f, new Color(0.25f, 0.60f, 1f), a);

            // 四块碎片（右侧 2×2 排列：外光晕 + 内核）
            for (int i = 0; i < 4; i++)
            {
                float fx = cx + w * 0.10f + (i % 2) * w * 0.09f;
                float fy = y + h * (0.50f - (i / 2) * 0.24f);
                GUI.color = new Color(1f, 0.85f, 0.35f, 0.32f * a);
                GUI.DrawTexture(new Rect(fx - h * 0.055f, fy - h * 0.055f, h * 0.11f, h * 0.11f), Icon.Circle());
                GUI.color = new Color(1f, 0.92f, 0.6f, 0.95f * a);
                GUI.DrawTexture(new Rect(fx - h * 0.022f, fy - h * 0.022f, h * 0.044f, h * 0.044f), Icon.Circle());
            }
            GUI.color = Color.white;

            // 地面线
            GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.9f * a);
            GUI.DrawTexture(new Rect(x, y + h * 0.82f, w, h * 0.18f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>页 4：守卫黑影 + 红眼（居中偏右），主角缩在左下角。</summary>
        void DrawPageGuard(float x, float y, float w, float h, float cx, float sh, float a)
        {
            float gs = h * 0.68f;
            float gx = cx + w * 0.12f, gy = y + h * 0.85f;
            GUI.color = new Color(0.05f, 0.05f, 0.09f, 0.95f * a);
            GUI.DrawTexture(new Rect(gx - gs * 0.28f, gy - gs * 0.75f, gs * 0.56f, gs * 0.75f), Texture2D.whiteTexture);   // 宽肩身体
            GUI.DrawTexture(new Rect(gx - gs * 0.13f, gy - gs * 0.95f, gs * 0.26f, gs * 0.22f), Texture2D.whiteTexture);    // 头
            GUI.color = new Color(1f, 0.18f, 0.12f, 0.95f * a);
            GUI.DrawTexture(new Rect(gx - gs * 0.075f, gy - gs * 0.89f, gs * 0.05f, gs * 0.05f), Icon.Circle());           // 左红眼
            GUI.DrawTexture(new Rect(gx + gs * 0.025f, gy - gs * 0.89f, gs * 0.05f, gs * 0.05f), Icon.Circle());           // 右红眼
            GUI.color = Color.white;

            // 主角缩在左下角（小号，被逼到角落）
            DrawPlayer(x + w * 0.13f, y + h * 0.85f, h * 0.22f, new Color(0.25f, 0.60f, 1f), a);

            // 红色警戒地面
            GUI.color = new Color(0.30f, 0.06f, 0.09f, 0.45f * a);
            GUI.DrawTexture(new Rect(x, y + h * 0.85f, w, h * 0.15f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>页 5：晨门（金色门框 + 门内暖光）+ 地平线晨光 + 主角跑向晨门。</summary>
        void DrawPageDawn(float x, float y, float w, float h, float cx, float sh, float a)
        {
            float horizon = y + h * 0.72f;

            // 地平线晨光（金色渐层）
            for (int i = 0; i < 5; i++)
            {
                float t = i / 5f;
                GUI.color = new Color(1f, 0.75f + 0.2f * t, 0.4f, (0.45f - 0.06f * i) * a);
                GUI.DrawTexture(new Rect(x, horizon - h * (0.012f + 0.014f * i), w, h * 0.014f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            // 晨门
            float gw = w * 0.2f, gh = h * 0.48f, post = w * 0.02f;
            float gx = cx - gw * 0.5f, gy = horizon - gh;
            GUI.color = new Color(1f, 0.85f, 0.5f, 0.95f * a);
            GUI.DrawTexture(new Rect(gx, gy, post, gh), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gx + gw - post, gy, post, gh), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gx - post * 0.5f, gy - post * 0.8f, gw + post, post * 1.1f), Texture2D.whiteTexture);

            // 门内暖光
            float glowR = gh * 0.6f;
            GUI.color = new Color(1f, 0.9f, 0.6f, 0.45f * a);
            GUI.DrawTexture(new Rect(cx - glowR * 0.5f, gy + gh * 0.25f - glowR * 0.5f, glowR, glowR), Icon.Circle());
            GUI.color = Color.white;

            // 主角跑向晨门（右侧）
            DrawPlayer(cx + gw * 0.6f, horizon, h * 0.28f, new Color(0.25f, 0.60f, 1f), a);

            // 地面
            GUI.color = new Color(0.05f, 0.06f, 0.10f, 0.95f * a);
            GUI.DrawTexture(new Rect(x, horizon, w, h * 0.28f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ---------- 工具 ----------

        /// <summary>方块小人（蓝/皮肤色身体 + 恒橙书包，与游戏内角色一致；cx 中心 x，cy 脚底 y，s 身高）。</summary>
        void DrawPlayer(float cx, float cy, float s, Color body, float a)
        {
            GUI.color = new Color(body.r, body.g, body.b, a);
            GUI.DrawTexture(new Rect(cx - s * 0.17f, cy - s * 0.30f, s * 0.15f, s * 0.30f), Texture2D.whiteTexture);   // 左腿
            GUI.DrawTexture(new Rect(cx + s * 0.02f, cy - s * 0.30f, s * 0.15f, s * 0.30f), Texture2D.whiteTexture);    // 右腿
            GUI.DrawTexture(new Rect(cx - s * 0.20f, cy - s * 0.62f, s * 0.40f, s * 0.32f), Texture2D.whiteTexture);   // 身体
            GUI.DrawTexture(new Rect(cx - s * 0.34f, cy - s * 0.60f, s * 0.13f, s * 0.28f), Texture2D.whiteTexture);   // 左臂
            GUI.DrawTexture(new Rect(cx + s * 0.21f, cy - s * 0.60f, s * 0.13f, s * 0.28f), Texture2D.whiteTexture);   // 右臂
            GUI.DrawTexture(new Rect(cx - s * 0.12f, cy - s * 0.82f, s * 0.24f, s * 0.20f), Texture2D.whiteTexture);   // 头
            GUI.color = new Color(0.85f, 0.55f, 0.20f, a);                                                             // 书包恒橙
            GUI.DrawTexture(new Rect(cx + s * 0.19f, cy - s * 0.60f, s * 0.20f, s * 0.26f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>星点（固定种子，一次生成）。</summary>
        void DrawStars(float x, float y, float w, float h, float a, int n)
        {
            float[] sx = new float[n], sy = new float[n], ss = new float[n];
            var rng = new System.Random(20260825);
            for (int i = 0; i < n; i++)
            {
                sx[i] = (float)rng.NextDouble();
                sy[i] = (float)rng.NextDouble();
                ss[i] = 1f + (float)rng.NextDouble() * 1.6f;
            }
            for (int i = 0; i < n; i++)
            {
                float flicker = 0.55f + 0.35f * Mathf.Sin(i * 3.7f);
                GUI.color = new Color(0.95f, 0.97f, 1f, flicker * a);
                GUI.DrawTexture(new Rect(x + sx[i] * w, y + sy[i] * h, ss[i], ss[i]), Icon.Circle());
            }
            GUI.color = Color.white;
        }

        void BuildSky()
        {
            // 天空渐变 1×64（同主菜单 BuildBackdrop 配色，漫画与菜单视觉连贯）
            skyTex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            skyTex.wrapMode = TextureWrapMode.Clamp;
            skyTex.filterMode = FilterMode.Bilinear;
            for (int i = 0; i < 64; i++)
            {
                float t = i / 63f;
                Color c = Color.Lerp(new Color(0.006f, 0.014f, 0.055f), new Color(0.07f, 0.12f, 0.21f), t);
                if (t > 0.82f) c = Color.Lerp(c, new Color(0.11f, 0.09f, 0.19f), (t - 0.82f) / 0.18f);
                skyTex.SetPixel(0, i, c);
            }
            skyTex.Apply();
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            float h = Screen.height;
            narrationStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.024f),
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            pageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.02f),
                alignment = TextAnchor.MiddleRight,
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.018f),
                alignment = TextAnchor.MiddleCenter,
            };
            stylesReady = true;
        }
    }
}
