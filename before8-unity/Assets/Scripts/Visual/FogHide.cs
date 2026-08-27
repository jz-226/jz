using UnityEngine;
using Before8AM.Camera;
using Before8AM.Player;   // [0.8.0] 探测器：守卫穿墙高亮

namespace Before8AM.Visual
{
    /// <summary>
    /// 守卫隐身（配合手电筒光圈）：守卫的 renderer 只在**玩家光圈半径内**显示，
    /// 光圈外完全隐藏（即使走过的地方是亮的也不显示守卫）——"守夜者靠近我才能看见他"
    /// （用户反馈：守卫不该自带发光，除非靠近才现形）。**光线穿不过墙**：被墙/障碍挡住的
    /// 守卫也不显示（防止"隔墙冒出来秒杀"）。挂在守卫根节点。
    /// AI 不受影响（仍会移动/追击，玩家撞上仍被抓），只是不可见。
    /// </summary>
    public class FogHide : MonoBehaviour
    {
        public ExplorationFog Fog;
        [Tooltip("显示缓冲：守卫在光圈半径外这多远处就开始显示（提前预警，防止守卫「突然闯进光圈」吓人）。光圈是软边渐隐，守卫到边缘才「突然出现」就离玩家很近了")]
        public float RevealBuffer = 3f;
        Renderer[] rends;
        bool cached;

        void Start() { Cache(); }

        void Cache()
        {
            if (cached) return;
            rends = GetComponentsInChildren<Renderer>(true);   // 含视野锥
            cached = true;
        }

        void Update()
        {
            Cache();

            // 第一人称：守卫永远可见——光圈概念失效，若仍按光圈隐藏，守卫会在玩家正前方凭空消失/出现（穿帮）。
            // AI/抓捕不受影响，只是始终显示。切回 2.5D 自动恢复隐身规则。
            if (ViewToggle.IsFirstPerson)
            {
                for (int i = 0; i < rends.Length; i++)
                    if (rends[i] != null) rends[i].enabled = true;
                return;
            }

            if (Fog == null) Fog = FindObjectOfType<ExplorationFog>();

            // [0.8.0] 探测器：无视光圈距离与墙体遮挡，守卫强制高亮现形（穿墙透视定位）。
            var pc = Fog != null && Fog.Player != null ? Fog.Player.GetComponent<PlayerController>() : null;
            bool detectorActive = pc != null && pc.DetectorActive;

            bool show = true;
            if (Fog != null && Fog.Player != null)
            {
                float dist = Vector3.Distance(transform.position, Fog.Player.position);
                // 光圈内可见 + 光圈外 RevealBuffer 内也可见（边缘提前显形，防止"突然闯进光圈"吓人）
                show = dist <= Fog.TorchRadius + RevealBuffer;
                if (show)
                {
                    // 光线穿不过墙：被墙/障碍挡住的守卫也不显示（防止"隔墙冒出来秒杀"）。
                    // 撞到玩家自身 / 守卫自身碰撞体不算遮挡。
                    Vector3 eye = Fog.Player.position + Vector3.up * 1.2f;
                    Vector3 target = transform.position + Vector3.up * 1f;
                    if (Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform != Fog.Player && !hit.collider.transform.IsChildOf(transform))
                            show = false;
                    }
                }
            }
            if (detectorActive) show = true;   // [0.8.0] 探测器优先级最高：穿墙也强制显示
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null) rends[i].enabled = show;
        }
    }
}
