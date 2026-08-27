using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Before8AM.Collection;
using Before8AM.Core;
using Before8AM.Reward;
using Before8AM.Mission;   // [0.8.0] 每日任务面板
using Before8AM.TapTap;    // [TapTap] TapTap 登录桥（TapPlay 上架要求）

namespace Before8AM.UI
{
    /// <summary>
    /// [0.6] 主菜单 v2（午夜氛围版，纯 OnGUI，挂 MainMenuBuilder 生成的 MenuController）：
    /// 深蓝夜空渐变 + 月光 + 星点 + 校园建筑剪影 + 校门剪影背景，标题发光；
    /// 规格书 6 入口：【翻窗进入】（主按钮）/ 午夜榜 / 角色皮肤 / 午夜图鉴 / 商店 / 设置。
    /// 地图卡保留 [0.5] 功能（午夜超市 ¥5000 金币解锁）。
    /// 商店/设置/图鉴 = 各自组件 visible 门控 + 本组件早退（OnGUI 多组件绘制顺序不保证，子面板自己画全屏遮罩+面板）。
    /// 开始撤离走 flag 模式（OnGUI 置 pendingLoad → Update 下一帧 LoadScene，不在渲染阶段切场景）。
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        /// <summary>[0.5] 午夜超市解锁价（用户确认均衡档：¥5000）。</summary>
        public const int ParkingPrice = 5000;

        enum SubPanel { None, Catalog, Shop, Settings, Tasks, Rank, Skin, RankTiers, Developing }

        SubPanel panel;
        string developingTitle, developingDesc;
        string notice;
        string pendingScene;   // [0.8.0] 待加载场景名（校园 Game / 停车场 Parking），OnGUI 置位 → Update 下一帧执行

        CollectionView collection;
        ShopController shop;
        SettingsController settings;
        MissionView mission;   // [0.8.0] 每日任务 + 7 日挑战面板
        MidnightRankView rankView;   // [0.8.0] 午夜榜面板
        SkinView skins;              // [0.9.0] 角色皮肤面板
        BootSplash bootSplash;       // [0.9.1] 启动过场（get-or-add，全程自管理）
        IntroComic introComic;       // [0.9.1] 首次启动开场漫画（BootSplash 播完接续）
        RankTiersView rankTiers;     // [0.9.2] 段位一览面板（段位表 + 达标分数）
        PrivacyPolicyPopup privacy;  // [TapTap 合规] 首次启动隐私政策弹窗（同意后才初始化 TapSDK）

        // 午夜氛围背景（Start 一次性生成，固定种子保证每次打开一致）
        Texture2D skyTex, glowTex, menuBackground;
        float[] starX, starY, starS;
        float[] bX, bW, bH;
        int[] bWinN;
        float[] bWinOff;   // 每建筑最多 3 窗的 x 偏移（相对建筑左缘 0~1）

        void Start()
        {
            // [0.9.4] 防御性复位：任何来源（设置退出/结算/重开）进主菜单都保证全局状态干净。
            // timeScale 残留 0 → BGM 淡入/过场用 deltaTime 冻结（静音/卡死感）；
            // InGameSettings.AnyOpen 残留 true → SFXManager 全局点击音被跳过（「点了没反应」错觉）；
            // Cursor.lockState 残留 Locked（首视模式退局）→ 光标钉在屏幕中心，主菜单偏置按钮点不到（桌面构建）。
            Time.timeScale = 1f;
            InGameSettings.AnyOpen = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            collection = GetComponent<CollectionView>() ?? gameObject.AddComponent<CollectionView>();
            collection.BackLabel = "返回主菜单";
            collection.OnBack = () => ClosePanel(SubPanel.Catalog);

            shop = GetComponent<ShopController>() ?? gameObject.AddComponent<ShopController>();
            shop.OnBack = () => ClosePanel(SubPanel.Shop);
            shop.OnPurchased = () => notice = null;   // 商店自己显示购买提示，主菜单清旧 notice

            settings = GetComponent<SettingsController>() ?? gameObject.AddComponent<SettingsController>();
            settings.OnBack = () => ClosePanel(SubPanel.Settings);

            mission = GetComponent<MissionView>() ?? gameObject.AddComponent<MissionView>();
            mission.OnBack = () => ClosePanel(SubPanel.Tasks);

            rankView = GetComponent<MidnightRankView>() ?? gameObject.AddComponent<MidnightRankView>();
            rankView.OnBack = () => ClosePanel(SubPanel.Rank);

            skins = GetComponent<SkinView>() ?? gameObject.AddComponent<SkinView>();
            skins.OnBack = () => ClosePanel(SubPanel.Skin);

            // [0.9.2] 段位一览面板（主菜单进度条右侧按钮打开）
            rankTiers = GetComponent<RankTiersView>() ?? gameObject.AddComponent<RankTiersView>();
            rankTiers.OnBack = () => ClosePanel(SubPanel.RankTiers);

            menuBackground = Resources.Load<Texture2D>("UI/MainMenuBackground");
            if (menuBackground == null) BuildBackdrop();   // 资源缺失时保留原夜景兜底，避免菜单空白

            // [TapTap 合规] 启动链：首次启动先弹隐私政策，用户点「同意」后才初始化 TapSDK + 播放启动过场。
            // 已同意过的老玩家直接初始化并正常走启动流程。
            if (PrivacyPolicyPopup.IsAccepted())
            {
                TapTapLoginBridge.Init();   // 初始化 SDK + 静默恢复上次登录态（失败不影响离线游戏本体）
                StartLaunchChain();
            }
            else
            {
                privacy = GetComponent<PrivacyPolicyPopup>() ?? gameObject.AddComponent<PrivacyPolicyPopup>();
                privacy.OnAccepted = () =>
                {
                    TapTapLoginBridge.Init();   // 点「同意」后才初始化 TapSDK（TapTap 审核要求）
                    StartLaunchChain();
                };
                privacy.Begin();   // 弹窗显示期间 BootSplash.Active 默认 true 挡住主菜单，仅弹窗可见
            }
        }

        /// <summary>[TapTap 合规] 播放启动过场（隐私政策同意后才调用；老玩家直接走这里）。
        /// 过场 get-or-add 挂到本物体（不动场景文件），全程自管理，播完放行主菜单。</summary>
        void StartLaunchChain()
        {
            bootSplash = GetComponent<BootSplash>() ?? gameObject.AddComponent<BootSplash>();
            introComic = GetComponent<IntroComic>() ?? gameObject.AddComponent<IntroComic>();
            bootSplash.OnFinished = () => { if (IntroComic.ShouldShow()) introComic.Begin(); };
        }

        void ClosePanel(SubPanel p)
        {
            panel = SubPanel.None;
            if (p == SubPanel.Catalog) collection?.SetVisible(false);
            if (p == SubPanel.Shop) shop?.SetVisible(false);
            if (p == SubPanel.Settings) settings?.SetVisible(false);
            if (p == SubPanel.Tasks) mission?.SetVisible(false);
            if (p == SubPanel.Rank) rankView?.SetVisible(false);   // [0.8.0] 午夜榜
            if (p == SubPanel.Skin) skins?.SetVisible(false);     // [0.9.0] 角色皮肤
            if (p == SubPanel.RankTiers) rankTiers?.SetVisible(false);   // [0.9.2] 段位一览
        }

        void OpenCatalog()  { panel = SubPanel.Catalog;  collection.SetVisible(true); }
        void OpenShop()     { panel = SubPanel.Shop;     shop.SetVisible(true); }
        void OpenSettings() { panel = SubPanel.Settings; settings.SetVisible(true); }
        void OpenTasks()    { panel = SubPanel.Tasks;    mission.SetVisible(true); }   // [0.8.0] 每日任务
        void OpenRank()     { panel = SubPanel.Rank;     rankView.SetVisible(true); }  // [0.8.0] 午夜榜
        void OpenSkins()    { panel = SubPanel.Skin;     skins.SetVisible(true); }     // [0.9.0] 角色皮肤
        void OpenRankTiers() { panel = SubPanel.RankTiers; rankTiers.SetVisible(true); }   // [0.9.2] 段位一览

        void ShowDeveloping(string title, string desc)
        {
            developingTitle = title;
            developingDesc = desc;
            panel = SubPanel.Developing;
        }

        void Update()
        {
            if (!string.IsNullOrEmpty(pendingScene))
            {
                string target = pendingScene;
                pendingScene = null;
                SceneManager.LoadScene(target);   // flag 模式：OnGUI 只置位，这里执行（[0.8.0] 支持校园/停车场两场景）
            }
        }

        void OnGUI()
        {
            if (PrivacyPolicyPopup.Active || BootSplash.Active || IntroComic.Active) return;   // [0.9.1][TapTap 合规] 隐私弹窗/启动过场/首次漫画播放中：主菜单整体不可见/不可点，播完放行
            if (panel == SubPanel.Catalog || panel == SubPanel.Shop || panel == SubPanel.Settings || panel == SubPanel.Tasks
                || panel == SubPanel.Rank || panel == SubPanel.Skin || panel == SubPanel.RankTiers) return;   // 子面板自己画
            DrawBackdrop();
            DrawMenuLogo();
            DrawMain();
            if (panel == SubPanel.Developing) DrawDevelopingOverlay();
        }

        void OnStartGame() => pendingScene = SceneNames.Game;

        void OnParkingClick()
        {
            // [0.8.0] 已解锁 → 直接进入停车场关卡；未解锁 → 尝试金币解锁（足额扣款成功 / 不足提示）
            if (GameProgress.IsMapUnlocked(GameProgress.MapParkingIndex))
            {
                pendingScene = SceneNames.Parking;
                notice = null;
                return;
            }
            if (GameProgress.TryUnlockMap(GameProgress.MapParkingIndex, ParkingPrice))
            {
                notice = null;
                Debug.Log($"[主菜单] 午夜超市已解锁！剩余金币 {GameProgress.PermanentCoins}");
            }
            else
            {
                notice = $"金币不足：解锁午夜超市需要 {ParkingPrice} 金（当前 {GameProgress.PermanentCoins}）";
            }
        }

        void DrawMain()
        {
            float w = Screen.width, h = Screen.height;
            float cx = w * 0.5f;
            float panelW = Mathf.Min(w * 0.92f, 680f);
            float px = (w - panelW) * 0.5f;

            // 标题（暖金大字 + 柔和阴影 + 金色分隔线，替代会糊的多层发光）+ 副题
            float titleY = h * 0.045f;
            DrawTitle(px, panelW, titleY);

            var sub = Label(0.02f, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.72f, 0.78f, 0.85f));
            GUI.Label(new Rect(px, titleY + h * 0.11f, panelW, h * 0.028f), "潜行 · 收集 · 在黎明前逃出校园", sub);

            // 金币 / 等级 / 段位（[0.9.2] 顶部四层【副标题→金币行→进度条→主按钮】间距全面拉开，
            // 字号 ≤ 行高，杜绝 IMGUI 文字渲染内边距溢出互相覆盖）
            var money = Label(0.024f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.85f, 0.4f));
            GUI.Label(new Rect(px, titleY + h * 0.145f, panelW, h * 0.026f),
                $"金币 {GameProgress.PermanentCoins}   ·   Lv.{GameProgress.Level} · {GameProgress.RankName}", money);

            // [0.9.2] 段位表按钮（行右侧：点开看全部段位/达标分数——给玩家目标感）
            var tierBtn = UiStyle.Btn(Mathf.RoundToInt(h * 0.015f));
            float tbW = panelW * 0.13f;
            if (GUI.Button(new Rect(px + panelW - tbW, titleY + h * 0.145f, tbW, h * 0.026f), "段位表", tierBtn))
                OpenRankTiers();

            // [0.9.2] 段位进度条：总进程（0 → 午夜王者 8000）+ 右侧标注距下一段位还差多少分
            DrawRankProgress(px, panelW, titleY + h * 0.179f, h);

            // 主按钮：翻窗进入（金色高亮居中）
            var startBtn = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(h * 0.034f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            startBtn.normal.textColor = new Color(0.16f, 0.10f, 0.03f);
            float sbW = panelW * 0.52f, sbH = h * 0.085f;
            float sbX = cx - sbW * 0.5f, sbY = titleY + h * 0.213f;   // [0.9.2] 主按钮下移：与进度条文字留 ≥0.014h 安全间隙
            GUI.color = new Color(1f, 0.82f, 0.35f);
            if (GUI.Button(new Rect(sbX, sbY, sbW, sbH), "翻窗进入", startBtn)) OnStartGame();
            GUI.color = Color.white;

            // 地图卡片（两张并排）
            float cardY = sbY + sbH + h * 0.022f;
            float cardGap = h * 0.016f;
            float cardW = (panelW - cardGap) * 0.5f;
            float cardH = h * 0.15f;
            DrawMapCardV(px, cardY, cardW, cardH,
                "午夜校园", "已解锁", "开始撤离", true, OnStartGame);
            DrawMapCardV(px + cardW + cardGap, cardY, cardW, cardH,
                "午夜超市",
                GameProgress.IsMapUnlocked(GameProgress.MapParkingIndex) ? "已解锁" : $"锁定 · {ParkingPrice} 金",
                GameProgress.IsMapUnlocked(GameProgress.MapParkingIndex) ? "进入" : "解锁",
                GameProgress.IsMapUnlocked(GameProgress.MapParkingIndex), OnParkingClick);

            // 金币不足提示
            if (!string.IsNullOrEmpty(notice))
            {
                var ns = Label(0.02f, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 0.6f, 0.5f));
                GUI.Label(new Rect(px, cardY + cardH + h * 0.012f, panelW, h * 0.03f), notice, ns);
            }

            // [0.9.0] 功能 3×2：午夜图鉴 / 商店 / 每日任务 / 角色皮肤 / 设置 / 午夜榜
            float fnY = cardY + cardH + h * 0.058f;
            float fnGap = h * 0.014f;
            float fnW = (panelW - fnGap) * 0.5f;
            float fnH = h * 0.09f;
            var fnBtn = UiStyle.Btn(Mathf.RoundToInt(h * 0.024f));
            // [0.9.0] 午夜榜/角色皮肤做真（规格书 85：翻窗进入 / 午夜榜 / 我的宿舍 / 午夜图鉴 / 商店 / 设置）
            DrawFnGrid(px, fnY, fnW, fnH, fnGap, fnBtn,
                new[] { "午夜图鉴", "商店", "每日任务", "角色皮肤", "设置", "午夜榜" },
                new Action[] { OpenCatalog, OpenShop, OpenTasks, OpenSkins, OpenSettings, OpenRank });

            // 底部：[TapTap 登录] [退出] 并排（[TapTap] TapPlay 上架要求接入 TapTap 登录）
            var botBtn = UiStyle.Btn(Mathf.RoundToInt(h * 0.023f));
            float bbH = h * 0.058f, bbY = h * 0.93f, bbGap = h * 0.014f;
            float loginW = panelW * 0.42f;
            string loginLabel = TapTapLoginBridge.IsLoggedIn
                ? $"TapTap ✓ {Truncate(TapTapLoginBridge.DisplayName, 6)}"
                : (TapTapLoginBridge.IsBusy ? "TapTap 账号恢复中…" : "TapTap 登录");
            if (GUI.Button(new Rect(px, bbY, loginW, bbH), loginLabel, botBtn)) OnTapTapLoginClicked();
            if (GUI.Button(new Rect(px + loginW + bbGap, bbY, panelW - loginW - bbGap, bbH), "退出", botBtn))
            {
                if (!Application.isEditor) Application.Quit();
                else Debug.Log("[主菜单] 退出（编辑器下不真退出）");
            }
        }

        /// <summary>[TapTap] 底部登录按钮：已登录→登出；未登录→发起 TapTap 授权登录。</summary>
        async void OnTapTapLoginClicked()
        {
            if (TapTapLoginBridge.IsLoggedIn)
            {
                TapTapLoginBridge.Logout();
                return;
            }
            bool ok = await TapTapLoginBridge.LoginAsync();
            if (!ok) notice = "TapTap 登录未完成（取消或失败），可离线继续玩";
        }

        static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLen) return s;
            return s.Substring(0, maxLen) + "…";
        }

        /// <summary>[0.9.2] 段位进度条：金色填充 = 段位总进程（0 → 午夜王者门槛），
        /// 左侧段位分，右侧「距下一段位还差 N 分」（王者已登顶显示提示）。
        /// 与午夜榜进度条同基准（GameProgress.RankKingThreshold），配色一致。</summary>
        void DrawRankProgress(float px, float panelW, float y, float h)
        {
            // [0.9.2] 两侧文字区宽度按最长文案配（左「段位分 9999」9 字 / 右「距 夜行者 I 还差 330 分」12 字），
            // 字号 0.013f 保证文案在 rect 内放下，不向左溢出盖进度条
            float barH = h * 0.014f;
            float leftW = panelW * 0.18f, rightW = panelW * 0.31f;
            float barX = px + leftW, barW = panelW - leftW - rightW;

            // 左侧：累计段位分
            var ls = Label(0.013f, TextAnchor.MiddleLeft, FontStyle.Normal, new Color(0.6f, 0.68f, 0.78f));
            GUI.Label(new Rect(px, y, leftW, barH + h * 0.006f), $"段位分 {GameProgress.RankScore}", ls);

            // 条：深灰底 + 金色填充（比例 = 累计分 / 王者门槛）
            GUI.color = new Color(0.22f, 0.22f, 0.28f, 0.9f);
            GUI.DrawTexture(new Rect(barX, y, barW, barH), Texture2D.whiteTexture);
            float p = Mathf.Clamp01((float)GameProgress.RankScore / GameProgress.RankKingThreshold);
            if (p > 0.001f)
            {
                GUI.color = new Color(1f, 0.82f, 0.4f);
                GUI.DrawTexture(new Rect(barX, y, barW * p, barH), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            // 右侧：距下一段位还差多少分（王者 = 已登顶）
            GameProgress.RankTierBounds(out _, out int nextT, out string nextName);
            var rs = Label(0.013f, TextAnchor.MiddleRight, FontStyle.Normal, new Color(0.85f, 0.88f, 0.95f));
            string txt = nextT < 0
                ? "已登顶 · 午夜王者"
                : $"距 {nextName} 还差 {nextT - GameProgress.RankScore} 分";
            GUI.Label(new Rect(px + leftW + barW, y, rightW, barH + h * 0.006f), txt, rs);
        }

        void DrawFnGrid(float x, float y, float w, float h, float gap, GUIStyle style, string[] labels, Action[] actions)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i / 2, col = i % 2;
                Rect r = new Rect(x + col * (w + gap), y + row * (h + gap), w, h);
                if (GUI.Button(r, labels[i], style)) actions[i]();
            }
        }

        void DrawMapCardV(float x, float y, float w, float h, string name, string status, string btnLabel, bool unlocked, Action onClick)
        {
            GUI.color = unlocked ? new Color(0.12f, 0.15f, 0.21f, 0.95f) : new Color(0.09f, 0.09f, 0.12f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var ns = Label(0.026f, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            GUI.Label(new Rect(x, y + h * 0.08f, w, h * 0.26f), name, ns);

            var ss = Label(0.018f, TextAnchor.MiddleCenter, FontStyle.Normal,
                unlocked ? new Color(0.6f, 0.9f, 0.6f) : new Color(1f, 0.75f, 0.35f));
            GUI.Label(new Rect(x, y + h * 0.4f, w, h * 0.18f), status, ss);

            var b = UiStyle.Btn(Mathf.RoundToInt(h * 0.18f));
            if (GUI.Button(new Rect(x + w * 0.14f, y + h * 0.62f, w * 0.72f, h * 0.28f), btnLabel, b))
                onClick?.Invoke();
        }

        // ---------- 午夜氛围背景 ----------

        void BuildBackdrop()
        {
            // 天空渐变 1×64（顶深蓝 → 地平线微紫，拉伸全屏，Bilinear 平滑）
            skyTex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            skyTex.wrapMode = TextureWrapMode.Clamp;
            skyTex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f;
                Color c = Color.Lerp(new Color(0.006f, 0.014f, 0.055f), new Color(0.07f, 0.12f, 0.21f), t);
                if (t > 0.82f) c = Color.Lerp(c, new Color(0.11f, 0.09f, 0.19f), (t - 0.82f) / 0.18f);
                skyTex.SetPixel(0, y, c);
            }
            skyTex.Apply();

            // 月光光晕 64×64 径向（中心亮 → 边缘透明，平方衰减更柔和）
            glowTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            glowTex.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < 64; x++)
            {
                for (int y = 0; y < 64; y++)
                {
                    float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a *= a;
                    glowTex.SetPixel(x, y, new Color(1f, 0.95f, 0.82f, a * 0.5f));
                }
            }
            glowTex.Apply();

            // 星点（固定种子：菜单每次打开星位不变）
            var rng = new System.Random(20260823);
            int n = 70;
            starX = new float[n]; starY = new float[n]; starS = new float[n];
            for (int i = 0; i < n; i++)
            {
                starX[i] = (float)rng.NextDouble();
                starY[i] = (float)rng.NextDouble() * 0.62f;
                starS[i] = 1f + (float)rng.NextDouble() * 1.8f;
            }

            // 建筑剪影（底部，深蓝黑 + 暖黄窗点）
            int b = 14;
            bX = new float[b]; bW = new float[b]; bH = new float[b];
            bWinN = new int[b]; bWinOff = new float[b * 3];
            float cursor = 0.03f;
            for (int i = 0; i < b; i++)
            {
                float bw = 0.035f + (float)rng.NextDouble() * 0.08f;
                bX[i] = cursor; bW[i] = bw;
                bH[i] = 0.06f + (float)rng.NextDouble() * 0.15f;
                bWinN[i] = 1 + rng.Next(3);
                float ox = 0.2f;
                for (int k = 0; k < bWinN[i]; k++)
                {
                    bWinOff[i * 3 + k] = ox;
                    ox += 0.22f;
                }
                cursor += bw + 0.01f + (float)rng.NextDouble() * 0.02f;
            }
        }

        void DrawBackdrop()
        {
            float w = Screen.width, h = Screen.height;

            // 正式菜单背景：保持原图构图，按不同屏幕比例等比裁切，不拉伸校园与天空。
            if (menuBackground != null)
            {
                GUI.DrawTexture(new Rect(0, 0, w, h), menuBackground, ScaleMode.ScaleAndCrop, true);
                // 轻微夜紫蒙层只为确保浅色天空上的菜单文字有足够对比度。
                GUI.color = new Color(0.045f, 0.025f, 0.090f, 0.18f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }

            // 夜空渐变
            GUI.DrawTexture(new Rect(0, 0, w, h), skyTex);

            // 月光（右上）+ 校门方向微弱暖光（中央偏下，提示门后有校园）
            float moonR = h * 0.17f;
            GUI.DrawTexture(new Rect(w * 0.78f - moonR, h * 0.1f - moonR, moonR * 2f, moonR * 2f), glowTex);
            float gateGlowR = h * 0.28f;
            GUI.color = new Color(1f, 0.85f, 0.6f, 0.10f);
            GUI.DrawTexture(new Rect(w * 0.5f - gateGlowR, h * 0.62f - gateGlowR * 1.2f, gateGlowR * 2f, gateGlowR * 2f), glowTex);
            GUI.color = Color.white;

            // 星点（大小 1~3px，微闪）
            for (int i = 0; i < starX.Length; i++)
            {
                float s = starS[i];
                float flicker = 0.6f + 0.35f * (float)Math.Sin(i * 3.7f);
                GUI.color = new Color(0.95f, 0.97f, 1f, flicker);
                GUI.DrawTexture(new Rect(starX[i] * w, starY[i] * h, s, s), Icon.Circle());   // 圆点星，比方块点柔和
            }
            GUI.color = Color.white;

            // 建筑剪影（地平线 h*0.62）
            float horizon = h * 0.62f;
            for (int i = 0; i < bX.Length; i++)
            {
                float bh = bH[i] * h;
                Rect r = new Rect(bX[i] * w, horizon - bh, bW[i] * w, bh);
                GUI.color = new Color(0.015f, 0.025f, 0.055f, 0.96f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                for (int k = 0; k < bWinN[i]; k++)
                {
                    Rect wr = new Rect(r.x + bWinOff[i * 3 + k] * r.width,
                        r.y + r.height * (0.3f + 0.28f * k),
                        Mathf.Max(2f, w * 0.008f), Mathf.Max(2f, w * 0.011f));
                    GUI.color = new Color(1f, 0.78f, 0.45f, 0.5f);
                    GUI.DrawTexture(wr, Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;

            // 校门剪影（中央，门洞透夜空微暖）
            float gw = w * 0.11f, gh = h * 0.17f;
            float gx = w * 0.5f - gw * 0.5f, gy = horizon - gh;
            float post = w * 0.012f;
            GUI.color = new Color(0.01f, 0.02f, 0.05f, 0.97f);
            GUI.DrawTexture(new Rect(gx, gy, post, gh), Texture2D.whiteTexture);                 // 左柱
            GUI.DrawTexture(new Rect(gx + gw - post, gy, post, gh), Texture2D.whiteTexture);     // 右柱
            GUI.DrawTexture(new Rect(gx - post * 0.5f, gy - post * 0.8f, gw + post, post * 1.2f), Texture2D.whiteTexture); // 横梁
            GUI.color = Color.white;
        }

        void DrawDevelopingOverlay()
        {
            float w = Screen.width, h = Screen.height;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.96f);

            float panelW = Mathf.Min(w * 0.72f, 560f);
            float panelH = h * 0.4f;
            float px = (w - panelW) * 0.5f, py = (h - panelH) * 0.5f;
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var t = Label(0.034f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.93f, 0.7f));
            GUI.Label(new Rect(px, py + panelH * 0.1f, panelW, panelH * 0.2f), developingTitle + " · 开发中", t);

            var s = Label(0.022f, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.75f, 0.8f, 0.85f));
            GUI.Label(new Rect(px, py + panelH * 0.36f, panelW, panelH * 0.24f), developingDesc, s);

            var btn = UiStyle.Btn(Mathf.RoundToInt(h * 0.024f));
            if (GUI.Button(new Rect(px + panelW * 0.35f, py + panelH * 0.68f, panelW * 0.3f, panelH * 0.18f), "返回", btn))
                panel = SubPanel.None;
        }

        // ---------- 工具 ----------

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

        /// <summary>标题「早八在逃」：暖金大字 + 柔和下阴影（比多层半透明字垫底更干净），下方金色分隔线 + 两侧亮点。</summary>
        void DrawMenuLogo()
        {
            float w = Screen.width, h = Screen.height;
            float margin = Mathf.Clamp(h * 0.022f, 16f, 28f);
            float size = Mathf.Clamp(Mathf.Min(w * 0.20f, h * 0.23f), 132f, 244f);
            UiStyle.DrawOfficialLogo(new Rect(w - margin - size, margin, size, size));
        }

        void DrawTitle(float px, float panelW, float titleY)
        {
            float h = Screen.height;
            int fs = Mathf.RoundToInt(h * 0.075f);
            float rH = h * 0.08f;

            var shadow = new GUIStyle(GUI.skin.label) { fontSize = fs, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            shadow.normal.textColor = new Color(0f, 0f, 0f, 0.45f);
            GUI.Label(new Rect(px + 3f, titleY + 3f, panelW, rH), "早八在逃", shadow);

            var core = new GUIStyle(GUI.skin.label) { fontSize = fs, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            core.normal.textColor = new Color(1f, 0.88f, 0.6f);
            GUI.Label(new Rect(px, titleY, panelW, rH), "早八在逃", core);

            // 金色分隔线 + 两侧亮点（[0.9.2] lineY 移到标题下 0.02h：之前 4px 会横穿副标题文字 = 字体被遮挡）
            float lineW = panelW * 0.46f;
            float lineX = px + (panelW - lineW) * 0.5f;
            float lineY = titleY + rH + h * 0.02f;
            GUI.color = new Color(1f, 0.82f, 0.45f, 0.55f);
            GUI.DrawTexture(new Rect(lineX, lineY, lineW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float dot = h * 0.01f;
            GUI.color = new Color(1f, 0.9f, 0.6f, 0.8f);
            GUI.DrawTexture(new Rect(lineX - dot * 1.4f, lineY - dot * 0.45f, dot, dot), Icon.Circle());
            GUI.DrawTexture(new Rect(lineX + lineW + dot * 0.4f, lineY - dot * 0.45f, dot, dot), Icon.Circle());
            GUI.color = Color.white;
        }
    }
}
