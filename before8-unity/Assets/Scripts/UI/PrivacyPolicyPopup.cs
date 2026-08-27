using System;
using UnityEngine;

namespace Before8AM.UI
{
    /// <summary>
    /// [TapTap 合规] 隐私政策弹窗：首次启动（从未同意过）时展示，含明确的「同意 / 拒绝」按钮。
    /// 同意后才允许初始化 TapSDK（TapTap 登录 SDK）并进入游戏；拒绝则退出应用。
    /// 同意状态持久化到 PlayerPrefs（Before8AM.PrivacyAccepted），下次启动不再弹。
    /// static Active 门控主菜单 OnGUI（与 BootSplash/IntroComic 同模式），
    /// 显示期间主菜单整体不可见/不可点。
    /// </summary>
    public class PrivacyPolicyPopup : MonoBehaviour
    {
        public const string PrefKey = "Before8AM.PrivacyAccepted";

        /// <summary>弹窗显示中 = 主菜单 OnGUI 早退。</summary>
        public static bool Active { get; private set; }

        /// <summary>用户点「同意」回调（MainMenuController 注入：初始化 TapSDK + 播放启动过场）。</summary>
        public Action OnAccepted;

        /// <summary>用户点「拒绝」回调（默认行为是退出应用，回调仅用于记录）。</summary>
        public Action OnRejected;

        bool showing;
        Vector2 scrollPos;

        /// <summary>弹窗内展示的隐私政策文本（含 TapSDK 第三方披露，TapTap 审核要求）。</summary>
        static string PolicyText =>
            "《早八在逃》隐私政策\n\n" +
            "本游戏由独立开发者姜梓开发。在首次使用前，请仔细阅读以下内容。\n\n" +
            "一、我们收集的信息及用途\n" +
            "1. 游戏数据：游戏进度、金币、皮肤等存档仅保存在您的设备本地，不会上传至任何服务器。\n" +
            "2. 第三方 SDK：为提供 TapTap 账号登录服务，本游戏集成了 TapTap 登录 SDK（TapSDK），" +
            "仅在您点击「同意」后初始化。\n\n" +
            "二、第三方 SDK 信息披露（TapSDK）\n" +
            "· 第三方主体：易玩（上海）网络科技有限公司（TapTap 平台）\n" +
            "· 使用目的：提供 TapTap 账号登录、账号状态获取\n" +
            "· 收集信息类型：设备信息（设备型号、操作系统版本、网络状态）、设备标识（如 OAID、Android ID）、" +
            "TapTap 账号信息（openId/unionId、昵称、头像）\n" +
            "· 官方隐私政策：https://developer.taptap.com/docs/sdk/start/agreement/\n\n" +
            "三、您的权利\n" +
            "您可选择「拒绝」不授权，游戏将退出且不会初始化任何 SDK；" +
            "您也可随时通过本政策底部邮箱联系我们，查阅、更正或删除个人信息。\n\n" +
            "四、联系我们\n" +
            "邮箱：2175564440@qq.com\n\n" +
            "点击「同意并进入」即表示您已阅读并同意上述全部内容。";

        /// <summary>是否已同意过（PlayerPrefs 持久化）。</summary>
        public static bool IsAccepted() => PlayerPrefs.GetInt(PrefKey, 0) == 1;

        /// <summary>开始显示弹窗（MainMenuController 在未同意时调用）。</summary>
        public void Begin()
        {
            showing = true;
            Active = true;
        }

        void OnGUI()
        {
            if (!showing || !Active) return;
            DrawPopup();
        }

        void DrawPopup()
        {
            float w = Screen.width, h = Screen.height;

            // 全屏遮罩（盖住一切，主菜单不可点）
            GUI.color = new Color(0f, 0f, 0f, 0.76f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 居中面板（横屏适配）
            float panelW = Mathf.Min(w * 0.92f, 780f);
            float panelH = h * 0.9f;
            float px = (w - panelW) * 0.5f, py = (h - panelH) * 0.5f;
            GUI.color = new Color(0.09f, 0.12f, 0.20f, 0.99f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;
            // 顶部金线（呼应主菜单配色）
            GUI.color = new Color(1f, 0.82f, 0.45f, 0.5f);
            GUI.DrawTexture(new Rect(px, py, panelW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 标题
            var title = Label(0.028f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.88f, 0.6f));
            GUI.Label(new Rect(px, py + panelH * 0.03f, panelW, panelH * 0.09f), "隐私政策提示", title);

            // 正文（可上下滚动）
            var body = Label(0.019f, TextAnchor.UpperLeft, FontStyle.Normal, new Color(0.84f, 0.87f, 0.93f));
            body.wordWrap = true;
            float contentX = px + panelW * 0.06f;
            float contentY = py + panelH * 0.14f;
            float contentW = panelW * 0.88f;
            float contentH = panelH * 0.66f;
            float textW = contentW - 18f;   // 预留滚动条
            float textH = body.CalcHeight(new GUIContent(PolicyText), textW);
            scrollPos = GUI.BeginScrollView(
                new Rect(contentX, contentY, contentW, contentH),
                scrollPos,
                new Rect(0, 0, textW, textH + 14f));
            GUI.Label(new Rect(0, 0, textW, textH), PolicyText, body);
            GUI.EndScrollView();

            // 底部按钮：左「拒绝」灰 / 右「同意并进入」金（TapTap 要求明确的同意、拒绝按钮）
            float btnH = panelH * 0.085f;
            float btnY = py + panelH * 0.84f;
            float rejectW = panelW * 0.24f;
            float agreeW = panelW * 0.28f;
            float btnGap = panelW * 0.03f;

            var rejectStyle = UiStyle.Btn(Mathf.RoundToInt(h * 0.022f));
            var agreeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(h * 0.024f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            agreeStyle.normal.textColor = new Color(0.16f, 0.10f, 0.03f);

            if (GUI.Button(new Rect(px + panelW * 0.5f - rejectW - btnGap, btnY, rejectW, btnH), "拒绝", rejectStyle))
                Reject();
            GUI.color = new Color(1f, 0.82f, 0.35f);
            if (GUI.Button(new Rect(px + panelW * 0.5f + btnGap, btnY, agreeW, btnH), "同意并进入", agreeStyle))
                Accept();
            GUI.color = Color.white;
        }

        void Accept()
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
            showing = false;
            Active = false;
            OnAccepted?.Invoke();
        }

        void Reject()
        {
            showing = false;
            Active = false;
            OnRejected?.Invoke();
            ForceQuit();
        }

        /// <summary>拒绝后退出应用（Android 上 finish 当前 Activity，比 Application.Quit 更可靠）。</summary>
        void ForceQuit()
        {
            if (Application.isEditor)
            {
                Debug.Log("[隐私政策] 用户拒绝，编辑器下不退出（仅记录）");
                return;
            }
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity.Call("finish");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[隐私政策] 退出失败：{e.Message}");
                Application.Quit();
            }
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
