using System.Collections;
using UnityEngine;
using Before8AM.Run;

namespace Before8AM.Core
{
    /// <summary>
    /// 开场翻窗过场（规格书 119/20 的落地版）：挂"IntroCamera"（第一人称透视相机）。
    /// 玩家在**宿舍房间里**醒来（床/书桌/台灯/衣柜）→ 环视确认宿舍 → 从里往外推开窗扇（掀上去）→
    /// 爬出窗户 → 落地校园 → 黑场切回 2.5D 俯视 → 开始游玩（用户反馈：应从宿舍爬出，宿舍是独立空间）。
    /// 期间禁用 2.5D 主相机 / 玩家 / 迷雾（否则全黑），结束后恢复并启动 RunManager 计时。
    /// 替代 AutoStartRun（不再场景放置，避免双重 StartRun）。
    /// </summary>
    public class WindowIntro : MonoBehaviour
    {
        [Header("引用")]
        public UnityEngine.Camera MainCamera;   // 2.5D 主相机（过场期间禁用）
        public GameObject Player;               // 过场期间禁用，结束后启用
        public GameObject FogPlane;             // 探索迷雾暗幕（过场期间禁用，否则全黑）
        public RunManager Run;                  // 过场结束后 StartRun
        public WindowSash Sash;                 // 南墙窗扇：爬窗前"从里往外掀开"
        public IntroRules RulesGate;            // [0.4.2] 开场规则解说面板：等它 Dismissed 才播过场（未接线=不挡）

        [Header("翻窗路径（世界坐标，相机=眼睛）")]
        public Vector3 CamStart = new Vector3(0f, 1.5f, 43f);     // 宿舍房间里（房间 z 40~45.5，床在左桌在右）[0.3.0] 南墙 z=40
        public Vector3 WindowMid = new Vector3(0f, 1.65f, 39.7f); // 穿过窗洞（窗 y 0.85~2.15，眼睛 1.65 探入）
        public Vector3 LandSpot = new Vector3(0f, 1.5f, 36f);     // 落地（校园内，墙内 4m）

        // 宿舍环视视线目标（床 / 书桌台灯 / 窗）
        readonly Vector3 BedLook = new Vector3(-2.9f, 0.8f, 42.2f);
        readonly Vector3 DeskLook = new Vector3(2.9f, 1.0f, 42.0f);
        readonly Vector3 WindowLook = new Vector3(0f, 1.35f, 40f);

        float fadeAlpha = 1f;
        string hintText;
        float hintTimer;

        void Start()
        {
            EnsureIntroFurnitureDetail();
            if (MainCamera != null) MainCamera.gameObject.SetActive(false);
            if (FogPlane != null) FogPlane.SetActive(false);
            if (Player != null) Player.SetActive(false);
            StartCoroutine(IntroFlow());
        }

        /// <summary>
        /// 兼容已保存的旧场景：开场床和书桌从早期的几个基础方块升级为近景模型。
        /// 新版场景生成器已直接保存这些模型时会检测根节点并跳过，避免重复；旧场景则在淡入前完成替换。
        /// </summary>
        static void EnsureIntroFurnitureDetail()
        {
            if (GameObject.Find("DormRoom_Bed") != null || GameObject.Find("DormRoom_Desk") != null) return;

            Material frame = FindDormMaterial("DormRoom_Bed_Frame", new Color(0.28f, 0.30f, 0.40f));
            Material wood = FindDormMaterial("DormRoom_Desk_Top", new Color(0.42f, 0.30f, 0.18f));
            Material blanket = FindDormMaterial("DormRoom_Bed_Blanket", new Color(0.75f, 0.45f, 0.28f));
            Material paper = FindDormMaterial("DormRoom_Bed_Mattress", new Color(0.74f, 0.80f, 0.88f));
            Material lamp = FindDormMaterial("DormRoom_Desk_Lamp", new Color(1.00f, 0.76f, 0.25f));

            HideLegacyDormFurniture();
            CreateDetailedBed(new Vector3(-2.9f, 0f, 42.3f), frame, paper, blanket, wood);
            CreateDetailedDesk(new Vector3(2.9f, 0f, 42.0f), wood, frame, blanket, paper, lamp);
        }

        static void HideLegacyDormFurniture()
        {
            string[] legacyNames =
            {
                "DormRoom_Bed_Frame", "DormRoom_Bed_Mattress", "DormRoom_Bed_Blanket", "DormRoom_Bed_Pillow",
                "DormRoom_Desk_Top", "DormRoom_Desk_Leg", "DormRoom_Desk_Lamp", "DormRoom_Desk_LampGlow"
            };
            foreach (string objectName in legacyNames)
            {
                GameObject go = GameObject.Find(objectName);
                if (go != null) go.SetActive(false);
            }
        }

        static Material FindDormMaterial(string objectName, Color fallbackColor)
        {
            GameObject source = GameObject.Find(objectName);
            Renderer renderer = source != null ? source.GetComponent<Renderer>() : null;
            if (renderer != null && renderer.sharedMaterial != null) return renderer.sharedMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material fallback = new Material(shader) { color = fallbackColor };
            return fallback;
        }

        static GameObject DetailCube(Transform root, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = 2;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.transform.SetParent(root, true);
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return go;
        }

        static void CreateDetailedBed(Vector3 center, Material frame, Material sheet, Material blanket, Material accent)
        {
            GameObject root = new GameObject("DormRoom_Bed");
            root.layer = 2;
            Transform t = root.transform;
            DetailCube(t, "Bed_Base", center + new Vector3(0f, 0.38f, 0f), new Vector3(1.42f, 0.14f, 2.05f), frame);
            DetailCube(t, "Bed_Rail_L", center + new Vector3(-0.72f, 0.53f, 0f), new Vector3(0.12f, 0.32f, 2.12f), frame);
            DetailCube(t, "Bed_Rail_R", center + new Vector3(0.72f, 0.53f, 0f), new Vector3(0.12f, 0.32f, 2.12f), frame);
            DetailCube(t, "Bed_Headboard", center + new Vector3(0f, 0.93f, -1.05f), new Vector3(1.58f, 1.16f, 0.14f), frame);
            DetailCube(t, "Bed_Footboard", center + new Vector3(0f, 0.67f, 1.05f), new Vector3(1.58f, 0.64f, 0.14f), frame);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    DetailCube(t, "Bed_Leg_" + x + "_" + z, center + new Vector3(x * 0.62f, 0.20f, z * 0.92f), new Vector3(0.14f, 0.40f, 0.14f), frame);
            DetailCube(t, "Bed_Mattress", center + new Vector3(0f, 0.58f, 0f), new Vector3(1.34f, 0.22f, 1.93f), sheet);
            DetailCube(t, "Bed_Blanket", center + new Vector3(0f, 0.74f, 0.48f), new Vector3(1.32f, 0.15f, 0.90f), blanket);
            DetailCube(t, "Bed_BlanketFold", center + new Vector3(0f, 0.82f, 0.08f), new Vector3(1.32f, 0.08f, 0.13f), blanket);
            DetailCube(t, "Bed_Pillow_L", center + new Vector3(-0.34f, 0.75f, -0.63f), new Vector3(0.53f, 0.15f, 0.46f), sheet);
            DetailCube(t, "Bed_Pillow_R", center + new Vector3(0.34f, 0.75f, -0.63f), new Vector3(0.53f, 0.15f, 0.46f), sheet);
            DetailCube(t, "Bed_Book", center + new Vector3(0.30f, 0.85f, 0.20f), new Vector3(0.34f, 0.05f, 0.42f), accent);
        }

        static void CreateDetailedDesk(Vector3 center, Material wood, Material drawer, Material book, Material paper, Material lamp)
        {
            GameObject root = new GameObject("DormRoom_Desk");
            root.layer = 2;
            Transform t = root.transform;
            DetailCube(t, "Desk_Top", center + new Vector3(0f, 0.72f, 0f), new Vector3(1.62f, 0.14f, 0.96f), wood);
            DetailCube(t, "Desk_Apron", center + new Vector3(0f, 0.61f, 0.43f), new Vector3(1.52f, 0.13f, 0.08f), wood);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    DetailCube(t, "Desk_Leg_" + x + "_" + z, center + new Vector3(x * 0.71f, 0.34f, z * 0.38f), new Vector3(0.11f, 0.66f, 0.11f), wood);
            DetailCube(t, "Desk_DrawerUnit", center + new Vector3(0.55f, 0.34f, -0.02f), new Vector3(0.38f, 0.64f, 0.78f), drawer);
            for (int i = 0; i < 2; i++)
            {
                float y = 0.28f + i * 0.26f;
                DetailCube(t, "Desk_Drawer_" + i, center + new Vector3(0.55f, y, 0.39f), new Vector3(0.32f, 0.20f, 0.05f), wood);
                DetailCube(t, "Desk_Handle_" + i, center + new Vector3(0.55f, y, 0.425f), new Vector3(0.12f, 0.035f, 0.035f), lamp);
            }
            DetailCube(t, "Desk_Notebook", center + new Vector3(-0.20f, 0.81f, 0.02f), new Vector3(0.42f, 0.045f, 0.55f), book);
            DetailCube(t, "Desk_Paper", center + new Vector3(0.16f, 0.815f, -0.18f), new Vector3(0.34f, 0.025f, 0.44f), paper);
            DetailCube(t, "Desk_LampBase", center + new Vector3(-0.57f, 0.82f, -0.12f), new Vector3(0.28f, 0.06f, 0.28f), drawer);
            DetailCube(t, "Desk_LampStem", center + new Vector3(-0.57f, 1.03f, -0.12f), new Vector3(0.06f, 0.40f, 0.06f), drawer);
            GameObject arm = DetailCube(t, "Desk_LampArm", center + new Vector3(-0.49f, 1.20f, -0.12f), new Vector3(0.05f, 0.38f, 0.05f), drawer);
            arm.transform.rotation = Quaternion.Euler(0f, 0f, -28f);
            DetailCube(t, "Desk_Lampshade", center + new Vector3(-0.35f, 1.31f, -0.12f), new Vector3(0.28f, 0.20f, 0.28f), lamp);
            DetailCube(t, "Desk_LampGlow", center + new Vector3(-0.35f, 1.18f, -0.12f), new Vector3(0.20f, 0.08f, 0.20f), lamp);

            GameObject lightObject = new GameObject("DormRoom_Desk_Light");
            lightObject.layer = 2;
            lightObject.transform.position = center + new Vector3(-0.35f, 1.18f, -0.12f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.70f, 0.42f);
            light.intensity = 1.2f;
            light.range = 3.8f;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>[0.4.2] 门控：规则面板（如有）先展示，等 Dismissed/销毁后才播翻窗过场。
        /// 空引用安全：RulesGate 未接线（null）或已销毁 → 不挡，立即播过场（向后兼容）。
        /// 等待必须在整个 PlayIntro 之前——面板自己画全屏黑底，关闭瞬间 fadeAlpha 仍=1 纯黑，无缝衔接淡入，无闪黑。</summary>
        IEnumerator IntroFlow()
        {
            if (RulesGate != null)
                while (!RulesGate.Dismissed)
                    yield return null;
            yield return StartCoroutine(PlayIntro());
        }

        IEnumerator PlayIntro()
        {
            var cam = GetComponent<UnityEngine.Camera>();

            // 0) 宿舍醒来：相机在房间里，先看向床——黑场淡入，第一眼就是"宿舍"（用户反馈：要从宿舍爬出）
            transform.position = CamStart;
            transform.LookAt(BedLook);
            yield return StartCoroutine(Fade(1f, 0f, 0.6f));
            yield return new WaitForSeconds(0.35f);

            // 1) 环视宿舍：床 → 书桌台灯 → 窗（扫过地毯/衣柜，确认这是宿舍）
            yield return StartCoroutine(PanLook(BedLook, DeskLook, 0.8f));
            yield return StartCoroutine(PanLook(DeskLook, WindowLook, 0.8f));
            yield return new WaitForSeconds(0.35f);

            // 2) 窗扇"掀上去"——从里往外推，下缘挑高（爬窗时缺口已让开，露出干净窗洞）
            if (Sash != null) Sash.OpenNow();

            // 3) 爬窗：前探 + 略微抬升穿过窗洞（宽 1.8m，相机不再贴窄框，不闪）
            float t = 0f;
            const float climb = 1.6f;
            while (t < climb)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / climb);
                transform.position = Vector3.Lerp(CamStart, WindowMid, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.2f);
                transform.LookAt(Vector3.Lerp(WindowLook, new Vector3(0f, 1.4f, 32f), k));
                yield return null;
            }

            // 4) 落地：下探 + 落进校园
            t = 0f;
            const float drop = 0.8f;
            while (t < drop)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / drop);
                transform.position = Vector3.Lerp(WindowMid, LandSpot, k);
                transform.LookAt(Vector3.Lerp(new Vector3(0f, 1.4f, 32f), new Vector3(0f, 1.2f, 29f), k));
                yield return null;
            }

            // 5) 落地小颠簸 + 稍作停留（第一人称环视校园）
            yield return StartCoroutine(LandSettle());
            yield return new WaitForSeconds(0.6f);

            // 6) 黑场 → 切回 2.5D 俯视，恢复玩家/迷雾，开始游玩
            yield return StartCoroutine(Fade(0f, 1f, 0.5f));

            cam.enabled = false;                      // 关掉第一人称相机
            if (MainCamera == null) MainCamera = UnityEngine.Camera.main;   // [0.8.9c] 接线丢失兜底：主相机 tag 唯一，Camera.main 必指 2.5D 主相机
            if (MainCamera != null) MainCamera.gameObject.SetActive(true);
            if (Player != null) Player.SetActive(true);
            if (FogPlane != null) FogPlane.SetActive(true);

            yield return StartCoroutine(Fade(1f, 0f, 0.6f));

            if (Run != null) Run.StartRun();
            StartCoroutine(ShowHint("集齐 3 个时间碎片 · 赶在巡夜者发现前逃出晨门！", 4f));
        }

        /// <summary>平滑环视：视线从 from 渐变到 to（宿舍里床 → 书桌 → 窗，SmoothStep 无跳变）。</summary>
        IEnumerator PanLook(Vector3 from, Vector3 to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                transform.LookAt(Vector3.Lerp(from, to, k));
                yield return null;
            }
            transform.LookAt(to);
        }

        IEnumerator LandSettle()
        {
            float baseY = LandSpot.y;
            float t = 0f;
            const float dur = 0.5f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                float bounce = Mathf.Sin(k * Mathf.PI * 2f) * (1f - k) * 0.12f;   // 0→上→0，衰减
                Vector3 p = transform.position;
                p.y = baseY + Mathf.Abs(bounce);
                transform.position = p;
                yield return null;
            }
            Vector3 p2 = transform.position;
            p2.y = baseY;
            transform.position = p2;
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                fadeAlpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            fadeAlpha = to;
        }

        IEnumerator ShowHint(string text, float dur)
        {
            hintText = text;
            hintTimer = dur;
            while (hintTimer > 0f)
            {
                hintTimer -= Time.deltaTime;
                yield return null;
            }
            hintText = null;
        }

        void OnGUI()
        {
            // [0.4.5.1] 等规则面板时黑幕让位：IntroRules 面板自己画全屏黑底（IntroRules.DrawPanel 首行全屏黑），
            // 若本黑幕后画（多个 MonoBehaviour 的 OnGUI 执行顺序不保证，编译/场景重载后可能翻转），
            // 会把规则面板盖成纯黑 → 用户只见黑屏、不知要点「开始游戏」，IntroFlow 永远等 Dismissed（重开后黑屏根因）。
            // 等待期间不画黑幕/hint；面板关闭瞬间 fadeAlpha 仍 =1，无缝衔接 Fade(1,0,0.6) 淡入，无闪黑。
            bool waitingRules = RulesGate != null && !RulesGate.Dismissed;
            if (!waitingRules)
            {
                if (fadeAlpha > 0.001f)
                {
                    GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                if (!string.IsNullOrEmpty(hintText))
                {
                    var style = new GUIStyle(GUI.skin.label);
                    style.fontSize = Mathf.RoundToInt(Screen.height * 0.028f);
                    style.alignment = TextAnchor.LowerCenter;
                    style.fontStyle = FontStyle.Bold;
                    float alpha = Mathf.Clamp01(hintTimer < 0.5f ? hintTimer / 0.5f : 1f);
                    style.normal.textColor = new Color(1f, 0.92f, 0.7f, alpha);
                    GUI.Label(new Rect(0, Screen.height * 0.74f, Screen.width, Screen.height * 0.12f), hintText, style);
                }
            }
        }
    }
}
