using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Before8AM.Audio;
using Before8AM.Camera;
using Before8AM.Player;
using Before8AM.Run;
using Before8AM.UI;
using Before8AM.World;

namespace Before8AM.Input
{
    /// <summary>
    /// [0.5] 手游触屏控制（挂 Player 同物体）：左半屏虚拟摇杆走位 + 右下 加速⚡/互动/跳跃 三圆键 + 右上 设置/视角按钮 + 道具格。
    /// [0.9.2] 首视右半屏空白区滑动转视角由 ViewToggle 接管：本组件只占用键位触摸，IsCapturedTouch 供其跳过。
    /// 命中计算在 Update 用 Input System Polling API（Touchscreen / Mouse 模拟）逐指自算，
    /// OnGUI 只画——规避 IMGUI 按钮在手游多指点击失效（GUI.Button 只响应主触摸指针）。
    /// 仅 RunState.Running 绘制（Intro 过场期间 Player 被禁，本组件随 Player 一并禁用）。
    /// PC 键鼠（WASD+Shift+数字键+E）完全保留；SimulateWithMouse 供编辑器用鼠标验证布局。
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        [Header("调试")]
        [Tooltip("PC/编辑器用鼠标左键模拟触摸（验证手游布局）")]
        public bool SimulateWithMouse = true;

        public bool Enabled => Application.isMobilePlatform || SimulateWithMouse;

        PlayerController pc;
        InteractionSystem inter;
        RunManager run;

        // 摇杆（[0.9.2] 动态浮动：玩家要求「点击左半屏任意处才出现方框、可拖动、松手消失，平时左侧留空」）
        const float JoyRadius = 130f;
        Rect joyZone;              // 左半屏接管区
        int joyTouch = -1;         // 占用摇杆的触摸 id（-1 = 无）
        Vector2 joyOrigin;         // 摇杆出现位置 = 手指落点
        Vector2 joyVector;

        // 按钮（[0.9.2] 放大：真机反馈 UI 太小）
        Vector2 interactCenter;    // [0.9.2] 圆形互动键中心（三键中间）
        Vector2 runCenter;         // [0.9.2] 圆形加速键中心（⚡ 图标，按住 = 奔跑）
        Vector2 jumpCenter;        // [0.9.2] 圆形跳跃键中心
        float interactRadius;      // [0.9.2] 三个圆形键共用半径
        Rect settingsRect;         // [0.9.2] 右上角设置按钮（手机没有 Esc，替代键）
        Rect viewRect;             // [0.9.2] 右上角视角切换按钮（第一人称 ↔ 2.5D）
        Texture2D circleTex;       // [0.9.2] 运行时生成的圆形纹理（画圆形按钮）
        Texture2D lightningTex;    // [0.9.2+] 运行时生成的闪电纹理（加速键图标，⚡ 字形 IMGUI 内置字体缺失 → 空白）
        readonly Rect[] itemRects = new Rect[ItemCatalog.Count];
        int interactTouch = -1, runTouch = -1, jumpTouch = -1, settingsTouch = -1, viewTouch = -1;
        readonly int[] itemTouches = new int[ItemCatalog.Count];

        // [0.9.2] 道具使用反馈（toast 提示）：点道具按钮有明确回显，不「无声无息」
        float useToastTimer;
        string useToastText;

        InGameSettings settings;   // [0.9.2] 设置面板（打开时本组件停止响应触摸）
        ViewToggle viewToggle;     // [0.9.2] 视角切换（第一人称 ↔ 2.5D，同 V 键）

        struct TouchPoint
        {
            public int id;
            public Vector2 pos;
            public bool began;
            public bool ended;
        }
        readonly List<TouchPoint> points = new List<TouchPoint>();

        void Start()
        {
            pc = GetComponent<PlayerController>();
            inter = GetComponent<InteractionSystem>();
            run = RunManager.Instance;
            // [0.9.2] 设置面板（InGameSettings 挂 RewardSystem 同物体，场景内单例）
            settings = UnityEngine.Object.FindObjectOfType<InGameSettings>();
            // [0.9.2] 视角切换（ViewToggle 挂主相机，场景内单例）
            viewToggle = UnityEngine.Object.FindObjectOfType<ViewToggle>();
            for (int i = 0; i < itemTouches.Length; i++) itemTouches[i] = -1;
        }

        void Update()
        {
            if (useToastTimer > 0f) useToastTimer -= Time.deltaTime;   // [0.9.2] 道具使用 toast 计时
            if (!Enabled) return;
            if (pc == null) pc = GetComponent<PlayerController>();
            if (run == null) run = RunManager.Instance;
            // [0.9.2] InGameSettings 是 RewardSystem.Start 运行期 get-or-add 挂载的，Start 里一次性找可能顺序反了
            // （RewardSystem 挂 rewardGo、本组件挂 Player，跨对象 Start 顺序不保证）→ 每帧兜底补查，找到即缓存。
            if (settings == null) settings = UnityEngine.Object.FindObjectOfType<InGameSettings>();
            if (pc == null) return;

            // [0.9.3+] 结算/失败期间不响应触摸（与 OnGUI 门控对齐）：UpdateRects 仍会算出隐藏的设置按钮区，
            // 结算期误触右上角会闪出设置面板（对抗审查确认的真实不一致）。
            if (run == null || run.State != RunState.Running)
            {
                ResetAll();
                return;
            }

            // [0.9.2] 设置面板打开时完全停止响应触摸（面板有自己的按钮，避免误触奔跑/道具）
            if (settings != null && settings.IsOpen)
            {
                ResetAll();
                return;
            }

            UpdateRects();
            CollectPoints();
            ProcessPoints();
        }

        void UpdateRects()
        {
            float w = Screen.width, h = Screen.height;
            // [0.9.2] 动态摇杆：左半屏任意处按下即出现，松手消失
            joyZone = new Rect(0f, 0f, w * 0.5f, h);
            float bs = Mathf.Clamp(h * 0.15f, 88f, 118f);   // [0.9.2] 放大（原封顶 96 太小）
            float margin = 34f;   // [0.9.4] 右侧按钮/道具格整体内收（用户反馈太贴屏幕边缘；原 20px）
            // [0.9.2] 三个圆形键并排（右下）：加速 ⚡（最右）→ 互动（中）→ 跳跃（最左），共用半径 bs*0.55
            interactRadius = bs * 0.55f;
            float cy = h - bs * 1.55f - 14f;   // [0.9.4] 圆键整体上移 14px（内收，远离底边）
            runCenter = new Vector2(w - bs * 1.35f - 18f, cy);          // [0.9.4] 右移内收 18px
            interactCenter = new Vector2(w - bs * 2.7f - 18f, cy);
            jumpCenter = new Vector2(w - bs * 4.05f - 18f, cy);

            // [0.9.2] 右上角两个按钮：设置 + 视角切换，在正式 Logo（~118px 右上角）左侧同排
            float sl = bs * 0.82f;
            // [0.9.4] 道具按钮区顶：从 h*0.16 往下压让出右上 Logo（RunHUD margin 加大后 Logo 底缘 132~160px，
            // 短屏下会盖住右列灯油格顶——对抗审查实测 720p 重叠 17.7px）。短屏才触发，长屏 h*0.16 本就更大。
            float top = Mathf.Max(h * 0.16f, 150f);
            // [0.9.4] 顶部按钮 y = margin+4。与道具格的间隙由 top 下限（150）保证：短屏下 settings/view 底
            // （sy+sl ≤ 38+96.76=134.76）恒 < top，不会侵入道具格首行（对抗审查复算确认，720p 原 margin 改动
            // 曾致 11.36px 重叠，改由 top 地板解决；此处不叠加 min() 钳制，避免死代码误导）。
            float sy = margin + 4f;
            settingsRect = new Rect(w - margin - 118f - sl - 10f, sy, sl, sl);
            viewRect = new Rect(w - margin - 118f - sl * 2f - 20f, sy, sl, sl);

            float gap = bs + 12f;
            // [0.8.0] 2 列网格：0=右列上、1=左列上（右列上=灯油、左列上=加速 视觉顺序保持）。
            // 注意 ItemCatalog.Count=4（0.8.1 回退后），实际 2 行，旧「2 列 × 5 行」注释是过期注释。
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                int col = i % 2;      // 0=右列，1=左列
                int row = i / 2;      // 0-1 从上到下
                float x = col == 0 ? w - bs - margin : w - bs * 2f - margin * 2f;
                float y = top + row * gap;
                itemRects[i] = new Rect(x, y, bs, bs);
            }
        }

        void CollectPoints()
        {
            points.Clear();
            var ts = Touchscreen.current;
            if (ts != null)
            {
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    // 不能用 isInProgress 过滤：释放帧 phase=Ended 时 isInProgress=false，
                    // 那样 wasReleasedThisFrame 永远检测不到 → 按钮 tap 丢失。
                    bool pressed = t.press.isPressed;
                    bool began = t.press.wasPressedThisFrame;
                    bool ended = t.press.wasReleasedThisFrame;
                    if (!pressed && !began && !ended) continue;
                    points.Add(new TouchPoint
                    {
                        id = t.touchId.ReadValue(),
                        pos = ToGuiPos(t.position.ReadValue()),
                        began = began,
                        ended = ended,
                    });
                }
            }
            else if (SimulateWithMouse)
            {
                var m = Mouse.current;
                if (m != null)
                {
                    // 编辑器鼠标模拟也必须把“抬起”传给按钮；旧代码在抬起帧先 ResetAll，
                    // 导致道具按钮永远收不到 ended，自然点了没有反应。
                    if (m.leftButton.wasReleasedThisFrame)
                    {
                        points.Add(new TouchPoint
                        {
                            id = -1,
                            pos = ToGuiPos(m.position.ReadValue()),
                            began = false,
                            ended = true,
                        });
                    }
                    else if (m.leftButton.isPressed)
                    {
                        points.Add(new TouchPoint
                        {
                            id = -1,
                            pos = ToGuiPos(m.position.ReadValue()),
                            began = m.leftButton.wasPressedThisFrame,
                            ended = m.leftButton.wasReleasedThisFrame,
                        });
                    }
                }
            }
        }

        /// <summary>[0.9.2] Input System 触摸/鼠标位置是左下原点，OnGUI 是左上原点：
        /// 统一翻转为 GUI 坐标，避免摇杆/按钮「判定区与绘制错位」导致点不中。</summary>
        static Vector2 ToGuiPos(Vector2 p)
        {
            p.y = Screen.height - p.y;
            return p;
        }

        void ProcessPoints()
        {
            // 1) 新按下分配角色：设置/视角按钮 → 摇杆（左半屏） → 加速 → 互动 → 跳跃（圆形） → 道具按钮
            foreach (var p in points)
            {
                if (!p.began) continue;
                if (settingsRect.Contains(p.pos) && settingsTouch < 0)
                {
                    settingsTouch = p.id;
                }
                else if (viewRect.Contains(p.pos) && viewTouch < 0)
                {
                    viewTouch = p.id;
                }
                else if (joyTouch < 0 && joyZone.Contains(p.pos))
                {
                    joyTouch = p.id;
                    joyOrigin = p.pos;   // 摇杆出现在手指落点
                }
                else if (runTouch < 0 && Vector2.Distance(p.pos, runCenter) <= interactRadius)
                {
                    runTouch = p.id;
                }
                else if (interactTouch < 0 && Vector2.Distance(p.pos, interactCenter) <= interactRadius)
                {
                    interactTouch = p.id;
                }
                else if (jumpTouch < 0 && Vector2.Distance(p.pos, jumpCenter) <= interactRadius)
                {
                    jumpTouch = p.id;
                }
                else
                {
                    // 道具按钮兜底（点击优先，不参与奔跑/转视角）
                    for (int i = 0; i < itemRects.Length; i++)
                    {
                        if (itemRects[i].Contains(p.pos) && itemTouches[i] < 0)
                        {
                            itemTouches[i] = p.id;
                            break;
                        }
                    }
                }
            }

            // 2) 逐指更新：摇杆向量 / 按钮释放
            foreach (var p in points)
            {
                if (p.id == joyTouch)
                {
                    if (p.ended)
                    {
                        joyTouch = -1;
                        joyVector = Vector2.zero;
                    }
                    else
                    {
                        joyVector = Vector2.ClampMagnitude((p.pos - joyOrigin) / JoyRadius, 1f);
                    }
                }
                if (p.id == interactTouch && p.ended) interactTouch = -1;
                if (p.id == runTouch && p.ended) runTouch = -1;
                if (p.id == jumpTouch && p.ended)
                {
                    jumpTouch = -1;
                    // tap：抬起在键附近（外扩一半防手抖）才跳跃
                    if (Vector2.Distance(p.pos, jumpCenter) <= interactRadius * 1.5f && pc != null)
                        pc.VirtualJump = true;
                }
                if (p.id == settingsTouch && p.ended)
                {
                    settingsTouch = -1;
                    // tap：抬起仍在按钮内才触发
                    if (settingsRect.Contains(p.pos)) ToggleSettings();
                }
                if (p.id == viewTouch && p.ended)
                {
                    viewTouch = -1;
                    // tap：抬起仍在按钮内才切换视角
                    if (viewRect.Contains(p.pos)) ToggleView();
                }

                for (int i = 0; i < itemTouches.Length; i++)
                {
                    if (p.id != itemTouches[i]) continue;
                    if (p.ended)
                    {
                        itemTouches[i] = -1;
                        // tap：抬起在按钮附近（外扩一半防手抖）才使用；成功给 toast 反馈
                        Rect r = itemRects[i];
                        Rect loose = new Rect(r.x - r.width * 0.5f, r.y - r.height * 0.5f, r.width * 2f, r.height * 2f);
                        if (loose.Contains(p.pos) && run != null && run.TryUseItem((RunItem)i))
                        {
                            useToastTimer = 1.6f;
                            useToastText = "使用 " + ItemCatalog.ShortName((RunItem)i);
                            SFXManager.Instance.PlayUiClick();
                        }
                    }
                }
            }

            // 3) 写入玩家输入
            if (pc != null)
            {
                // [0.9.2] 摇杆向量是 GUI 坐标（y 向下），玩家移动期望屏幕上方为正（键盘 W=+1）→ y 取反
                pc.VirtualMove = new Vector2(joyVector.x, -joyVector.y);
                pc.VirtualRunning = runTouch >= 0;   // 加速键按住 = 奔跑
            }
            if (inter != null)
                inter.VirtualInteract = interactTouch >= 0;
        }

        /// <summary>[0.9.2] 该触摸 id 是否已被本组件占用（任一按钮/摇杆）。
        /// 给 ViewToggle 用：首视右半屏滑动转视角时跳过已被 UI 键位占用的手指，防「按住奔跑键顺手转了视角」相机乱晃。
        /// 按 id 判定而非几何位置——手指滑过道具格中途也不会误断转视角（位置判定会误伤）。</summary>
        public bool IsCapturedTouch(int touchId)
        {
            if (touchId == joyTouch || touchId == interactTouch || touchId == runTouch
                || touchId == jumpTouch || touchId == settingsTouch || touchId == viewTouch) return true;
            for (int i = 0; i < itemTouches.Length; i++)
                if (itemTouches[i] == touchId) return true;
            return false;
        }

        /// <summary>[0.9.2] 当前鼠标是否正被本组件的键位/摇杆区占用。给 ViewToggle 的编辑器拖拽转视角用——
        /// 光标未锁定时，按住左键拖屏幕 = 转视角（MC 手感），但拖到左半屏摇杆区/任一按钮上就不转（防冲突）。
        /// 鼠标是单指针，位置即身份，几何判定足够（多指触摸才需要走 IsCapturedTouch 按 id 判）。</summary>
        public bool IsMouseCaptured()
        {
            if (Mouse.current == null) return false;
            Vector2 p = ToGuiPos(Mouse.current.position.ReadValue());
            if (joyZone.Contains(p)) return true;   // 左半屏 = 摇杆区
            if (settingsRect.Contains(p) || viewRect.Contains(p)) return true;
            if (Vector2.Distance(p, runCenter) <= interactRadius) return true;
            if (Vector2.Distance(p, interactCenter) <= interactRadius) return true;
            if (Vector2.Distance(p, jumpCenter) <= interactRadius) return true;
            for (int i = 0; i < itemRects.Length; i++)
                if (itemRects[i].Contains(p)) return true;
            return false;
        }

        void ResetAll()
        {
            joyTouch = -1;
            interactTouch = -1;
            runTouch = -1;
            jumpTouch = -1;
            settingsTouch = -1;
            viewTouch = -1;
            for (int i = 0; i < itemTouches.Length; i++) itemTouches[i] = -1;
            joyVector = Vector2.zero;
            if (pc != null) { pc.VirtualMove = Vector2.zero; pc.VirtualRunning = false; pc.VirtualJump = false; }
            if (inter != null) inter.VirtualInteract = false;
        }

        /// <summary>[0.9.2] 手机设置按钮：开/关局内设置面板（InGameSettings.Toggle，同 Esc 行为）。</summary>
        void ToggleSettings()
        {
            // [0.9.2] 兜底再找一次（防御：Start 顺序反了 / Update 早退路径没补查）。找不到时打日志，不静默失败。
            if (settings == null) settings = UnityEngine.Object.FindObjectOfType<InGameSettings>();
            if (settings == null)
            {
                Debug.LogWarning("MobileControls: 找不到 InGameSettings（RewardSystem 未挂载？），设置面板无法打开");
                return;
            }
            settings.Toggle();
        }

        void OnGUI()
        {
            if (InGameSettings.AnyOpen) return;   // [0.9.3] 设置面板打开时不画手游 UI（避免道具格/圆键 Label 挡面板按钮，用户反馈首视下设置点不了）
            if (!Enabled) return;
            if (pc == null) return;
            if (run == null || run.State != RunState.Running) return;

            DrawJoystick();
            // [0.9.2] 三个圆形键并排：加速 ⚡（最右，图标代替文字）→ 互动（中）→ 跳跃（最左）
            DrawCircleButton(runCenter, interactRadius, "⚡", runTouch >= 0, fontScale: 0.9f);
            DrawCircleButton(interactCenter, interactRadius, "互动", interactTouch >= 0);
            DrawCircleButton(jumpCenter, interactRadius, "跳", jumpTouch >= 0);
            DrawItemButtons();
            DrawHoldButton(settingsRect, "设置", settingsTouch >= 0);
            DrawHoldButton(viewRect, "视角", viewTouch >= 0);
            DrawUseToast();
        }

        void DrawJoystick()
        {
            // [0.9.2] 动态浮动摇杆：没按下就什么都不画（左半屏留空），按下才在落点画方框
            if (joyTouch < 0) return;
            // 底盘（方形方框，灰盒风格）
            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(new Rect(joyOrigin.x - JoyRadius, joyOrigin.y - JoyRadius, JoyRadius * 2f, JoyRadius * 2f), Texture2D.whiteTexture);
            // 摇杆头（跟随拖动）
            Vector2 head = joyOrigin + joyVector * JoyRadius;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(new Rect(head.x - 40f, head.y - 40f, 80f, 80f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawHoldButton(Rect r, string label, bool held)
        {
            GUI.color = held ? new Color(0.95f, 0.85f, 0.3f, 0.55f) : new Color(1f, 1f, 1f, 0.22f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(r.height * 0.32f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = Color.white;
            GUI.Label(r, label, style);
        }

        /// <summary>[0.9.2] 圆形按钮：运行时生成的圆形纹理绘制，不透明度同上。fontScale 控制字号（普通文字按钮用）。
        /// [0.9.2+] ⚡ 图标不用 GUI.Label：⚡ 是 Unicode 杂项符号，IMGUI 内置字体无字形 → 空白（用户反馈闪电按钮没图标没字），
        /// 改画运行时生成的闪电纹理（亮黄多边形，圆内居中）。</summary>
        void DrawCircleButton(Vector2 center, float radius, string label, bool held, float fontScale = 0.55f)
        {
            if (circleTex == null) circleTex = MakeCircleTexture(64);
            Rect r = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            GUI.color = held ? new Color(0.95f, 0.85f, 0.3f, 0.55f) : new Color(1f, 1f, 1f, 0.22f);
            GUI.DrawTexture(r, circleTex);
            GUI.color = Color.white;
            if (label == "⚡")
            {
                if (lightningTex == null) lightningTex = MakeLightningTexture(64);
                float s = radius * 1.05f;
                GUI.DrawTexture(new Rect(center.x - s, center.y - s, s * 2f, s * 2f), lightningTex);
                return;
            }
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(radius * fontScale),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = Color.white;
            GUI.Label(r, label, style);
        }

        /// <summary>[0.9.2] 程序化生成圆形纹理（边缘按距离抗锯齿），供圆形按钮绘制。</summary>
        static Texture2D MakeCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r + 0.5f, dy = y - r + 0.5f;
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>[0.9.2+] 程序化生成闪电纹理（加速键图标）：多边形顶点（归一化视觉坐标，y 上大下小）+
        /// 射线法逐像素填充，4×4 子采样抗锯齿。
        /// [0.9.3] 顶点改为标准闪电轮廓（Material bolt 路径归一化）：顶部尖居中偏右 → 中段左侧尖刺 → 中左 →
        /// 底部尖（偏左）→ 右上角 → 中右。原顶点上尖偏左/下尖偏右 = 标准 ⚡ 的左右镜像（用户反馈「闪电图标反了」）。</summary>
        static Texture2D MakeLightningTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2[] pts =
            {
                new Vector2(0.54f, 0.92f),   // 顶部尖（居中偏右，朝上）
                new Vector2(0.19f, 0.46f),   // 中段左侧尖刺（朝左）
                new Vector2(0.46f, 0.46f),   // 中左（左刺内侧）
                new Vector2(0.40f, 0.08f),   // 底部尖（偏左，朝下）
                new Vector2(0.81f, 0.60f),   // 右上角（宽侧）
                new Vector2(0.54f, 0.60f),   // 中右（右上内侧）
            };
            const int ss = 4;   // 每边子采样数（抗锯齿）
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                            if (PointInPolygon((x + (sx + 0.5f) / ss) / size, (y + (sy + 0.5f) / ss) / size, pts)) hit++;
                    float a = hit / (float)(ss * ss);
                    tex.SetPixel(x, y, a <= 0f ? new Color(0f, 0f, 0f, 0f) : new Color(1f, 0.9f, 0.3f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>[0.9.2+] 点在多边形内判定（射线法，凹多边形也正确）。</summary>
        static bool PointInPolygon(float x, float y, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                float xi = poly[i].x, yi = poly[i].y;
                float xj = poly[j].x, yj = poly[j].y;
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>[0.9.2] 右上角视角切换按钮：2.5D ↔ 第一人称（同 V 键，调 ViewToggle.Toggle）。</summary>
        void ToggleView()
        {
            if (viewToggle == null) return;
            viewToggle.Toggle();
        }

        /// <summary>[0.9.2] 道具使用 toast：点道具按钮后的文字回显，防止「没反应」误判。
        /// [0.9.2+] 全宽 Label + 大背景条：原方案用 CalcSize 撑 Rect 依赖字体度量，粗体中文在部分
        /// 设备/编辑器下 CalcSize 偏窄 → 文字溢出 Rect 被 Clip 裁掉两端（用户反馈「只显示一半」）。
        /// 改全宽 Label（MiddleCenter）后文字必然完整显示，从数学上消除裁剪；背景条独立，居中醒目。</summary>
        void DrawUseToast()
        {
            if (useToastTimer <= 0f || string.IsNullOrEmpty(useToastText)) return;
            float w = Screen.width, h = Screen.height;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.03f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = new Color(1f, 0.9f, 0.55f);
            // 背景条：固定占屏 60% 居中（短文案足够宽）；文字 Label 用全宽 Rect，无论字体度量如何都不会被裁。
            float barH = h * 0.08f;
            Rect bar = new Rect(w * 0.2f, h * 0.38f, w * 0.6f, barH);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, h * 0.38f, w, barH), useToastText, style);
        }

        void DrawItemButtons()
        {
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                Rect r = itemRects[i];
                bool held = itemTouches[i] >= 0;
                int count = run != null ? run.GetItemCount((RunItem)i) : 0;
                GUI.color = held ? new Color(0.95f, 0.85f, 0.3f, 0.55f)
                    : (count > 0 ? new Color(0.3f, 0.55f, 0.9f, 0.6f) : new Color(1f, 1f, 1f, 0.16f));
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;

                var label = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(r.height * 0.28f),
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
                label.normal.textColor = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                GUI.Label(new Rect(r.x, r.y, r.width, r.height * 0.6f), ItemCatalog.ShortName((RunItem)i), label);
                GUI.Label(new Rect(r.x, r.y + r.height * 0.55f, r.width, r.height * 0.45f), $"×{count}", label);
            }
        }
    }
}
