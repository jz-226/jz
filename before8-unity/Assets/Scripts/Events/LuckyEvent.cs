using UnityEngine;
using Before8AM.Run;
using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件·幸运房间：发光幸运礼盒，碰触开大奖——随机三选一（金币 50~150 / 随机道具 / +15s）。
    /// 数据驱动目录 EventCatalog.LuckyRoom（World）。一次性。三层权重：道具最常出，时间是稀有奖。
    /// </summary>
    public class LuckyEvent : MonoBehaviour
    {
        bool taken;
        Transform _t;
        Vector3 basePos;
        bool basePosInit;

        void Awake() { _t = transform; }

        void Update()
        {
            if (taken) return;
            if (!basePosInit) { basePos = _t.position; basePosInit = true; }
            _t.Rotate(0f, 100f * Time.deltaTime, 0f);
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.18f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            if (!other.CompareTag("Player")) return;
            taken = true;

            RunManager run = RunManager.Instance;
            CollectionSystem.Unlock(CollectionEntry.LuckyRoom);
            if (run == null) { Destroy(gameObject, 0.3f); return; }

            float roll = Random.value;
            if (roll < 0.5f)
            {
                // 50%：金币 50~150
                int coins = Random.Range(50, 151);
                run.AddCoins(coins);
                Debug.Log($"[幸运房间] 抽中 {coins} 金币！");
            }
            else if (roll < 0.85f)
            {
                // 35%：随机一件道具
                RunItem item = (RunItem)Random.Range(0, ItemCatalog.Count);
                run.AddItem(item);
                Debug.Log($"[幸运房间] 抽中道具 {ItemCatalog.DisplayName(item)} x1！");
            }
            else
            {
                // 15%：稀有奖——+15s 时间
                run.AddTime(15f);
                Debug.Log("[幸运房间] 头奖——时间沙漏！倒计时 +15s");
            }
            Destroy(gameObject, 0.3f);
        }
    }
}
