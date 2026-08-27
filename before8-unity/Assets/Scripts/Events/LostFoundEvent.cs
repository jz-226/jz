using UnityEngine;
using Before8AM.Run;
using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件·遗失物招领处：失物招领箱，碰触寻回失物 +40 金币 +20 XP。
    /// 数据驱动目录 EventCatalog.LostAndFound（World）。一次性。
    /// </summary>
    public class LostFoundEvent : MonoBehaviour
    {
        [Tooltip("寻回金币")]
        public int Coins = 40;
        [Tooltip("寻回 XP")]
        public int XP = 20;

        bool taken;
        Transform _t;
        Vector3 basePos;
        bool basePosInit;

        void Awake() { _t = transform; }

        void Update()
        {
            if (taken) return;
            if (!basePosInit) { basePos = _t.position; basePosInit = true; }
            _t.Rotate(0f, 60f * Time.deltaTime, 0f);
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.12f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            if (!other.CompareTag("Player")) return;
            taken = true;

            RunManager run = RunManager.Instance;
            run?.AddCoins(Coins);
            run?.AddXP(XP);
            CollectionSystem.Unlock(CollectionEntry.LostAndFound);
            Debug.Log($"[遗失物招领处] 找回失物：+{Coins} 金币、+{XP} XP");
            Destroy(gameObject, 0.3f);
        }
    }
}
