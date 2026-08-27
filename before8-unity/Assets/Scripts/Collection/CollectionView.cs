using UnityEngine;

namespace Before8AM.Collection
{
    /// <summary>
    /// [0.4.5] 图鉴面板（纯 OnGUI，挂 RewardSystem 同物体；RewardSystem showCatalog 时 SetVisible(true)）。
    /// 渲染照 IntroRules.DrawSection 范式：色块 + 名称 + 描述 + 已收集✓/未收集???
    /// 返回 = 回调 OnBack（RewardSystem 置 showCatalog=false + SetVisible(false)），纯 bool 切换，无 LoadScene 问题。
    /// visible 默认 false：游玩时绝不整屏绘制。
    /// </summary>
    public class CollectionView : MonoBehaviour
    {
        /// <summary>返回结算回调（RewardSystem 注入）。</summary>
        public System.Action OnBack;

        /// <summary>[0.5] 返回按钮文案（结算=「返回结算」/主菜单=「返回主菜单」，两场景复用本组件）。</summary>
        public string BackLabel = "返回结算";

        bool visible;
        Vector2 scrollPos;   // [0.8.0] 33 条目超一屏 → 滚动
        GUIStyle titleStyle, headStyle, rowStyle, rowDimStyle, statusStyle, btnStyle;
        GUIStyle nameWrapStyle, descWrapStyle, descWrapDimStyle;   // [0.9.1] 换行变体：名称/描述超列宽换行（防溢出被状态列遮挡）
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
            titleStyle = MakeLabel(0.042f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.93f, 0.7f));
            headStyle  = MakeLabel(0.026f, TextAnchor.MiddleLeft,  FontStyle.Bold, new Color(1f, 0.85f, 0.5f));
            rowStyle   = MakeLabel(0.022f, TextAnchor.MiddleLeft,  FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f));
            rowDimStyle = MakeLabel(0.022f, TextAnchor.MiddleLeft, FontStyle.Normal, new Color(0.55f, 0.55f, 0.58f)); // 未收录：名称/描述变暗
            statusStyle = MakeLabel(0.024f, TextAnchor.MiddleRight, FontStyle.Bold, new Color(1f, 0.85f, 0.4f));      // ✓ 金黄
            btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = Mathf.RoundToInt(h * 0.026f);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            // [0.9.1] wordWrap 变体：GUI.skin.label 默认不换行，长文字会横向溢出到状态列被 ✓/??? 盖住
            nameWrapStyle = new GUIStyle(rowStyle) { wordWrap = true };
            descWrapStyle = new GUIStyle(rowStyle) { wordWrap = true };
            descWrapDimStyle = new GUIStyle(rowDimStyle) { wordWrap = true };
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

            float panelW = Mathf.Min(w * 0.86f, 1500f);
            float panelH = h * 0.9f;
            float px = (w - panelW) * 0.5f;
            float py = (h - panelH) * 0.5f;
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.96f);   // 居中深色面板
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(px, py + h * 0.02f, panelW, h * 0.055f), "午夜图鉴", titleStyle);
            GUI.Label(new Rect(px, py + h * 0.078f, panelW, h * 0.03f),
                $"已收录 {CollectionSystem.CollectedCount}/{CollectionSystem.TotalCount}", headStyle);

            // [0.8.0] 条目超一屏 → ScrollView（内容坐标相对视口原点；标题/返回固定）
            // [0.8.1] 回退：事件节已删（事件暂缓）→ 4 节标题
            // [0.9.1] 内容高按实际换行高度动态累计（节标题固定，条目行取 max(基准, 名称高, 描述高)）
            float rowH = h * 0.034f;
            float cx = h * 0.025f;
            float colW = panelW - h * 0.05f;
            float contentH = ComputeContentH(cx, colW, rowH) + rowH;   // 底部余量
            float innerTop = py + h * 0.118f;
            float innerH = py + panelH - h * 0.085f - innerTop;

            scrollPos = GUI.BeginScrollView(new Rect(px, innerTop, panelW, innerH), scrollPos,
                new Rect(0f, 0f, panelW, contentH));
            float y = 0f;
            y = DrawSection(cx, y, colW, rowH, "道具", CollectionCatalog.Items);
            y = DrawSection(cx, y, colW, rowH, "守卫", CollectionCatalog.Guards);
            y = DrawSection(cx, y, colW, rowH, "碎片", CollectionCatalog.Fragments);
            DrawSection(cx, y, colW, rowH, "遗物", CollectionCatalog.Relics);
            GUI.EndScrollView();

            float bw = panelW * 0.28f, bh = h * 0.055f;
            if (GUI.Button(new Rect(px + panelW * 0.5f - bw * 0.5f, py + panelH - bh - h * 0.015f, bw, bh), BackLabel, btnStyle))
                OnBack?.Invoke();
        }

        /// <summary>[0.9.1] 名称/描述列宽基准（与 DrawSection / ComputeContentH 共用同一套，保证测量与绘制一致）。</summary>
        void RowMetrics(float rowH, out float sw, out float nameW, out float gap, out float statusW, out float descW, float colW)
        {
            sw = rowH * 0.62f;
            nameW = rowH * 4.2f;   // 放宽到 ~4 汉字：长名称换行兜底，多数名称单行
            gap = rowH * 0.2f;
            statusW = rowH * 1.8f;
            descW = colW - sw - gap - nameW - statusW;
        }

        /// <summary>[0.9.1] 一行实际高度 = max(基准行高, 名称换行高度, 描述换行高度) + 少量内边距。
        /// 名称/描述都在各自列内换行，行高按内容自适应 → 任何分辨率都不溢出。</summary>
        float RowHeight(EntryInfo r, float nameW, float descW, float rowH)
        {
            bool unlocked = CollectionSystem.Has(r.Id);
            GUIStyle ns = unlocked ? nameWrapStyle : rowDimStyle;
            GUIStyle ds = unlocked ? descWrapStyle : descWrapDimStyle;
            return Mathf.Max(rowH, ns.CalcHeight(new GUIContent(r.Name), nameW), ds.CalcHeight(new GUIContent(r.Desc), descW)) + Screen.height * 0.004f;
        }

        /// <summary>[0.9.1] 4 节总高（节标题固定 rowH，条目行动态）——Render 用它算 ScrollView 内容高度。</summary>
        float ComputeContentH(float x, float colW, float rowH)
        {
            RowMetrics(rowH, out float sw, out float nameW, out float gap, out float statusW, out float descW, colW);
            float total = 0f;
            total += rowH; foreach (var r in CollectionCatalog.Items) total += RowHeight(r, nameW, descW, rowH);
            total += rowH; foreach (var r in CollectionCatalog.Guards) total += RowHeight(r, nameW, descW, rowH);
            total += rowH; foreach (var r in CollectionCatalog.Fragments) total += RowHeight(r, nameW, descW, rowH);
            total += rowH; foreach (var r in CollectionCatalog.Relics) total += RowHeight(r, nameW, descW, rowH);
            return total;
        }

        /// <summary>画一个 section：节标题 + 若干行（色块 + 名称 + 描述 + ✓/???）。照 IntroRules.DrawSection。
        /// [0.9.1] 行高按内容动态（名称/描述换行），色块在行内垂直居中。</summary>
        float DrawSection(float x, float y, float colW, float rowH, string head, EntryInfo[] rows)
        {
            GUI.Label(new Rect(x, y, colW, rowH), head, headStyle);
            y += rowH;

            RowMetrics(rowH, out float sw, out float nameW, out float gap, out float statusW, out float descW, colW);
            foreach (var r in rows)
            {
                bool unlocked = CollectionSystem.Has(r.Id);
                GUIStyle nameStyle = unlocked ? nameWrapStyle : rowDimStyle;
                GUIStyle descStyle = unlocked ? descWrapStyle : descWrapDimStyle;
                float rH = RowHeight(r, nameW, descW, rowH);

                // 色块：已收录填真实色（带浅描边，深黑/暗色可读），未收录灰块占位；行内垂直居中
                float cy = y + (rH - sw) * 0.5f;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(x - 1f, cy - 1f, sw + 2f, sw + 2f), Texture2D.whiteTexture);
                GUI.color = unlocked ? r.Swatch : new Color(0.22f, 0.22f, 0.26f, 0.7f);
                GUI.DrawTexture(new Rect(x, cy, sw, sw), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(x + sw + gap, y, nameW, rH), r.Name, nameStyle);
                GUI.Label(new Rect(x + sw + gap + nameW, y, descW, rH), r.Desc, descStyle);
                GUI.Label(new Rect(x + colW - statusW, y, statusW, rH),
                    unlocked ? "✓" : "???", unlocked ? statusStyle : rowDimStyle);
                y += rH;
            }
            return y;
        }
    }
}
