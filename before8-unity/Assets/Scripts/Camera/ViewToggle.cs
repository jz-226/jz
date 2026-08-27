using UnityEngine;
using UnityEngine.InputSystem;
using Before8AM.Input;
using Before8AM.Visual;

namespace Before8AM.Camera
{
    /// <summary>
    /// 2.5D 斜俯视 ↔ 第一人称 一键切换（V 键，游玩中随时可切——用户反馈最大的更新点）。
    /// 第一人称 = 标准 FPS 操作：**鼠标转动视角**（左右=转身、上下=俯仰，灵敏度可调：[ 降 / ] 升，PlayerPrefs 记忆），
    /// WASD 相对视角移动（PlayerController 已按相机前向算 WASD → 移动代码零改动）。
    /// 挂在 2.5D 主相机上。首视切换时：锁鼠标 + 隐藏玩家身体（否则看到头内部）+ 隐藏迷雾平面（从下方看是遮天暗幕）
    /// + FogHide 首视豁免（守卫不会在视线里凭空消失）+ 暖黄头灯补光（夜里首视也能看清路）。
    /// 切回 2.5D：恢复正交 + CameraController 接管 + 立即落位（不从首视位置飞回，避免穿墙）+ 鼠标解锁。
    /// </summary>
    public class ViewToggle : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("玩家根节点（读取位置/朝向）")]
        public Transform Player;
        [Tooltip("迷雾平面（首视从下方看是遮天暗幕，隐藏；切回 2.5D 恢复）")]
        public GameObject FogPlane;

        [Header("首视参数")]
        public float EyeHeight = 1.45f;
        public float Fov = 65f;
        [Tooltip("鼠标灵敏度（度/像素）。游玩中 [ 降 / ] 升 实时微调，自动记忆")]
        public float LookSensitivity = 2.5f;
        [Tooltip("水平反向开关（默认关）。游玩中 ; 键实时切换，自动记忆——方向不合习惯不用改代码")]
        public bool InvertHorizontal = false;
        [Tooltip("视角上下限（度）")]
        public float MinPitch = -80f;
        public float MaxPitch = 80f;
        [Tooltip("进入首视时的初始下俯角（度）")]
        public float InitialPitch = -8f;

        /// <summary>全局查询：当前是否第一人称（FogHide 守卫隐身据此豁免）。</summary>
        public static bool IsFirstPerson { get; private set; }

        const string SENS_KEY = "Before8AM.FirstPersonSensitivity";
        const string INVERT_KEY = "Before8AM.InvertHorizontal";

        /// <summary>[0.6] 设置面板入口：读取当前灵敏度（主菜单没有本组件实例，走 PlayerPrefs 唯一数据源，键与 [ ] 微调共用）。</summary>
        public static float GetSensitivity() => PlayerPrefs.GetFloat(SENS_KEY, 2.5f);
        /// <summary>[0.6] 设置面板入口：写入灵敏度（与游玩中 [ 降 / ] 升 同存储）。</summary>
        public static void SetSensitivity(float v) => PlayerPrefs.SetFloat(SENS_KEY, Mathf.Clamp(v, 0.1f, 15f));
        /// <summary>[0.6] 设置面板入口：当前水平方向是否反转。</summary>
        public static bool GetInvertHorizontal() => PlayerPrefs.GetInt(INVERT_KEY, 0) == 1;
        /// <summary>[0.6] 设置面板入口：写入水平反转（与游玩中 ; 键同存储）。</summary>
        public static void SetInvertHorizontal(bool v) => PlayerPrefs.SetInt(INVERT_KEY, v ? 1 : 0);

        UnityEngine.Camera cam;
        CameraController topDown;
        MobileControls mobile;   // [0.9.2] 触屏按钮区判定（滑动转视角跳过 UI 键位，防按住奔跑键相机乱晃）
        Light headlamp;
        bool inFirstPerson;
        float yaw;       // 视角水平角（鼠标左右）
        float pitch;     // 视角俯仰（鼠标上下）
        Vector2 lastMousePos;  // [0.9.2] 编辑器未锁定时用位置差算鼠标移动（系统 delta 在焦点不稳时可能为 0）

        void Awake()
        {
            cam = GetComponent<UnityEngine.Camera>();
            topDown = GetComponent<CameraController>();
            // [0.9.2] 触屏按钮区（MobileControls 挂玩家，场景内单例）
            mobile = UnityEngine.Object.FindObjectOfType<MobileControls>();

            // 首视看向校园夜空：主相机改纯色深蓝（默认天空盒白天会穿帮）；2.5D 俯视用不到也安全
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.04f, 0.08f);

            // 首视头灯（组件在相机上，跟随相机移动）：夜里没有光圈时首视也要能看清路
            headlamp = gameObject.AddComponent<Light>();
            headlamp.type = LightType.Point;
            headlamp.range = 14f;
            headlamp.intensity = 2.2f;
            headlamp.color = new Color(1f, 0.86f, 0.62f);
            headlamp.enabled = false;   // 仅首视开启（2.5D 开着会在俯视下把地面照亮穿帮）

            LookSensitivity = PlayerPrefs.GetFloat(SENS_KEY, LookSensitivity);   // 上次调好的灵敏度
            InvertHorizontal = PlayerPrefs.GetInt(INVERT_KEY, 0) == 1;           // 上次调好的水平方向
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.vKey.wasPressedThisFrame)
                    Toggle();
                if (inFirstPerson && kb.leftBracketKey.wasPressedThisFrame)
                    AdjustSensitivity(-0.25f);
                if (inFirstPerson && kb.rightBracketKey.wasPressedThisFrame)
                    AdjustSensitivity(0.25f);
                if (inFirstPerson && kb.semicolonKey.wasPressedThisFrame)
                    ToggleInvert();
            }
        }

        void AdjustSensitivity(float delta)
        {
            LookSensitivity = Mathf.Clamp(LookSensitivity + delta, 0.1f, 15f);   // 下限 0.5→0.1（用户反馈灵敏度还想更低）
            PlayerPrefs.SetFloat(SENS_KEY, LookSensitivity);   // 记住，下次进游戏/重建场景不丢
        }

        /// <summary>水平反转开关：方向不合习惯时游戏中一键切换，不用改代码等重编译。</summary>
        void ToggleInvert()
        {
            InvertHorizontal = !InvertHorizontal;
            PlayerPrefs.SetInt(INVERT_KEY, InvertHorizontal ? 1 : 0);   // 记住，下次不丢
        }

        public void Toggle()
        {
            inFirstPerson = !inFirstPerson;
            IsFirstPerson = inFirstPerson;
            ApplyMode();
        }

        void ApplyMode()
        {
            cam.orthographic = !inFirstPerson;
            if (inFirstPerson)
            {
                cam.fieldOfView = Fov;
                // 进入首视：视角从玩家面朝方向起步，接下来交给鼠标
                yaw = Player != null ? Player.eulerAngles.y : 0f;
                pitch = InitialPitch;
            }
            else
            {
                SnapTopDown();   // 立即落回俯视，避免相机从首视位置飞回穿过墙体
            }
            topDown.enabled = !inFirstPerson;   // 首视期间 CameraController 停摆，由本脚本接管相机

            if (headlamp != null) headlamp.enabled = inFirstPerson;

            // 首视锁鼠标（否则拖到屏幕边缘就转不了视角）；切回 2.5D 解锁。
            // 编辑器里不锁：Game 视图焦点易碎，锁经常不生效 → 用「按住左键拖拽」替代（光标保持可见可拖，与手机右半屏滑动对应）。
            bool lockCursor = !Application.isEditor && inFirstPerson;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = inFirstPerson ? Application.isEditor : true;
            if (Mouse.current != null) lastMousePos = Mouse.current.position.ReadValue();   // 进首视重置，防首帧跳变

            // 首视藏起玩家身体（否则镜头在头里看到头/身体内部）；切回 2.5D 恢复
            if (Player != null)
            {
                CharacterVisual visual = Player.GetComponent<CharacterVisual>();
                if (visual != null) visual.SetVisible(!inFirstPerson);
            }

            // 首视藏起迷雾平面（从下方看是遮天暗幕，反而挡视野）；切回恢复（2.5D 光圈机制不变）
            if (FogPlane != null) FogPlane.SetActive(!inFirstPerson);
        }

        void LateUpdate()
        {
            if (!inFirstPerson || Player == null) return;   // 2.5D 完全交给 CameraController
            // [0.9.3] 设置面板打开时冻结视角：timeScale=0 只停 deltaTime 不停 LateUpdate，
            // 否则点面板按钮时鼠标一按相机就转（首视下「设置点不了」的强烈手感来源）。
            if (Before8AM.UI.InGameSettings.AnyOpen) return;

            // 鼠标转动视角：左右 = yaw，上下 = pitch（上下限内不翻转）。Mouse.current.delta 单位 = 像素。
            // 符号（实测修正）：水平 yaw 默认 +（鼠标右=右转），垂直 pitch 用 -（鼠标上=抬头）。
            // 此前把水平也翻成 - 导致左右反了（用户反馈"左右转向又不对"）→ 恢复 +；
            // 万一还不合习惯，游戏中按 ; 一键反转水平方向（InvertHorizontal），不用改代码。
            // [0.9.1] 灵敏度/反转实时读 PlayerPrefs：局内设置面板（Esc）只写存档，
            //   若继续用 Awake 读一次的字段，局内调了不生效（用户反馈 bug）；GetSensitivity 是内存缓存字典读，无压力。
            float hSign = GetInvertHorizontal() ? -1f : 1f;
            // 鼠标转视角：光标锁定（PC 构建标准 FPS）→ 用系统 delta 自由移动即转；
            // 光标未锁定（编辑器测试）→ 按住左键拖拽 = 转视角（MC 手感），移动量自己用位置差算（系统 delta 在焦点不稳时可能为 0）；
            // 拖到 UI 键位/左半屏摇杆区上不转（IsMouseCaptured，与手机左右分工一致：左=走位、右=转视角）。
            if (Mouse.current != null)
            {
                Vector2 pos = Mouse.current.position.ReadValue();
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                bool dragging = Mouse.current.leftButton.isPressed && !(mobile != null && mobile.IsMouseCaptured());
                if (locked || dragging)
                {
                    Vector2 d = locked ? Mouse.current.delta.ReadValue() : pos - lastMousePos;
                    yaw += hSign * d.x * GetSensitivity();
                    pitch = Mathf.Clamp(pitch - d.y * GetSensitivity(), MinPitch, MaxPitch);
                }
                lastMousePos = pos;
            }
            // [0.9.2] 触屏：第一人称右半屏滑动转视角（像我的世界），灵敏度与设置面板同步（GetSensitivity）。
            // 真机专用（编辑器/桌面用鼠标段，避免双倍转向）。
            if (Application.isMobilePlatform && Touchscreen.current != null)
            {
                var touches = Touchscreen.current.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (!t.isInProgress) continue;
                    if (t.press.wasPressedThisFrame) continue;   // 按下首帧 delta 是无效初值，跳过防相机瞬跳
                    Vector2 pos = t.position.ReadValue();
                    if (pos.x < Screen.width * 0.5f) continue;   // 左半屏是摇杆，不转视角
                    if (mobile != null && mobile.IsCapturedTouch(t.touchId.ReadValue())) continue;   // 已被按钮/摇杆占用的手指不转视角（防按住奔跑键相机乱晃）
                    Vector2 d = t.delta.ReadValue();
                    yaw += hSign * d.x * GetSensitivity();
                    pitch = Mathf.Clamp(pitch - d.y * GetSensitivity(), MinPitch, MaxPitch);
                    break;   // 只跟第一根右半屏触摸
                }
            }

            cam.transform.position = Player.position + Vector3.up * EyeHeight;
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>切回 2.5D 立即落位（同 CameraController.Start 公式），避免相机从首视位置飞回时穿过墙体。</summary>
        void SnapTopDown()
        {
            if (topDown == null || Player == null) return;
            transform.rotation = Quaternion.Euler(topDown.PitchAngle, 180f, 0f);
            transform.position = Player.position + Vector3.up * topDown.LookHeight
                                 - transform.rotation * Vector3.forward * topDown.Distance;
            cam.orthographicSize = topDown.OrthoSize;
        }
    }
}
