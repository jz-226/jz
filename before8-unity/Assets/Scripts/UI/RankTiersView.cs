using UnityEngine;
using Before8AM.Reward;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.9.2] 段位一览面板（纯 OnGUI，挂 MainMenuController 同物体）：全部段位档 + 达标分数一览，
    /// 给玩家目标感——已达成白色、当前档金底高亮、下一档「冲刺目标」标记、未达灰。
    /// 数据源 GameProgress 段位表只读访问（RankTierCount/ThresholdAt/NameAt）。
    /// 挂载：MainMenuController.Start get-or-add + visible 门控 + OnBack（同其他子面板范式，不动场景文件）。
    /// </summary>
    public class RankTiersView : MonoBehaviour
    {
        /// <summary>返回回调（MainMenuController 注入：ClosePanel）。</summary>
        public System.Action OnBack;

        bool visible;
        Vector2 scrollPos;
        GUIStyle titleStyle, headStyle, rowStyle, curStyle, nextStyle, dimStyle, scoreStyle, btnStyle;
        bool stylesReady;

        public void SetVisible(bool v) => visible = v;

        void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();
            Render();
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            float h = Screen.height;
            titleStyle = MakeLabel(0.036f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.93f, 0.7f));
            headStyle  = MakeLabel(0.02f,  TextAnchor.MiddleLeft,  FontStyle.Normal, new Color(0.6f, 0.68f, 0.78f));
            rowStyle   = MakeLabel(0.022f, TextAnchor.MiddleLeft,  FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f));
            curStyle   = MakeLabel(0.022f, TextAnchor.MiddleLeft,  FontStyle.Bold,   new Color(1f, 0.88f, 0.55f));
            nextStyle  = MakeLabel(0.022f, TextAnchor.MiddleLeft,  FontStyle.Bold,   new Color(0.75f, 0.95f, 0.75f));
            dimStyle   = MakeLabel(0.022f, TextAnchor.MiddleLeft,  FontStyle.Normal, new Color(0.5f, 0.5f, 0.55f));
            scoreStyle = MakeLabel(0.02f,  TextAnchor.MiddleRight, FontStyle.Normal, new Color(0.6f, 0.68f, 0.78f));
            btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = Mathf.RoundToInt(h * 0.022f);
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

        void Render()
        {
            float w = Screen.width, h = Screen.height;
            int score = GameProgress.RankScore;

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);   // 全屏暗色遮罩
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = prev;

            float panelW = Mathf.Min(w * 0.8f, 560f);
            float panelH = h * 0.66f;
            float px = (w - panelW) * 0.5f;
            float py = (h - panelH) * 0.5f;
            GUI.color = new Color(0.07f, 0.09f, 0.13f, 0.97f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 标题 + 说明
            GUI.Label(new Rect(px, py + h * 0.02f, panelW, h * 0.055f), "段位一览", titleStyle);
            GUI.Label(new Rect(px + panelW * 0.06f, py + h * 0.078f, panelW * 0.88f, h * 0.03f),
                $"当前 {GameProgress.RankName} · 总积分 {score}    （成功撤离一局 ≈110 分 · 失败不掉分）", headStyle);

            // 档位行（滚动：16 档超一屏）
            float rowH = h * 0.052f;
            float viewY = py + h * 0.12f;
            float viewH = panelH - h * 0.2f;
            float contentH = rowH * (GameProgress.RankTierCount + 1);   // 表头 + 16 档

            scrollPos = GUI.BeginScrollView(new Rect(px, viewY, panelW, viewH), scrollPos,
                new Rect(0f, 0f, panelW, contentH));

            float y = 0f;
            // 表头
            GUI.Label(new Rect(panelW * 0.06f, y, panelW * 0.4f, rowH), "段位", headStyle);
            GUI.Label(new Rect(panelW * 0.5f, y, panelW * 0.4f, rowH), "累计段位分", scoreStyle);
            y += rowH;

            // 当前档 index（从阈值命中最高档）
            int curIdx = 0;
            for (int i = 0; i < GameProgress.RankTierCount; i++)
                if (score >= GameProgress.RankTierThresholdAt(i)) curIdx = i;

            for (int i = 0; i < GameProgress.RankTierCount; i++)
            {
                int th = GameProgress.RankTierThresholdAt(i);
                string name = GameProgress.RankTierNameAt(i);

                GUIStyle nameStyle = rowStyle;
                string mark = "";
                if (i == curIdx)
                {
                    nameStyle = curStyle;              // 当前档：金底高亮
                    mark = "当前";
                    GUI.color = new Color(0.5f, 0.4f, 0.18f, 0.5f);
                    GUI.DrawTexture(new Rect(panelW * 0.03f, y + h * 0.004f, panelW * 0.94f, rowH - h * 0.008f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                else if (i == curIdx + 1)
                {
                    nameStyle = nextStyle;             // 下一档：绿色冲刺目标
                    mark = "冲刺目标";
                }
                else if (score < th)
                {
                    nameStyle = dimStyle;              // 未达成：灰
                }

                GUI.Label(new Rect(panelW * 0.06f, y, panelW * 0.4f, rowH), name, nameStyle);
                GUI.Label(new Rect(panelW * 0.5f, y, panelW * 0.22f, rowH), $"{th} 分", scoreStyle);

                if (mark.Length > 0)
                    GUI.Label(new Rect(panelW * 0.74f, y, panelW * 0.2f, rowH), mark, nameStyle);

                y += rowH;
            }

            GUI.EndScrollView();

            // 返回按钮
            if (GUI.Button(new Rect(px + panelW * 0.35f, py + panelH - h * 0.07f, panelW * 0.3f, h * 0.05f), "返回", btnStyle))
                OnBack?.Invoke();
        }
    }
}
