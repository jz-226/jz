using UnityEngine;
using Before8AM.Reward;
using Before8AM.Run;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.6] 商店面板（纯 OnGUI，挂 MenuController，MainMenuController 切 SubPanel.Shop 时 SetVisible(true)）。
    /// 永久金币买"下局消耗品"（规格书 25 商店）：购买写 PurchasedItems（PlayerPrefs 记账），
    /// 开局 RunManager.LoadPurchasedItems 注入背包（一次性，注入后清记录）。
    /// 购买价 = ItemCatalog.ShopPrice = 撤离折价 × 2（防套利）。
    /// 界面：干净列表式——徽章图标 + 名称/效果 + 价格 + 深色按钮，无行底方块/无 emoji（默认字体没有 emoji 字形）。
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        /// <summary>返回回调（MainMenuController 注入：关面板回主界面）。</summary>
        public System.Action OnBack;
        /// <summary>购买成功回调（主菜单可借此清掉旧 notice）。</summary>
        public System.Action OnPurchased;

        bool visible;
        string notice;
        float noticeTimer;
        Vector2 scrollPos;   // [0.8.0] 商店滚动位置（10 道具超一屏）

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

        bool Buy(RunItem item)
        {
            int price = ItemCatalog.ShopPrice(item);
            if (!GameProgress.TrySpendCoins(price))
            {
                notice = $"金币不足：{ItemCatalog.DisplayName(item)} 需要 {price} 金（当前 {GameProgress.PermanentCoins}）";
                noticeTimer = 3f;
                return false;
            }
            PurchasedItems.Set(item, PurchasedItems.Count(item) + 1);
            notice = $"已购买 {ItemCatalog.DisplayName(item)} x1（-{price} 金，余额 {GameProgress.PermanentCoins}）· 下局开局自带";
            noticeTimer = 3f;
            OnPurchased?.Invoke();
            return true;
        }

        void OnGUI()
        {
            if (!visible) return;
            float w = Screen.width, h = Screen.height;

            // 全屏暗遮罩（实，面板内干净）
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float panelW = Mathf.Min(w * 0.9f, 720f);
            float panelH = h * 0.9f;
            float px = (w - panelW) * 0.5f, py = (h - panelH) * 0.5f;
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 标题 + 余额
            var title = Label(0.04f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.93f, 0.7f));
            GUI.Label(new Rect(px, py + h * 0.02f, panelW, h * 0.055f), "商店", title);

            var bal = Label(0.025f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.85f, 0.4f));
            GUI.Label(new Rect(px, py + h * 0.075f, panelW, h * 0.035f), $"永久金币 {GameProgress.PermanentCoins}", bal);

            // [0.8.0] 10 道具超一屏 → 滚动列表（内容高 = Count × step；可视区到返回按钮上方）
            float rowW = panelW * 0.92f;
            float rowH = h * 0.145f;
            float step = rowH + h * 0.02f;
            float contentH = ItemCatalog.Count * step;
            float viewY = py + h * 0.125f;
            float viewH = py + panelH - h * 0.09f - viewY;
            scrollPos = GUI.BeginScrollView(new Rect(px, viewY, panelW, viewH), scrollPos,
                new Rect(0f, 0f, panelW, contentH));
            // [0.8.1] 内容坐标：ScrollView 内 0 = 视口左缘，绝不能加 px（屏幕偏移）→ 整行被裁出可视区
            for (int i = 0; i < ItemCatalog.Count; i++)
                DrawItemRow(panelW * 0.04f, i * step, rowW, rowH, (RunItem)i);
            GUI.EndScrollView();

            // 说明 + 提示（固定底部，滚动区上方不重叠）
            float tipY = py + panelH - h * 0.082f;
            var tip = Label(0.019f, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.65f, 0.7f, 0.78f));
            GUI.Label(new Rect(px, tipY, panelW, h * 0.028f), "购买的物品下局开局自带 · 撤离未用会折金币", tip);

            if (!string.IsNullOrEmpty(notice))
            {
                var ns = Label(0.019f, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 0.62f, 0.5f));
                GUI.Label(new Rect(px, tipY + h * 0.03f, panelW, h * 0.03f), notice, ns);
            }

            // 返回
            var btn = UiStyle.Btn(Mathf.RoundToInt(h * 0.024f));
            if (GUI.Button(new Rect(px + panelW * 0.34f, py + panelH - h * 0.06f, panelW * 0.32f, h * 0.05f), "返回主菜单", btn))
                OnBack?.Invoke();
        }

        void DrawItemRow(float x, float y, float w, float rowH, RunItem item)
        {
            // 图标徽章（彩圆 + 单字）
            float sw = rowH * 0.55f;
            Icon.Badge(new Rect(x, y + rowH * 0.2f, sw, sw), ItemColor(item), BadgeChar(item));

            // 名称 + 效果（徽章右侧，窄区防与价格重叠）
            float nameX = x + sw + rowH * 0.24f;
            // [0.8.1] 字号传"屏高比例"（Label 内部 ×Screen.height）：绝不能传 rowH×系数——
            // rowH 已是绝对像素，会被二次放大到 h² 级 → 文字溢出屏幕，价格整段被推到屏外不可见
            var nameStyle = Label(0.026f, TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            GUI.Label(new Rect(nameX, y + rowH * 0.1f, w * 0.34f, rowH * 0.28f), ItemCatalog.DisplayName(item), nameStyle);

            var effStyle = Label(0.018f, TextAnchor.MiddleLeft, FontStyle.Normal, new Color(0.7f, 0.75f, 0.82f));
            GUI.Label(new Rect(nameX, y + rowH * 0.42f, w * 0.36f, rowH * 0.24f), EffectText(item), effStyle);

            // 价格（右对齐到按钮左缘）+ 购买按钮
            var priceStyle = Label(0.02f, TextAnchor.MiddleRight, FontStyle.Bold, new Color(1f, 0.85f, 0.4f));
            GUI.Label(new Rect(x + w * 0.60f, y + rowH * 0.16f, w * 0.175f, rowH * 0.26f), $"{ItemCatalog.ShopPrice(item)} 金", priceStyle);

            var btn = UiStyle.Btn(Mathf.RoundToInt(rowH * 0.22f));
            if (GUI.Button(new Rect(x + w * 0.78f, y + rowH * 0.24f, w * 0.18f, rowH * 0.52f), "购买", btn))
                Buy(item);
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

        /// <summary>徽章单字（美术图标出来前，彩色圆 + 单字表示道具）。[0.8.1] 回退删 6 新道具。</summary>
        static string BadgeChar(RunItem item) => item switch
        {
            RunItem.Torch => "灯",
            RunItem.SpeedDrink => "速",
            RunItem.TimeHourglass => "沙",
            RunItem.InvisibilityPotion => "隐",
            _ => "?",
        };

        static string EffectText(RunItem item) => item switch
        {
            RunItem.Torch => "扩光圈",
            RunItem.SpeedDrink => "移速 x1.4",
            RunItem.TimeHourglass => "+20 秒",
            RunItem.InvisibilityPotion => "6 秒隐身",
            _ => "",
        };

        static Color ItemColor(RunItem item) => item switch
        {
            RunItem.Torch => new Color(0.95f, 0.55f, 0.2f),       // 灯油橙
            RunItem.SpeedDrink => new Color(0.4f, 0.85f, 0.4f),   // 加速绿
            RunItem.TimeHourglass => new Color(0.4f, 0.6f, 0.95f),// 沙漏蓝
            RunItem.InvisibilityPotion => new Color(0.7f, 0.5f, 0.9f), // 隐身紫
            _ => Color.gray,
        };
    }
}
