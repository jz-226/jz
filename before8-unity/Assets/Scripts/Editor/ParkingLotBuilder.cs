using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;   // NavMeshSurface / CollectObjects（AI Navigation 包，同 VerticalSliceBuilder）
using Before8AM.Camera;
using Before8AM.Collection;   // CollectionView（结算面板/图鉴视图）
using Before8AM.Core;
using Before8AM.Events;
using Before8AM.Input;
using Before8AM.Loot;
using Before8AM.Patrol;
using Before8AM.Player;
using Before8AM.Reward;
using Before8AM.Run;
using Before8AM.Visual;
using Before8AM.World;
using Before8AM.Effects;   // [0.8.8] FlickerLight 午夜坏灯

namespace Before8AM.EditorTools
{
    /// <summary>
    /// [0.8.8] 第二张地图：午夜校园超市（原地下停车场重做——用户反馈停车场不贴「白天正常校园→午夜诡异」的
    /// 双时空理念，且空旷无视线死角）。
    /// 布局 = 64×44 超市：入口收银台区 → 6 排货架夹道（1.8m 实体：挡视线+挡移动+NavMesh 守卫绕行，
    /// 断口错位制造转角视线死角）→ 北区冷柜/饮料架/后仓（危险藏宝区）。
    /// 高难度不变：守卫 4（视野更远、追得更快）+ 一局 420s + 无安全屋。
    /// 午夜氛围：暖黄顶灯 ×7（2 盏闪烁 FlickerLight）+ 冷柜冷蓝发光条 + 收银机微亮 + 极黑迷雾。
    /// 菜单：Before8AM > 1.7 Build Parking Lot Scene。
    /// </summary>
    public static class ParkingLotBuilder
    {
        public const string ScenePath = "Assets/Scenes/ParkingLot/ParkingLot.unity";

        [MenuItem("Before8AM/1.7 Build Parking Lot Scene")]
        public static void BuildParkingLot()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[构建] 请先退出 Play 模式，再重建停车场场景（NewScene 在 Play 中被 Unity 禁止）。");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- 超市材质（货架金属灰 + 收银台白 + 冷柜冷蓝 + 品质宝箱/碎片/晨门） ----------
            Material groundMat = VerticalSliceBuilder.MakeMaterial(new Color(0.125f, 0.157f, 0.227f), "MAT_SM_Ground");
            Material wallMat  = VerticalSliceBuilder.MakeMaterial(new Color(0.165f, 0.220f, 0.329f), "MAT_SM_Wall");
            Material shelfMat = VerticalSliceBuilder.MakeMaterial(new Color(0.592f, 0.635f, 0.706f), "MAT_SM_Shelf");       // 冷灰金属
            Material shelfTrimMat = VerticalSliceBuilder.MakeMaterial(new Color(0.265f, 0.325f, 0.430f), "MAT_SM_ShelfTrim");
            Material counterMat = VerticalSliceBuilder.MakeMaterial(new Color(0.545f, 0.408f, 0.294f), "MAT_SM_Counter");
            Material registerMat = VerticalSliceBuilder.MakeMaterial(new Color(1.0f, 0.698f, 0.376f), "MAT_SM_Register", emissive: true, emissiveColor: new Color(1.4f, 1.0f, 0.55f));
            Material freezerMat = VerticalSliceBuilder.MakeMaterial(new Color(0.173f, 0.220f, 0.314f), "MAT_SM_Freezer");
            Material freezerGlowMat = VerticalSliceBuilder.MakeMaterial(new Color(0.46f, 0.78f, 1.0f), "MAT_SM_FreezerGlow", emissive: true, emissiveColor: new Color(0.7f, 1.15f, 1.65f));
            Material fridgeMat = VerticalSliceBuilder.MakeMaterial(new Color(0.125f, 0.157f, 0.227f), "MAT_SM_DrinkFridge");
            Material cartMat = VerticalSliceBuilder.MakeMaterial(new Color(0.592f, 0.635f, 0.706f), "MAT_SM_Cart");
            Material itemMat = VerticalSliceBuilder.MakeMaterial(new Color(0.816f, 0.541f, 0.275f), "MAT_SM_Item");
            Material chestMat = VerticalSliceBuilder.MakeMaterial(new Color(0.478f, 0.329f, 0.220f), "MAT_ChestCommon");
            Material chestRareMat = VerticalSliceBuilder.MakeMaterial(new Color(0.369f, 0.561f, 0.910f), "MAT_ChestRare", emissive: true, emissiveColor: new Color(0.45f, 0.75f, 1.45f));
            Material chestEpicMat = VerticalSliceBuilder.MakeMaterial(new Color(1.0f, 0.820f, 0.40f), "MAT_ChestEpic", emissive: true, emissiveColor: new Color(1.65f, 1.25f, 0.40f));
            Material chestLegendMat = VerticalSliceBuilder.MakeMaterial(new Color(0.941f, 0.471f, 0.259f), "MAT_ChestLegend", emissive: true, emissiveColor: new Color(1.9f, 0.85f, 0.35f));
            Material chestRelicMat = VerticalSliceBuilder.MakeMaterial(new Color(0.878f, 0.608f, 0.910f), "MAT_ChestRelic", emissive: true, emissiveColor: new Color(1.6f, 0.9f, 1.7f));
            Material fragMat = VerticalSliceBuilder.MakeMaterial(new Color(1.0f, 0.886f, 0.478f), "MAT_SM_Fragment", emissive: true, emissiveColor: new Color(1.8f, 1.45f, 0.55f));
            Material gateMat  = VerticalSliceBuilder.MakeMaterial(new Color(1f, 0.85f, 0.4f), "MAT_SM_Gate", emissive: true);
            Material frameMat = VerticalSliceBuilder.MakeMaterial(new Color(0.95f, 0.80f, 0.5f), "MAT_SM_GateFrame", emissive: true, emissiveColor: new Color(1.35f, 1.05f, 0.55f));
            Material torchMat = VerticalSliceBuilder.MakeMaterial(new Color(1.0f, 0.906f, 0.651f), "MAT_SM_Torch", emissive: true, emissiveColor: new Color(1.6f, 1.35f, 0.75f));
            Material speedMat = VerticalSliceBuilder.MakeMaterial(new Color(0.945f, 0.357f, 0.290f), "MAT_SM_Speed", emissive: true, emissiveColor: new Color(1.55f, 0.40f, 0.32f));
            Material hourglassMat = VerticalSliceBuilder.MakeMaterial(new Color(1.0f, 0.820f, 0.40f), "MAT_SM_Hourglass", emissive: true, emissiveColor: new Color(1.65f, 1.25f, 0.4f));
            Material invisMat = VerticalSliceBuilder.MakeMaterial(new Color(0.373f, 0.851f, 1.0f, 0.55f), "MAT_SM_Invisibility", transparent: true);
            Material patrolMat = VerticalSliceBuilder.MakeMaterial(new Color(0.063f, 0.075f, 0.129f), "MAT_SM_Patrol");
            Material runnerMat = VerticalSliceBuilder.MakeMaterial(new Color(0.635f, 0.278f, 0.302f), "MAT_SM_Runner");
            Material guardianMat = VerticalSliceBuilder.MakeMaterial(new Color(0.831f, 0.651f, 0.227f), "MAT_SM_Guardian", emissive: true, emissiveColor: new Color(1.15f, 0.85f, 0.30f));
            Material guardRingMat = VerticalSliceBuilder.MakeMaterial(new Color(1f, 0.85f, 0.3f, 0.35f), "MAT_SM_GuardRing", transparent: true);
            Material playerMat = VerticalSliceBuilder.MakeMaterial(new Color(0.435f, 0.659f, 1.0f), "MAT_SM_Player");

            ApplyEnvironmentTextures(groundMat, wallMat, shelfMat, counterMat, freezerMat);

            // ---------- 超市几何：地面 + 周墙（南北门洞，同原停车场外壳） ----------
            const float mapW = 64f, mapZ = 44f, wallH = 4f, wallThick = 0.4f;
            GameObject ground = CreateStoreBlock("Ground", new Vector3(0, -0.5f, 0), new Vector3(mapW, 1, mapZ), groundMat, groundMat, 4);

            // 周墙四段；南北墙中央留 5m 门洞（南=玩家入口 z=+22，北=晨门出口 z=-22）
            const float gap = 5f;
            const float halfW = mapW * 0.5f;
            const float halfGap = gap * 0.5f;
            const float segW = halfW - halfGap;
            const float segC = (halfW + halfGap) * 0.5f;
            CreateStoreBlock("Wall_N_L", new Vector3(-segC, 2f, -mapZ * 0.5f - wallThick * 0.5f), new Vector3(segW, wallH, wallThick), wallMat, shelfTrimMat, 4);
            CreateStoreBlock("Wall_N_R", new Vector3(segC, 2f, -mapZ * 0.5f - wallThick * 0.5f), new Vector3(segW, wallH, wallThick), wallMat, shelfTrimMat, 4);
            CreateStoreBlock("Wall_S_L", new Vector3(-segC, 2f, mapZ * 0.5f + wallThick * 0.5f), new Vector3(segW, wallH, wallThick), wallMat, shelfTrimMat, 4);
            CreateStoreBlock("Wall_S_R", new Vector3(segC, 2f, mapZ * 0.5f + wallThick * 0.5f), new Vector3(segW, wallH, wallThick), wallMat, shelfTrimMat, 4);
            CreateStoreBlock("Wall_E", new Vector3(mapW * 0.5f + wallThick * 0.5f, 2f, 0f), new Vector3(wallThick, wallH, mapZ), wallMat, shelfTrimMat, 4);
            CreateStoreBlock("Wall_W", new Vector3(-mapW * 0.5f - wallThick * 0.5f, 2f, 0f), new Vector3(wallThick, wallH, mapZ), wallMat, shelfTrimMat, 4);

            // ---------- 收银台区（南门入口正对，一排 4 台：矮掩体 + 台后员工通道 = 藏身点 1） ----------
            float[] counterX = { -18f, -6f, 6f, 18f };
            for (int i = 0; i < counterX.Length; i++)
            {
                CreateStoreCounter($"Counter_{i + 1}", new Vector3(counterX[i], 0.55f, 17f), new Vector3(1.8f, 1.1f, 0.9f), counterMat, shelfTrimMat);
                // 收银机屏幕（微亮，午夜无人的收银台）
                GameObject scr = VerticalSliceBuilder.CreateCube($"Register_{i + 1}", new Vector3(counterX[i], 1.22f, 16.6f), new Vector3(0.3f, 0.12f, 0.5f), registerMat);
                scr.layer = 2;
            }

            // ---------- 货架区：6 排 × 分段，1.8m 高实体（挡视线 + 挡移动 + 进 NavMesh 守卫绕行） ----------
            // 断口设计：R1/R3/R5 中央断口（x=0），R2/R4/R6 双侧断口（x=±13）交替 →
            // 东西穿行必须 Z 字绕断口，断口转角 = 守卫视线死角（排端头挡 Linecast）。
            // 排中心距 3.4 → 夹道净宽 2.6m（玩家 0.4 胶囊 + 守卫 0.5 胶囊贴身可过）。
            float[] shelfZ = { 8.2f, 4.8f, 1.4f, -2.0f, -5.4f, -8.8f };
            for (int r = 0; r < shelfZ.Length; r++)
            {
                bool centerGap = (r % 2 == 0);   // R1/R3/R5：中央断口
                if (centerGap)
                {
                    CreateShelfSegment($"Shelf_{r + 1}_A", new Vector3(-15.5f, 0.9f, shelfZ[r]), new Vector3(25f, 1.8f, 0.8f), shelfMat, shelfTrimMat);
                    CreateShelfSegment($"Shelf_{r + 1}_B", new Vector3(15.5f, 0.9f, shelfZ[r]), new Vector3(25f, 1.8f, 0.8f), shelfMat, shelfTrimMat);
                }
                else
                {
                    CreateShelfSegment($"Shelf_{r + 1}_A", new Vector3(-21.5f, 0.9f, shelfZ[r]), new Vector3(13f, 1.8f, 0.8f), shelfMat, shelfTrimMat);
                    CreateShelfSegment($"Shelf_{r + 1}_B", new Vector3(0f, 0.9f, shelfZ[r]), new Vector3(22f, 1.8f, 0.8f), shelfMat, shelfTrimMat);
                    CreateShelfSegment($"Shelf_{r + 1}_C", new Vector3(21.5f, 0.9f, shelfZ[r]), new Vector3(13f, 1.8f, 0.8f), shelfMat, shelfTrimMat);
                }
            }

            // ---------- 北区：冷柜（冷蓝发光条，1.8m 实体挡视线）+ 饮料架 + 后仓 ----------
            // 冷柜两段（中央留 6m 晨门走廊），前脸冷蓝发光条 = 午夜氛围
            CreateStoreBlock("Freezer_W", new Vector3(-15.5f, 0.9f, -13.5f), new Vector3(25f, 1.8f, 0.8f), freezerMat, shelfTrimMat, 4);
            CreateStoreBlock("Freezer_E", new Vector3(15.5f, 0.9f, -13.5f), new Vector3(25f, 1.8f, 0.8f), freezerMat, shelfTrimMat, 4);
            GameObject fgW = VerticalSliceBuilder.CreateCube("Freezer_Glow_W", new Vector3(-15.5f, 1.0f, -13.1f), new Vector3(24.4f, 0.5f, 0.1f), freezerGlowMat);
            fgW.layer = 2;
            GameObject fgE = VerticalSliceBuilder.CreateCube("Freezer_Glow_E", new Vector3(15.5f, 1.0f, -13.1f), new Vector3(24.4f, 0.5f, 0.1f), freezerGlowMat);
            fgE.layer = 2;
            CreateVisualCube("Freezer_Rail_W", new Vector3(-15.5f, 1.74f, -13.08f), new Vector3(24.8f, 0.08f, 0.08f), shelfTrimMat);
            CreateVisualCube("Freezer_Rail_E", new Vector3(15.5f, 1.74f, -13.08f), new Vector3(24.8f, 0.08f, 0.08f), shelfTrimMat);

            // 饮料架（西北角矮架 1.2m：半掩体，夹角藏 Epic 宝箱）
            CreateShelfSegment("DrinkShelf_1", new Vector3(-25f, 0.7f, -19f), new Vector3(4f, 1.2f, 0.8f), fridgeMat, shelfTrimMat);
            CreateShelfSegment("DrinkShelf_2", new Vector3(-17f, 0.7f, -17.5f), new Vector3(0.8f, 1.2f, 4f), fridgeMat, shelfTrimMat);

            // 后仓（储物间，东北角封闭房）：南墙两段 + 西墙（北/东 = 周墙），门洞朝南宽 3 —— 藏身点 4（危险区）
            CreateStoreBlock("StoreRoom_South_W", new Vector3(18.5f, 2f, -15.5f), new Vector3(1f, 4f, 0.3f), wallMat, shelfTrimMat, 4);
            CreateStoreBlock("StoreRoom_South_E", new Vector3(26f, 2f, -15.5f), new Vector3(8f, 4f, 0.3f), wallMat, shelfTrimMat, 4);
            CreateStoreBlock("StoreRoom_West", new Vector3(18f, 2f, -18f), new Vector3(0.3f, 4f, 5f), wallMat, shelfTrimMat, 4);
            // 房内货架（内部掩体，藏 MidnightRelic 宝箱）
            CreateShelfSegment("Shelf_Room", new Vector3(24.5f, 0.9f, -18.5f), new Vector3(4f, 1.8f, 0.8f), shelfMat, shelfTrimMat);

            // 购物车 ×3（路障掩体，挡移动不挡视线）
            float[,] cartPos = { { 8f, 12.5f }, { 22f, -3.6f }, { -10f, -7f } };
            for (int i = 0; i < 3; i++)
                CreateShoppingCart($"Cart_{i + 1}", new Vector3(cartPos[i, 0], 0.45f, cartPos[i, 1]), cartMat, shelfTrimMat);

            // 散落商品 ×6（贴地小方块，纯装饰 layer2）
            float[,] itemPos = { { -22f, 6.5f }, { 18f, 6.5f }, { 0f, 3.1f }, { -15f, -0.3f }, { 12f, -7.1f }, { 28f, 3.1f } };
            for (int i = 0; i < 6; i++)
            {
                StylizedLowPolyFactory.CreateTaperedPrism($"Item_{i + 1}", new Vector3(itemPos[i, 0], 0.22f, itemPos[i, 1]),
                    new Vector2(0.34f, 0.28f), new Vector2(0.19f, 0.16f), 0.36f, 6, itemMat);
            }

            // ---------- 玩家 + 2.5D 相机（同校园：方块小人 + CharacterController + 正交投影） ----------
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.layer = 2;
            player.transform.position = new Vector3(0f, 0f, 19f);   // 南墙门洞内出生，北上去晨门
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.4f; cc.center = new Vector3(0, 1f, 0);
            VerticalSliceBuilder.CreateCharacterBody(player.transform, playerMat, player: true);
            player.AddComponent<PlayerController>();
            player.AddComponent<SafeZoneDetector>();
            player.AddComponent<MobileControls>();

            GameObject cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 18f, 0f);
            UnityEngine.Camera c = cam.AddComponent<UnityEngine.Camera>();
            c.orthographic = true;
            c.orthographicSize = 14f;
            c.farClipPlane = 120f;
            cam.AddComponent<AudioListener>();
            CameraController camCtrl = cam.AddComponent<CameraController>();
            camCtrl.Target = player.transform;
            camCtrl.OrthoSize = 14f;
            InteractionSystem inter = player.AddComponent<InteractionSystem>();
            inter.CameraRoot = cam.transform;

            // ---------- 守卫 ×4（高难度参数不变；巡逻点重排：Scout 顺夹道、Runner 断口游走、Guardian 守后仓门口） ----------
            List<PatrolController> guards = new List<PatrolController>();
            GameObject g1 = VerticalSliceBuilder.CreatePatroller("Patrol_Scout_A",
                new Vector3(0f, 0f, 14f),
                new Vector3[] { new Vector3(0, 0.5f, 14f), new Vector3(-26, 0.5f, 6.5f), new Vector3(26, 0.5f, 6.5f), new Vector3(-20, 0.5f, 3.1f), new Vector3(20, 0.5f, 3.1f) },
                patrolMat, patrolSpeed: 3f, chaseSpeed: 6.8f, visionRange: 12f, visionAngle: 65f, hearingRange: 8f,
                isTracker: false, tall: false, wide: false, player: player.transform,
                coneColor: new Color(0.40f, 0.60f, 1.00f, 0.18f));
            GameObject g2 = VerticalSliceBuilder.CreatePatroller("Patrol_Scout_B",
                new Vector3(0f, 0f, -10f),
                new Vector3[] { new Vector3(0, 0.5f, -10f), new Vector3(-24, 0.5f, -0.3f), new Vector3(24, 0.5f, -0.3f), new Vector3(-16, 0.5f, -3.7f), new Vector3(16, 0.5f, -3.7f) },
                patrolMat, patrolSpeed: 3f, chaseSpeed: 6.8f, visionRange: 12f, visionAngle: 65f, hearingRange: 8f,
                isTracker: false, tall: false, wide: false, player: player.transform,
                coneColor: new Color(0.40f, 0.60f, 1.00f, 0.18f));
            GameObject g3 = VerticalSliceBuilder.CreatePatroller("Patrol_Runner_C",
                new Vector3(0f, 0f, 0f),
                new Vector3[] { new Vector3(-13, 0.5f, 6.5f), new Vector3(13, 0.5f, 6.5f), new Vector3(-13, 0.5f, -0.3f), new Vector3(13, 0.5f, -0.3f) },
                runnerMat, patrolSpeed: 5.5f, chaseSpeed: 7.8f, visionRange: 7f, visionAngle: 30f, hearingRange: 5f,
                isTracker: false, tall: true, wide: false, player: player.transform,
                detectRate: 3f, coneColor: new Color(1.00f, 0.35f, 0.30f, 0.16f));
            GameObject g4 = VerticalSliceBuilder.CreatePatroller("Patrol_Guardian_A",
                new Vector3(16f, 0f, -12.5f),
                new Vector3[] { new Vector3(16, 0.5f, -12.5f) },
                guardianMat, patrolSpeed: 3f, chaseSpeed: 6.5f, visionRange: 13f, visionAngle: 360f, hearingRange: 8f,
                isTracker: false, tall: true, wide: false, player: player.transform,
                isGuardian: true, guardRadius: 9f, ringMat: guardRingMat);
            // 固定布局：守卫直接激活（校园因 LayoutRandomizer 重烘焙需存 inactive，这里不需要）
            g1.SetActive(true); g2.SetActive(true); g3.SetActive(true); g4.SetActive(true);
            guards.Add(g1.GetComponent<PatrolController>());
            guards.Add(g2.GetComponent<PatrolController>());
            guards.Add(g3.GetComponent<PatrolController>());
            guards.Add(g4.GetComponent<PatrolController>());

            // ---------- 时间碎片 ×3（收银台后主通道 + 夹道北端 + 后仓门口） ----------
            Vector3[] fragPos = { new Vector3(0f, 0.8f, 11f), new Vector3(-14f, 0.8f, -10f), new Vector3(20f, 0.8f, -15f) };
            for (int i = 0; i < fragPos.Length; i++)
            {
                GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                frag.name = $"TimeFragment_{i + 1}";
                frag.layer = 2;
                frag.transform.position = fragPos[i];
                frag.transform.localScale = Vector3.one * 0.6f;
                frag.GetComponent<Renderer>().enabled = false;
                VerticalSliceBuilder.CreateClockFragmentVisual(frag.transform, fragMat, frameMat, shelfTrimMat);
                frag.GetComponent<SphereCollider>().isTrigger = true;   // [审查] 拾取靠 OnTriggerEnter，缺 isTrigger 实心碰撞永不触发 → 集齐 3 碎片才开晨门，漏了必死
                frag.AddComponent<TimeFragment>();
            }

            // ---------- 宝箱 ×5（越高藏越深：收银台后 → 断口 → 饮料架夹角 → 冷柜北死角 → 后仓内） ----------
            Vector3[] chestPos = { new Vector3(18f, 0.7f, 15.5f), new Vector3(-8f, 0.7f, 12.5f), new Vector3(-20f, 0.7f, -17f), new Vector3(-30f, 0.7f, -16f), new Vector3(24.5f, 0.7f, -20f) };
            LootChest.ChestQuality[] chestQ = { LootChest.ChestQuality.Common, LootChest.ChestQuality.Rare, LootChest.ChestQuality.Epic, LootChest.ChestQuality.Legendary, LootChest.ChestQuality.MidnightRelic };
            Material[] chestMats = { chestMat, chestRareMat, chestEpicMat, chestLegendMat, chestRelicMat };
            string[] chestNames = { "Chest_Common_1", "Chest_Rare_1", "Chest_Epic_1", "Chest_Legendary_1", "Chest_MidnightRelic_1" };
            for (int i = 0; i < chestPos.Length; i++)
            {
                GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chest.name = chestNames[i];
                chest.layer = 2;
                chest.transform.position = chestPos[i];
                chest.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
                chest.GetComponent<Renderer>().sharedMaterial = chestMats[i];
                AddChestDetails(chest.transform, chestMats[i], frameMat);
                chest.AddComponent<LootChest>().Quality = chestQ[i];
            }

            // ---------- 道具拾取物 ×4（灯油/加速/沙漏/隐身） ----------
            MakePickup("Pickup_Torch_1", new Vector3(24f, 0.5f, 16f), torchMat, frameMat, typeof(TorchItem));
            MakePickup("Pickup_SpeedDrink_1", new Vector3(-24f, 0.5f, -3f), speedMat, frameMat, typeof(SpeedDrink));
            MakePickup("Pickup_Hourglass_1", new Vector3(0f, 0.5f, -4.5f), hourglassMat, frameMat, typeof(TimeHourglass));
            MakePickup("Pickup_Invisibility_1", new Vector3(-8f, 0.5f, -12.5f), invisMat, frameMat, typeof(Invisibility));

            // ---------- 晨门（北墙中央门洞，集齐碎片逃离） ----------
            GameObject gate = VerticalSliceBuilder.CreateCube("ExitGate", new Vector3(0f, 1.5f, -mapZ * 0.5f), new Vector3(4f, 3f, 0.6f), gateMat);
            ExitGate gateComp = gate.AddComponent<ExitGate>();
            gateComp.GateRenderer = gate.GetComponent<Renderer>();
            VerticalSliceBuilder.CreateCube("Gate_Frame_L", new Vector3(-1.6f, 2f, -mapZ * 0.5f), new Vector3(0.25f, 4f, 0.25f), frameMat).layer = 2;
            VerticalSliceBuilder.CreateCube("Gate_Frame_R", new Vector3(1.6f, 2f, -mapZ * 0.5f), new Vector3(0.25f, 4f, 0.25f), frameMat).layer = 2;
            VerticalSliceBuilder.CreateCube("Gate_Frame_T", new Vector3(0f, 4f, -mapZ * 0.5f), new Vector3(3.6f, 0.25f, 0.25f), frameMat).layer = 2;
            CreateGateDetails(new Vector3(0f, 0f, -mapZ * 0.5f), frameMat, shelfTrimMat);

            // ---------- 灯光：暗冷蓝平行光 + 暖黄顶灯 ×7（2 盏闪烁）+ 冷柜冷蓝点光 ----------
            GameObject moon = new GameObject("DimLight");
            Light light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            light.color = new Color(0.38f, 0.47f, 0.69f);
            light.intensity = 0.35f;
            // 顶灯两列（夹道上空）+ 冷柜区一盏；Lamp_4 与 Lamp_7 挂 FlickerLight（坏灯闪烁，Seed 错相）
            float[,] lampXY = { { -16f, 6.5f }, { 16f, 6.5f }, { -16f, -0.3f }, { 16f, -0.3f }, { -16f, -3.7f }, { 16f, -3.7f }, { 0f, -12.5f } };
            for (int i = 0; i < lampXY.GetLength(0); i++)
            {
                GameObject pl = new GameObject($"Lamp_{i + 1}");
                pl.transform.position = new Vector3(lampXY[i, 0], 3.2f, lampXY[i, 1]);
                Light l = pl.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.70f, 0.40f);
                l.intensity = 1.4f;
                l.range = 7.5f;
                l.shadows = LightShadows.None;
                if (i == 3 || i == 6)
                {
                    FlickerLight fl = pl.AddComponent<FlickerLight>();
                    fl.Seed = (i == 3) ? 1.3f : 4.2f;   // 不同步闪
                    fl.MinRatio = 0.25f;
                    fl.Speed = 8f;
                }
            }
            // 冷柜冷蓝点光（冷柜区氛围灯）
            GameObject coldLightGo = new GameObject("Freezer_Light");
            coldLightGo.transform.position = new Vector3(-16f, 2.5f, -13.5f);
            Light coldLight = coldLightGo.AddComponent<Light>();
            coldLight.type = LightType.Point;
            coldLight.color = new Color(0.46f, 0.78f, 1f);
            coldLight.intensity = 0.95f;
            coldLight.range = 5.5f;
            coldLight.shadows = LightShadows.None;

            // ---------- 全局系统（同校园：RunManager/RewardSystem/迷雾/自动开跑） ----------
            GameObject runGo = new GameObject("RunManager");
            RunManager run = runGo.AddComponent<RunManager>();
            run.MaxTime = 420f;   // 高难度：一局 7 分钟（校园 480s）
            runGo.AddComponent<RunHUD>();
            runGo.AddComponent<ItemUseController>();
            GameObject rewardGo = new GameObject("RewardSystem");
            rewardGo.AddComponent<RewardSystem>();
            rewardGo.AddComponent<CollectionView>();

            // 探索迷雾：与校园一致的纯黑暗幕 + 跟随玩家的 9m 手电筒光圈。
            // 灯油仍通过 RunManager -> ExplorationFog.AddRadius(+2m) 直接扩大此光圈。
            Shader fogShader = Shader.Find("Before8AM/FogOfWar");
            if (fogShader == null) fogShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/FogOfWar.shader");
            Material fogMat = new Material(fogShader);
            fogMat.SetVector("_BoundsMin", new Vector4(-32f, -22f, 0f, 0f));
            fogMat.SetVector("_BoundsMax", new Vector4(32f, 22f, 0f, 0f));
            fogMat.SetColor("_DarkColor", Color.black);
            fogMat.SetFloat("_MaxDarkness", 1f);
            fogMat.SetFloat("_TorchRadius", 9f);
            fogMat.SetFloat("_TorchSoft", 1.2f);
            fogMat.SetVector("_TorchPos", new Vector4(0f, 0f, 19f, 0f));
            fogMat.renderQueue = 3000;
            GameObject fogPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fogPlane.name = "FogOfWarPlane";
            fogPlane.layer = 2;
            fogPlane.transform.position = new Vector3(0f, 10f, 0f);
            fogPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            fogPlane.transform.localScale = new Vector3(128f, 88f, 1f);
            var fogCollider = fogPlane.GetComponent<Collider>();
            if (fogCollider != null) Object.DestroyImmediate(fogCollider);
            fogPlane.GetComponent<Renderer>().sharedMaterial = fogMat;
            ExplorationFog fog = fogPlane.AddComponent<ExplorationFog>();
            fog.TorchBaseRadius = 9f;
            fog.TorchSoft = 1.2f;
            fog.Init(player.transform, fogMat);
            fogPlane.SetActive(true);
            // 有些项目相机预设会排除 Ignore Raycast；雾平面必须在 2.5D 主相机中可见。
            c.cullingMask |= 1 << fogPlane.layer;

            // 相机 2.5D ↔ 第一人称切换（V 键，同校园）
            ViewToggle viewToggle = cam.AddComponent<ViewToggle>();
            viewToggle.Player = player.transform;
            viewToggle.FogPlane = fogPlane;

            // 自动开跑（无开场过场；守卫已激活固定巡逻）
            GameObject autoGo = new GameObject("GameAutoStart");
            autoGo.AddComponent<GameAutoStart>();

            // ---------- NavMesh 烘焙（地面 + 周墙 + 货架/冷柜/后仓墙/收银台/购物车 = 掩体与绕行障碍；装饰 layer2 排除） ----------
            NavMeshSurface navSurface = ground.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navSurface.layerMask = ~(1 << 2);
            navSurface.BuildNavMesh();

            // ---------- 保存并注册进 Build Settings ----------
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            MainMenuBuilder.ReorderBuildSettings();   // 重建 [主菜单, 校园, 停车场]（停车场场景已存在 → 加入）
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log($"[Before8AM] 午夜超市场景已生成并保存: {ScenePath}（主菜单可进入）");
        }

        static void ApplyEnvironmentTextures(Material groundMat, Material wallMat, Material shelfMat, Material counterMat, Material freezerMat)
        {
            VerticalSliceBuilder.ApplySurfaceTexture(groundMat,
                ProceduralTextureLibrary.GetOrCreate("PT_StoreConcrete", ProceduralTextureLibrary.Pattern.Concrete,
                    new Color(0.125f, 0.157f, 0.227f), new Color(0.075f, 0.095f, 0.145f)), new Vector2(8f, 6f));
            VerticalSliceBuilder.ApplySurfaceTexture(wallMat,
                ProceduralTextureLibrary.GetOrCreate("PT_StoreWall", ProceduralTextureLibrary.Pattern.WallPanels,
                    new Color(0.165f, 0.220f, 0.329f), new Color(0.095f, 0.135f, 0.220f)), new Vector2(2f, 2f));
            VerticalSliceBuilder.ApplySurfaceTexture(shelfMat,
                ProceduralTextureLibrary.GetOrCreate("PT_Metal", ProceduralTextureLibrary.Pattern.Concrete,
                    new Color(0.592f, 0.635f, 0.706f), new Color(0.405f, 0.455f, 0.535f)), new Vector2(1f, 2f));
            VerticalSliceBuilder.ApplySurfaceTexture(counterMat,
                ProceduralTextureLibrary.GetOrCreate("PT_Counter", ProceduralTextureLibrary.Pattern.Paving,
                    new Color(0.545f, 0.408f, 0.294f), new Color(0.355f, 0.245f, 0.165f)), new Vector2(1f, 1f));
            VerticalSliceBuilder.ApplySurfaceTexture(freezerMat,
                ProceduralTextureLibrary.GetOrCreate("PT_Freezer", ProceduralTextureLibrary.Pattern.WallPanels,
                    new Color(0.173f, 0.220f, 0.314f), new Color(0.095f, 0.135f, 0.205f)), new Vector2(1f, 2f));
        }

        /// <summary>保留灰盒作为物理体，显示层用收腰低模网格替换。</summary>
        static GameObject CreateStoreBlock(string name, Vector3 pos, Vector3 scale, Material bodyMat, Material trimMat, int sides)
        {
            GameObject physics = VerticalSliceBuilder.CreateCube(name, pos, scale, bodyMat);
            physics.GetComponent<Renderer>().enabled = false;
            StylizedLowPolyFactory.CreateTaperedPrism(name + "_Visual", pos,
                new Vector2(scale.x, scale.z), new Vector2(scale.x * 0.94f, scale.z * 0.92f), scale.y, sides, bodyMat);
            if (scale.y > 0.5f)
                CreateVisualCube(name + "_Cap", pos + Vector3.up * (scale.y * 0.5f - 0.06f),
                    new Vector3(scale.x * 0.90f, 0.08f, scale.z * 0.90f), trimMat);
            return physics;
        }

        static GameObject CreateStoreCounter(string name, Vector3 pos, Vector3 scale, Material bodyMat, Material trimMat)
        {
            GameObject counter = CreateStoreBlock(name, pos, scale, bodyMat, trimMat, 4);
            CreateVisualCube(name + "_FrontApron", pos + new Vector3(0f, 0f, -scale.z * 0.52f),
                new Vector3(scale.x * 0.70f, scale.y * 0.40f, 0.05f), trimMat);
            return counter;
        }

        static GameObject CreateShoppingCart(string name, Vector3 pos, Material bodyMat, Material trimMat)
        {
            GameObject cart = VerticalSliceBuilder.CreateCube(name, pos, new Vector3(0.8f, 0.6f, 1.1f), bodyMat);
            cart.GetComponent<Renderer>().enabled = false;
            StylizedLowPolyFactory.CreateTaperedPrism(name + "_Basket", pos + new Vector3(0f, 0.10f, -0.05f),
                new Vector2(0.76f, 0.92f), new Vector2(0.58f, 0.68f), 0.48f, 4, bodyMat);
            CreateVisualCube(name + "_Handle", pos + new Vector3(0f, 0.62f, 0.52f), new Vector3(0.66f, 0.08f, 0.08f), trimMat);
            return cart;
        }

        /// <summary>货架实体保持原碰撞和 NavMesh 作用，外层仅添加无碰撞层板和立柱。</summary>
        static GameObject CreateShelfSegment(string name, Vector3 pos, Vector3 scale, Material bodyMat, Material trimMat)
        {
            GameObject shelf = VerticalSliceBuilder.CreateCube(name, pos, scale, bodyMat);
            shelf.GetComponent<Renderer>().enabled = false;
            StylizedLowPolyFactory.CreateTaperedPrism(name + "_Body", pos,
                new Vector2(scale.x, scale.z), new Vector2(scale.x * 0.94f, scale.z * 0.88f), scale.y, 4, bodyMat);
            float frontZ = pos.z - scale.z * 0.5f - 0.025f;
            CreateVisualCube(name + "_TopRail", new Vector3(pos.x, pos.y + scale.y * 0.5f - 0.10f, frontZ),
                new Vector3(scale.x * 0.96f, 0.08f, 0.06f), trimMat);
            CreateVisualCube(name + "_ShelfRail", new Vector3(pos.x, pos.y, frontZ),
                new Vector3(scale.x * 0.96f, 0.06f, 0.06f), trimMat);
            CreateVisualCube(name + "_Post_L", new Vector3(pos.x - scale.x * 0.46f, pos.y, frontZ),
                new Vector3(0.08f, scale.y * 0.90f, 0.06f), trimMat);
            CreateVisualCube(name + "_Post_R", new Vector3(pos.x + scale.x * 0.46f, pos.y, frontZ),
                new Vector3(0.08f, scale.y * 0.90f, 0.06f), trimMat);
            return shelf;
        }

        static void AddChestDetails(Transform chest, Material chestMat, Material metalMat)
        {
            VerticalSliceBuilder.CreatePropPart(chest, "Chest_Lid", PrimitiveType.Cube,
                new Vector3(0f, 0.58f, 0f), new Vector3(0.92f, 0.18f, 0.92f), chestMat);
            VerticalSliceBuilder.CreatePropPart(chest, "Chest_Band", PrimitiveType.Cube,
                new Vector3(0f, 0.08f, -0.51f), new Vector3(0.12f, 0.86f, 0.08f), metalMat);
            VerticalSliceBuilder.CreatePropPart(chest, "Chest_Lock", PrimitiveType.Cube,
                new Vector3(0f, 0.08f, -0.61f), new Vector3(0.22f, 0.28f, 0.08f), metalMat);
        }

        static void CreateGateDetails(Vector3 center, Material glowMat, Material stoneMat)
        {
            CreateVisualCube("Gate_Base_L", center + new Vector3(-1.6f, 0.18f, 0f), new Vector3(0.58f, 0.36f, 0.72f), stoneMat);
            CreateVisualCube("Gate_Base_R", center + new Vector3(1.6f, 0.18f, 0f), new Vector3(0.58f, 0.36f, 0.72f), stoneMat);
            CreateVisualCube("Gate_Crown", center + new Vector3(0f, 4.26f, 0f), new Vector3(4.15f, 0.22f, 0.46f), stoneMat);
            CreateVisualCube("Gate_Glow_L", center + new Vector3(-1.23f, 2.05f, -0.33f), new Vector3(0.08f, 2.75f, 0.05f), glowMat);
            CreateVisualCube("Gate_Glow_R", center + new Vector3(1.23f, 2.05f, -0.33f), new Vector3(0.08f, 2.75f, 0.05f), glowMat);
        }

        static GameObject CreateVisualCube(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = VerticalSliceBuilder.CreateCube(name, pos, scale, mat);
            go.layer = 2;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return go;
        }

        /// <summary>拾取物根节点继续负责 Trigger 和脚本；局部部件只提高低模轮廓可读性。</summary>
        static GameObject MakePickup(string name, Vector3 pos, Material mat, Material accentMat, System.Type scriptType)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = 2;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.5f, 0.35f, 0.5f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Collider>().isTrigger = true;   // [审查] 道具拾取同碎片：全走 OnTriggerEnter，缺 isTrigger 全部捡不到
            if (scriptType == typeof(TorchItem))
            {
                VerticalSliceBuilder.CreateTorchOilVisual(go.transform, mat, accentMat, accentMat);
            }
            else if (scriptType == typeof(SpeedDrink))
            {
                VerticalSliceBuilder.CreateSpeedDrinkVisual(go.transform, mat, accentMat, accentMat);
            }
            else if (scriptType == typeof(TimeHourglass))
            {
                VerticalSliceBuilder.CreateHourglassVisual(go.transform, accentMat, mat);
            }
            else if (scriptType == typeof(Invisibility))
            {
                VerticalSliceBuilder.CreateInvisibilityVialVisual(go.transform, mat, accentMat, accentMat);
            }
            go.AddComponent(scriptType);
            return go;
        }
    }
}
