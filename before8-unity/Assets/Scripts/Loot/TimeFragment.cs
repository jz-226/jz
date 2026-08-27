using UnityEngine;
using Before8AM.Run;
using Before8AM.Visual;
using Before8AM.Collection;

namespace Before8AM.Loot
{
    /// <summary>
    /// 时间碎片：旋转漂浮的收集物，触发拾取。集齐 TimeFragmentsRequired 个后晨门激活（规格书 23/53）。
    /// 同时**扩大玩家手电筒光圈半径**（用户反馈：拿到碎片可视范围扩大）。
    /// </summary>
    public class TimeFragment : MonoBehaviour
    {
        public float BobSpeed = 2f;
        public float BobHeight = 0.15f;
        public float RotateSpeed = 60f;
        [Tooltip("拾取后光圈半径增加量（世界单位）")]
        public float RadiusBoost = 3f;

        bool collected;
        Transform _t;
        Vector3 basePos;
        bool basePosInit;   // [0.4.3] 首帧 Update 捕获随机化后位置（LayoutRandomizer.Start 先移动物体）

        void Awake()
        {
            _t = transform;
        }

        void Update()
        {
            if (collected) return;
            if (!basePosInit) { basePos = _t.position; basePosInit = true; }   // [0.4.3] 惰性初始化（随机化后位置）
            _t.Rotate(0f, RotateSpeed * Time.deltaTime, 0f);
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * BobSpeed) * BobHeight);
        }

        void OnTriggerEnter(Collider other)
        {
            if (collected) return;
            if (!other.CompareTag("Player")) return;
            collected = true;
            CollectionSystem.Unlock(CollectionEntry.TimeFragment);   // [0.4.5] 图鉴：拾取时间碎片
            RunManager run = RunManager.Instance;
            run?.AddFragment();
            ExplorationFog fog = FindObjectOfType<ExplorationFog>();
            if (fog != null)
            {
                fog.AddRadius(RadiusBoost);
                Debug.Log($"[时间碎片] 已收集 {run?.TimeFragments}/{run?.TimeFragmentsRequired}，视野光圈 +{RadiusBoost}m → 半径 {fog.TorchRadius:0}m");
            }
            else
            {
                Debug.Log($"[时间碎片] 已收集 {run?.TimeFragments}/{run?.TimeFragmentsRequired}");
            }
            Destroy(gameObject, 0.3f);
        }
    }
}
