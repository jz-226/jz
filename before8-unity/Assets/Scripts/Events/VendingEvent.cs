using UnityEngine;
using Before8AM.Run;
using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件·神秘售货机：自动售货机，碰触投 20 本局金币随机弹出一件道具（进背包）。
    /// 数据驱动目录 EventCatalog.MysteryVending（World）。一次性：出货即收摊销毁。
    /// 与午夜商人区别：无面板、即触即出、价格更低（20），但只出一件。
    /// </summary>
    public class VendingEvent : MonoBehaviour
    {
        [Tooltip("投币价格（本局金币）")]
        public int Price = 20;

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
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.15f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            if (!other.CompareTag("Player")) return;
            taken = true;

            RunManager run = RunManager.Instance;
            if (run == null) { Destroy(gameObject, 0.3f); return; }
            if (!run.SpendCoins(Price))
            {
                Debug.Log($"[神秘售货机] 金币不足（需要 {Price}，现有 {run.TemporaryCoins}），投币失败。");
                taken = false;   // 钱不够不吞货，可稍后再来
                return;
            }

            RunItem item = (RunItem)Random.Range(0, ItemCatalog.Count);
            run.AddItem(item);
            CollectionSystem.Unlock(CollectionEntry.MysteryVending);
            Debug.Log($"[神秘售货机] 叮咚——掉出 {ItemCatalog.DisplayName(item)} x1（-{Price} 金）");
            Destroy(gameObject, 0.3f);
        }
    }
}
