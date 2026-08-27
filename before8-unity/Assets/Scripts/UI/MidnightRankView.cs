using System;
using UnityEngine;
using Before8AM.Reward;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.8.0] 午夜榜面板（纯 OnGUI，挂 MainMenuController 同物体；规格书 85 主菜单入口）。
    /// 本地积分排行榜 Top 8（RankBoard）：当前段位/总积分 + 通往午夜王者的进度条 + 榜单行（名次/段位档/分数/地图/遗物/日期）。
    /// visible 门控 + OnBack 回调（主菜单 ClosePanel 切换），照 MissionView/CollectionView 范式。
    /// </summary>
    public class MidnightRankView : MonoBehaviour
    {
        public Action OnBack;
        public string BackLabel = "返回主菜单";

        /// <summary>[0.8.0] 午夜王者门槛（= GameProgress 段位表顶档阈值；进度条归一化基准，不硬编码防两处漂移）。</summary>
        public static int KingScore => GameProgress.RankKingThreshold;

        bool visible;
        Vector2 scrollPos;

        GUIStyle titleStyle, headStyle, rowStyle, smallStyle, statusStyle, btnStyle, goldStyle, dimStyle;
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
            titleStyle  = MakeLabel(0.042f, TextAnchor.MiddleCenter,  FontStyle.Bold,   new Color(1f, 0.93f, 0.7f));
            headStyle   = MakeLabel(0.024f, TextAnchor.MiddleLeft,    FontStyle.Bold,   new Color(1f, 0.85f, 0.5f));
            rowStyle    = MakeLabel(0.024f, TextAnchor.MiddleLeft,    FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f));
            smallStyle  = MakeLabel(0.02f,  TextAnchor.MiddleLeft,    FontStyle.Normal, new Color(0.6f, 0.68f, 0.78f));
            statusStyle = MakeLabel(0.024f, TextAnchor.MiddleLeft,    FontStyle.Bold,   new Color(1f, 0.85f, 0.4f));
            goldStyle   = MakeLabel(0.026f, TextAnchor.MiddleCenter,  FontStyle.Bold,   new Color(1f, 0.88f, 0.55f));
            dimStyle    = MakeLabel(0.024f, TextAnchor.MiddleCenter,  FontStyle.Normal, new Color(0.55f, 0.55f, 0.58f));
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

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);   // 全屏暗色遮罩
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = prev;

            float panelW = Mathf.Min(w * 0.88f, 840f);
            float panelH = h * 0.9f;
            float px = (w - panelW) * 0.5f;
            float py = (h - panelH) * 0.5f;

            GUI.color = new Color(0.07f, 0.09f, 0.13f, 0.97f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float titleH = h * 0.07f;
            GUI.Label(new Rect(px, py + titleH * 0.15f, panelW, titleH), "午夜榜 · 积分排行", titleStyle);

            // ---- 档案卡（不滚动）：当前段位 + 总积分 + 通往午夜王者进度条 ----
            float cardY = py + titleH * 0.15f + titleH + h * 0.01f;
            float cardH = h * 0.11f;
            GUI.color = new Color(0.11f, 0.13f, 0.18f, 0.9f);
            GUI.DrawTexture(new Rect(px + 20f, cardY, panelW - 40f, cardH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(px + 40f, cardY + h * 0.012f, panelW, h * 0.035f),
                $"{GameProgress.RankName}  ·  总积分 {GameProgress.RankScore}", statusStyle);
            float barX = px + panelW * 0.18f, barW = panelW * 0.64f, barH = h * 0.018f;
            float barY = cardY + cardH * 0.58f;
            GUI.color = new Color(0.22f, 0.22f, 0.28f, 0.9f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);
            float p = Mathf.Clamp01((float)GameProgress.RankScore / KingScore);
            if (p > 0.001f)
            {
                GUI.color = new Color(1f, 0.82f, 0.4f);
                GUI.DrawTexture(new Rect(barX, barY, barW * p, barH), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 40f, barY - h * 0.004f, panelW * 0.14f, barH + h * 0.01f), $"段位分 {GameProgress.RankScore}", smallStyle);
            GUI.Label(new Rect(px + panelW - panelW * 0.22f, barY - h * 0.004f, panelW * 0.18f, barH + h * 0.01f), $"午夜王者 {KingScore}", smallStyle);

            // ---- 榜单区（滚动）：Top 8 ----
            float viewY = cardY + cardH + h * 0.012f;
            float viewH = panelH - (viewY - py) - h * 0.085f;
            // 内容 = 标题 0.055h + 8 行 × 0.08h = 0.695h；contentH 必须 ≥ 它，否则第 8 名被截断
            float contentH = h * 0.72f;
            scrollPos = GUI.BeginScrollView(new Rect(px, viewY, panelW, viewH), scrollPos,
                new Rect(0f, 0f, panelW, contentH));

            float y = 0f;
            // [0.8.1] 内容坐标：ScrollView 内 0 = 视口左缘，绝不能加 px（屏幕偏移）
            GUI.Label(new Rect(30f, y, panelW - 40f, h * 0.05f), "历史最佳 · 单局成绩 Top 8", headStyle);
            y += h * 0.055f;

            if (RankBoard.Count == 0)
            {
                GUI.Label(new Rect(40f, y + h * 0.01f, panelW - 80f, h * 0.1f),
                    "暂无成绩——翻窗去校园或午夜超市闯一局，荣登午夜榜！", rowStyle);
            }
            else
            {
                for (int i = 0; i < RankBoard.MaxEntries; i++)
                {
                    DrawRankRow(0f, y, panelW, h * 0.075f, i);
                    y += h * 0.08f;
                }
            }

            GUI.EndScrollView();

            // 返回按钮
            if (GUI.Button(new Rect(px + panelW * 0.35f, py + panelH - h * 0.075f, panelW * 0.3f, h * 0.05f), BackLabel, btnStyle))
                OnBack?.Invoke();
        }

        /// <summary>榜单行（内容坐标：x=0 是 ScrollView 视口左缘）。</summary>
        void DrawRankRow(float x, float y, float w, float h, int i)
        {
            bool filled = i < RankBoard.Count;
            var e = filled ? RankBoard.Get(i) : default(RankEntry);

            GUI.color = filled ? new Color(0.11f, 0.13f, 0.18f, 0.9f) : new Color(0.08f, 0.09f, 0.11f, 0.7f);
            GUI.DrawTexture(new Rect(x + 20f, y, w - 40f, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 名次徽标：第 1 名金色高亮，其余灰
            string rankTxt = i == 0 ? "冠军" : $"第 {i + 1} 名";
            GUI.Label(new Rect(x + 32f, y, w * 0.16f, h), rankTxt, i == 0 ? goldStyle : rowStyle);

            if (!filled)
            {
                GUI.Label(new Rect(x + w * 0.34f, y, w * 0.4f, h), "—— 待挑战 ——", dimStyle);
                return;
            }

            GUI.Label(new Rect(x + w * 0.24f, y, w * 0.2f, h), GameProgress.RankNameFor(e.Score), statusStyle);   // 单局分对应段位档
            GUI.Label(new Rect(x + w * 0.42f, y, w * 0.16f, h), $"{e.Score} 分", rowStyle);
            GUI.Label(new Rect(x + w * 0.58f, y, w * 0.14f, h), e.MapIndex == 1 ? "午夜超市" : "校园", rowStyle);
            if (e.Relic)
                GUI.Label(new Rect(x + w * 0.72f, y, w * 0.12f, h), "遗物", goldStyle);   // 金色徽标（不用 ◆：动态字体缺字形风险）
            GUI.Label(new Rect(x + w * 0.85f, y, w * 0.1f, h), e.Date, smallStyle);
        }
    }
}
