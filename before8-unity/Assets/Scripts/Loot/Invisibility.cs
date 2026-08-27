using UnityEngine;
using Before8AM.Run;
using Before8AM.Collection;
using Before8AM.Mission;   // [0.8.0] 每日任务"搜刮"进度

namespace Before8AM.Loot
{
    /// <summary>
    /// [0.4.1] 隐身药水：**[0.5] 拾取进背包，使用时 6 秒内完全隐身**（RunManager.ApplyItemEffect 数值，与旧"拾取即用"一致）。
    /// 期间守卫看不见、听不见（奔跑也不出声）、贴身不抓捕，追击中的守卫当场失去目标去消失点搜索（PatrolController 全部拦截）。
    /// 到期后有 1.5s 感知宽限（守卫仍找不到你），防"最后一帧"猝死。
    /// 不是无敌：时长短 + 限量，宽限过后若还在守卫附近仍会被重新发现。
    /// 拾取进背包，手动使用（TorchItem 模式）。
    /// </summary>
    public class Invisibility : MonoBehaviour
    {
        public float RotateSpeed = 80f;
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
            CollectionSystem.Unlock(CollectionEntry.InvisibilityPotion);   // [0.4.5] 图鉴：拾取隐身药水

            // [0.5] 拾取进背包（不再立即隐身；使用时 RunManager.ApplyItemEffect）
            var run = RunManager.Instance;
            if (run != null)
            {
                run.AddItem(RunItem.InvisibilityPotion);
                MissionSystem.OnLootCollected();   // [0.8.0] 每日任务"搜刮"进度
                Debug.Log($"[隐身] 拾取进背包 ×{run.GetItemCount(RunItem.InvisibilityPotion)}（使用时 6s 守卫看不见）");
            }
            Destroy(gameObject, 0.3f);
        }
    }
}
