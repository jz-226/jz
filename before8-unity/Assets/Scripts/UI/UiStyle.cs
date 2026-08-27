using UnityEngine;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.6] 统一 UI 按钮样式：深蓝灰底 + 浅色描边 + 白字（hover 亮 / active 暗）。
    /// 替代默认皮肤灰按钮在深色面板上的突兀感——全界面按钮用同一套，界面干净统一。
    /// </summary>
    public static class UiStyle
    {
        static Texture2D bgNormal, bgHover, bgActive, panelBg, campusSilhouette, before8AMLogo;

        public static GUIStyle Btn(int fontSize)
        {
            var s = new GUIStyle(GUI.skin.button);
            s.normal.background = Bg(ref bgNormal, new Color(0.075f, 0.118f, 0.190f), new Color(0.30f, 0.42f, 0.62f));
            s.hover.background = Bg(ref bgHover, new Color(0.115f, 0.180f, 0.280f), new Color(0.52f, 0.68f, 0.92f));
            s.active.background = Bg(ref bgActive, new Color(0.045f, 0.075f, 0.130f), new Color(0.25f, 0.35f, 0.52f));
            s.normal.textColor = new Color(0.91f, 0.95f, 1.0f);
            s.hover.textColor = Color.white;
            s.active.textColor = new Color(0.78f, 0.82f, 0.88f);
            s.fontSize = fontSize;
            s.alignment = TextAnchor.MiddleCenter;
            s.border = new RectOffset(1, 1, 1, 1);   // 覆掉默认按钮的 9-slice border(4,4,4,4)：16×16 纹理是 1px 边框设计，
            // 否则四角 4px 被放大成粗框，小按钮上形成一圈浅色块 → 被误认为"白色/黄色残留"
            return s;
        }

        /// <summary>IMGUI 共用深色信息面板。使用缓存纹理，避免 OnGUI 重复分配。</summary>
        public static void DrawPanel(Rect rect)
        {
            GUI.DrawTexture(rect, Bg(ref panelBg, new Color(0.025f, 0.047f, 0.100f, 0.88f), new Color(0.28f, 0.40f, 0.62f, 0.95f)));
        }

        /// <summary>横屏 HUD 专用信息块：沿用夜蓝面板，只加一条细语义色带区分本局、时间和档案。</summary>
        public static void DrawHudPanel(Rect rect, Color accent)
        {
            DrawPanel(rect);
            Color previous = GUI.color;
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.92f);
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>本局状态：左侧夹条让资源信息像一张随身校园通行卡，而不是普通方框。</summary>
        public static void DrawStatusPlate(Rect rect, Color accent)
        {
            DrawPanel(rect);
            Color previous = GUI.color;
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.90f);
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 7f, 3f, Mathf.Max(0f, rect.height - 14f)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 7f, 7f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.yMax - 9f, 7f, 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>倒计时：只画刻度和细边，避免在屏幕正中放又一张大色块卡片。</summary>
        public static void DrawTimerFrame(Rect rect, Color accent)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.015f, 0.030f, 0.070f, 0.68f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.86f);
            GUI.DrawTexture(new Rect(rect.x + 8f, rect.y + 1f, Mathf.Max(0f, rect.width - 16f), 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + 8f, rect.yMax - 3f, Mathf.Max(0f, rect.width - 16f), 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + rect.height * 0.30f, 2f, rect.height * 0.40f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 3f, rect.y + rect.height * 0.30f, 2f, rect.height * 0.40f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>永久档案：低对比校园楼剪影做水印，避免段位区域与其它 HUD 同形。</summary>
        public static void DrawArchivePlate(Rect rect, Color accent)
        {
            DrawPanel(rect);
            Color previous = GUI.color;
            // 原水印过大，会和右上角档案信息抢注意力；缩成安静的校徽印记。
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.10f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.06f, rect.y + rect.height * 0.23f,
                rect.width * 0.20f, rect.height * 0.54f), CampusSilhouette());
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.78f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.32f, rect.y + 1f, rect.width * 0.62f, 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>绘制正式品牌 Logo。资源位于 Resources/UI，菜单与游戏 HUD 共用同一份图。</summary>
        public static bool DrawOfficialLogo(Rect rect, float alpha = 1f)
        {
            if (before8AMLogo == null)
                before8AMLogo = Resources.Load<Texture2D>("UI/Before8AMLogo");
            if (before8AMLogo == null) return false;

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(rect, before8AMLogo, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
            return true;
        }

        public static GUIStyle HudLabel(int fontSize, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var s = new GUIStyle(GUI.skin.label);
            s.fontSize = fontSize;
            s.alignment = alignment;
            s.normal.textColor = new Color(0.88f, 0.93f, 1.0f);
            return s;
        }

        static Texture2D Bg(ref Texture2D cache, Color body, Color edge)
        {
            if (cache != null) return cache;
            cache = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            cache.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    bool border = x == 0 || y == 0 || x == 15 || y == 15;
                    cache.SetPixel(x, y, border ? edge : body);
                }
            }
            cache.Apply();
            return cache;
        }

        static Texture2D CampusSilhouette()
        {
            if (campusSilhouette != null) return campusSilhouette;

            const int width = 128;
            const int height = 64;
            campusSilhouette = new Texture2D(width, height, TextureFormat.RGBA32, false);
            campusSilhouette.wrapMode = TextureWrapMode.Clamp;
            campusSilhouette.filterMode = FilterMode.Bilinear;
            Color ink = new Color(1f, 1f, 1f, 1f);
            Color clear = new Color(1f, 1f, 1f, 0f);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    campusSilhouette.SetPixel(x, y, clear);

            // 主楼、两侧塔楼和三角屋顶；它是低细节水印，不与实际场景建筑争夺注意力。
            for (int x = 16; x <= 111; x++)
                for (int y = 7; y <= 34; y++)
                    campusSilhouette.SetPixel(x, y, ink);
            for (int x = 24; x <= 42; x++)
                for (int y = 7; y <= 44; y++)
                    campusSilhouette.SetPixel(x, y, ink);
            for (int x = 85; x <= 103; x++)
                for (int y = 7; y <= 44; y++)
                    campusSilhouette.SetPixel(x, y, ink);
            for (int y = 35; y <= 55; y++)
            {
                int halfWidth = (y - 34) * 3;
                for (int x = 64 - halfWidth; x <= 64 + halfWidth; x++)
                    if (x >= 16 && x < 112) campusSilhouette.SetPixel(x, y, ink);
            }
            for (int x = 56; x <= 71; x++)
                for (int y = 7; y <= 23; y++)
                    campusSilhouette.SetPixel(x, y, clear);
            for (int x = 31; x <= 35; x += 4)
                for (int y = 17; y <= 32; y += 8)
                    campusSilhouette.SetPixel(x, y, clear);
            for (int x = 93; x <= 97; x += 4)
                for (int y = 17; y <= 32; y += 8)
                    campusSilhouette.SetPixel(x, y, clear);
            campusSilhouette.Apply();
            return campusSilhouette;
        }
    }
}
