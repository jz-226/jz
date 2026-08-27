using UnityEngine;

namespace Before8AM.Visual
{
    /// <summary>
    /// 手电筒光圈（Fog of War）：全图暗幕 + 以玩家为圆心的亮圈。光圈**严格跟随玩家**（每帧更新
    /// 玩家位置到 shader），**纯光圈无探索记忆**——移动到哪里光照到哪里，光圈外完全黑暗，
    /// 其他地方看不见（用户反馈：不要"走过保持点亮"，也不是固定光圈）。
    /// 光圈半径可被道具（灯油）/时间碎片扩大。挂在全图暗幕 FogPlane 上。
    /// </summary>
    public class ExplorationFog : MonoBehaviour
    {
        [Header("手电筒光圈（以玩家为圆心）")]
        [Tooltip("基础光圈半径（世界单位）")]
        public float TorchBaseRadius = 8f;   // [修复] 9→8：用户反馈起始光圈略大，稍微缩小
        [Tooltip("光圈边缘软过渡（世界单位）——越小越像手电筒的清晰光圈")]
        public float TorchSoft = 1.2f;

        [Header("引用")]
        public Transform Player;
        public Material FogMat;   // FogOfWar.shader 材质

        float torchRadius;
        /// <summary>当前手电筒光圈半径（基础 + 道具/碎片加成）。</summary>
        public float TorchRadius => torchRadius;

        // [0.8.1] 回退：夜视仪已删（新道具 6 去 1），AddNightVision/NightVisionActive/FullRevealRadius 不再存在。
        /// <summary>torchRadius 是私有非序列化字段：构建时 Init 设置的值，场景重新加载（退出 Play）后会清零，
        /// 导致 FogHide 判断「距离<=0」让守卫永远隐身（用户反馈"没碰到就被杀"根因之一）。
        /// Awake 兜底：按基础半径初始化。</summary>
        void Awake()
        {
            if (torchRadius <= 0f) torchRadius = TorchBaseRadius;
        }

        /// <summary>扩大光圈半径（灯油道具 +2、时间碎片 +3）。</summary>
        public void AddRadius(float r)
        {
            torchRadius += r;
            if (FogMat != null) FogMat.SetFloat("_TorchRadius", torchRadius);
        }

        /// <summary>由构建器调用：绑定材质与玩家，设置初始光圈参数。</summary>
        public void Init(Transform player, Material fogMat)
        {
            Player = player;
            FogMat = fogMat;
            torchRadius = TorchBaseRadius;
            fogMat.SetFloat("_TorchRadius", torchRadius);
            fogMat.SetFloat("_TorchSoft", TorchSoft);
            fogMat.SetVector("_TorchPos", new Vector4(player.position.x, 0f, player.position.z, 0f));
        }

        void Update()
        {
            if (Player == null || FogMat == null) return;
            if (!Player.gameObject.activeInHierarchy) return;

            // 手电筒光圈严格跟随玩家（每帧，即使没移动也保持当前位置）
            FogMat.SetVector("_TorchPos", new Vector4(Player.position.x, 0f, Player.position.z, 0f));
        }
    }
}
