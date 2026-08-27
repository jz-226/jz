using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Before8AM.Patrol;
using Before8AM.Loot;
using Before8AM.Visual;
using Before8AM.Events;

namespace Before8AM.Core
{
    /// <summary>
    /// [0.4.3] 每把随机布局：房屋街区式随机 + 守卫巡逻/驻守跟随 + 碎片/宝箱/道具随机。
    /// R 重开（LoadScene）→ 场景恢复 builder 原始布局 → 本组件 Start 重新洗牌 → 每把地图不同。
    ///
    /// 架构：生成逻辑全在 Editor 程序集（VerticalSliceBuilder），运行时无法重建物体，唯一务实路径 =
    /// 洗牌现有物体位置 + 运行时重烘焙 NavMesh。NavMeshSurface.BuildNavMesh() 同步可调用，
    /// CollectSources 按当前世界坐标收集 ModifierVolume → 建筑根节点移动后挖空自动跟随。
    ///
    /// 时序（全同步单帧，早于开场过场/守卫巡逻）：关守卫 → 记录巡逻区 → 摆建筑 → 重设 RoofFade →
    /// 烘焙 → 重生成巡逻点/驻守点 → 摆碎片/宝箱/道具 → 激活守卫（Start 的 agent.Warp 落新点）。
    /// </summary>
    public class LayoutRandomizer : MonoBehaviour
    {
        [System.Serializable]
        public class BuildingInfo
        {
            public Transform Root;
            public Vector2 Size;
            public bool SafeHouse;
            public bool RearDoor;
        }

        [Header("构建时接线（VerticalSliceBuilder 赋值，不靠 Find）")]
        public BuildingInfo[] Buildings;          // 13
        public PatrolController[] Guards;         // 7（开局全部激活）
        public PatrolController[] ReserveGuards;  // [0.8.6] 5 只增援守卫（段位增援，inactive 待 ReinforceDirector 到点激活；不入 Guards → 开局不激活）
        public NavMeshSurface Surface;            // Ground 上
        public TimeFragment[] Fragments;          // 3（Frag1 恒安全屋 / Frag2 / Frag3）
        public LootChest[] Chests;                // 3（Common / Epic / Rare）
        public Transform[] Pickups;               // 14（灯油8+加速2+沙漏2+隐身2；[0.8.1] 回退删 6 新道具）

        // ---- 校园/道路/禁区常量（对齐 VerticalSliceBuilder 实际坐标，围墙 x=±48 / z=±40）----
        const float CenterRangeX = 42f;           // 建筑中心 x 允许范围
        const float CenterRangeZMin = -36f;
        const float CenterRangeZMax = 33f;
        static readonly Rect CampusRect = new Rect(-42f, -34f, 84f, 66f);   // 巡逻点/道具全局兜底范围

        // 道路（±3m 排除带，保证井字主干道畅通）
        static readonly Rect RoadNS = new Rect(-3f, -40f, 6f, 80f);          // 晨门主大道 x∈±3（出生→晨门恒畅通）
        static readonly Rect RoadEWNorth = new Rect(-48f, -31f, 96f, 6f);    // 北区横穿 z=-28±3
        static readonly Rect RoadEWMain = new Rect(-48f, -5f, 96f, 6f);      // 中庭横穿 z=-2±3
        static readonly Rect RoadEWSouth = new Rect(-48f, 25f, 96f, 6f);     // 南区横穿 z=28±3
        static readonly Rect SpawnCorridor = new Rect(-4f, 30f, 8f, 6f);     // 出生走廊
        static readonly Rect GateCorridor = new Rect(-3f, -41f, 6f, 4f);     // 晨门前

        // 路灯 14 盏（±2m 排除带；灯柱 layer0 烘焙成障碍，建筑不能盖灯）
        static readonly Vector2[] Lamps = {
            new Vector2(-20f, -28f), new Vector2(-4f, -24f), new Vector2(12f, -18f), new Vector2(34f, -10f),
            new Vector2(-34f, -20f), new Vector2(2f, -2f), new Vector2(22f, 2f), new Vector2(36f, 10f),
            new Vector2(-30f, 6f), new Vector2(-38f, 26f), new Vector2(-6f, 18f), new Vector2(18f, 26f),
            new Vector2(46f, -4f), new Vector2(4f, 32f),
        };

        // 障碍墙 6 面（center + size；AABB 外扩 2m 排除）
        static readonly Vector2[] ObstacleCenters = {
            new Vector2(-6f, 3f), new Vector2(12f, 8f), new Vector2(-14f, 12f),
            new Vector2(40f, 2f), new Vector2(-42f, -6f), new Vector2(6f, 32f),
        };
        static readonly Vector2[] ObstacleSizes = {
            new Vector2(6f, 0.4f), new Vector2(0.4f, 5f), new Vector2(4f, 0.4f),
            new Vector2(5f, 0.4f), new Vector2(4f, 0.4f), new Vector2(7f, 0.4f),
        };

        // 候选街区（井字主干道切出的 4 大块 + 2 北条；Rect: x=minX, y=minZ, w, h）
        static readonly Rect[] Blocks = {
            new Rect(-42f, -24f, 38f, 18f),   // 西北
            new Rect(4f, -24f, 38f, 18f),      // 东北
            new Rect(-42f, 2f, 38f, 22f),      // 西南
            new Rect(4f, 2f, 38f, 22f),        // 东南
            new Rect(-42f, -35f, 38f, 4f),     // 北条西（太浅放不下多数建筑，走兜底）
            new Rect(4f, -35f, 38f, 4f),       // 北条东
        };

        int walkableMask;
        readonly List<Rect> placed = new List<Rect>();            // 已放置建筑 AABB（重叠校验）
        readonly List<Vector3> placedPickups = new List<Vector3>();   // 已放置道具位置（互斥校验）

        void Start()
        {
            if (Buildings == null || Buildings.Length == 0) return;

            // 1. 关守卫（builder 已存 inactive，双保险）：随机化+烘焙期间守卫静止，避免按旧点/旧 NavMesh 卡死
            foreach (var g in Guards) if (g != null) g.gameObject.SetActive(false);

            // 2. 记录各守卫原巡逻区域（AABB+5m，clamp 校园）——必须在建筑移动前捕获
            var regions = CaptureGuardRegions();

            // 3. 街区式放置 13 栋建筑（建筑根节点移动 = 整楼移动）
            PlaceBuildings();

            // 4. RoofFade 旧中心缓存失效 → 按新中心重设（否则屋顶不淡出）
            RefreshRoofFade();

            // 5. 运行时重烘焙 NavMesh（挖空体积随根节点移动，同步收集生效）
            if (Surface != null) Surface.BuildNavMesh();
            walkableMask = NavMesh.AllAreas & ~(1 << NavMesh.GetAreaFromName("Not Walkable"));

            // 6. 非守卫巡逻点重生成（烘焙后 SamplePosition 校验可走；守卫巡逻区跟随随机后的建筑）
            PlacePatrolPoints(regions);

            // 7. 碎片/宝箱（碎片楼先定，守卫驻守点依赖碎片楼位置）
            PlaceFragmentsAndChests();

            // 8. 守卫驻守点（Guardian_A→Frag2 楼南门 / Guardian_B→Frag3 楼南门）+ 金圈跟随
            PlaceGuardianPosts();

            // 9. 14 个道具随机（楼外净空）
            PlacePickups();
            // [0.8.1] 回退：随机事件摆位（PlaceRandomEvents）已删——随机事件暂缓。

            // 10. 激活守卫 → PatrolController.Start 的 agent.Warp 落到新巡逻点/新驻守点
            foreach (var g in Guards) if (g != null) g.gameObject.SetActive(true);

            Debug.Log("[LayoutRandomizer] 随机布局完成：13 建筑/守卫巡逻/碎片/宝箱/道具已重排");
        }

        // ---------- 建筑街区式放置 ----------

        void PlaceBuildings()
        {
            placed.Clear();
            var order = new List<BuildingInfo>(Buildings);
            order.Sort((a, b) => (b.Size.x * b.Size.y).CompareTo(a.Size.x * a.Size.y));   // 面积降序，大建筑优先占位

            foreach (var b in order)
            {
                Vector3 pos = TryPlace(b.Size);
                b.Root.position = pos;
                placed.Add(Footprint(pos, b.Size));
            }
        }

        /// <summary>街区尝试 60 次，失败兜底全局粗网格；几乎恒能放下（校园空地远多于建筑占用）。</summary>
        Vector3 TryPlace(Vector2 size)
        {
            for (int i = 0; i < 60; i++)
            {
                Rect block = Blocks[Random.Range(0, Blocks.Length)];
                float minX = block.xMin + size.x * 0.5f + 1.5f, maxX = block.xMax - size.x * 0.5f - 1.5f;
                float minZ = block.yMin + size.y * 0.5f + 1.5f, maxZ = block.yMax - size.y * 0.5f - 1.5f;
                if (minX >= maxX || minZ >= maxZ) continue;   // 该街区放不下此建筑（如 4m 深北条），换一块
                Vector2 c = new Vector2(Random.Range(minX, maxX), Random.Range(minZ, maxZ));
                if (ValidPlacement(c, size)) return new Vector3(c.x, 0f, c.y);
            }
            // 兜底：全局粗网格
            for (float z = -34f; z <= 32f; z += 3f)
                for (float x = -42f; x <= 42f; x += 3f)
                {
                    Vector2 c = new Vector2(x, z);
                    if (ValidPlacement(c, size)) return new Vector3(x, 0f, z);
                }
            // 理论不可达：远离主大道的角落（避免堵死出生→晨门通路）
            Debug.LogWarning("[LayoutRandomizer] 建筑街区放置失败，落到角落兜底");
            return new Vector3(-40f, 0f, 24f);
        }

        bool ValidPlacement(Vector2 c, Vector2 size)
        {
            float halfX = size.x * 0.5f, halfZ = size.y * 0.5f;
            // 建筑中心限位（保持井字主干道留白密度）。注意：Vector2 约定 (x, z) → (x, y)
            if (c.x < -CenterRangeX || c.x > CenterRangeX) return false;
            if (c.y < CenterRangeZMin || c.y > CenterRangeZMax) return false;
            // 足迹不出校园围墙（墙在 ±48 / ±40，留 1m）
            if (c.x - halfX < -47f || c.x + halfX > 47f) return false;
            if (c.y - halfZ < -39f || c.y + halfZ > 39f) return false;

            Rect foot = new Rect(c.x - halfX, c.y - halfZ, size.x, size.y);
            // 主干道 / 出生走廊 / 晨门走廊（恒空）
            if (foot.Overlaps(RoadNS) || foot.Overlaps(RoadEWNorth) || foot.Overlaps(RoadEWMain) || foot.Overlaps(RoadEWSouth)) return false;
            if (foot.Overlaps(SpawnCorridor) || foot.Overlaps(GateCorridor)) return false;
            // 路灯 ±2m（灯柱 layer0 是烘焙障碍，不能盖）
            for (int i = 0; i < Lamps.Length; i++)
                if (foot.Overlaps(new Rect(Lamps[i].x - 2f, Lamps[i].y - 2f, 4f, 4f))) return false;
            // 障碍墙 AABB+2m
            for (int i = 0; i < ObstacleCenters.Length; i++)
                if (foot.Overlaps(ExpandRect(Footprint(ObstacleCenters[i], ObstacleSizes[i]), 2f))) return false;
            // 已放置建筑（间距 1.5m，保证玩家/守卫能穿过）
            for (int i = 0; i < placed.Count; i++)
                if (foot.Overlaps(ExpandRect(placed[i], 1.5f))) return false;
            return true;
        }

        // ---------- RoofFade 重设 ----------

        void RefreshRoofFade()
        {
            foreach (var b in Buildings)
            {
                if (b.Root == null) continue;
                var rf = b.Root.GetComponentInChildren<RoofFade>(true);
                if (rf != null) rf.Setup(b.Root.position, b.Size);
            }
        }

        // ---------- 守卫巡逻点 / 驻守点 ----------

        struct GuardRegion { public Rect Rect; public int Count; }

        /// <summary>[0.8.6] 全部巡逻守卫（基础 7 + 增援 5）——巡逻区捕获/重排共用同一集合，保证 regions 索引与 PlacePatrolPoints 一一对应。
        /// 增援守卫不进 Guards（开局不激活），但巡逻点同样每局重排，激活时 Warp 落新点。</summary>
        List<PatrolController> AllPatrollers()
        {
            var all = new List<PatrolController>(Guards);
            if (ReserveGuards != null) all.AddRange(ReserveGuards);
            return all;
        }

        List<GuardRegion> CaptureGuardRegions()
        {
            var list = new List<GuardRegion>();
            foreach (var g in AllPatrollers())
            {
                if (g == null) { list.Add(new GuardRegion { Rect = CampusRect, Count = 0 }); continue; }
                var pts = g.PatrolPoints;
                int count = pts != null ? pts.Length : 0;
                if (count == 0) { list.Add(new GuardRegion { Rect = CampusRect, Count = 0 }); continue; }
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = pts[i].position;
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                }
                Rect r = new Rect(minX - 5f, minZ - 5f, (maxX - minX) + 10f, (maxZ - minZ) + 10f);
                list.Add(new GuardRegion { Rect = ClampToCampus(r), Count = count });
            }
            return list;
        }

        void PlacePatrolPoints(List<GuardRegion> regions)
        {
            int gi = 0;
            foreach (var g in AllPatrollers())
            {
                if (g == null || g.IsGuardian) { gi++; continue; }   // 守卫驻守点单独处理
                if (g.PatrolPoints == null || g.PatrolPoints.Length == 0) { gi++; continue; }

                // 采样本区域可走点（排除建筑+2m + SamplePosition 校验可走/不在挖空区）；不够则外扩、再全局兜底
                List<Vector3> pool = SampleWalkable(regions[gi].Rect);
                if (pool.Count < g.PatrolPoints.Length) pool = SampleWalkable(ExpandRect(regions[gi].Rect, 6f));
                if (pool.Count < g.PatrolPoints.Length) pool = SampleWalkable(CampusRect);

                Vector3[] picked = PickDistinct(pool, g.PatrolPoints.Length);
                for (int i = 0; i < picked.Length; i++)
                    g.PatrolPoints[i].position = picked[i];
                gi++;
            }
        }

        List<Vector3> SampleWalkable(Rect region)
        {
            var list = new List<Vector3>();
            const float step = 3f;
            for (float x = region.xMin; x <= region.xMax; x += step)
                for (float z = region.yMin; z <= region.yMax; z += step)
                {
                    Vector3 p = new Vector3(x, 0.5f, z);
                    if (InAnyBuilding(p, 2f)) continue;   // 不进建筑内部（挖空区 + 室内家具）
                    if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, walkableMask) && hit.hit)
                        list.Add(hit.position);
                }
            return list;
        }

        /// <summary>从可走点池挑 n 个互异点：随机起点 + 贪心取最近未用点（同连通域，巡逻闭环可走）。</summary>
        Vector3[] PickDistinct(List<Vector3> pool, int n)
        {
            var result = new Vector3[n];
            if (pool == null || pool.Count == 0)
            {
                for (int i = 0; i < n; i++) result[i] = Vector3.zero;
                return result;
            }
            var used = new bool[pool.Count];
            int cur = Random.Range(0, pool.Count);
            result[0] = pool[cur]; used[cur] = true;
            int count = 1;
            while (count < n && count < pool.Count)
            {
                int best = -1; float bestD = float.MaxValue;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (used[i]) continue;
                    float d = (pool[i] - pool[cur]).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best < 0) break;
                result[count] = pool[best]; used[best] = true; cur = best;
                count++;
            }
            while (count < n) { result[count] = result[count - 1]; count++; }   // 池子不足时补最后一个（理论不会）
            return result;
        }

        /// <summary>守卫驻守点：Guardian 第 i 个守 Fragments[i+1] 所在楼（Frag1 恒安全屋无人守）。
        /// 楼南门（+z 侧，距楼面 Random(2,4)）SamplePosition 校验；金圈 GameObject.Find 跟随。</summary>
        void PlaceGuardianPosts()
        {
            int guardianIdx = 0;
            foreach (var g in Guards)
            {
                if (g == null || !g.IsGuardian) continue;
                BuildingInfo target = null;
                int fragIndex = guardianIdx + 1;
                if (Fragments != null && Fragments.Length > fragIndex && Fragments[fragIndex] != null)
                    target = BuildingAt(Fragments[fragIndex].transform.position);
                if (target != null)
                {
                    // 楼南门外 2~4m 驻守；SamplePosition 失败（落邻楼/挖空区）就向门外每档外移 3m 重试，
                    // 楼南是校园空地，几乎必然找到可走面，保证守卫不被 Warp 进楼内
                    Vector3 post = Vector3.zero;
                    bool placed = false;
                    for (int attempt = 0; attempt < 4; attempt++)
                    {
                        float dist = Random.Range(2f, 4f) + attempt * 3f;
                        Vector3 cand = new Vector3(target.Root.position.x, 0f,
                            target.Root.position.z + target.Size.y * 0.5f + dist);
                        if (NavMesh.SamplePosition(cand, out NavMeshHit hit, 2f, walkableMask) && hit.hit)
                        { post = hit.position; placed = true; break; }
                    }
                    if (!placed)   // 极端兜底：楼南 12m 直落（校园南半场空地，几乎必然可走）
                        post = new Vector3(target.Root.position.x, 0f,
                            target.Root.position.z + target.Size.y * 0.5f + 12f);
                    if (g.PatrolPoints != null && g.PatrolPoints.Length > 0)
                        g.PatrolPoints[0].position = post;
                    GameObject ring = GameObject.Find(g.name + "_GuardRing");
                    if (ring != null) ring.transform.position = post + Vector3.up * 0.03f;
                }
                guardianIdx++;
            }
        }

        // ---------- 碎片 / 宝箱 ----------

        void PlaceFragmentsAndChests()
        {
            BuildingInfo safeHouse = null;
            var others = new List<BuildingInfo>();
            foreach (var b in Buildings)
            {
                if (b.SafeHouse) safeHouse = b;
                else others.Add(b);
            }

            // Frag1 恒在安全屋（低风险保底，规则面板已教"安全屋绿地板"）
            if (safeHouse != null && Fragments != null && Fragments.Length > 0 && Fragments[0] != null)
                Fragments[0].transform.position = ClearSpot(safeHouse, 0.7f);

            // Frag2/Frag3：随机 2 栋非安全屋，中心距 ≥25m（强迫横穿校园）。
            BuildingInfo f2 = null, f3 = null;
            if (others.Count >= 2)   // 非安全屋 ≥2 栋才摆 Frag2/Frag3（Frag1 恒安全屋已摆）
            {
                for (int t = 0; t < 80; t++)
                {
                    BuildingInfo a = others[Random.Range(0, others.Count)];
                    BuildingInfo b = others[Random.Range(0, others.Count)];
                    if (a == b) continue;
                    if (Vector2.Distance(Center2(a), Center2(b)) < 25f) continue;
                    f2 = a; f3 = b;
                    break;
                }
                if (f2 == null || f3 == null)   // 兜底：任选两栋互异（有界，防 others.Count==1 死循环）
                {
                    f2 = others[Random.Range(0, others.Count)];
                    int guard = 0;
                    do { f3 = others[Random.Range(0, others.Count)]; guard++; }
                    while (f3 == f2 && guard < 50);
                    if (f3 == f2) f3 = others[(others.IndexOf(f2) + 1) % others.Count];   // 极端兜底：必取到不同栋
                }
            }

            if (Fragments != null && Fragments.Length >= 3)
            {
                if (Fragments[1] != null) Fragments[1].transform.position = ClearSpot(f2, 0.7f);
                if (Fragments[2] != null) Fragments[2].transform.position = ClearSpot(f3, 0.7f);
            }

            // 宝箱 3 品质：随机 3 栋互异建筑，避开安全屋 + 碎片楼（奖励不堆叠、风险梯度保留）
            var candidates = new List<BuildingInfo>();
            foreach (var b in others) if (b != f2 && b != f3) candidates.Add(b);
            for (int i = candidates.Count - 1; i > 0; i--)   // Fisher-Yates 洗牌
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            int take = Mathf.Min(Chests != null ? Chests.Length : 0, candidates.Count);
            for (int i = 0; i < take; i++)
                if (Chests[i] != null) Chests[i].transform.position = ClearSpot(candidates[i], 0.7f);
        }

        /// <summary>建筑内净空点：中心 + 小幅偏移（限楼内过道，避免落家具/墙上）；OverlapAboveKnee 清场重试。</summary>
        Vector3 ClearSpot(BuildingInfo b, float y)
        {
            Vector3 c = b.Root.position;
            for (int i = 0; i < 8; i++)
            {
                float ox = i == 0 ? 0f : Random.Range(-1.2f, 1.2f);
                float oz = i == 0 ? 0f : Random.Range(-1.2f, 1.2f);
                Vector3 p = new Vector3(c.x + ox, y, c.z + oz);
                if (OverlapAboveKnee(p, 0.6f)) continue;   // 落家具/墙上，重试
                return p;
            }
            return new Vector3(c.x, y, c.z);   // 全试失败（理论不会：各内饰中央是过道），中心兜底
        }

        /// <summary>半径内是否有"高于膝盖"的固体（家具/墙）。楼层 y≈0.12 不算（bounds.max.y&lt;0.3），避免把地板当障碍。</summary>
        static bool OverlapAboveKnee(Vector3 p, float radius)
        {
            Collider[] hits = Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
                if (hits[i].bounds.max.y > 0.3f) return true;
            return false;
        }

        // ---------- 道具 ----------

        void PlacePickups()
        {
            if (Pickups == null) return;
            placedPickups.Clear();
            foreach (var p in Pickups)
            {
                if (p == null) continue;
                Vector3 pos = Vector3.zero;
                bool placed = false;
                for (int t = 0; t < 40 && !placed; t++)
                {
                    Vector3 cand = new Vector3(Random.Range(-42f, 42f), 0.3f, Random.Range(-34f, 32f));
                    if (ValidPickupSpot(cand)) { pos = cand; placed = true; }
                }
                if (!placed)   // 兜底：校园 2m 粗网格（避开已放道具），几乎必然命中
                    for (float z = -34f; z <= 32f && !placed; z += 2f)
                        for (float x = -42f; x <= 42f && !placed; x += 2f)
                        {
                            Vector3 cand = new Vector3(x, 0.3f, z);
                            if (ValidPickupSpot(cand)) { pos = cand; placed = true; }
                        }
                if (placed)
                {
                    p.position = pos;
                    placedPickups.Add(pos);
                }
                // placed==false 理论不可达（校园空地远大于 14 个道具占用），滞留原位仅作极端兜底
            }
        }

        bool ValidPickupSpot(Vector3 pos)
        {
            if (pos.x < -45f || pos.x > 45f || pos.z < -38f || pos.z > 36f) return false;   // 校园内
            if (InAnyBuilding(pos, 1.5f)) return false;   // 建筑外 +1.5m（不落楼内/贴墙）
            if (SpawnCorridor.Contains(new Vector2(pos.x, pos.z))) return false;   // 出生走廊（开局白送）
            if (GateCorridor.Contains(new Vector2(pos.x, pos.z))) return false;     // 晨门前
            for (int i = 0; i < Lamps.Length; i++)
                if (Vector2.Distance(Lamps[i], new Vector2(pos.x, pos.z)) < 1.5f) return false;
            for (int i = 0; i < ObstacleCenters.Length; i++)
                if (PointInExpanded(pos, ObstacleCenters[i], ObstacleSizes[i], 1f)) return false;
            // 与已放置道具保持 ≥1.2m 间距（不重叠贴脸）
            for (int i = 0; i < placedPickups.Count; i++)
                if (Vector3.Distance(pos, placedPickups[i]) < 1.2f) return false;
            return true;
        }

        // [0.8.1] 回退：随机事件摆位（PlaceRandomEvents / PickOutdoorSpot / TimeRifts / LureChest / SpawnedEvents）已删除——随机事件暂缓。

        // ---------- 工具 ----------

        BuildingInfo BuildingAt(Vector3 p)
        {
            foreach (var b in Buildings)
                if (Mathf.Abs(p.x - b.Root.position.x) <= b.Size.x * 0.5f &&
                    Mathf.Abs(p.z - b.Root.position.z) <= b.Size.y * 0.5f)
                    return b;
            return null;
        }

        bool InAnyBuilding(Vector3 p, float expand)
        {
            foreach (var b in Buildings)
                if (Mathf.Abs(p.x - b.Root.position.x) <= b.Size.x * 0.5f + expand &&
                    Mathf.Abs(p.z - b.Root.position.z) <= b.Size.y * 0.5f + expand)
                    return true;
            return false;
        }

        static Vector2 Center2(BuildingInfo b) => new Vector2(b.Root.position.x, b.Root.position.z);

        static Rect Footprint(Vector3 center, Vector2 size) =>
            new Rect(center.x - size.x * 0.5f, center.z - size.y * 0.5f, size.x, size.y);
        static Rect Footprint(Vector2 center, Vector2 size) =>
            new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        static Rect ExpandRect(Rect r, float m) => new Rect(r.xMin - m, r.yMin - m, r.width + m * 2f, r.height + m * 2f);

        static Rect ClampToCampus(Rect r)
        {
            float minX = Mathf.Max(r.xMin, -42f), maxX = Mathf.Min(r.xMax, 42f);
            float minZ = Mathf.Max(r.yMin, -34f), maxZ = Mathf.Min(r.yMax, 32f);
            return new Rect(minX, minZ, Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxZ - minZ));
        }

        static bool PointInExpanded(Vector3 p, Vector2 center, Vector2 size, float expand) =>
            Mathf.Abs(p.x - center.x) <= size.x * 0.5f + expand &&
            Mathf.Abs(p.z - center.y) <= size.y * 0.5f + expand;
    }
}
