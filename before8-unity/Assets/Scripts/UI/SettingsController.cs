using UnityEngine;
using Before8AM.Camera;
using Before8AM.Core;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.6] 设置面板（纯 OnGUI，挂 MenuController，MainMenuController 切 SubPanel.Settings 时 SetVisible(true)）。
    /// 灵敏度 [−][+]（读写 ViewToggle 的 PlayerPrefs，与游玩中 [ ] 微调同存储）、
    /// 水平反转 [切换]（与游玩中 ; 键同存储）、重看开场规则（清 IntroRules.SkipKey）。
    /// 界面：干净列表式——徽章图标 + 标签 + 值 + 深色按钮，无行底方块。
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        /// <summary>返回回调（MainMenuController 注入：关面板回主界面）。</summary>
        public System.Action OnBack;

        bool visible;
        string notice;
        float noticeTimer;

        public void SetVisible(bool v)
        {
            visible = v;
            if (v) notice = null;
        }

        void Update()
        {
            if (noticeTimer > 0f)
            {
                noticeTimer -= Time.deltaTime;
                if (noticeTimer <= 0f) notice = null;
            }
        }

        void OnGUI()
        {
            if (!visible) return;
            float w = Screen.width, h = Screen.height;

            GUI.color = new Color(0f, 0f, 0f, 0.82f);   // 实遮罩：面板内干净
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // [0.8.1] 面板尺寸/行节奏对齐商店（720 宽、行高 0.145、行距 0.02）
            float panelW = Mathf.Min(w * 0.9f, 720f);
            float panelH = h * 0.82f;
            float px = (w - panelW) * 0.5f, py = (h - panelH) * 0.5f;
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var title = Label(0.04f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.93f, 0.7f));
            GUI.Label(new Rect(px, py + h * 0.03f, panelW, h * 0.055f), "设置", title);

            float rowX = px + panelW * 0.05f;
            float rowW = panelW * 0.9f;
            float rowH = h * 0.145f;
            float rowY = py + h * 0.13f;

            // 行 1：灵敏度（[−] [ + ] 分列，不重叠）
            rowY = DrawRow(rowX, rowY, rowW, rowH, "灵", new Color(0.45f, 0.8f, 0.9f), "灵敏度", ViewToggle.GetSensitivity().ToString("0.0"), y =>
            {
                float s = ViewToggle.GetSensitivity();
                if (GUI.Button(LeftBtnRect(rowX, y, rowW, rowH), "-", RowBtn(rowH)))
                    AdjustSensitivity(s - 0.25f);
                if (GUI.Button(MidBtnRect(rowX, y, rowW, rowH), "+", RowBtn(rowH)))
                    AdjustSensitivity(s + 0.25f);
            });
            rowY += h * 0.02f;

            // 行 2：水平反转
            bool inv = ViewToggle.GetInvertHorizontal();
            rowY = DrawRow(rowX, rowY, rowW, rowH, "转", new Color(0.45f, 0.7f, 0.95f), "水平方向", inv ? "反转" : "正常", y =>
            {
                if (GUI.Button(RightBtnRect(rowX, y, rowW, rowH), "切换", RowBtn(rowH)))
                {
                    ViewToggle.SetInvertHorizontal(!ViewToggle.GetInvertHorizontal());
                    notice = "已保存：下次游玩水平方向生效";
                    noticeTimer = 2.5f;
                }
            });
            rowY += h * 0.02f;

            // 行 3：重看开场规则
            rowY = DrawRow(rowX, rowY, rowW, rowH, "规", new Color(0.95f, 0.75f, 0.4f), "开场规则", null, y =>
            {
                if (GUI.Button(RightBtnRect(rowX, y, rowW, rowH), "重看规则", RowBtn(rowH)))
                {
                    PlayerPrefs.DeleteKey(IntroRules.SkipKey);
                    notice = "已开启：下次进入游戏将再次播放开场规则";
                    noticeTimer = 3f;
                }
            });
            rowY += h * 0.02f;

            // 行 4：[0.8.9] 音量（[−][+]，全局主音量 = AudioListener.volume ← PlayerPrefs；SFXManager.Awake 同键读取）
            rowY = DrawRow(rowX, rowY, rowW, rowH, "音", new Color(0.6f, 0.9f, 0.65f), "音量", Mathf.RoundToInt(AudioListener.volume * 100f) + "%", y =>
            {
                if (GUI.Button(LeftBtnRect(rowX, y, rowW, rowH), "-", RowBtn(rowH)))
                    AdjustVolume(-0.1f);
                if (GUI.Button(MidBtnRect(rowX, y, rowW, rowH), "+", RowBtn(rowH)))
                    AdjustVolume(0.1f);
            });

            // 提示
            if (!string.IsNullOrEmpty(notice))
            {
                var ns = Label(0.019f, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 0.62f, 0.5f));
                GUI.Label(new Rect(px, rowY + h * 0.02f, panelW, h * 0.03f), notice, ns);
            }

            // 返回
            var btn = UiStyle.Btn(Mathf.RoundToInt(h * 0.024f));
            if (GUI.Button(new Rect(px + panelW * 0.34f, py + panelH - h * 0.06f, panelW * 0.32f, h * 0.05f), "返回主菜单", btn))
                OnBack?.Invoke();
        }

        /// <summary>画一行设置（左图标徽章 + 标签 + 中值 + 右侧操作区），返回行底 Y。操作绘制交给 inRow
        /// （参数=本行 Y，由 DrawRow 传入而非闭包捕获外部 rowY——后者会在赋值后读到更新过的行底，按钮位置错位）。</summary>
        float DrawRow(float x, float y, float w, float rowH, string badgeChar, Color badgeColor, string labelText, string valueText, System.Action<float> inRow)
        {
            // 图标徽章（左）
            float sw = rowH * 0.55f;
            Icon.Badge(new Rect(x + w * 0.03f, y + rowH * 0.2f, sw, sw), badgeColor, badgeChar);

            // [0.8.1] 字号制式同商店：传屏高比例（Label 内部 ×Screen.height），
            // 绝不能传 rowH×系数——rowH 已是绝对像素，会被二次放大到 h² 级 → 文字溢出被裁剪
            var ls = Label(0.026f, TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            GUI.Label(new Rect(x + w * 0.03f + sw + w * 0.03f, y, w * 0.48f, rowH), labelText, ls);

            if (!string.IsNullOrEmpty(valueText))
            {
                // [0.8.1] 值框移到商店价格同位置（0.60w 起，右对齐到 0.775w，按钮左缘 0.78w 前）
                var vs = Label(0.02f, TextAnchor.MiddleRight, FontStyle.Normal, new Color(0.7f, 0.75f, 0.82f));
                GUI.Label(new Rect(x + w * 0.60f, y, w * 0.175f, rowH), valueText, vs);
            }

            inRow?.Invoke(y);
            return y + rowH;
        }

        // [0.8.1] 按钮区对齐商店：一律从 x+0.78w 起（值框 0.775w 右对齐后紧邻，0.005w 防叠）。
        /// <summary>灵敏度减号（左）。</summary>
        Rect LeftBtnRect(float x, float y, float w, float rowH)
        {
            float bw = w * 0.09f, bh = rowH * 0.5f;
            return new Rect(x + w * 0.78f, y + rowH * 0.25f, bw, bh);
        }

        /// <summary>灵敏度加号（右，与减号分列不重叠）。</summary>
        Rect MidBtnRect(float x, float y, float w, float rowH)
        {
            float bw = w * 0.09f, bh = rowH * 0.5f;
            return new Rect(x + w * 0.885f, y + rowH * 0.25f, bw, bh);
        }

        /// <summary>单按钮（切换/重看规则）。</summary>
        Rect RightBtnRect(float x, float y, float w, float rowH)
        {
            float bw = w * 0.18f, bh = rowH * 0.5f;
            return new Rect(x + w * 0.78f, y + rowH * 0.25f, bw, bh);
        }

        GUIStyle RowBtn(float rowH) => UiStyle.Btn(Mathf.RoundToInt(rowH * 0.16f));

        void AdjustSensitivity(float v)
        {
            ViewToggle.SetSensitivity(v);
            notice = $"灵敏度 {ViewToggle.GetSensitivity():0.00}（已保存）";
            noticeTimer = 2f;
        }

        /// <summary>[0.8.9] 音量 ±10%：立即生效（AudioListener.volume）+ 存 PlayerPrefs（SFXManager 下次启动读取）。</summary>
        void AdjustVolume(float delta)
        {
            float v = Mathf.Clamp01(AudioListener.volume + delta);
            AudioListener.volume = v;
            PlayerPrefs.SetFloat("Before8AM.MasterVolume", v);
            notice = $"音量 {Mathf.RoundToInt(v * 100f)}%（已保存）";
            noticeTimer = 2f;
        }

        static GUIStyle Label(float fontScale, TextAnchor anchor, FontStyle fs, Color c)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * fontScale),
                alignment = anchor,
                fontStyle = fs,
            };
            s.normal.textColor = c;
            return s;
        }
    }
}
