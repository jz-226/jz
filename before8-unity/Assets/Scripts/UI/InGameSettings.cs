using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;   // [0.9.3+] 逐指生命周期触摸（官方保证不丢同帧起落/槽复用的短命触摸）
using UnityEngine.SceneManagement;
using Before8AM.Run;
using Before8AM.Camera;
using Before8AM.Audio;
using Before8AM.Core;

namespace Before8AM.UI
{
    /// <summary>
    /// [0.8.9i] 游玩中设置面板：Esc 开/关（打开时暂停游戏），灵敏度/水平反转/音量同主菜单设置
    /// 同存储（ViewToggle/AudioListener），另加「退出本局」——确认后直接回主菜单，
    /// 本局金币/道具全部损失（场景卸载天然丢弃，不走结算不入账）。
    /// 挂接：RewardSystem.Start get-or-add（不动场景文件，校园/停车场两图自动生效）。
    /// 门控：仅 Running/Ready 响应 Esc（结算/失败后自动关闭并恢复暂停态，防止画面冻结）。
    /// [0.9.3] 面板按钮改轮询命中（同 MobileControls）：真机上 IMGUI GUI.Button 的触摸链路
    /// 依赖「触摸模拟鼠标」，且易被 HUD Label 挡（用户两次反馈首视下「设置点不动」）→
    /// Update 里逐指自算命中，OnGUI 只画（GUI.Box 无交互），彻底绕开 IMGUI 点击机制。
    /// [0.9.3+] 触摸侧升级 EnhancedTouch（逐指生命周期，不丢同帧起落/槽复用），模态单指（已有按压时忽略
    /// 其他手指）、取消相不触发、Close 清空按压态、开面板当帧跳过轮询。对抗审查确认过原 pressId 方案的
    /// 多指吞点击 / 同帧丢点击 / 残留误触发等真实缺陷，本轮一并修复。
    /// </summary>
    public class InGameSettings : MonoBehaviour
    {
        bool open;
        bool confirmQuit;
        bool pendingQuit;      // [0.9.4] 「确定退出」置位：Update 延迟执行「等手指抬起→Close()+LoadScene」（见 QuitRun）
        float quitWaitStart;   // [0.9.4] pendingQuit 置位时刻（unscaledTime）：等手指抬起的超时基准

        /// <summary>[0.9.2] 面板是否打开（MobileControls 据此停止响应触摸，防止面板下误触奔跑/道具）。</summary>
        public bool IsOpen => open;

        /// <summary>[0.9.3] 面板全局开关：局内各 OnGUI HUD（MobileControls/ViewToggle/PlayerController/InteractionSystem/RunHUD）
        /// 据此停止绘制，避免它们的 Label 挡面板按钮（用户反馈首视下设置点不了）。
        /// 静态跨场景保留 → 需 Awake/OnDestroy 重置防泄漏。</summary>
        public static bool AnyOpen;

        void Awake()
        {
            AnyOpen = false;   // [0.9.3] 新场景重置（防退出本局回主菜单后残留 true 吞掉主菜单 HUD）
            EnhancedTouchSupport.Enable();   // [0.9.3+] 面板触摸用 EnhancedTouch（引用计数，可重复调）
        }
        void OnDestroy()
        {
            AnyOpen = false;   // [0.9.3] 防泄漏
            Time.timeScale = 1f;   // [0.9.4] 防泄漏（Open() 置 0 的源头对称复位：任何卸载路径都不残留冻结态）
            EnhancedTouchSupport.Disable();   // [0.9.3+] 引用计数平衡（每场景 AddComponent 一次 Enable）
        }

        /// <summary>[0.9.2] 开/关切换（手机右上角设置按钮入口，替代 PC 的 Esc）。</summary>
        public void Toggle()
        {
            if (open) Close();
            else Open();
        }

        // ---------- 面板按钮轮询命中（[0.9.3] 真机 GUI.Button 触摸链路不可靠 → 同 MobileControls：Update 逐指自算，OnGUI 只画） ----------

        struct PanelBtn
        {
            public Rect rect;
            public System.Action onTap;
        }
        readonly List<PanelBtn> btnList = new List<PanelBtn>();
        int pressFinger = -1;      // [0.9.3+] 正在按压的触摸 finger 索引（EnhancedTouch 稳定身份；模态只认第一根）
        bool pressIsMouse;         // 鼠标分支哨兵（编辑器/PC 无触摸屏）
        bool hasPress;
        Rect pressRect;            // 按下时命中的按钮 rect（外扩 15% 容差，抬起仍在内才触发）
        System.Action pressTap;
        bool firstOpenFrame;       // [0.9.3+] 开面板当帧跳过轮询（btnList 未重建 + 防开帧误触手指抢占模态位）

        void Update()
        {
            // [0.9.4] 「退出本局」延迟执行：QuitRun 由 ReleaseAt 在 EnhancedTouch activeTouches 迭代中被调用，
            // 绝不在那里同步切场景——同步 LoadScene 会触发本组件 OnDestroy→EnhancedTouchSupport.Disable()
            // →TearDownState() 在迭代中途销毁 InputStateHistory 缓冲（后续 touches[i] 读已释放内存：
            // dev 构建抛 "Record is no longer valid" 或读垃圾 phase），这是旧代码确定性缺陷。
            // 改置位延迟到 Update 执行，同 RewardSystem.pendingMenu / MainMenuController.pendingScene 范式。
            // 且等所有手指抬起再切：主触摸（摇杆拇指）仍按住期间，legacy 合成鼠标（simulateMouseWithTouches）
            // 被钉在其位置，主菜单 IMGUI 按钮要「框内按下→框内抬起」才触发，任何点按都无 MouseDown 转变 → 菜单全死。
            if (pendingQuit)
            {
                // 等待期玩家点「继续游戏」（Close 已执行，open=false）→ 取消退出，留在本局
                if (!open)
                {
                    pendingQuit = false;
                    quitWaitStart = 0f;
                    return;
                }
                // 手指全抬才切；EnhancedTouch 不可用 / 超时 2s 兜底（防手指久按挂死）。期间 open 仍 true，
                // 下方 PollPanel 照常跑 → 玩家随时可点「继续游戏」取消。超时兜底用 !hasPress 门控：
                // 若面板按钮正被按住（如手指按着「继续游戏」未抬），绝不因超时误踢退出（对抗审查确认的边界）。
                bool allUp = !EnhancedTouchSupport.enabled
                    || UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0;
                if (allUp || (Time.unscaledTime - quitWaitStart > 2f && !hasPress))
                {
                    pendingQuit = false;
                    quitWaitStart = 0f;
                    Close();   // 组件仍活着，安全清理 open/confirmQuit/AnyOpen/press；timeScale=1
                    SceneManager.LoadScene(SceneNames.MainMenu);
                    return;
                }
                // 手指还没全抬：继续等待（落下去走 Esc 开关 / PollPanel，玩家仍可取消）
            }

            var run = RunManager.Instance;
            bool inRun = run != null && (run.State == RunState.Running || run.State == RunState.Ready);

            // 结算/失败后自动关闭（防 timeScale 残留 0 冻结结算画面）
            if (open && !inRun)
            {
                Close();
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (open) Close();
                else if (inRun) Open();
            }

            if (open)
            {
                // [0.9.3+] 开面板当帧跳过：btnList 要等 OnGUI 重建，且避免开帧一根误触手指抢占模态位
                if (firstOpenFrame) firstOpenFrame = false;
                else PollPanel();   // [0.9.3] timeScale=0 不影响 Update 轮询（同 MobileControls）
            }
        }

        /// <summary>[0.9.3+] 面板按钮命中轮询：触摸用 EnhancedTouch（逐指生命周期），鼠标（编辑器/PC）退回轮询。
        /// 模态单指：已有按压时忽略其他手指，防第二根拇指/手掌误触吞掉正在进行的点击（原 pressId 方案的真机缺陷）。
        /// 同帧起落（快速轻点碰上帧卡顿）EnhancedTouch 会本帧以 Began、下一帧以 Ended 浮现，两次都可命中——
        /// 低级 Touchscreen 轮询的 wasPressed/wasReleased 对同帧起落双 false，整次点击会隐形。
        /// 取消相（来电/通知栏/切后台）不触发动作，防误触「确定退出」等破坏性按钮。</summary>
        void PollPanel()
        {
            if (EnhancedTouchSupport.enabled && Touchscreen.current != null)
            {
                // Touch 与老 Input Manager 的 UnityEngine.Touch 撞名 → 全限定 EnhancedTouch.Touch
                var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (t.began)
                    {
                        // 模态：第一根手指优先；已有按压时忽略其他手指（防误触抢占）
                        if (pressFinger < 0)
                        {
                            pressFinger = t.finger.index;
                            pressIsMouse = false;
                            PressAt(ToGuiPos(t.screenPosition));
                        }
                    }
                    else if (t.ended)
                    {
                        if (t.finger.index == pressFinger)
                        {
                            // 取消相是系统打断触摸，不是用户抬手 → 不触发动作
                            if (t.phase != UnityEngine.InputSystem.TouchPhase.Canceled)
                                ReleaseAt(ToGuiPos(t.screenPosition));
                            ResetPress();
                        }
                    }
                }
            }
            else if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    pressIsMouse = true;
                    PressAt(ToGuiPos(Mouse.current.position.ReadValue()));
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame && pressIsMouse)
                {
                    ReleaseAt(ToGuiPos(Mouse.current.position.ReadValue()));
                    ResetPress();
                }
            }
        }

        void ResetPress()
        {
            pressFinger = -1;
            pressIsMouse = false;
            hasPress = false;
            pressTap = null;
        }

        void PressAt(Vector2 p)
        {
            pressTap = null;
            for (int i = 0; i < btnList.Count; i++)
            {
                if (btnList[i].rect.Contains(p))
                {
                    // [0.9.3+] 外扩 15% 容差：真机点按-抬起之间手指轻微漂移，严格矩形必漏点（MobileControls 用 1.5×/2× 同因）
                    pressRect = Inflate(btnList[i].rect, 0.15f);
                    pressTap = btnList[i].onTap;
                    break;
                }
            }
            hasPress = pressTap != null;
        }

        void ReleaseAt(Vector2 p)
        {
            if (!hasPress) return;
            // tap：抬起仍在按钮内才触发（防手抖拖出取消）
            if (pressTap != null && pressRect.Contains(p))
            {
                pressTap();
                // [0.9.3+] 按钮真触发才响点击音（SFXManager 面板打开时不监听 MouseDown，
                // 否则点面板空白也响、造成「点了没反应」错觉——用户两次反馈设置点不动）
                if (SFXManager.Instance != null) SFXManager.Instance.PlayUiClick();
            }
            hasPress = false;
            pressTap = null;
        }

        /// <summary>[0.9.3+] 矩形四周按比例外扩（返回新 Rect，不修改原值）。</summary>
        static Rect Inflate(Rect r, float f)
        {
            float dx = r.width * f, dy = r.height * f;
            return new Rect(r.x - dx, r.y - dy, r.width + dx * 2f, r.height + dy * 2f);
        }

        /// <summary>[0.9.2] Input System 触摸/鼠标位置是左下原点，OnGUI 是左上原点：统一翻转。</summary>
        static Vector2 ToGuiPos(Vector2 p)
        {
            p.y = Screen.height - p.y;
            return p;
        }

        void Open()
        {
            open = true;
            confirmQuit = false;
            AnyOpen = true;   // [0.9.3] 全局：局内 HUD 停止绘制
            Time.timeScale = 0f;   // 暂停（守卫 AI/计时全部用 deltaTime，自动冻结）
            ResetPress();          // [0.9.3+] 清空上一会话残留按压（防重开后旧手指抬起误触发）
            firstOpenFrame = true; // [0.9.3+] 开面板当帧 btnList 未重建，跳过轮询
        }

        void Close()
        {
            open = false;
            confirmQuit = false;
            AnyOpen = false;   // [0.9.3] 全局恢复
            Time.timeScale = 1f;
            ResetPress();      // [0.9.3+] 关键：面板关闭时若手指仍按着，抬起不再被 PollPanel 捕获；
                               // 不清空则重开面板后旧手指抬起会误触发上一会话的按钮（对抗审查确认）
        }

        void QuitRun()
        {
            // [0.9.4] 不再同步 LoadScene：本方法由 ReleaseAt 在 EnhancedTouch activeTouches 迭代中被调用，
            // 同步切场景会令 OnDestroy→EnhancedTouchSupport.Disable() 在迭代中途 teardown（销毁正被迭代的
            // InputStateHistory 缓冲 → 读已释放内存，dev 构建异常/垃圾 phase），且主触摸未抬时 legacy 合成
            // 鼠标钉住主菜单按钮（见 Update 等待逻辑）。改置位 + 记时刻，Update 延迟执行（同 pendingMenu 范式）。
            // timeScale 保持 0（面板仍暂停），切场景前由 Close() 统一恢复为 1。
            // 点击音已由 ReleaseAt 统一播放（按钮真触发才响），此处不重复播（否则「确定退出」双响）。
            pendingQuit = true;
            quitWaitStart = Time.unscaledTime;
        }

        void OnGUI()
        {
            if (!open) return;
            // [0.9.3+] 面板置顶渲染：GUI.depth 越低越靠上（默认 0）。此前误设 100 把面板画在所有 HUD 之下
            // （方向反了，对抗审查确认）——改 -100 真置顶，任何漏加 AnyOpen 的 OnGUI（增援字幕/开箱文字）都盖在面板下。
            // GUI.depth 是全局静态，跨 OnGUI 回调保留 → 用 try/finally 恢复原值。
            int savedDepth = GUI.depth;
            GUI.depth = -100;
            try
            {
                DrawPanel();
            }
            finally
            {
                GUI.depth = savedDepth;
            }
        }

        void DrawPanel()
        {
            float w = Screen.width, h = Screen.height;

            // 遮罩
            GUI.color = new Color(0f, 0f, 0f, confirmQuit ? 0.55f : 0.45f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // [0.8.9i] 面板节奏对齐主菜单设置（0.82h 面板 + 0.13 行高 + 0.02 行距，720 屏不叠字）
            float panelW = Mathf.Min(w * 0.9f, 640f);
            float panelH = confirmQuit ? h * 0.34f : h * 0.82f;
            float px = (w - panelW) * 0.5f, py = (h - panelH) * 0.5f;
            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // [0.9.3] 每帧重建按钮命中区（与绘制同源，防 rect 错位；PollPanel 读上一帧的结果）
            btnList.Clear();

            if (confirmQuit)
            {
                DrawConfirm(px, py, panelW, panelH, h);
                return;
            }

            var title = Label(0.04f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.93f, 0.7f));
            GUI.Label(new Rect(px, py + h * 0.03f, panelW, h * 0.055f), "设置（游戏中）", title);

            float rowX = px + panelW * 0.05f;
            float rowW = panelW * 0.9f;
            float rowH = h * 0.13f;
            float rowY = py + h * 0.13f;

            // 行 1：灵敏度（[−][+] 分列，不重叠）
            rowY = DrawRow(rowX, rowY, rowW, rowH, "灵", new Color(0.45f, 0.8f, 0.9f), "灵敏度", ViewToggle.GetSensitivity().ToString("0.0"), y =>
            {
                RegisterBtn(LeftBtnRect(rowX, y, rowW, rowH), "-", () => ViewToggle.SetSensitivity(ViewToggle.GetSensitivity() - 0.25f));
                RegisterBtn(MidBtnRect(rowX, y, rowW, rowH), "+", () => ViewToggle.SetSensitivity(ViewToggle.GetSensitivity() + 0.25f));
            });
            rowY += h * 0.02f;

            // 行 2：水平反转
            rowY = DrawRow(rowX, rowY, rowW, rowH, "转", new Color(0.45f, 0.7f, 0.95f), "水平方向", ViewToggle.GetInvertHorizontal() ? "反转" : "正常", y =>
            {
                RegisterBtn(RightBtnRect(rowX, y, rowW, rowH), "切换", () => ViewToggle.SetInvertHorizontal(!ViewToggle.GetInvertHorizontal()));
            });
            rowY += h * 0.02f;

            // 行 3：音量（[−][+]，全局主音量 = AudioListener.volume ← PlayerPrefs）
            rowY = DrawRow(rowX, rowY, rowW, rowH, "音", new Color(0.6f, 0.9f, 0.65f), "音量", Mathf.RoundToInt(AudioListener.volume * 100f) + "%", y =>
            {
                RegisterBtn(LeftBtnRect(rowX, y, rowW, rowH), "-", () => AdjustVolume(-0.1f));
                RegisterBtn(MidBtnRect(rowX, y, rowW, rowH), "+", () => AdjustVolume(0.1f));
            });
            rowY += h * 0.02f;

            // 行 4：退出本局（红字警示，点击进确认层）
            var quitBtn = RowBtn(rowH);
            quitBtn.normal.textColor = new Color(1f, 0.45f, 0.4f);
            rowY = DrawRow(rowX, rowY, rowW, rowH, "退", new Color(0.95f, 0.5f, 0.45f), "退出本局", "损失全部", y =>
            {
                RegisterBtn(RightBtnRect(rowX, y, rowW, rowH), "退出", () => confirmQuit = true, quitBtn);
            });

            // 底部：返回游戏
            RegisterBtn(new Rect(px + panelW * 0.34f, py + panelH - h * 0.06f, panelW * 0.32f, h * 0.05f),
                "返回游戏", Close, UiStyle.Btn(Mathf.RoundToInt(h * 0.024f)));
        }

        void DrawConfirm(float px, float py, float panelW, float panelH, float h)
        {
            var t = Label(0.03f, TextAnchor.MiddleCenter, FontStyle.Bold, new Color(1f, 0.9f, 0.75f));
            GUI.Label(new Rect(px, py + panelH * 0.16f, panelW, h * 0.07f), "退出将损失本局全部金币与道具", t);

            var s = Label(0.02f, TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.75f, 0.8f, 0.85f));
            GUI.Label(new Rect(px, py + panelH * 0.32f, panelW, h * 0.05f), "永久金币 / 等级 / 段位不受影响", s);

            var btn = UiStyle.Btn(Mathf.RoundToInt(h * 0.024f));
            var quitBtn = UiStyle.Btn(Mathf.RoundToInt(h * 0.024f));
            quitBtn.normal.textColor = new Color(1f, 0.45f, 0.4f);
            RegisterBtn(new Rect(px + panelW * 0.08f, py + panelH * 0.55f, panelW * 0.4f, h * 0.08f), "确定退出", QuitRun, quitBtn);
            RegisterBtn(new Rect(px + panelW * 0.52f, py + panelH * 0.55f, panelW * 0.4f, h * 0.08f), "继续游戏", Close, btn);
        }

        /// <summary>[0.9.3] 注册面板按钮命中区 + 绘制按钮外观（GUI.Box 无交互，点击全走 PollPanel）。
        /// 与 MobileControls 一致：命中计算在 Update，OnGUI 只画，规避 IMGUI 按钮真机触摸失效。
        /// 3 参重载字号 = rect.height×0.32（行按钮 height=rowH×0.5 → 0.32×0.5×rowH = rowH×0.16，同 RowBtn）。</summary>
        void RegisterBtn(Rect rect, string label, System.Action onTap)
        {
            RegisterBtn(rect, label, onTap, UiStyle.Btn(Mathf.RoundToInt(rect.height * 0.32f)));
        }

        void RegisterBtn(Rect rect, string label, System.Action onTap, GUIStyle style)
        {
            btnList.Add(new PanelBtn { rect = rect, onTap = onTap });
            GUI.Box(rect, label, style);
        }

        /// <summary>画一行设置（左图标徽章 + 标签 + 中值 + 右侧操作区），返回行底 Y。操作绘制交给 inRow
        /// （参数=本行 Y，由 DrawRow 传入而非闭包捕获外部 rowY——后者会在赋值后读到更新过的行底，按钮位置错位）。</summary>
        float DrawRow(float x, float y, float w, float rowH, string badgeChar, Color badgeColor, string labelText, string valueText, System.Action<float> inRow)
        {
            float sw = rowH * 0.55f;
            Icon.Badge(new Rect(x + w * 0.03f, y + rowH * 0.2f, sw, sw), badgeColor, badgeChar);

            var ls = Label(0.026f, TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            GUI.Label(new Rect(x + w * 0.03f + sw + w * 0.03f, y, w * 0.48f, rowH), labelText, ls);

            if (!string.IsNullOrEmpty(valueText))
            {
                var vs = Label(0.02f, TextAnchor.MiddleRight, FontStyle.Normal, new Color(0.7f, 0.75f, 0.82f));
                GUI.Label(new Rect(x + w * 0.60f, y, w * 0.175f, rowH), valueText, vs);
            }

            inRow?.Invoke(y);
            return y + rowH;
        }

        // [0.9.3+] ± 按钮加宽（0.09→0.12 行宽，1080p 下 52→69px）：原尺寸低于移动端最小触控目标，点偏/漂移必漏点（用户反馈设置点不动）。
        Rect LeftBtnRect(float x, float y, float w, float rowH)
        {
            float bw = w * 0.12f, bh = rowH * 0.5f;
            return new Rect(x + w * 0.78f, y + rowH * 0.25f, bw, bh);
        }

        Rect MidBtnRect(float x, float y, float w, float rowH)
        {
            float bw = w * 0.12f, bh = rowH * 0.5f;
            return new Rect(x + w * 0.885f, y + rowH * 0.25f, bw, bh);
        }

        Rect RightBtnRect(float x, float y, float w, float rowH)
        {
            float bw = w * 0.18f, bh = rowH * 0.5f;
            return new Rect(x + w * 0.78f, y + rowH * 0.25f, bw, bh);
        }

        GUIStyle RowBtn(float rowH) => UiStyle.Btn(Mathf.RoundToInt(rowH * 0.16f));

        void AdjustVolume(float delta)
        {
            float v = Mathf.Clamp01(AudioListener.volume + delta);
            AudioListener.volume = v;
            PlayerPrefs.SetFloat("Before8AM.MasterVolume", v);
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
