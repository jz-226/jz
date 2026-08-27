using UnityEngine;
using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件·传送门：一对发光门洞，碰触 A 传送到 B（单向，每扇对应另一扇）。
    /// 数据驱动目录 EventCatalog.PortalEvent（World）。两扇都保留（不销毁），0.8s 冷却防来回刷。
    /// 垂直旋转的发光门面，进 B 同理传回 A。
    /// </summary>
    public class PortalEvent : MonoBehaviour
    {
        /// <summary>配对门（VerticalSliceBuilder 创建两扇后互设）。</summary>
        public PortalEvent Pair;

        [Tooltip("传送冷却（秒）：碰触后短时间不重复传）")]
        public float Cooldown = 0.8f;

        Transform _t;
        Vector3 basePos;
        bool basePosInit;
        float cooldownTimer;
        float yaw;

        void Awake() { _t = transform; }

        void Update()
        {
            if (!basePosInit) { basePos = _t.position; basePosInit = true; }
            yaw += 120f * Time.deltaTime;
            _t.rotation = Quaternion.Euler(0f, yaw, 0f);
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.12f);
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (cooldownTimer > 0f) return;
            if (Pair == null) return;

            cooldownTimer = Cooldown;
            if (Pair != null) Pair.cooldownTimer = Cooldown;   // 目标门同样进冷却，防 A→B→A 秒回

            // 玩家瞬移到目标门正上方（CharacterController 由 Teleport 重定位）
            Vector3 dest = Pair.transform.position + Vector3.up * 1.2f;
            var pc = other.GetComponent<Before8AM.Player.PlayerController>();
            if (pc != null) pc.Teleport(dest);
            else other.transform.position = dest;

            CollectionSystem.Unlock(CollectionEntry.PortalEvent);
            Debug.Log("[传送门] 穿过门洞——传送到另一扇门前");
        }
    }
}
