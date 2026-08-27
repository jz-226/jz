using UnityEngine;
using Before8AM.Run;

namespace Before8AM.World
{
    /// <summary>
    /// 晨门：集齐时间碎片后 ACTIVE，靠近按 E 持续 5 秒开启，触发成功撤离（规格书 53/54/55）。
    /// 状态流：LOCKED → ACTIVE → ESCAPED。成功时保留本局战利品进入结算。
    /// </summary>
    public class ExitGate : Interactable
    {
        public float EscapeHoldDuration = 5f;
        public Renderer GateRenderer;
        public Color LockedColor = new Color(0.30f, 0.30f, 0.42f);
        public Color ActiveColor = new Color(0.40f, 0.80f, 1.00f);

        public bool IsActive => RunManager.Instance != null && RunManager.Instance.AllFragmentsCollected;

        public override string PromptText => !IsActive
            ? $"晨门（需 {RunManager.Instance?.TimeFragmentsRequired ?? 3} 时间碎片）"
            : "开启晨门";

        public override bool RequiresHold => IsActive;
        public override float HoldDuration => EscapeHoldDuration;

        void Start()
        {
            if (GateRenderer != null)
                GateRenderer.material.color = LockedColor;
        }

        void Update()
        {
            if (GateRenderer == null) return;
            Color target = IsActive ? ActiveColor : LockedColor;
            if (GateRenderer.material.color != target)
                GateRenderer.material.color = Color.Lerp(GateRenderer.material.color, target, 4f * Time.deltaTime);
        }

        public override void Interact()
        {
            if (!IsActive) return;
            RunManager run = RunManager.Instance;
            if (run != null && run.State == RunState.Running)
            {
                Debug.Log("[晨门] 撤离成功！");
                run.Escape();
            }
        }
    }
}
