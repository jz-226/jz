using UnityEngine;
using UnityEngine.InputSystem;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.9.1] 启动过场：进主菜单前先播品牌画面（夜空渐变 + 星点 + 官方 Logo + 「早八在逃」大标题），
    /// 淡入 → 停留 → 淡出，随后放行主菜单；点击/触摸/任意键立即跳过。
    /// 挂载：MainMenuController.Start get-or-add（不动场景文件）——Build Settings 首场景即主菜单（buildIndex 0），
    /// 打包后启动先看到这段画面，避免「点开就黑屏」的廉价感。
    /// 遮挡：static Active=true 期间 MainMenuController.OnGUI 早退，其余 UI 被全屏不透明背景盖住。
    /// 计时用 unscaledDeltaTime：不依赖 Time.timeScale，任何残留暂停态都不冻结。
    /// </summary>
    public class BootSplash : MonoBehaviour
    {
        /// <summary>过场播放中（MainMenuController.OnGUI 据此早退，防主菜单在过场底下可点）。
        /// 默认 true = 场景一加载即挡住，MainMenuController.Start 挂载前主菜单也不裸奔。</summary>
        public static bool Active { get; private set; } = true;

        /// <summary>[0.9.1] 过场播完回调（MainMenuController 注入：首次启动 → 接播开场漫画 IntroComic）。</summary>
        public System.Action OnFinished;

        const float FADE_IN = 1.1f;    // 标题/Logo 淡入时长
        const float HOLD = 1.6f;       // 全亮停留时长
        const float FADE_OUT = 0.9f;   // 自然淡出时长
        const float SKIP_OUT = 0.3f;   // 跳过时快速淡出时长

        float t;            // 当前阶段已播放时长（unscaled）
        float outDur;       // 实际淡出时长（跳过时缩短）
        bool outgoing;      // 已进入淡出阶段
        bool finished;      // 全部播完（放行主菜单）
        Texture2D skyTex;   // 夜空渐变 1×64（懒生成，配色与主菜单 BuildBackdrop 一致）
        float[] starX, starY, starS;
        GUIStyle titleShadow, titleCore, subStyle, hintStyle;
        bool stylesReady;

        void Update()
        {
            if (finished) return;
            t += Time.unscaledDeltaTime;
            if (!outgoing)
            {
                bool natural = t >= FADE_IN + HOLD;
                if (natural || AnyInput())
                {
                    outgoing = true;
                    outDur = natural ? FADE_OUT : SKIP_OUT;
                    t = 0f;
                }
            }
            else if (t >= outDur)
            {
                finished = true;
                Active = false;   // 放行主菜单
                OnFinished?.Invoke();   // [0.9.1] 通知接续（首次启动 → IntroComic 接管，继续挡主菜单）
            }
        }

        /// <summary>任意输入（键盘任意键 / 鼠标左键 / 触摸按下）→ 跳过过场。</summary>
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

        /// <summary>当前整体 alpha：淡入/停留期 SmoothStep 升到 1，淡出期降到 0。</summary>
        float CurrentAlpha()
        {
            if (!outgoing)
            {
                float a = Mathf.Clamp01(t / FADE_IN);
                return a * a * (3f - 2f * a);
            }
            float o = Mathf.Clamp01(1f - t / outDur);
            return o * o * (3f - 2f * o);
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            float h = Screen.height;
            titleShadow = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.078f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            titleShadow.normal.textColor = new Color(0f, 0f, 0f, 0.45f);
            titleCore = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.078f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            titleCore.normal.textColor = new Color(1f, 0.88f, 0.6f);
            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.024f),
                alignment = TextAnchor.MiddleCenter,
            };
            subStyle.normal.textColor = new Color(0.72f, 0.78f, 0.85f);
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.018f),
                alignment = TextAnchor.MiddleCenter,
            };
            hintStyle.normal.textColor = new Color(0.85f, 0.88f, 0.95f, 0.55f);
            stylesReady = true;
        }

        void BuildSky()
        {
            // 天空渐变 1×64（顶深蓝 → 地平线微紫），同主菜单 BuildBackdrop 配色，过场与菜单视觉连贯
            skyTex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            skyTex.wrapMode = TextureWrapMode.Clamp;
            skyTex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f;
                Color c = Color.Lerp(new Color(0.006f, 0.014f, 0.055f), new Color(0.07f, 0.12f, 0.21f), t);
                if (t > 0.82f) c = Color.Lerp(c, new Color(0.11f, 0.09f, 0.19f), (t - 0.82f) / 0.18f);
                skyTex.SetPixel(0, y, c);
            }
            skyTex.Apply();

            // 星点（固定种子：每次启动位置一致）
            var rng = new System.Random(20260824);
            int n = 60;
            starX = new float[n]; starY = new float[n]; starS = new float[n];
            for (int i = 0; i < n; i++)
            {
                starX[i] = (float)rng.NextDouble();
                starY[i] = (float)rng.NextDouble() * 0.8f;
                starS[i] = 1f + (float)rng.NextDouble() * 1.8f;
            }
        }

        void OnGUI()
        {
            if (finished) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;
            float alpha = CurrentAlpha();

            // 夜空背景恒画（不透明）：任何阶段（含 alpha≈0 的首帧）都不黑屏
            if (skyTex == null) BuildSky();
            GUI.DrawTexture(new Rect(0, 0, w, h), skyTex);

            // 星点微闪（整体随 alpha 淡入淡出）
            for (int i = 0; i < starX.Length; i++)
            {
                float flicker = 0.55f + 0.35f * Mathf.Sin(i * 3.7f);
                GUI.color = new Color(0.95f, 0.97f, 1f, flicker * alpha);
                float s = starS[i];
                GUI.DrawTexture(new Rect(starX[i] * w, starY[i] * h, s, s), Icon.Circle());
            }
            GUI.color = Color.white;

            // 官方品牌 Logo（资源在才画，返回 true → 标题下移让位；无资源标题居中）
            float logoSize = Mathf.Min(w * 0.26f, h * 0.24f);
            bool hasLogo = UiStyle.DrawOfficialLogo(new Rect(w * 0.5f - logoSize * 0.5f, h * 0.16f, logoSize, logoSize), alpha);

            // 大标题「早八在逃」（暖金 + 下阴影，同主菜单 DrawTitle 风格）
            float titleY = hasLogo ? h * 0.44f : h * 0.38f;
            float rH = h * 0.09f;
            titleShadow.normal.textColor = new Color(0f, 0f, 0f, 0.45f * alpha);
            titleCore.normal.textColor = new Color(1f, 0.88f, 0.6f, alpha);
            GUI.Label(new Rect(3f, titleY + 3f, w, rH), "早八在逃", titleShadow);
            GUI.Label(new Rect(0f, titleY, w, rH), "早八在逃", titleCore);

            // 金色分隔线
            float lineW = w * 0.3f;
            GUI.color = new Color(1f, 0.82f, 0.45f, 0.55f * alpha);
            GUI.DrawTexture(new Rect(w * 0.5f - lineW * 0.5f, titleY + rH + h * 0.006f, lineW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 副标题
            subStyle.normal.textColor = new Color(0.72f, 0.78f, 0.85f, alpha);
            GUI.Label(new Rect(0f, titleY + rH + h * 0.018f, w, h * 0.04f), "潜行 · 收集 · 在黎明前逃出校园", subStyle);

            // 底部跳过提示（淡出期不显示）
            if (alpha > 0.6f && !outgoing)
                GUI.Label(new Rect(0f, h * 0.9f, w, h * 0.04f), "点击或按任意键跳过", hintStyle);
        }
    }
}
