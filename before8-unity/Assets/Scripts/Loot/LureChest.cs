using UnityEngine;
using Before8AM.Patrol;
using Before8AM.World;
using Before8AM.Collection;

namespace Before8AM.Loot
{
    /// <summary>
    /// [0.4.4] 随机事件·诱饵宝箱：暗红发光外观（区别于真宝箱棕/蓝/金），瞬时交互（E 即触发，无需长按）。
    /// 开箱 = 报警：最近守卫被引到诱饵点搜索（引开守卫玩法，与 Guardian 金圈同构）。无金币产出——诱饵是陷阱。
    /// 已被触发后变灰 + CanInteract=false（不再可交互）。
    /// </summary>
    public class LureChest : Interactable
    {
        public override string PromptText => opened ? "（已触发）" : "检查";
        public override bool RequiresHold => false;   // 瞬时交互（基类默认）
        public override bool CanInteract => !opened;

        bool opened;
        Transform _t;
        Vector3 baseScale;

        void Awake()
        {
            _t = transform;
            baseScale = _t.localScale;
        }

        public override void Interact()
        {
            if (opened) return;
            opened = true;
            CollectionSystem.Unlock(CollectionEntry.LureChest);   // [0.4.5] 图鉴：触发诱饵宝箱（交互即算，无条件）

            // 报警视觉：闪红 + 回弹（照 LootChest Juice 简化版）
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = new Color(1f, 0.25f, 0.2f);
            _t.localScale = baseScale * 1.15f;

            // 最近守卫被引到诱饵点搜索（正在追玩家的守卫 AlertAt 内部跳过，不会坑玩家）
            PatrolController nearest = null;
            float best = float.MaxValue;
            foreach (var g in FindObjectsOfType<PatrolController>())
            {
                if (g == null) continue;
                float d = Vector3.Distance(g.transform.position, _t.position);
                if (d < best) { best = d; nearest = g; }
            }
            if (nearest != null)
            {
                nearest.AlertAt(_t.position);
                Debug.Log($"[诱饵宝箱] 报警：{nearest.name} 被引向诱饵点搜索");
            }
            else
            {
                Debug.Log("[诱饵宝箱] 报警（当前无守卫在场）");
            }
        }
    }
}
