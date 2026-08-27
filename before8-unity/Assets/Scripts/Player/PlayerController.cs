using UnityEngine;
using UnityEngine.InputSystem;
using Before8AM.Patrol;
using Before8AM.Run;
using Before8AM.Visual;
using Before8AM.Audio;   // [0.8.9] 脚步声

namespace Before8AM.Player
{
    /// <summary>
    /// 2.5D 平面玩家移动：CharacterController + Input System Polling API。
    /// WASD 相对屏幕方向移动（固定俯视相机下 = 世界方向），Shift 奔跑，空格跳跃。
    /// 玩家不能攻击，只能跑/躲/绕（规格书 34）。接触巡夜者 = 被抓，本局全部没收（规格书 27/28）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float WalkSpeed = 4.5f;
        public float RunSpeed = 7f;
        public float Gravity = -22f;
        public float JumpHeight = 1.2f;
        public float RotationSmooth = 12f;

        // [0.3.0] 道具 buff：加速 / 隐身。时长由外部 Add 方法设置，Update 倒计时到期还原。
        float speedBoostTimer;
        float invisibilityTimer;
        float invisGraceTimer;                          // [0.4.1] 隐身到期后的感知/抓捕宽限倒计时
        public float InvisibilityGrace = 1.5f;          // [0.4.1] 到期后守卫仍感知不到/抓不到（防"最后一帧"猝死/消失点秒杀）
        const float MaxInvisibilityStack = 12f;         // [0.4.1] 连吃两瓶/宝箱 buff 叠加封顶
        public float SpeedMultiplier = 1f;      // 加速期间 >1，Update 里乘到移动速度上
        public bool IsInvisible => invisibilityTimer > 0f || invisGraceTimer > 0f;   // 守卫判定的隐身开关（含到期宽限）

        // [0.8.0] 新道具 buff（守卫/雾效侧读取；时长由 RunManager.ApplyItemEffect 调 Add 方法设置）。
        float detectorTimer;    // 探测器：守卫穿墙高亮现形（FogHide 强制显示）
        float jammerTimer;      // 干扰器：全场守卫停摆（PatrolController 原地发呆）
        float fakeCardTimer;    // 假学生卡：守卫无视（不抓不追看不见）
        public bool DetectorActive => detectorTimer > 0f;      // [0.8.0] FogHide 读
        public bool JammerActive => jammerTimer > 0f;          // [0.8.0] PatrolController 读
        public bool FakeCardActive => fakeCardTimer > 0f;      // [0.8.0] PatrolController 读
        // [审查] 夜视仪不在此实现：RunManager.ApplyItemEffect 直接调 ExplorationFog.AddNightVision（雾效侧自带 timer），PlayerController 原版整套是死代码已删

        // [0.5] 手游虚拟输入（MobileControls 每帧写入）：摇杆向量（已 clamp 到 1）+ 奔跑开关。PC 键鼠照常，二者合并。
        public Vector2 VirtualMove;
        public bool VirtualRunning;
        /// <summary>[0.9.2] 手游跳跃键（MobileControls tap 时置 true，本帧消费后复位）。</summary>
        public bool VirtualJump;
        CharacterVisual visual;                 // 玩家小人，隐身时 SetGhost 半透明

        CharacterController cc;
        Vector3 velocity;
        UnityEngine.Camera camCached;
        bool cameraMissingLogged;
        float footstepTimer;   // [0.8.9] 脚步节拍（走 0.45s / 跑 0.28s，±10% 随机防机械感）

        /// <summary>水平移动速度（供 AI/动画使用）。</summary>
        public float CurrentSpeed => new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            camCached = UnityEngine.Camera.main;   // 缓存，避免每帧查找（规格书 116）
            visual = GetComponent<CharacterVisual>();
            SkinCatalog.ApplyTo(visual);   // [0.9.0] 角色皮肤：应用已装备皮肤（换 6 身体部位，Bag 恒橙）
        }

        /// <summary>被巡夜者真正接触：RUN FAILED，本局全部清空（规格书 27）。</summary>
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // [0.4.1] 隐身中撞到守卫也不抓（配合守卫侧 CanCatch 拦截；否则玩家主动贴守卫仍会被捕，药水白开）
            if (hit.collider.GetComponentInParent<PatrolController>() != null && !IsInvisible)
            {
                RunManager.Instance?.Fail(RunState.Caught);
            }
        }

        void Update()
        {
            TickBuffTimers();

            // [0.8.2] 逃出/失败后冻结玩家：State != Running 不再读输入移动（结算界面玩家不应再操控）
            var rm = RunManager.Instance;
            if (rm != null && rm.State != RunState.Running) return;

            // [0.5] 输入合并：键盘 WASD + 手游虚拟摇杆（VirtualMove）。移除 kb==null 早退（安卓无键盘卡死点）。
            Keyboard kb = Keyboard.current;

            Vector2 input = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed) input.y -= 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
            }
            input += VirtualMove;   // [0.5] 手游摇杆（MobileControls 已 clamp 到 1）
            input = Vector2.ClampMagnitude(input, 1f);

            bool running = (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) || VirtualRunning;
            float speed = (running ? RunSpeed : WalkSpeed) * SpeedMultiplier;   // [0.3.0] 加速饮料 buff

            // 2.5D：移动方向基于相机前向的水平投影。
            // 固定俯视相机屏幕上方=世界北方(-Z)，W=北/A=西/S=南/D=东，直观且不依赖相机水平朝向。
            UnityEngine.Camera cam = camCached;
            Vector3 camForward;
            Vector3 camRight;
            if (cam != null)
            {
                camForward = cam.transform.forward;
                camRight = cam.transform.right;
            }
            else
            {
                // 主相机缺失时固定回退到世界方向并明确告警（不要回退到玩家面朝方向，会导致 WASD 漂移难排查）
                if (!cameraMissingLogged)
                {
                    Debug.LogWarning("PlayerController: 未找到主相机，WASD 回退为世界方向（W=北/S=南）");
                    cameraMissingLogged = true;
                }
                camForward = Vector3.forward;
                camRight = Vector3.right;
            }
            camForward.y = 0f; camForward.Normalize();
            camRight.y = 0f; camRight.Normalize();
            if (camForward.sqrMagnitude < 0.001f) camForward = Vector3.forward;   // 正上方俯视时兜底
            if (camRight.sqrMagnitude < 0.001f) camRight = Vector3.right;

            Vector3 desired = camForward * input.y + camRight * input.x;
            velocity.x = desired.x * speed;
            velocity.z = desired.z * speed;

            // 重力与跳跃
            if (cc.isGrounded)
            {
                if ((kb != null && kb.spaceKey.wasPressedThisFrame) || VirtualJump)
                {
                    VirtualJump = false;   // [0.9.2] 移动端跳跃键置位，本帧消费后复位
                    velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }
                else
                    velocity.y = -2f;
            }
            else
            {
                velocity.y += Gravity * Time.deltaTime;
            }

            cc.Move(velocity * Time.deltaTime);

            // [0.8.9] 脚步声：贴地且水平移动才响；停/跳立即归零，下次起步立即响。
            // 与守卫听力同源（CurrentSpeed）：走 4.5 声轻、跑 7+ 声重（SFXManager.PlayFootstep 内分套）
            float hSpeed = CurrentSpeed;
            if (cc.isGrounded && hSpeed > 0.5f)
            {
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0f)
                {
                    footstepTimer = (running ? 0.28f : 0.45f) * Random.Range(0.9f, 1.1f);
                    SFXManager.Instance.PlayFootstep(running);
                }
            }
            else
            {
                footstepTimer = 0f;
            }

            // 朝向移动方向（帧率无关指数平滑）
            Vector3 planar = new Vector3(desired.x, 0f, desired.z);
            if (planar.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planar), 1f - Mathf.Exp(-RotationSmooth * Time.deltaTime));
        }

        /// <summary>[0.3.0] 加速饮料：拾取后移动速度 ×multiplier 持续 duration 秒。</summary>
        public void AddSpeedBoost(float duration, float multiplier)
        {
            SpeedMultiplier = multiplier;
            speedBoostTimer = duration;
        }

        /// <summary>[0.3.0] 隐身药水：期间守卫完全看不见（visible 判定被 IsInvisible 拦截），身体半透明反馈。
        /// [0.4.1] 改叠加：连吃两瓶/宝箱 buff 累加（上限 12s），不再覆盖只留 5s；时长 ≤0 直接忽略（防幽灵材质卡死）。</summary>
        public void AddInvisibility(float duration)
        {
            if (duration <= 0f) return;
            invisibilityTimer = Mathf.Min(MaxInvisibilityStack, invisibilityTimer + duration);
            if (visual != null) visual.SetGhost(true);
        }

        // ---------- [0.8.0] 新道具 buff（探测器/干扰器/夜视/假卡） + 传送 ----------

        /// <summary>探测器：duration 秒内守卫穿墙高亮现形（FogHide 读到 DetectorActive 强制显示）。</summary>
        public void AddDetector(float duration)
        {
            if (duration <= 0f) return;
            detectorTimer = Mathf.Max(detectorTimer, duration);
        }

        /// <summary>干扰器：duration 秒内全场守卫停摆原地发呆（PatrolController 读到 JammerActive 停摆）。</summary>
        public void AddJammer(float duration)
        {
            if (duration <= 0f) return;
            jammerTimer = Mathf.Max(jammerTimer, duration);
        }

        /// <summary>假学生卡：duration 秒内守卫无视（PatrolController 读到 FakeCardActive 拦截抓捕/感知）。</summary>
        public void AddFakeCard(float duration)
        {
            if (duration <= 0f) return;
            fakeCardTimer = Mathf.Max(fakeCardTimer, duration);
        }

        /// <summary>传送器：瞬移到目标点（CharacterController 瞬移需先禁用再启用，否则物理层纠错弹回原地）。</summary>
        public void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            velocity = Vector3.zero;
        }

        /// <summary>[0.4.1] 本局结束清空全部 buff 并还原身体（防失效状态带入下一局；场景重载兜底）。
        /// [0.8.0] 新增 4 个新道具 buff 一并清空（探测器/干扰器/夜视/假卡）。</summary>
        public void ClearBuffs()
        {
            invisibilityTimer = 0f;
            invisGraceTimer = 0f;
            speedBoostTimer = 0f;
            detectorTimer = 0f;
            jammerTimer = 0f;
            fakeCardTimer = 0f;
            SpeedMultiplier = 1f;
            if (visual != null) visual.SetGhost(false);
        }

        void TickBuffTimers()
        {
            if (speedBoostTimer > 0f)
            {
                speedBoostTimer -= Time.deltaTime;
                if (speedBoostTimer <= 0f) SpeedMultiplier = 1f;
            }
            if (invisibilityTimer > 0f)
            {
                invisibilityTimer -= Time.deltaTime;
                if (invisibilityTimer <= 0f)
                {
                    // [0.4.1] 到期进入感知宽限（守卫仍看不见/听不见/抓不到）——防"最后一帧"被重新锁定
                    invisGraceTimer = InvisibilityGrace;
                    if (visual != null) visual.SetGhost(false);
                }
            }
            if (invisGraceTimer > 0f) invisGraceTimer -= Time.deltaTime;
            if (detectorTimer > 0f) detectorTimer -= Time.deltaTime;      // [0.8.0]
            if (jammerTimer > 0f) jammerTimer -= Time.deltaTime;          // [0.8.0]
            if (fakeCardTimer > 0f) fakeCardTimer -= Time.deltaTime;      // [0.8.0]
        }

        void OnGUI()
        {
            if (Before8AM.UI.InGameSettings.AnyOpen) return;   // [0.9.3] 设置面板打开时隐藏 buff 提示
            // [0.3.0] 当前 buff 提示（左上 HUD 下方，跟随 Timer 倒计时）
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = Mathf.RoundToInt(Screen.height * 0.028f);
            style.fontStyle = FontStyle.Bold;
            // [0.9.2+] 行高按字号自适应：原固定 30f 高度，竖屏字号 ~54px（行高 ~65px）时文字上下各被裁掉
            // 一半（用户反馈「只显示一半，上下之间是一半」）。按 fontSize×1.35 撑高，任何分辨率都完整显示。
            float rowH = Mathf.RoundToInt(style.fontSize * 1.35f);
            float stepY = rowH + 4f;
            // [0.9.4+] 起点避让左上卡：卡底随字号/行高动态变化（内容反推），固定比例在低高度屏
            // 会压进卡片。读 RunHUD 公布的卡底 + 8px 间隙，任何分辨率都不重叠；RunHUD 未显示
            // （非 Running）时兜底 0.18h。
            float y = RunHUD.LeftCardBottomY > 0f ? RunHUD.LeftCardBottomY + 8f : Screen.height * 0.18f;
            if (speedBoostTimer > 0f)
            {
                style.normal.textColor = new Color(1f, 0.45f, 0.4f);
                // [0.9.2+] 去掉 ⚡ 前缀：⚡ 字形 IMGUI 内置字体缺失渲染为空白（同加速按钮图标问题）
                GUI.Label(new Rect(16f, y, Screen.width, rowH), $"加速 ×{SpeedMultiplier:0.0}  {speedBoostTimer:0.0}s", style);
                y += stepY;
            }
            if (invisibilityTimer > 0f || invisGraceTimer > 0f)
            {
                style.normal.textColor = new Color(0.4f, 0.85f, 1f);
                float remain = invisibilityTimer > 0f ? invisibilityTimer : invisGraceTimer;   // 宽限期也提示（守卫仍找不到你）
                GUI.Label(new Rect(16f, y, Screen.width, rowH), $"👻 隐身中（守卫看不到你）  {remain:0.0}s", style);
                y += stepY;
            }
            // [0.8.0] 新道具 buff 提示（左下角逐条；探测/干扰/假卡都是限时效果）
            if (detectorTimer > 0f)
            {
                style.normal.textColor = new Color(0.4f, 0.85f, 1f);
                GUI.Label(new Rect(16f, y, Screen.width, rowH), $"🔍 探测器（守卫现形）  {detectorTimer:0.0}s", style);
                y += stepY;
            }
            if (jammerTimer > 0f)
            {
                style.normal.textColor = new Color(0.75f, 0.45f, 1f);
                GUI.Label(new Rect(16f, y, Screen.width, rowH), $"📡 干扰器（守卫停摆）  {jammerTimer:0.0}s", style);
                y += stepY;
            }
            if (fakeCardTimer > 0f)
            {
                style.normal.textColor = new Color(0.95f, 0.85f, 0.4f);
                GUI.Label(new Rect(16f, y, Screen.width, rowH), $"🪪 假学生卡（守卫无视）  {fakeCardTimer:0.0}s", style);
            }
        }
    }
}
