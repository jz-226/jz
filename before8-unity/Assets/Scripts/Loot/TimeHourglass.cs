using UnityEngine;
using Before8AM.Run;
using Before8AM.Collection;
using Before8AM.Mission;   // [0.8.0] 每日任务"搜刮"进度

namespace Before8AM.Loot
{
    /// <summary>
    /// [0.3.0] 时间沙漏：**[0.5] 拾取进背包，使用时倒计时回退 +20 秒**（RunManager.ApplyItemEffect 数值，与旧"拾取即用"一致）。
    /// 沙漏是主要补时来源——但拿它要绕路去角落，本身消耗时间，收益要玩家权衡。
    /// 照抄 TorchItem 自动拾取模式。
    /// </summary>
    public class TimeHourglass : MonoBehaviour
    {
        public float RotateSpeed = 60f;
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
            CollectionSystem.Unlock(CollectionEntry.TimeHourglass);   // [0.4.5] 图鉴：拾取时间沙漏

            // [0.5] 拾取进背包（不再立即加时；使用时 RunManager.ApplyItemEffect）
            var run = RunManager.Instance;
            if (run != null)
            {
                run.AddItem(RunItem.TimeHourglass);
                MissionSystem.OnLootCollected();   // [0.8.0] 每日任务"搜刮"进度
                Debug.Log($"[沙漏] 拾取进背包 ×{run.GetItemCount(RunItem.TimeHourglass)}（使用时 +20s）");
            }
            Destroy(gameObject, 0.3f);
        }
    }
}
