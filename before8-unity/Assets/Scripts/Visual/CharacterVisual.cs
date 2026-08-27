using System.Collections.Generic;
using UnityEngine;

namespace Before8AM.Visual
{
    /// <summary>
    /// 角色视觉基础型号：挂在小人根节点，收集所有身体部位 Renderer（Body/Head/LeftArm/RightArm/LeftLeg/RightLeg/Bag）。
    /// 提供按部位名换材质/查询的接口——**皮肤系统（Skin）基于它换装：换材质=换皮肤，不必动模型**。
    /// 部位名约定：Body 身体 / Head 头 / LeftArm RightArm 手臂 / LeftLeg RightLeg 腿 / Bag 书包（仅玩家）。
    /// [0.9.0] 手臂实际由 pivot 携带 Upper/Forearm/Hand 三段，运行时自收集会把三段全注册（隐身/换肤覆盖完整手臂）。
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        readonly Dictionary<string, Renderer> parts = new Dictionary<string, Renderer>();

        void Awake()
        {
            // [0.9.0] 部位字典不序列化：EditMode 构建时 RegisterPart 的条目在场景加载后全部丢失，
            // 导致 SetPartMaterial/ApplySkin/SetGhost 静默无效（换肤不生效根因）。Awake 按命名约定自收集兜底。
            EnsurePartsRegistered();
        }

        /// <summary>[0.9.0] 运行时自收集身体部位（幂等）。方块小人部件命名（CreateOriginalBlockPlayer）：
        /// Body/Head/LeftLeg/RightLeg/Bag 为直子；手臂是 pivot 子物体，Upper/Forearm/Hand 三段全注册。</summary>
        public void EnsurePartsRegistered()
        {
            if (parts.Count > 0) return;
            var rends = GetComponentsInChildren<Renderer>(true);
            int found = 0;
            for (int i = 0; i < rends.Length; i++)
            {
                string key = rends[i].gameObject.name;
                if (!IsBodyPartName(key) || parts.ContainsKey(key)) continue;
                parts[key] = rends[i];
                found++;
            }
            if (found == 0)
                Debug.LogWarning("CharacterVisual: 未按命名约定找到身体部位（Body/Head/LeftLeg/RightLeg/Bag/LeftArm_* /RightArm_*）", this);
        }

        static bool IsBodyPartName(string n)
        {
            return n == "Body" || n == "Head" || n == "LeftLeg" || n == "RightLeg" || n == "Bag"
                || n == "LeftArm_Upper" || n == "LeftArm_Forearm" || n == "LeftArm_Hand"
                || n == "RightArm_Upper" || n == "RightArm_Forearm" || n == "RightArm_Hand";
        }

        /// <summary>注册一个身体部位（构建角色时由生成器调用）。</summary>
        public void RegisterPart(string partName, Renderer renderer)
        {
            if (renderer == null) return;
            parts[partName] = renderer;
        }

        /// <summary>查询部位 Renderer；未注册返回 null。</summary>
        public Renderer GetPart(string partName)
        {
            return parts.TryGetValue(partName, out var r) ? r : null;
        }

        /// <summary>换部位材质（皮肤系统入口）。部位不存在返回 false。</summary>
        public bool SetPartMaterial(string partName, Material mat)
        {
            var r = GetPart(partName);
            if (r == null) return false;
            r.sharedMaterial = mat;
            return true;
        }

        /// <summary>一键换整套皮肤：以"部位名→材质"映射换装。</summary>
        public void ApplySkin(Dictionary<string, Material> skin)
        {
            foreach (var kv in skin)
                SetPartMaterial(kv.Key, kv.Value);
        }

        // [0.3.0] 隐身：缓存各部位原材质，统一换半透明克隆（alpha 0.35），结束还原。重复进入不重复缓存。
        Dictionary<string, Material> ghostOriginals;

        /// <summary>[0.3.0] 隐身效果开关：全身半透明轮廓（玩家仍能看见自己在哪，比全隐好操作）。
        /// 每个部位克隆原材质并切 URP Transparent + 降 alpha；结束还原缓存的原材质。</summary>
        public void SetGhost(bool ghost)
        {
            if (ghost)
            {
                if (ghostOriginals == null)
                    ghostOriginals = new Dictionary<string, Material>();
                foreach (var kv in parts)
                {
                    Renderer r = kv.Value;
                    Material orig = r.sharedMaterial;
                    if (orig == null || ghostOriginals.ContainsKey(kv.Key)) continue;
                    ghostOriginals[kv.Key] = orig;
                    Material g = new Material(orig) { name = orig.name + "_ghost" };
                    MakeGhostTransparent(g, 0.45f);   // [0.4.1] 0.35→0.45：幽灵轮廓更清晰，玩家能明确看到"我在隐身"
                    r.sharedMaterial = g;
                }
            }
            else
            {
                if (ghostOriginals == null) return;
                foreach (var kv in ghostOriginals)
                    if (parts.TryGetValue(kv.Key, out var r) && r != null)
                        r.sharedMaterial = kv.Value;
                ghostOriginals = null;
            }
        }

        static void MakeGhostTransparent(Material g, float alpha)
        {
            // 同 VerticalSliceBuilder.MakeTransparent：URP Lit 切 Transparent surface
            g.SetFloat("_Surface", 1f);
            g.SetFloat("_Blend", 0f);
            g.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            g.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            g.SetFloat("_ZWrite", 0f);
            g.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            g.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            g.SetOverrideTag("RenderType", "Transparent");
            Color c = g.HasProperty("_BaseColor") ? g.GetColor("_BaseColor") : g.color;
            c.a = alpha;
            if (g.HasProperty("_BaseColor")) g.SetColor("_BaseColor", c);
            g.color = c;
        }

        /// <summary>整体显隐（第一人称时隐藏玩家身体，否则镜头在头里看到头/身体内部穿帮；切回 2.5D 恢复）。
        /// 遍历所有子 Renderer（含未注册的装饰件），用 renderer.enabled 而非 SetActive，小人骨骼动画照常播放。</summary>
        public void SetVisible(bool visible)
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null) rends[i].enabled = visible;
        }
    }
}
