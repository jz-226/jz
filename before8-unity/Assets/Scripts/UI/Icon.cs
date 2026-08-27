using UnityEngine;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.6] 轻量 UI 图标工具：程序化圆形纹理（边缘抗锯齿）+ 彩色圆徽章（中心单字）。
    /// 道具/设置项先用简单图形示意（正式美术贴图出来前），视觉清晰、不依赖 Unicode 符号字形（默认字体没有 emoji）。
    /// </summary>
    public static class Icon
    {
        static Texture2D circle;

        /// <summary>64×64 实心白色圆纹理（边缘 1.5px 渐变抗锯齿，GUI.color 染色画任意大小圆）。</summary>
        public static Texture2D Circle()
        {
            if (circle == null)
            {
                int size = 64;
                circle = new Texture2D(size, size, TextureFormat.RGBA32, false);
                circle.wrapMode = TextureWrapMode.Clamp;
                float r = size * 0.5f - 1.5f;
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = d <= r - 1f ? 1f : Mathf.Clamp01((r - d) / 1.5f);
                        circle.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                circle.Apply();
            }
            return circle;
        }

        /// <summary>彩色圆徽章 + 中心白字（无描边——彩圆在深色面板上已足够突出，去描边更干净）。</summary>
        public static void Badge(Rect r, Color bg, string text)
        {
            GUI.color = bg;
            GUI.DrawTexture(r, Circle());
            GUI.color = Color.white;

            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(r.height * 0.48f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            s.normal.textColor = Color.white;
            GUI.Label(r, text, s);
        }
    }
}
