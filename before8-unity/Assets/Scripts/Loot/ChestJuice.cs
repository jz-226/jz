using System.Collections.Generic;
using UnityEngine;

namespace Before8AM.Loot
{
    /// <summary>
    /// [0.8.0] 宝箱开箱 Juice（纯代码，无 ParticleSystem 依赖）：
    /// 1) 开箱瞬间 12 个金色小方块沿随机方向飞散（带重力，0.8s 销毁）；
    /// 2) 箱子缩放回弹（1 → 1.35 → 1）；
    /// 3) 开箱内容文字从箱子上方浮起渐隐（OnGUI 世界投影）。
    /// 挂宝箱同物体，LootChest.Interact 调 Burst()。材质 clone 箱子材质（继承发光色）。
    /// </summary>
    public class ChestJuice : MonoBehaviour
    {
        string floatText;
        float floatTimer;
        readonly List<Transform> shards = new List<Transform>();
        readonly List<Vector3> shardVel = new List<Vector3>();
        float shardLife;
        Material shardMat;
        Vector3 baseScale;
        bool popped;
        float popT;

        /// <summary>开箱爆发：spawn 飞散小方块 + 记录上浮文字。</summary>
        public void Burst(string text, Material sourceMat)
        {
            floatText = text;
            floatTimer = 2.2f;

            if (shardMat == null && sourceMat != null)
                shardMat = new Material(sourceMat);   // 继承箱子材质（含发射色），独立副本防共享污染

            shards.Clear();
            shardVel.Clear();
            for (int i = 0; i < 12; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "ChestShard";
                go.layer = 2;   // Ignore Raycast：不干扰交互/烘焙
                go.transform.position = transform.position + Vector3.up * 0.6f;
                go.transform.localScale = Vector3.one * 0.09f;
                var r = go.GetComponent<Renderer>();
                if (r != null && shardMat != null) r.material = shardMat;
                var c = go.GetComponent<Collider>();
                if (c != null) c.enabled = false;
                shards.Add(go.transform);
                shardVel.Add(Vector3.up * Random.Range(2.5f, 4.5f)
                    + new Vector3(Random.Range(-1.6f, 1.6f), 0f, Random.Range(-1.6f, 1.6f)));
            }
            shardLife = 0.8f;

            popped = true;
            popT = 0f;
            baseScale = transform.localScale;
        }

        void Update()
        {
            if (floatTimer > 0f) floatTimer -= Time.deltaTime;

            // 箱子弹跳：0 → π 正弦峰值 1.35 → 回弹 1
            if (popped)
            {
                popT += Time.deltaTime * 3f;
                float t = Mathf.Clamp01(popT);
                float s = 1f + 0.35f * Mathf.Sin(t * Mathf.PI);
                transform.localScale = baseScale * s;
                if (t >= 1f) popped = false;
            }

            // 飞散小方块
            if (shardLife > 0f)
            {
                shardLife -= Time.deltaTime;
                for (int i = 0; i < shards.Count; i++)
                {
                    if (shards[i] == null) continue;
                    shards[i].position += shardVel[i] * Time.deltaTime;
                    shardVel[i] -= Vector3.up * 5f * Time.deltaTime;   // 重力拉回
                    shards[i].Rotate(0f, 360f * Time.deltaTime, 0f);
                }
                if (shardLife <= 0f)
                {
                    for (int i = 0; i < shards.Count; i++)
                        if (shards[i] != null) Destroy(shards[i].gameObject);
                    shards.Clear();
                    shardVel.Clear();
                }
            }
        }

        void OnGUI()
        {
            if (floatTimer <= 0f) return;
            UnityEngine.Camera cam = UnityEngine.Camera.main;   // 全限定：Before8AM.Camera 命名空间与类型同名
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.1f);
            if (sp.z < 0f) return;   // 在相机背后不画
            float screenY = Screen.height - sp.y;   // GUI 坐标 y 向下

            float alpha = Mathf.Clamp01(floatTimer / 1.0f);   // 最后 1s 渐隐
            float rise = (2.2f - floatTimer) * Screen.height * 0.06f;   // 随时间上浮

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.032f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = new Color(1f, 0.88f, 0.4f, alpha);
            // [0.9.2+] 行高按字号自适应（原固定 50f 高度，竖屏字号 ~61px 行高 ~73px 时上下被裁）
            int rowH = Mathf.RoundToInt(style.fontSize * 1.35f);
            GUI.Label(new Rect(sp.x - 260f, screenY - rowH - rise, 520f, rowH), floatText, style);
        }
    }
}
