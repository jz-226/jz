using System.Collections.Generic;
using UnityEngine;
using Before8AM.Reward;

namespace Before8AM.Visual
{
    /// <summary>
    /// [0.9.0] 角色皮肤系统：静态数据表（照 CollectionCatalog 范式）+ 拥有/购买/材质/应用逻辑。
    /// 8 款皮肤：id 1-3 免费（夜海蓝原皮/薄荷绿/活力橙），id 4-7 金币购买（占 Cosmetics 位），id 0 七日挑战限定（CosmeticSevenDay）。
    /// 换肤 = CharacterVisual 换 6 身体部位材质，Bag 恒橙色（书包是玩家固定标志）。
    /// 付费皮肤用自身 Id 作解锁位（bit4-7），与七日 bit0、免费不写位无冲突。
    /// </summary>
    public static class SkinCatalog
    {
        /// <summary>一款皮肤。Price：0=免费恒拥有；&gt;0=金币购买价；七日限定由 Id==CosmeticSevenDay 判定。</summary>
        public struct SkinDef
        {
            public int Id;
            public string Name;
            public Color Color;
            public int Price;
        }

        /// <summary>8 款皮肤（顺序即画廊顺序：原皮在前，七日限定殿后）。</summary>
        public static readonly SkinDef[] All =
        {
            new SkinDef { Id = 1, Name = "夜海蓝",         Color = new Color(0.25f, 0.60f, 1.00f), Price = 0 },     // 原皮（默认装备）
            new SkinDef { Id = 2, Name = "薄荷绿",         Color = new Color(0.30f, 0.75f, 0.35f), Price = 0 },
            new SkinDef { Id = 3, Name = "活力橙",         Color = new Color(0.95f, 0.55f, 0.20f), Price = 0 },
            new SkinDef { Id = 4, Name = "雾石灰",         Color = new Color(0.55f, 0.55f, 0.58f), Price = 80 },
            new SkinDef { Id = 5, Name = "樱花粉",         Color = new Color(0.95f, 0.45f, 0.65f), Price = 120 },
            new SkinDef { Id = 6, Name = "午夜紫",         Color = new Color(0.55f, 0.30f, 0.85f), Price = 200 },
            new SkinDef { Id = 7, Name = "鎏金",           Color = new Color(0.95f, 0.75f, 0.20f), Price = 300 },
            new SkinDef { Id = 0, Name = "七日挑战·限定外观", Color = new Color(0.35f, 0.85f, 0.85f), Price = 0 }, // CosmeticSevenDay=0
        };

        /// <summary>按 id 查皮肤；未知返回 null。</summary>
        public static SkinDef? Get(int id)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>是否已拥有：免费恒拥有（永不写 Cosmetics 位）；七日=HasCosmetic(0)；付费=HasCosmetic(id)。</summary>
        public static bool IsOwned(int id)
        {
            var def = Get(id);
            if (def == null) return false;
            var d = def.Value;
            if (d.Id == GameProgress.CosmeticSevenDay) return GameProgress.HasCosmetic(GameProgress.CosmeticSevenDay);
            if (d.Price <= 0) return true;
            return GameProgress.HasCosmetic(d.Id);
        }

        /// <summary>金币购买皮肤（照 ShopController 范式：扣费成功才置位；免费/七日/已拥有/未知不可买）。</summary>
        public static bool TryBuy(int id)
        {
            var def = Get(id);
            if (def == null) return false;
            var d = def.Value;
            if (d.Price <= 0 || IsOwned(id)) return false;
            if (!GameProgress.TrySpendCoins(d.Price)) return false;
            GameProgress.UnlockCosmetic(d.Id);
            return true;
        }

        /// <summary>已装备皮肤 id：读 GameProgress.EquippedSkin，未拥有/未知回退原皮 1（防脏数据/坏键）。</summary>
        public static int ValidatedEquipped
        {
            get
            {
                int id = GameProgress.EquippedSkin;
                return IsOwned(id) ? id : 1;
            }
        }

        static readonly Dictionary<int, Material> matCache = new Dictionary<int, Material>();

        /// <summary>皮肤材质（运行时 URP Lit；静态缓存每皮肤一个，防每局重复建材质泄漏）。</summary>
        public static Material GetMaterial(int id)
        {
            if (matCache.TryGetValue(id, out var cached) && cached != null) return cached;
            var def = Get(id);
            if (def == null) return null;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh) { name = "MAT_Skin_" + id };
            Color c = def.Value.Color;
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.20f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            matCache[id] = mat;
            return mat;
        }

        /// <summary>玩家 10 个身体部位（Bag 不在此列——恒橙色，不进皮肤映射）。
        /// [0.9.0] 手臂含 Upper/Forearm/Hand 三段：只换 Upper 的话前臂/手仍是原色，必须全换。</summary>
        static readonly string[] BodyParts =
        {
            "Body", "Head", "LeftLeg", "RightLeg",
            "LeftArm_Upper", "LeftArm_Forearm", "LeftArm_Hand",
            "RightArm_Upper", "RightArm_Forearm", "RightArm_Hand",
        };

        /// <summary>应用已装备皮肤到玩家小人（PlayerController.Awake 调用；换 10 身体部位材质，Bag 恒橙）。
        /// 先 EnsurePartsRegistered（部位字典不序列化，Awake 顺序不保证，显式兜底）。
        /// 在隐身（SetGhost）之前执行，ghost 缓存的是皮肤材质，结束时还原皮肤色，天然一致。</summary>
        public static void ApplyTo(CharacterVisual visual)
        {
            if (visual == null) return;
            visual.EnsurePartsRegistered();
            Material mat = GetMaterial(ValidatedEquipped);
            if (mat == null) return;
            var map = new Dictionary<string, Material>();
            for (int i = 0; i < BodyParts.Length; i++)
                map[BodyParts[i]] = mat;
            visual.ApplySkin(map);
        }
    }
}
