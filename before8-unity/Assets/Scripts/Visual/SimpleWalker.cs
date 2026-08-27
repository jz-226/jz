using UnityEngine;

namespace Before8AM.Visual
{
    /// <summary>
    /// 方块小人走路动画：四肢从**关节 pivot**（髋/肩）摆动，而不是绕肢体自身中心转——
    /// 前者才是真人的前后摆腿/摆臂，后者像陀螺扭（用户反馈"太假"）。
    /// 手臂是**两节**（肩 pivot 摆上臂 + 肘 pivot 折前臂），末端带手部方块——
    /// 俯视 60° 下手臂整体只有一小条，摆动幅度原本看不清（用户反馈"手臂不摆动"），
    /// 两节+手让摆臂在俯视下也明显可读。
    /// 另加身体上下起伏（每一步一个峰，脚落地时最高），更接近真实步态。
    /// 用位移差分算速度（兼容 CharacterController 与 NavMeshAgent），静止时四肢归位。
    /// </summary>
    public class SimpleWalker : MonoBehaviour
    {
        [Header("关节 pivot 引用（在髋/肩，摆腿/摆臂绕这里）")]
        public Transform LeftLegPivot;
        public Transform RightLegPivot;
        public Transform LeftArmPivot;   // 肩
        public Transform RightArmPivot;
        public Transform LeftElbowPivot; // 肘（前臂折叠，摆臂明显）
        public Transform RightElbowPivot;
        [Tooltip("身体方块：走路时上下起伏")]
        public Transform Body;
        [Tooltip("用于导入骨骼模型：在骨骼的原始姿势上叠加摆动，而不是重置为单位旋转。")]
        public bool UseRestPose;

        public float SpeedThreshold = 0.5f;   // 低于此速度视为静止，停摆
        public float WalkFrequency = 11f;
        [Tooltip("腿前后摆幅度（髋关节）")]
        public float LegAmplitude = 24f;
        [Tooltip("手臂摆动幅度（与腿反相）")]
        public float ArmAmplitude = 22f;
        [Tooltip("肘部折叠幅度（摆臂时前臂向前折，俯视也明显）")]
        public float ElbowAmplitude = 32f;
        [Tooltip("身体上下起伏幅度")]
        public float BodyBob = 0.04f;

        Vector3 lastPos;
        float phase;
        float bodyBaseY;
        Quaternion leftLegRest;
        Quaternion rightLegRest;
        Quaternion leftArmRest;
        Quaternion rightArmRest;
        Quaternion leftElbowRest;
        Quaternion rightElbowRest;

        void Awake()
        {
            CaptureRestPose();
        }

        /// <summary>在 Builder 或 Avatar 重接骨骼后重新记录导入模型的静止姿势。</summary>
        public void CaptureRestPose()
        {
            if (Body != null) bodyBaseY = Body.localPosition.y;
            leftLegRest = LeftLegPivot != null ? LeftLegPivot.localRotation : Quaternion.identity;
            rightLegRest = RightLegPivot != null ? RightLegPivot.localRotation : Quaternion.identity;
            leftArmRest = LeftArmPivot != null ? LeftArmPivot.localRotation : Quaternion.identity;
            rightArmRest = RightArmPivot != null ? RightArmPivot.localRotation : Quaternion.identity;
            leftElbowRest = LeftElbowPivot != null ? LeftElbowPivot.localRotation : Quaternion.identity;
            rightElbowRest = RightElbowPivot != null ? RightElbowPivot.localRotation : Quaternion.identity;
            lastPos = transform.position;
        }

        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            Vector3 vel = (transform.position - lastPos) / Time.deltaTime;
            lastPos = transform.position;
            vel.y = 0f;
            float speed = vel.magnitude;

            if (speed > SpeedThreshold)
            {
                phase += Time.deltaTime * WalkFrequency * Mathf.Clamp(speed, 1f, 2.5f);   // 跑得越快摆得越快
                float a = Mathf.Sin(phase) * LegAmplitude;
                float armA = -Mathf.Sin(phase) * ArmAmplitude;   // 摆臂与同侧腿反相
                float elbowBend = Mathf.Abs(Mathf.Sin(phase)) * ElbowAmplitude;   // 摆臂时前臂折叠
                SetJointRotation(LeftLegPivot, leftLegRest, a);
                SetJointRotation(RightLegPivot, rightLegRest, -a);
                SetJointRotation(LeftArmPivot, leftArmRest, armA);
                SetJointRotation(RightArmPivot, rightArmRest, -armA);
                SetJointRotation(LeftElbowPivot, leftElbowRest, elbowBend);
                SetJointRotation(RightElbowPivot, rightElbowRest, -elbowBend);
                if (Body != null)
                {
                    Vector3 bp = Body.localPosition;
                    bp.y = bodyBaseY + Mathf.Abs(Mathf.Sin(phase)) * BodyBob;   // 每步一个起伏峰
                    Body.localPosition = bp;
                }
            }
            else
            {
                SetJointRotation(LeftLegPivot, leftLegRest, 0f);
                SetJointRotation(RightLegPivot, rightLegRest, 0f);
                SetJointRotation(LeftArmPivot, leftArmRest, 0f);
                SetJointRotation(RightArmPivot, rightArmRest, 0f);
                SetJointRotation(LeftElbowPivot, leftElbowRest, 0f);
                SetJointRotation(RightElbowPivot, rightElbowRest, 0f);
                if (Body != null)
                {
                    Vector3 bp = Body.localPosition;
                    bp.y = bodyBaseY;
                    Body.localPosition = bp;
                }
            }
        }

        void SetJointRotation(Transform joint, Quaternion restPose, float angle)
        {
            if (joint == null) return;
            Quaternion swing = Quaternion.Euler(angle, 0f, 0f);
            joint.localRotation = UseRestPose ? restPose * swing : swing;
        }
    }
}
