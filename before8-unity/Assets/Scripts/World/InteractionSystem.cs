using UnityEngine;
using UnityEngine.InputSystem;
using Before8AM.Run;   // [0.8.2] 结算冻结交互：读 RunManager.State
using Before8AM.Input;   // [0.9.3] MobileControls 编辑器模拟触摸的按键提示

namespace Before8AM.World
{
    /// <summary>
    /// 交互系统：以玩家为中心球形检测最近的可交互物，显示提示，处理瞬时/持续交互（按 E）。
    /// 2.5D 俯视下相机射线交互会失效，改为距离检测（第三人称遗留的 CameraRoot 字段仅保留兼容）。
    /// Vertical Slice 用 OnGUI 显示提示（后续替换为 TMP HUD）。
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("（2.5D 已弃用）第三人称交互射线发射原点")]
        public Transform CameraRoot;
        public float MaxRayDistance = 4f;

        Interactable current;
        float holdTime;
        bool holding;
        MobileControls mobile;   // [0.9.3] 编辑器用鼠标模拟触摸时也提示「互动按钮」（Application.isMobilePlatform 编辑器恒 false）

        /// <summary>[0.5] 手游互动按钮（MobileControls 每帧写入）：按住 = E 键持续交互（宝箱长按），抬起触发瞬时交互。</summary>
        public bool VirtualInteract;
        bool prevVirtualInteract;

        public Interactable Current => current;

        void Update()
        {
            // [0.8.2] 逃出/失败后冻结交互：结算期间不再检测/触发交互物（按 E 无反应、提示消失）
            var rm = RunManager.Instance;
            if (rm != null && rm.State != RunState.Running)
            {
                current = null;
                holding = false;
                return;
            }

            Interactable target = Detect();
            if (target != current)
            {
                current = target;
                holdTime = 0f;
            }

            if (current == null)
            {
                holding = false;
                return;
            }

            // [0.5] E 键 + 手游互动按钮合并；kb 可为 null（安卓）不再早退（否则手游交互整体失效）
            Keyboard kb = Keyboard.current;
            bool ePressed = (kb != null && kb.eKey.isPressed) || VirtualInteract;
            bool eDown = (kb != null && kb.eKey.wasPressedThisFrame) || (VirtualInteract && !prevVirtualInteract);
            prevVirtualInteract = VirtualInteract;

            if (ePressed && current.CanInteract)
            {
                if (current.RequiresHold)
                {
                    holding = true;
                    holdTime += Time.deltaTime;
                    float p = Mathf.Clamp01(holdTime / Mathf.Max(0.001f, current.HoldDuration));
                    current.OnHoldProgress(p);
                    if (p >= 1f)
                    {
                        current.Interact();
                        holding = false;
                        holdTime = 0f;
                    }
                }
                else if (eDown)
                {
                    current.Interact();
                }
            }
            else
            {
                holding = false;
                holdTime = 0f;
            }
        }

        Interactable Detect()
        {
            // 2.5D 俯视：以玩家为中心球形检测最近的可交互物（相机射线在俯视下角度固定，打不到屏幕上的目标）
            const float scanRadius = 3f;
            Interactable best = null;
            float bestDist = float.MaxValue;
            foreach (var col in Physics.OverlapSphere(transform.position, scanRadius))
            {
                Interactable ia = col.GetComponentInParent<Interactable>();
                if (ia == null || !ia.CanInteract) continue;
                float d = Vector3.Distance(transform.position, ia.transform.position);
                if (d <= ia.InteractionRange && d < bestDist)
                {
                    best = ia;
                    bestDist = d;
                }
            }
            return best;
        }

        void OnGUI()
        {
            if (Before8AM.UI.InGameSettings.AnyOpen) return;   // [0.9.3] 设置面板打开时隐藏交互提示
            if (current == null) return;
            string text = current.PromptText;
            if (current.RequiresHold && holding)
                text += $" {Mathf.RoundToInt(Mathf.Clamp01(holdTime / Mathf.Max(0.001f, current.HoldDuration)) * 100f)}%";
            var style = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            // [0.9.2] 手机没有键盘：「按 E」提示改成「按互动按钮」（右下角）。
            // [0.9.3] Application.isMobilePlatform 编辑器恒 false（用户在编辑器里模拟手机玩仍显示「按 E」）→
            // 改跟随 MobileControls 实际启用：真机或编辑器鼠标模拟触摸（SimulateWithMouse）都提示「互动按钮」。
            if (mobile == null) mobile = GetComponent<MobileControls>();   // 同挂 Player 物体；lazy 防挂载顺序反了
            bool isMobile = Application.isMobilePlatform || (mobile != null && mobile.Enabled);
            string key = isMobile ? "互动按钮" : "E";
            GUI.Label(new Rect(Screen.width / 2f - 250f, Screen.height * 0.72f, 500f, 40f), "【" + key + "】" + text, style);
        }
    }
}
