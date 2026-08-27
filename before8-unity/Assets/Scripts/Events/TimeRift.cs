using UnityEngine;
using Before8AM.Run;
using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>
    /// [0.4.4] 随机事件·时间裂缝：青色发光裂缝，碰触回退倒计时 +15s（复用 RunManager.AddTime）。
    /// 每把 2 个随机位置（LayoutRandomizer 摆楼外净空）。旋转 + 漂浮表现。
    /// basePos 首帧惰性捕获（[0.4.3 范式]：LayoutRandomizer.Start 先移动物体，首帧 Update 才记录随机化后位置）。
    /// </summary>
    public class TimeRift : MonoBehaviour
    {
        [Tooltip("碰触回退的秒数（加时）")]
        public float AddSeconds = 15f;
        public float RotateSpeed = 60f;
        public float BobSpeed = 2.5f;
        public float BobHeight = 0.2f;

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
            if (!basePosInit) { basePos = _t.position; basePosInit = true; }
            _t.Rotate(0f, RotateSpeed * Time.deltaTime, 0f);
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * BobSpeed) * BobHeight);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            if (!other.CompareTag("Player")) return;
            taken = true;
            CollectionSystem.Unlock(CollectionEntry.TimeRift);   // [0.4.5] 图鉴：触发时间裂缝
            RunManager run = RunManager.Instance;
            run?.AddTime(AddSeconds);
            float left = run != null ? run.TimeLeft : 0f;
            Debug.Log($"[时间裂缝] 回退 {AddSeconds}s → 剩余 {left:0} 秒");
            Destroy(gameObject, 0.3f);
        }
    }
}
