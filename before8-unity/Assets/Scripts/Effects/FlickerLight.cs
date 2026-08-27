using UnityEngine;

namespace Before8AM.Effects
{
    /// <summary>
    /// [0.8.8] 午夜坏灯：挂 PointLight 上做 intensity 抖动（闪烁顶灯，强化「白天正常超市 → 午夜诡异」氛围）。
    /// 基于 baseIntensity 在 MinRatio~1 间正弦抖动，Seed 让各灯相位不同（不同步闪）。
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [Header("闪烁参数")]
        public float MinRatio = 0.2f;   // 最低亮度比例（保持微亮不死灭，避免全黑迷路）
        public float Speed = 8f;        // 闪烁速度（弧度/秒）
        public float Seed = 0f;         // 相位种子（每盏灯给不同值 → 不同步闪）

        Light targetLight;   // 不叫 light：遮蔽 Component.light（CS0108）
        float baseIntensity;

        void Awake()
        {
            targetLight = GetComponent<Light>();
            baseIntensity = targetLight.intensity;
        }

        void Update()
        {
            // abs(sin) 双倍频波形：亮-灭-亮节奏，比纯 sin 更像坏灯
            float wave = Mathf.Abs(Mathf.Sin(Time.time * Speed + Seed));
            targetLight.intensity = baseIntensity * (MinRatio + (1f - MinRatio) * wave);
        }
    }
}
