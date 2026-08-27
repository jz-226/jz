using UnityEngine;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.8.6] 滚动字幕提示：文字从屏幕右缘水平滚入、滚出左缘后消失（字幕式，滚一遍就不见）。
    /// 用于段位增援播报「新增巡夜者（类型）」；红色、字号适中（不抢 HUD 视线）。
    /// 连续触发时直接覆盖（新提示替换旧提示，不排队）。
    /// </summary>
    public class ScrollingNotice : MonoBehaviour
    {
        [Header("滚动参数")]
        public float Speed = 420f;          // 像素/秒（全屏约 4~5 秒滚完）
        public float YRatio = 0.84f;        // 字幕纵向位置（屏幕高度比例，底部字幕位）
        public Color TextColor = new Color(1f, 0.35f, 0.35f);   // 警示红

        string text;
        float x;
        bool active;
        GUIStyle style;   // GUI.skin 只能在 OnGUI 内访问 → 懒创建

        /// <summary>从屏幕右缘开始滚动一条提示（滚一遍后自动消失）。</summary>
        public void Show(string message)
        {
            text = message;
            x = Screen.width;   // 右缘外进入
            active = true;
        }

        void Update()
        {
            if (!active) return;
            x -= Speed * Time.deltaTime;
        }

        void OnGUI()
        {
            if (!active || string.IsNullOrEmpty(text)) return;

            if (style == null)
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.032f),   // 适中：略小于 HUD 主行，不抢视线
                    fontStyle = FontStyle.Bold,
                };
            style.normal.textColor = TextColor;

            float tw = style.CalcSize(new GUIContent(text)).x;
            if (x + tw < 0f)   // 完全滚出左缘 → 本次提示结束
            {
                active = false;
                return;
            }
            GUI.Label(new Rect(x, Screen.height * YRatio, tw, style.lineHeight), text, style);
        }
    }
}
