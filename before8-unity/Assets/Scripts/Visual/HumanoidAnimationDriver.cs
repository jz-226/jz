using UnityEngine;

namespace Before8AM.Visual
{
    /// <summary>驱动导入 Humanoid 模型的 Idle/Walk 动画，不参与玩家移动或任何玩法判定。</summary>
    public class HumanoidAnimationDriver : MonoBehaviour
    {
        public Animator Animator;
        public float SpeedThreshold = 0.12f;

        Vector3 lastPosition;

        void Awake()
        {
            lastPosition = transform.position;
        }

        void Update()
        {
            if (Animator == null || Time.deltaTime <= 0f) return;
            Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;
            velocity.y = 0f;
            Animator.SetFloat("Speed", velocity.magnitude > SpeedThreshold ? velocity.magnitude : 0f);
        }
    }
}
