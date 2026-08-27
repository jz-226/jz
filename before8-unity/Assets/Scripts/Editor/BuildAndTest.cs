using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;   // [0.8.0] NavMeshSurface（停车场烘焙验证）
using UnityEngine.SceneManagement;
using Before8AM.Player;
using Before8AM.Core;
using Before8AM.Events;
using Before8AM.Run;
using Before8AM.Loot;
using Before8AM.Input;   // [0.5] ItemUseController / MobileControls
using Before8AM.UI;      // [0.5] MainMenuController
using Before8AM.World;   // [0.8.0] ExitGate（停车场验证）
using Before8AM.Effects; // [0.8.8] FlickerLight（午夜超市坏灯验证）

namespace Before8AM.EditorTools
{
    /// <summary>
    /// batchmode 自动验证（规格书 133/134：必须真正运行验证，不允许假装完成）。
    /// 用法：
    ///   Unity.exe -batchmode -quit -projectPath F:\Before8AM \
    ///     -executeMethod Before8AM.EditorTools.BuildAndTest.ValidateVerticalSlice
    /// 流程：一键生成灰盒场景 → 打开 → 断言关键要素/组件存在 → 输出 PASS/FAIL 并退出。
    /// </summary>
    public static class BuildAndTest
    {
        static readonly string[] RequiredObjects =
        {
            "Player", "Main Camera", "Patrol_Scout_A", "Chest_Common_1",   // [0.4.3] 修陈旧精确名（实际名带 _A/_1）
            "TimeFragment_1", "TimeFragment_2", "TimeFragment_3",
            "ExitGate", "RunManager", "RewardSystem", "IntroRulesPanel",
            "LayoutRandomizer", "Building_Teaching_Root", "Building_Gym_Root",   // [0.4.3] 随机布局接线
            "Chest_Legendary_1", "Chest_MidnightRelic_1"   // [0.8.0] 5 宝箱：Legendary + MidnightRelic
            // [0.8.1] 回退：随机事件/6 新道具实体断言已删（事件/道具暂缓）
        };

        public static void ValidateVerticalSlice()
        {
            VerticalSliceBuilder.BuildVerticalSlice();

            Scene scene = EditorSceneManager.OpenScene(VerticalSliceBuilder.ScenePath);

            bool ok = true;
            foreach (string name in RequiredObjects)
            {
                bool present = GameObject.Find(name) != null;
                ok &= present;
                Debug.Log($"[验证] {name}: {(present ? "OK" : "MISSING")}");
            }

            GameObject player = GameObject.Find("Player");
            ok &= player != null && player.GetComponent<PlayerController>() != null;
            ok &= player != null && player.GetComponent<CharacterController>() != null;
            Debug.Log($"[验证] PlayerController+CharacterController: {(player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<CharacterController>() != null ? "OK" : "MISSING")}");

            GameObject scout = GameObject.Find("Patrol_Scout_A");
            ok &= scout != null && scout.GetComponent<NavMeshAgent>() != null;
            ok &= scout != null && scout.GetComponent<Before8AM.Patrol.PatrolController>() != null;
            Debug.Log($"[验证] Patrol(NavMeshAgent+PatrolController): {(scout != null && scout.GetComponent<NavMeshAgent>() != null && scout.GetComponent<Before8AM.Patrol.PatrolController>() != null ? "OK" : "MISSING")}");

            // [0.4.3] LayoutRandomizer 接线断言：13 建筑 / 7 守卫 / Surface / 3 碎片 / 5 宝箱 / 14 道具
            // [0.8.1] 回退：时间裂缝/诱饵宝箱接线断言已删（随机事件暂缓）
            GameObject layoutGo = GameObject.Find("LayoutRandomizer");
            LayoutRandomizer lr = layoutGo != null ? layoutGo.GetComponent<LayoutRandomizer>() : null;
            bool lrOk = lr != null
                && lr.Buildings != null && lr.Buildings.Length == 13
                && lr.Guards != null && lr.Guards.Length == 7
                && lr.Surface != null
                && lr.Fragments != null && lr.Fragments.Length == 3
                && lr.Chests != null && lr.Chests.Length == 5
                && lr.Pickups != null && lr.Pickups.Length == 14;
            ok &= lrOk;
            Debug.Log($"[验证] LayoutRandomizer 接线(13建筑/7守卫/Surface/3碎片/5宝箱/14道具): {(lrOk ? "OK" : "MISSING")}");

            // [0.8.6] 段位增援：ReserveGuards 5 只 + ReinforceDirector 挂在 LayoutRandomizer 同物体
            var rd = layoutGo != null ? layoutGo.GetComponent<Before8AM.Patrol.ReinforceDirector>() : null;
            bool reinOk = lr != null && lr.ReserveGuards != null && lr.ReserveGuards.Length == 5 && rd != null;
            ok &= reinOk;
            Debug.Log($"[验证] 段位增援(ReserveGuards×5/ReinforceDirector): {(reinOk ? "OK" : "MISSING")}");

            // [0.4.3] 层掩码矛盾修复：挖空体积必须 layer 0（NavMeshSurface.layerMask=~(1<<2) 排除 layer2，
            // 若在 layer2 会被过滤，挖空不生效，守卫可进楼）
            GameObject teachNavMod = GameObject.Find("Building_Teaching_NavMod");
            GameObject safeDoorBlock = GameObject.Find("Teaching_SafeDoorBlock");
            bool layerOk = teachNavMod != null && teachNavMod.layer == 0
                && safeDoorBlock != null && safeDoorBlock.layer == 0;
            ok &= layerOk;
            Debug.Log($"[验证] 挖空体积 layer0(Teaching_NavMod/SafeDoorBlock): {(layerOk ? "OK" : "MISSING")}");

            // [0.4.3] 守卫场景存 inactive：LayoutRandomizer 随机化+重烘焙后统一激活，
            // 否则 Start 的 agent.Warp 按旧巡逻点/旧 NavMesh 出生、随机化时可能穿楼卡死
            GameObject scoutA = GameObject.Find("Patrol_Scout_A");
            GameObject guardianA = GameObject.Find("Patrol_Guardian_A");
            bool guardsInactive = scoutA != null && !scoutA.activeInHierarchy
                && guardianA != null && !guardianA.activeInHierarchy;
            ok &= guardsInactive;
            Debug.Log($"[验证] 守卫存 inactive(Scout_A/Guardian_A): {(guardsInactive ? "OK" : "MISSING")}");

            // [0.8.1] 回退：随机事件接线断言（RandomEvents/SpawnedEvents/LureChest/事件目录=15）已删——随机事件暂缓。

            // [0.4.5] 图鉴系统断言：RewardSystem 挂 CollectionView + 条目总数 12（[0.8.1] 回退：33→12，删事件/新道具）；守卫 Kind 接线
            GameObject rewardGo2 = GameObject.Find("RewardSystem");
            var cv = rewardGo2 != null ? rewardGo2.GetComponent<Before8AM.Collection.CollectionView>() : null;
            bool catOk = cv != null && Before8AM.Collection.CollectionSystem.TotalCount == 12;
            ok &= catOk;
            Debug.Log($"[验证] CollectionView + 图鉴条目=12: {(catOk ? "OK" : "MISSING")}");

            GameObject trackerGo = GameObject.Find("Patrol_Tracker_A");
            var trackerCtrl = trackerGo != null ? trackerGo.GetComponent<Before8AM.Patrol.PatrolController>() : null;
            bool kindOk = trackerCtrl != null && trackerCtrl.Kind == Before8AM.Patrol.GuardType.Tracker;
            ok &= kindOk;
            Debug.Log($"[验证] 守卫 Kind 接线(Tracker_A=Tracker): {(kindOk ? "OK" : "MISSING")}");

            GameObject runGo = GameObject.Find("RunManager");
            RunManager run = runGo != null ? runGo.GetComponent<RunManager>() : null;
            bool timeOk = run != null && Mathf.Approximately(run.MaxTime, 480f);
            ok &= timeOk;
            Debug.Log($"[验证] RunManager.MaxTime=480s: {(timeOk ? "OK" : "MISSING")}");

            // [0.5] 输入接线断言（必须在切到主菜单前——游戏场景当前仍打开）
            GameObject runGo2 = GameObject.Find("RunManager");
            GameObject playerGo2 = GameObject.Find("Player");
            bool inputOk = runGo2 != null && runGo2.GetComponent<ItemUseController>() != null
                && playerGo2 != null && playerGo2.GetComponent<MobileControls>() != null;
            ok &= inputOk;
            Debug.Log($"[验证] Input 接线(ItemUseController/MobileControls): {(inputOk ? "OK" : "MISSING")}");

            // [0.5] 主菜单场景断言（BuildMainMenu 已由 BuildVerticalSlice 调用生成）
            EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
            GameObject menuGo = GameObject.Find("MenuController");
            bool menuOk = menuGo != null
                && menuGo.GetComponent<MainMenuController>() != null
                && menuGo.GetComponent<Before8AM.Collection.CollectionView>() != null;
            // [0.6] 主菜单 v2：商店/设置面板组件（与 CollectionView 同 visible 门控，MainMenuController 切换）
            bool panelOk = menuGo != null
                && menuGo.GetComponent<ShopController>() != null
                && menuGo.GetComponent<SettingsController>() != null;
            // [0.8.0] 每日任务面板（MissionView + 系统常量）
            bool missionOk = menuGo != null
                && menuGo.GetComponent<Before8AM.Mission.MissionView>() != null
                && Before8AM.Mission.MissionSystem.TaskCount == 5;
            // [0.8.0] 午夜榜（做真：本地积分排行榜 Top 8）+ [0.9.0] 角色皮肤面板（SkinView + 皮肤目录 8 款）
            bool rankOk = menuGo != null
                && menuGo.GetComponent<Before8AM.UI.MidnightRankView>() != null
                && Before8AM.Reward.RankBoard.MaxEntries == 8;
            bool skinOk = menuGo != null
                && menuGo.GetComponent<Before8AM.UI.SkinView>() != null
                && Before8AM.Visual.SkinCatalog.All.Length == 8;
            ok &= menuOk && panelOk && missionOk && rankOk && skinOk;
            Debug.Log($"[验证] 主菜单(MainMenuController+CollectionView): {(menuOk ? "OK" : "MISSING")}");
            Debug.Log($"[验证] 子面板(Shop/Settings): {(panelOk ? "OK" : "MISSING")}");
            Debug.Log($"[验证] 每日任务面板(MissionView+5任务): {(missionOk ? "OK" : "MISSING")}");
            Debug.Log($"[验证] 午夜榜面板(MidnightRankView+Top8): {(rankOk ? "OK" : "MISSING")}");
            Debug.Log($"[验证] 角色皮肤面板(SkinView+8皮肤): {(skinOk ? "OK" : "MISSING")}");

            // [0.5] Build Settings 顺序断言：[0]=MainMenu / [1]=VS（主菜单 buildIndex 0）
            // [0.8.0] 停车场场景若已生成则追加 [2]（ReorderBuildSettings 按 File.Exists 决定，不影响校园 buildIndex）
            var scenes = EditorBuildSettings.scenes;
            bool parkingExists = File.Exists(ParkingLotBuilder.ScenePath);
            bool bsOk = scenes != null && scenes.Length >= 2 && scenes.Length <= 3
                && scenes[0].path == MainMenuBuilder.ScenePath
                && scenes[1].path == VerticalSliceBuilder.ScenePath
                && (!parkingExists || (scenes.Length == 3 && scenes[2].path == ParkingLotBuilder.ScenePath));
            ok &= bsOk;
            Debug.Log($"[验证] BuildSettings 顺序([MainMenu, VS{(parkingExists ? ", ParkingLot" : "")}]): {(bsOk ? "OK" : "MISSING")}");

            AssetDatabase.SaveAssets();

            if (!ok)
            {
                Debug.LogError("[验证] FAILED：Vertical Slice 场景要素不完整");
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("[验证] PASSED：Vertical Slice 场景要素完整，可进入 Play 试玩");
                EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// [0.8.0] 地下停车场关卡自动验证（第二张地图 / 深层区域高难度）。
        /// 用法（同 ValidateVerticalSlice）：
        ///   Unity.exe -batchmode -quit -projectPath F:\Before8AM \
        ///     -executeMethod Before8AM.EditorTools.BuildAndTest.ValidateParkingLot
        /// 生成停车场场景 → 打开 → 断言核心要素/组件/参数/烘焙 → PASS/FAIL 退出。
        /// </summary>
        public static void ValidateParkingLot()
        {
            ParkingLotBuilder.BuildParkingLot();

            Scene scene = EditorSceneManager.OpenScene(ParkingLotBuilder.ScenePath);   // [0.8.8] 存引用：货架/闪烁灯断言遍历根物体

            bool ok = true;

            string[] parkingObjects =
            {
                "Player", "Main Camera", "Ground", "Shelf_1_A",   // 超市几何（地面/货架/周墙）
                "Patrol_Scout_A", "Patrol_Scout_B", "Patrol_Runner_C", "Patrol_Guardian_A",   // 守卫 ×4
                "TimeFragment_1", "TimeFragment_2", "TimeFragment_3",   // 碎片 ×3
                "Chest_Common_1", "Chest_Rare_1", "Chest_Epic_1", "Chest_Legendary_1", "Chest_MidnightRelic_1",   // 宝箱 ×5
                "Pickup_Torch_1", "Pickup_SpeedDrink_1", "Pickup_Hourglass_1", "Pickup_Invisibility_1",   // 道具 ×4（[0.8.1] 回退删新道具 3）
                "ExitGate", "RunManager", "RewardSystem", "GameAutoStart", "FogOfWarPlane"
            };
            foreach (string name in parkingObjects)
            {
                bool present = GameObject.Find(name) != null;
                ok &= present;
                Debug.Log($"[验证-停车场] {name}: {(present ? "OK" : "MISSING")}");
            }

            // 守卫固定布局：场景中必须 active（校园存 inactive 由 LayoutRandomizer 激活；停车场直接激活，无随机化）
            GameObject scoutA = GameObject.Find("Patrol_Scout_A");
            GameObject runnerC = GameObject.Find("Patrol_Runner_C");
            GameObject guardianA = GameObject.Find("Patrol_Guardian_A");
            bool guardsActive = scoutA != null && scoutA.activeInHierarchy
                && runnerC != null && runnerC.activeInHierarchy
                && guardianA != null && guardianA.activeInHierarchy;
            ok &= guardsActive;
            Debug.Log($"[验证-停车场] 守卫固定激活(Scout_A/Runner_C/Guardian_A): {(guardsActive ? "OK" : "MISSING")}");

            // [0.8.8] 超市货架：6 排 × 2~3 段 + 后仓货架 = 16 个 1.8m 实体（掩体/视线死角）
            int shelfCount = 0;
            foreach (var rootGo in scene.GetRootGameObjects())
                if (rootGo.name.StartsWith("Shelf_")) shelfCount++;
            ok &= shelfCount >= 12;
            Debug.Log($"[验证-停车场] 货架实体数量≥12(实际{shelfCount}): {(shelfCount >= 12 ? "OK" : "MISSING")}");

            // [0.8.8] 午夜坏灯：至少 1 盏顶灯挂 FlickerLight（闪烁氛围）
            int flickerCount = 0;
            foreach (var rootGo in scene.GetRootGameObjects())
                flickerCount += rootGo.GetComponentsInChildren<FlickerLight>(true).Length;
            ok &= flickerCount >= 1;
            Debug.Log($"[验证-停车场] FlickerLight 闪烁灯≥1(实际{flickerCount}): {(flickerCount >= 1 ? "OK" : "MISSING")}");

            // 高难度参数：Scout 视野 ≥12m / Runner 追击 ≥7.8 / Guardian 视野 ≥13
            var scoutCtrl = scoutA != null ? scoutA.GetComponent<Before8AM.Patrol.PatrolController>() : null;
            var runnerCtrl = runnerC != null ? runnerC.GetComponent<Before8AM.Patrol.PatrolController>() : null;
            var guardianCtrl = guardianA != null ? guardianA.GetComponent<Before8AM.Patrol.PatrolController>() : null;
            bool guardParamOk = scoutCtrl != null && runnerCtrl != null && guardianCtrl != null
                && scoutCtrl.VisionRange >= 12f && runnerCtrl.ChaseSpeed >= 7.8f && guardianCtrl.VisionRange >= 13f;
            ok &= guardParamOk;
            Debug.Log($"[验证-停车场] 守卫高难度参数(Scout≥12m/Runner追≥7.8/Guardian≥13m): {(guardParamOk ? "OK" : "MISSING")}");

            // RunManager.MaxTime=420s（高难度一局 7 分钟，校园 480s）
            GameObject runGo = GameObject.Find("RunManager");
            RunManager run = runGo != null ? runGo.GetComponent<RunManager>() : null;
            bool timeOk = run != null && Mathf.Approximately(run.MaxTime, 420f);
            ok &= timeOk;
            Debug.Log($"[验证-停车场] RunManager.MaxTime=420s: {(timeOk ? "OK" : "MISSING")}");

            // [0.8.1] 回退：RandomEvents 接守卫断言已删（随机事件暂缓）

            // 图鉴挂接（RewardSystem → CollectionView）
            GameObject rewardGo = GameObject.Find("RewardSystem");
            bool colOk = rewardGo != null && rewardGo.GetComponent<Before8AM.Collection.CollectionView>() != null;
            ok &= colOk;
            Debug.Log($"[验证-停车场] CollectionView 挂接: {(colOk ? "OK" : "MISSING")}");

            // 晨门 GateRenderer 接线（锁定灰 / 集齐发光门片）
            GameObject gateGo = GameObject.Find("ExitGate");
            var gate = gateGo != null ? gateGo.GetComponent<ExitGate>() : null;
            bool gateOk = gate != null && gate.GateRenderer != null;
            ok &= gateOk;
            Debug.Log($"[验证-停车场] ExitGate.GateRenderer 接线: {(gateOk ? "OK" : "MISSING")}");

            // NavMesh 已烘焙（Ground 挂 NavMeshSurface + 有三角网格数据 → 守卫可寻路）
            GameObject groundGo = GameObject.Find("Ground");
            bool navOk = groundGo != null && groundGo.GetComponent<NavMeshSurface>() != null
                && NavMesh.CalculateTriangulation().vertices.Length > 0;
            ok &= navOk;
            Debug.Log($"[验证-停车场] NavMesh 烘焙(守卫可寻路): {(navOk ? "OK" : "MISSING")}");

            // Build Settings 已注册停车场（ReorderBuildSettings 按 File.Exists 追加）
            bool bsHasParking = false;
            var scenes = EditorBuildSettings.scenes;
            if (scenes != null)
                foreach (var s in scenes)
                    if (s.path == ParkingLotBuilder.ScenePath) { bsHasParking = true; break; }
            ok &= bsHasParking;
            Debug.Log($"[验证-停车场] BuildSettings 含停车场: {(bsHasParking ? "OK" : "MISSING")}");

            AssetDatabase.SaveAssets();

            if (!ok)
            {
                Debug.LogError("[验证-停车场] FAILED：停车场场景要素不完整");
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("[验证-停车场] PASSED：停车场场景要素完整，主菜单已解锁可进入");
                EditorApplication.Exit(0);
            }
        }
    }
}
