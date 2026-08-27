using UnityEngine;
using Before8AM.Player;
using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件·旧学生卡：地上陈年学生卡，拾取后守卫把你当本校学生——6 秒守卫无视。
    /// 数据驱动目录 EventCatalog.OldStudentCard（World）。一次性。
    /// 复用假学生卡道具效果（PlayerController.AddFakeCard），场景版白给一个——限时更长当福利。
    /// </summary>
    public class OldCardEvent : MonoBehaviour
    {
        [Tooltip("守卫无视时长（秒）")]
        public float Duration = 6f;

        bool taken;
        Transform _t;
        Vector3 basePos;
        bool basePosInit;

        void Awake() { _t = transform; }

        void Update()
        {
            if (taken) return;
            if (!basePosInit) { basePos = _t.position; basePosInit = true; }
            _t.Rotate(0f, 90f * Time.deltaTime, 0f);
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.12f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            if (!other.CompareTag("Player")) return;
            taken = true;

            var pc = other.GetComponent<PlayerController>();
            if (pc != null) pc.AddFakeCard(Duration);
            CollectionSystem.Unlock(CollectionEntry.OldStudentCard);
            Debug.Log($"[旧学生卡] 捡到一张泛黄的学生卡——好像能糊弄过守夜者 {Duration}s");
            Destroy(gameObject, 0.3f);
        }
    }
}
