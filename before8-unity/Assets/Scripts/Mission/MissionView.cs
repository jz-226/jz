using System;
using UnityEngine;

namespace Before8AM.Mission
{
    /// <summary>
    /// [0.8.0] 每日任务 + 7 日挑战面板（纯 OnGUI，挂 MainMenuController 同物体）。
    /// 渲染：标题 + 5 行每日任务（名称/描述/进度/奖励/领取按钮）+ 7 日挑战区（第 N 天规则 + 状态 + 领奖）。
    /// visible 门控 + OnBack 回调（主菜单 ClosePanel 切换），照 CollectionView 范式。
    /// 每日任务奖励在按钮上直接入账（GameProgress.AddXP / AddPermanentCoins，MissionSystem.ClaimDaily）。
    /// </summary>
    public class MissionView : MonoBehaviour
    {
        public Action OnBack;
        public string BackLabel = "返回主菜单";

        bool visible;
        Vector2 scrollPos;   // 内容超一屏滚动
        string notice;
        float noticeTimer;

        GUIStyle titleStyle, headStyle, rowStyle, smallStyle, statusStyle, btnStyle, claimedStyle, dimStyle, noticeStyle;
        bool stylesReady;

        public void SetVisible(bool v)
        {
            visible = v;
            notice = null;
            noticeTimer = 0f;
        }

        void Update()
        {
            if (noticeTimer > 0f) noticeTimer -= Time.deltaTime;
        }

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
            headStyle   = MakeLabel(0.026f, TextAnchor.MiddleLeft,    FontStyle.Bold,   new Color(1f, 0.85f, 0.5f));
            rowStyle    = MakeLabel(0.024f, TextAnchor.MiddleLeft,    FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f));
            smallStyle  = MakeLabel(0.02f,  TextAnchor.MiddleLeft,    FontStyle.Normal, new Color(0.6f, 0.68f, 0.78f));
            statusStyle = MakeLabel(0.024f, TextAnchor.MiddleLeft,    FontStyle.Bold,   new Color(1f, 0.85f, 0.4f));
            noticeStyle = MakeLabel(0.022f, TextAnchor.MiddleCenter,  FontStyle.Bold,   new Color(1f, 0.9f, 0.6f));
            btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = Mathf.RoundToInt(h * 0.022f);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            claimedStyle = MakeLabel(0.022f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(0.6f, 0.9f, 0.6f));
            dimStyle     = MakeLabel(0.022f, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.55f, 0.55f, 0.58f));
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
            GUI.Label(new Rect(px, py + titleH * 0.15f, panelW, titleH), "每日任务 · 7 日挑战", titleStyle);

            // 滚动内容区
            float viewY = py + titleH * 0.15f + titleH + h * 0.015f;
            float viewH = panelH - (viewY - py) - h * 0.085f;
            float contentH = h * 0.88f;
            scrollPos = GUI.BeginScrollView(new Rect(px, viewY, panelW, viewH), scrollPos,
                new Rect(0f, 0f, panelW, contentH));

            float y = 0f;
            // [0.8.1] 内容坐标：ScrollView 内 0 = 视口左缘，绝不能加 px（屏幕偏移）
            GUI.Label(new Rect(30f, y, panelW - 40f, h * 0.05f), "每日任务（每天 0 点刷新）", headStyle);
            y += h * 0.055f;
            for (int i = 0; i < MissionSystem.TaskCount; i++)
            {
                DrawTaskRow(0f, y, panelW, h * 0.1f, i);
                y += h * 0.105f;
            }

            y += h * 0.02f;
            GUI.Label(new Rect(30f, y, panelW - 40f, h * 0.05f), "7 日挑战（连续 7 天 · 断签重来）", headStyle);
            y += h * 0.055f;
            DrawChallenge(0f, y, panelW, h * 0.24f);
            y += h * 0.25f;

            GUI.EndScrollView();

            // 返回按钮
            if (GUI.Button(new Rect(px + panelW * 0.35f, py + panelH - h * 0.075f, panelW * 0.3f, h * 0.05f), BackLabel, btnStyle))
                OnBack?.Invoke();

            // 领取提示
            if (notice != null && noticeTimer > 0f)
                GUI.Label(new Rect(px, py + panelH - h * 0.115f, panelW, h * 0.045f), notice, noticeStyle);
        }

        void DrawTaskRow(float x, float y, float w, float h, int i)
        {
            var task = MissionSystem.GetTask(i);
            int prog = MissionSystem.GetProgress(i);
            bool done = MissionSystem.IsDone(i);
            bool claimed = MissionSystem.IsClaimed(i);

            GUI.color = new Color(0.11f, 0.13f, 0.18f, 0.9f);
            GUI.DrawTexture(new Rect(x + 20f, y, w - 40f, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 40f, y + h * 0.1f, w * 0.26f, h * 0.42f), task.Name, rowStyle);
            GUI.Label(new Rect(x + 40f, y + h * 0.52f, w * 0.5f, h * 0.4f), task.Desc, smallStyle);
            GUI.Label(new Rect(x + w * 0.3f, y + h * 0.1f, w * 0.18f, h * 0.42f), $"{prog} / {task.Target}", statusStyle);
            GUI.Label(new Rect(x + w * 0.3f, y + h * 0.52f, w * 0.22f, h * 0.4f), $"奖励 {task.RewardCoins} 金 · {task.RewardXp} XP", smallStyle);

            float bx = x + w - w * 0.19f - 40f;
            float bw = w * 0.15f;
            if (done && !claimed)
            {
                if (GUI.Button(new Rect(bx, y + h * 0.15f, bw, h * 0.7f), "领取", btnStyle))
                {
                    var (xp, coins) = MissionSystem.ClaimDaily(i);
                    notice = $"领取成功：+{coins} 金币 · +{xp} XP";
                    noticeTimer = 3f;
                }
            }
            else if (claimed)
                GUI.Label(new Rect(bx, y + h * 0.15f, bw, h * 0.7f), "已领取", claimedStyle);
            else
                GUI.Label(new Rect(bx, y + h * 0.15f, bw, h * 0.7f), done ? "已完成" : "进行中", dimStyle);
        }

        void DrawChallenge(float x, float y, float w, float h)
        {
            bool complete = MissionSystem.ChallengeComplete;
            bool claimed = MissionSystem.ChallengeRewardClaimed;

            GUI.color = new Color(0.11f, 0.13f, 0.18f, 0.9f);
            GUI.DrawTexture(new Rect(x + 20f, y, w - 40f, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string dayTxt = claimed ? "7 日挑战 · 已完成" : $"第 {MissionSystem.ChallengeDay} / 7 天";
            GUI.Label(new Rect(x + 40f, y + h * 0.08f, w * 0.5f, h * 0.28f), dayTxt, rowStyle);
            GUI.Label(new Rect(x + 40f, y + h * 0.4f, w * 0.6f, h * 0.28f), $"今日目标：{MissionSystem.ChallengeRuleName}", smallStyle);

            string st;
            if (claimed) st = "限定外观已解锁";
            else if (complete) st = "7 日达成！可领取奖励";
            else if (MissionSystem.ChallengeDoneToday) st = "今日目标已完成 ✓";
            else if (MissionSystem.ChallengeTarget > 1) st = $"进度 {MissionSystem.ChallengeProgress} / {MissionSystem.ChallengeTarget}";
            else st = "今日目标未完成";
            GUI.Label(new Rect(x + w * 0.32f, y + h * 0.08f, w * 0.42f, h * 0.28f), st, statusStyle);

            float bx = x + w - w * 0.24f - 40f;
            if (complete && !claimed)
            {
                if (GUI.Button(new Rect(bx, y + h * 0.55f, w * 0.2f, h * 0.36f), "领取限定外观", btnStyle))
                {
                    if (MissionSystem.ClaimChallengeReward())
                    {
                        notice = "限定外观已解锁！（美术升级后生效）";
                        noticeTimer = 3f;
                    }
                }
            }
            else if (claimed)
                GUI.Label(new Rect(bx, y + h * 0.55f, w * 0.2f, h * 0.36f), "已解锁", claimedStyle);
        }
    }
}
