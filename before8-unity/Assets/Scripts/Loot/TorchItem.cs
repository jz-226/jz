using UnityEngine;
using Before8AM.Run;
using Before8AM.Collection;
using Before8AM.Mission;   // [0.8.0] 每日任务"搜刮"进度

namespace Before8AM.Loot
{
    /// <summary>
    /// 灯油道具：散落地图的暖黄发光小罐。**[0.5] 拾取进背包，使用时扩玩家探索光圈 +2m**
    /// （RunManager.ApplyItemEffect 数值，与旧"拾取即扩"一致）。旋转漂浮提示可拾取，Trigger 碰撞拾取。
    /// </summary>
    public class TorchItem : MonoBehaviour
    {
        public float RotateSpeed = 70f;
        public float BobSpeed = 2.5f;
        public float BobHeight = 0.12f;

        bool taken;
        Transform _t;
        Vector3 basePos;
        bool basePosInit;   // [0.4.3] 首帧 Update 捕获随机化后位置（LayoutRandomizer.Start 先移动物体）

        void Awake()
        {
            _t = transform;
        }

        void Update()
        {
            if (taken) return;
            if (!basePosInit) { basePos = _t.position; basePosInit = true; }   // [0.4.3] 惰性初始化（随机化后位置）
            _t.Rotate(0f, RotateSpeed * Time.deltaTime, 0f);
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * BobSpeed) * BobHeight);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            if (!other.CompareTag("Player")) return;
            taken = true;
            CollectionSystem.Unlock(CollectionEntry.TorchItem);   // [0.4.5] 图鉴：拾取灯油

            // [0.5] 拾取进背包（不再立即扩光圈；使用时 RunManager.ApplyItemEffect 扩）
            var run = RunManager.Instance;
            if (run != null)
            {
                run.AddItem(RunItem.Torch);
                MissionSystem.OnLootCollected();   // [0.8.0] 每日任务"搜刮"进度
                Debug.Log($"[灯油] 拾取进背包 ×{run.GetItemCount(RunItem.Torch)}（使用时扩视野 +2m）");
            }
            Destroy(gameObject, 0.3f);
        }
    }
}
