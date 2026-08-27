using UnityEngine;
using Before8AM.Run;
using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件·深夜食堂：发光餐盘，碰触吃宵夜补给——倒计时 +15s + 30 XP。
    /// 数据驱动目录 EventCatalog.LateNightCanteen（World）。一次性。
    /// 与时间裂缝（纯 +15s）区分：额外给 XP，且是"食物"定位（摆摊发光盘）。
    /// </summary>
    public class CanteenEvent : MonoBehaviour
    {
        [Tooltip("宵夜加时秒数")]
        public float AddSeconds = 15f;
        [Tooltip("宵夜 XP")]
        public int XP = 30;

        bool taken;
        Transform _t;
        Vector3 basePos;
        bool basePosInit;

        void Awake() { _t = transform; }

        void Update()
        {
            if (taken) return;
            if (!basePosInit) { basePos = _t.position; basePosInit = true; }
            _t.Rotate(0f, 80f * Time.deltaTime, 0f);
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.15f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            if (!other.CompareTag("Player")) return;
            taken = true;

            RunManager run = RunManager.Instance;
            run?.AddTime(AddSeconds);
            run?.AddXP(XP);
            CollectionSystem.Unlock(CollectionEntry.LateNightCanteen);
            Debug.Log($"[深夜食堂] 深夜食堂还开着——吃饱喝足，倒计时 +{AddSeconds}s、+{XP} XP");
            Destroy(gameObject, 0.3f);
        }
    }
}
