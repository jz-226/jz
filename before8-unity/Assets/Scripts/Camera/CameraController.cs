using UnityEngine;

namespace Before8AM.Camera
{
    /// <summary>
    /// 2.5D 固定斜俯视相机（Plan B，用户选择斜俯视 60°）：
    /// 正交投影 + 俯角 60°，固定角度、轻微跟随玩家，屏幕上方恒为世界北方（-Z）。
    /// 不读取鼠标 —— 玩家纯键盘平面移动，第三人称视角的灵敏度问题彻底消失。
    /// 相机恒在建筑上方俯看，天然不穿墙（替代原规格书 37 的球形遮挡逻辑）。
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("目标")]
        public Transform Target;        // 玩家

        [Header("参数")]
        [Tooltip("俯角：60° 斜俯视（2.5D）；Yaw 180° 使屏幕上方=世界北方(-Z)")]
        public float PitchAngle = 60f;
        [Tooltip("相机到目标中心的斜向距离")]
        public float Distance = 22f;
        [Tooltip("正交视野高度（屏幕垂直显示范围的一半）")]
        public float OrthoSize = 11f;
        public float LookHeight = 1.2f;
        public float SmoothSpeed = 6f;

        UnityEngine.Camera cam;

        void Awake()
        {
            cam = GetComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            // [0.9.2] 手机屏幕小：视野默认推近（正交高度 11 → 6.5），主角更大、地图少漏景
            if (Application.isMobilePlatform) OrthoSize = Mathf.Min(OrthoSize, 6.5f);
            cam.orthographicSize = OrthoSize;
            // 裁剪面自持，避免沿用场景里可能残留的旧值导致远处地面/建筑被裁掉
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 150f;
        }

        void Start()
        {
            if (Target == null && transform.parent != null) Target = transform.parent;
            if (Target == null) return;
            // 直接落位，避免开局镜头从远处飞过来
            transform.rotation = Quaternion.Euler(PitchAngle, 180f, 0f);
            transform.position = Target.position + Vector3.up * LookHeight - transform.rotation * Vector3.forward * Distance;
        }

        void LateUpdate()
        {
            if (Target == null) return;
            transform.rotation = Quaternion.Euler(PitchAngle, 180f, 0f);
            Vector3 desired = Target.position + Vector3.up * LookHeight - transform.rotation * Vector3.forward * Distance;
            // 帧率无关的指数平滑（避免低帧率下 Lerp 系数>1 过冲抖动）
            float t = 1f - Mathf.Exp(-SmoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            cam.orthographicSize = OrthoSize;
        }
    }
}
