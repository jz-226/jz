using System;
using UnityEngine;
using Before8AM.Reward;
using Before8AM.Visual;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.9.0] 角色皮肤面板（纯 OnGUI，挂 MainMenuController 同物体，替换「我的宿舍」DormView）。
    /// 画廊：8 款皮肤逐行展示**真实 3D 方块小人预览**（独立相机渲染到 RenderTexture，身体=皮肤色，书包恒橙），
    /// 免费/金币购买/七日挑战三态；已装备存 GameProgress.EquippedSkin，进游戏 PlayerController.Awake 应用（Bag 恒橙）。
    /// visible 门控 + OnBack 回调（主菜单 ClosePanel 切换），照 DormView/MissionView 范式。
    /// </summary>
    public class SkinView : MonoBehaviour
    {
        public Action OnBack;
        public string BackLabel = "返回主菜单";

        bool visible;
        Vector2 scrollPos;
        string notice;
        float noticeTimer;

        // [0.9.0] 3D 预览：每款皮肤一个真实方块小人（独立相机渲染到 RenderTexture，替代 2D 剪影）
        struct Preview
        {
            public GameObject Root;
            public UnityEngine.Camera Cam;   // 全限定：Before8AM.Camera 是命名空间（CameraController），裸写 Camera 会 CS0118
            public RenderTexture Rt;
        }
        Preview[] previews;
        bool previewsReady;
        const int PreviewLayer = 31;   // 自定义层：主相机 cullingMask 排除，预览相机只渲染它

        GUIStyle titleStyle, goldStyle, rowStyle, smallStyle, statusStyle, btnStyle, dimStyle;
        bool stylesReady;

        public void SetVisible(bool v)
        {
            visible = v;
            notice = null;
            // 面板关闭时不渲染预览（省 8 次 RT）
            if (previews != null)
                for (int i = 0; i < previews.Length; i++)
                    if (previews[i].Cam != null) previews[i].Cam.enabled = v;
        }

        void Start()
        {
            EnsurePreviews();
        }

        void OnDestroy()
        {
            if (previews == null) return;
            for (int i = 0; i < previews.Length; i++)
            {
                if (previews[i].Rt != null) previews[i].Rt.Release();
                if (previews[i].Cam != null) Destroy(previews[i].Cam.gameObject);
                if (previews[i].Root != null) Destroy(previews[i].Root);
            }
            previews = null;
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
            goldStyle   = MakeLabel(0.028f, TextAnchor.MiddleCenter,  FontStyle.Bold,   new Color(1f, 0.88f, 0.55f));
            rowStyle    = MakeLabel(0.024f, TextAnchor.MiddleLeft,    FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f));
            smallStyle  = MakeLabel(0.02f,  TextAnchor.MiddleLeft,    FontStyle.Normal, new Color(0.6f, 0.68f, 0.78f));
            statusStyle = MakeLabel(0.022f, TextAnchor.MiddleCenter,  FontStyle.Bold,   new Color(1f, 0.85f, 0.4f));
            dimStyle    = MakeLabel(0.022f, TextAnchor.MiddleCenter,  FontStyle.Normal, new Color(0.55f, 0.55f, 0.58f));
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

            // 标题 + 金币余额
            float titleH = h * 0.06f;
            GUI.Label(new Rect(px, py + titleH * 0.2f, panelW, titleH), "角色皮肤", titleStyle);
            GUI.Label(new Rect(px, py + titleH * 0.2f + titleH, panelW, h * 0.038f), $"金币 {GameProgress.PermanentCoins}", goldStyle);

            // ---- 滚动内容：8 款皮肤 ----
            float viewY = py + titleH * 0.2f + titleH + h * 0.05f;
            float backTop = py + panelH - h * 0.075f;
            float viewH = backTop - h * 0.02f - viewY - (noticeTimer > 0f ? h * 0.05f : 0f);
            float rowH = h * 0.115f;
            float step = rowH + h * 0.012f;
            float contentH = 8 * step + h * 0.02f;
            scrollPos = GUI.BeginScrollView(new Rect(px, viewY, panelW, viewH), scrollPos,
                new Rect(0f, 0f, panelW, contentH));

            // [0.9.0] 内容坐标：ScrollView 内 0 = 视口左缘，绝不能加 px（屏幕偏移）
            float y = h * 0.01f;
            for (int i = 0; i < SkinCatalog.All.Length; i++)
            {
                DrawSkinRow(0f, y, panelW, rowH, i, SkinCatalog.All[i]);
                y += step;
            }

            GUI.EndScrollView();

            // 提示（金币不足 / 已购买 / 已装备，3s 淡出）
            if (noticeTimer > 0f && !string.IsNullOrEmpty(notice))
            {
                var ns = MakeLabel(0.02f, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 0.6f, 0.5f));
                GUI.Label(new Rect(px, viewY + viewH + h * 0.008f, panelW, h * 0.035f), notice, ns);
            }

            // 返回按钮
            if (GUI.Button(new Rect(px + panelW * 0.35f, backTop, panelW * 0.3f, h * 0.05f), BackLabel, btnStyle))
                OnBack?.Invoke();
        }

        void DrawSkinRow(float x, float y, float w, float h, int index, SkinCatalog.SkinDef def)
        {
            bool owned = SkinCatalog.IsOwned(def.Id);
            bool equipped = def.Id == SkinCatalog.ValidatedEquipped;
            bool sevenDay = def.Id == GameProgress.CosmeticSevenDay;

            // 行底（统一色 = 预览 RT 背景色，预览图与行无缝融合）
            GUI.color = new Color(0.11f, 0.13f, 0.18f, 0.9f);
            GUI.DrawTexture(new Rect(x + 20f, y, w - 40f, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // [0.9.0] 3D 预览：真实方块小人（RT 与行同底色，ScaleToFit 不拉伸）
            float boxH = h * 0.84f;
            if (previews != null && previews[index].Rt != null)
                GUI.DrawTexture(new Rect(x + 24f, y + h * 0.08f, boxH * 0.8f, boxH), previews[index].Rt, ScaleMode.ScaleToFit, false);

            // 名称 + 副文案（免费 / 价格 / 七日限定）
            float nameX = x + h * 0.9f;
            GUI.Label(new Rect(nameX, y + h * 0.12f, w * 0.42f, h * 0.28f), def.Name, rowStyle);
            string sub = sevenDay ? "7 日挑战限定 · 外观" : (def.Price <= 0 ? "免费" : $"{def.Price} 金购买");
            GUI.Label(new Rect(nameX, y + h * 0.46f, w * 0.42f, h * 0.28f), sub, smallStyle);

            // 右侧状态 / 按钮：使用中 / 装备 / 购买 / 未解锁
            float bx = x + w - w * 0.2f - 24f, bw = w * 0.16f, bh = h * 0.56f;
            Rect btnRect = new Rect(bx, y + h * 0.22f, bw, bh);
            if (equipped)
            {
                GUI.Label(btnRect, "使用中", statusStyle);
            }
            else if (owned)
            {
                if (GUI.Button(btnRect, "装备", btnStyle))
                {
                    GameProgress.EquippedSkin = def.Id;
                    notice = $"已装备「{def.Name}」，进入游戏即生效";
                    noticeTimer = 3f;
                }
            }
            else if (def.Price > 0)
            {
                if (GUI.Button(btnRect, $"购买 {def.Price}金", btnStyle)) TryBuy(def);
            }
            else
            {
                GUI.Label(btnRect, "未解锁", dimStyle);
            }
        }

        void TryBuy(SkinCatalog.SkinDef def)
        {
            // 金币不足预检（照 ShopController 范式：先查余额，再走 TryBuy 扣费+置位）
            if (GameProgress.PermanentCoins < def.Price)
            {
                notice = $"金币不足：「{def.Name}」需要 {def.Price} 金（当前 {GameProgress.PermanentCoins}）";
                noticeTimer = 3f;
                return;
            }
            if (SkinCatalog.TryBuy(def.Id))
            {
                notice = $"已购买「{def.Name}」（-{def.Price} 金，余额 {GameProgress.PermanentCoins}），点击装备生效";
                noticeTimer = 3f;
            }
        }

        // ---------- [0.9.0] 3D 预览：真实方块小人渲染到 RenderTexture ----------

        void EnsurePreviews()
        {
            if (previewsReady) return;
            previewsReady = true;
            var all = SkinCatalog.All;
            previews = new Preview[all.Length];

            // 主相机不渲染预览层（否则菜单背景里会看到 8 个小人）
            if (UnityEngine.Camera.main != null) UnityEngine.Camera.main.cullingMask &= ~(1 << PreviewLayer);

            // 书包材质（恒橙，= 游戏内 MAT_Bag）
            Material bagMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                name = "MAT_SkinPreview_Bag",
                color = new Color(0.85f, 0.55f, 0.20f),
            };

            for (int i = 0; i < all.Length; i++)
            {
                int id = all[i].Id;

                GameObject root = new GameObject("SkinPreview_" + id);
                SetLayer(root, PreviewLayer);
                BuildPreviewCharacter(root.transform, SkinCatalog.GetMaterial(id), bagMat);

                GameObject camGo = new GameObject("SkinPreviewCam_" + id);
                camGo.layer = PreviewLayer;
                var cam = camGo.AddComponent<UnityEngine.Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 0.78f;   // 小人纵向跨度 0.14~1.375，中心 0.76
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.11f, 0.13f, 0.18f, 1f);   // = 行底色，预览图与行无缝融合
                cam.cullingMask = 1 << PreviewLayer;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 10f;
                camGo.transform.position = new Vector3(0f, 0.76f, -3f);
                camGo.transform.LookAt(new Vector3(0f, 0.76f, 0f));
                cam.enabled = false;   // 面板打开才渲染（SetVisible 控制）
                var rt = new RenderTexture(160, 200, 24);
                cam.targetTexture = rt;

                previews[i] = new Preview { Root = root, Cam = cam, Rt = rt };
            }
        }

        /// <summary>搭一个静止站姿的方块小人（镜像 CreateOriginalBlockPlayer 的部件尺寸/位置，去动画关节）。
        /// 命名与游戏内一致：CharacterVisual.EnsurePartsRegistered 用同一套名字。</summary>
        static void BuildPreviewCharacter(Transform parent, Material bodyMat, Material bagMat)
        {
            Cube(parent, "Body",            new Vector3(0f, 0.80f, 0f),      new Vector3(0.42f, 0.55f, 0.26f), bodyMat);
            Cube(parent, "Head",            new Vector3(0f, 1.225f, 0f),     Vector3.one * 0.30f, bodyMat);
            Cube(parent, "LeftLeg",         new Vector3(-0.09f, 0.33f, 0f),  new Vector3(0.15f, 0.38f, 0.20f), bodyMat);
            Cube(parent, "RightLeg",        new Vector3(0.09f, 0.33f, 0f),   new Vector3(0.15f, 0.38f, 0.20f), bodyMat);
            Cube(parent, "LeftArm_Upper",   new Vector3(-0.24f, 0.94f, 0f),  new Vector3(0.10f, 0.22f, 0.10f), bodyMat);
            Cube(parent, "LeftArm_Forearm", new Vector3(-0.24f, 0.73f, 0f),  new Vector3(0.085f, 0.20f, 0.085f), bodyMat);
            Cube(parent, "LeftArm_Hand",    new Vector3(-0.24f, 0.63f, 0f),  Vector3.one * 0.15f, bodyMat);
            Cube(parent, "RightArm_Upper",  new Vector3(0.24f, 0.94f, 0f),   new Vector3(0.10f, 0.22f, 0.10f), bodyMat);
            Cube(parent, "RightArm_Forearm", new Vector3(0.24f, 0.73f, 0f),  new Vector3(0.085f, 0.20f, 0.085f), bodyMat);
            Cube(parent, "RightArm_Hand",   new Vector3(0.24f, 0.63f, 0f),   Vector3.one * 0.15f, bodyMat);
            Cube(parent, "Bag",             new Vector3(0f, 0.80f, -0.22f),  new Vector3(0.26f, 0.30f, 0.12f), bagMat);
        }

        static void Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());   // 全限定：using System 与 UnityEngine 的 Object 歧义
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayer(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
