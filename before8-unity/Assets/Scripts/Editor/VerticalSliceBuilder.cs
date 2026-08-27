using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Before8AM.Player;
using Before8AM.Camera;
using Before8AM.Patrol;
using Before8AM.Visual;
using Before8AM.Loot;
using Before8AM.World;
using Before8AM.Run;
using Before8AM.Reward;
using Before8AM.Core;
using Before8AM.Events;
using Before8AM.Collection;
using Before8AM.Input;   // [0.5] MobileControls 挂 player / ItemUseController 挂 runGo
using Before8AM.UI;   // [0.8.6] ScrollingNotice 增援播报

namespace Before8AM.EditorTools
{
    /// <summary>
    /// 全自动灰盒场景生成器（规格书 108/109/140：所有 Unity Editor 手工操作转成脚本）。
    /// 菜单：Before8AM > Build Vertical Slice Scene —— 一键生成可玩闭环场景。
    /// 包括：地面/围墙/障碍墙、玩家+第三人称相机、Scout 巡夜者+巡逻点、宝箱、3 时间碎片、晨门、
    /// NavMesh 烘焙、URP 管线、新输入系统、Player Tag、Build Settings 注册。
    /// </summary>
    public static class VerticalSliceBuilder
    {
        public const string ScenePath = "Assets/Scenes/MidnightCampus/VS_MidnightCampus.unity";

        [MenuItem("Before8AM/1. Configure Project (URP + Input + Tag)")]
        public static void ConfigureProject()
        {
            EnsurePlayerTag();
            SetupURP();
            SetupActiveInput();
            AssetDatabase.SaveAssets();
            Debug.Log("[Before8AM] 项目配置完成：URP + 输入系统(Both) + Player Tag");
        }

        [MenuItem("Before8AM/2. Build Vertical Slice Scene")]
        public static void BuildVerticalSlice()
        {
            EnsurePlayerTag();
            SetupURP();
            SetupActiveInput();
            // 正式 Art Bible 资产进入项目后，先确保其作为可平铺的 3D Base Map 导入。
            // 这样无论场景何时重建，草地、石板、墙体、屋顶都不会退回为程序贴图。
            AssetDatabase.Refresh();
            ConfigureArtReferenceImports();

            // [0.5] Play 模式防护：NewScene 在 Play 中被 Unity 禁止（InvalidOperationException）。
            // 用户若在 Play 中误点构建，直接提示返回，不抛异常、不破坏当前场景。
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[构建] 请先退出 Play 模式，再重建场景（NewScene 在 Play 中被 Unity 禁止）。");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 全局基调：不覆盖环境光（恢复 Codex 改动前的 Unity 默认，用户确认过该亮度），夜间氛围靠月光 + 暗幕。
            RenderSettings.fog = false; // 探索黑暗由现有 FogOfWar 控制，避免双重雾化画面。

            // ---- 材质（画面 v1：夜间低多边形校园）----
            // 环境氛围 v2：午夜蓝紫三渲二。大面积表面低饱和，靠冷暖光对比建立层次。
            Material groundMat = MakeMaterial(new Color(0.071f, 0.145f, 0.122f), "MAT_Grass_Night");
            Material wallMat = MakeMaterial(new Color(0.165f, 0.220f, 0.329f), "MAT_Wall_BlueGray");
            Material playerMat = MakeMaterial(new Color(0.25f, 0.60f, 1.00f), "MAT_Player");
            Material patrolMat = MakeMaterial(new Color(0.02f, 0.02f, 0.04f), "MAT_Patrol");
            // 巡夜者颜色区分（GAME_DESIGN 巡夜者类型）：Scout 深黑 / Runner 暗红（快） / Tracker 暗紫（追踪者）
            Material runnerMat = MakeMaterial(new Color(0.45f, 0.10f, 0.08f), "MAT_Runner");
            Material trackerMat = MakeMaterial(new Color(0.36f, 0.12f, 0.44f), "MAT_Tracker");
            // [0.3.0] Guardian 金铜守卫 + 金色警戒圈 + 新道具材质（加速=红 / 时间=金 / 隐身=青）
            Material guardianMat = MakeMaterial(new Color(0.72f, 0.55f, 0.18f), "MAT_Guardian", emissive: true, emissiveColor: new Color(1.1f, 0.75f, 0.2f));
            Material guardRingMat = MakeMaterial(new Color(1.00f, 0.75f, 0.20f, 0.30f), "MAT_GuardRing", emissive: true, emissiveColor: new Color(2.0f, 1.4f, 0.4f), transparent: true);
            Material speedDrinkMat = MakeMaterial(new Color(0.95f, 0.20f, 0.15f), "MAT_SpeedDrink", emissive: true, emissiveColor: new Color(1.6f, 0.3f, 0.2f));
            Material hourglassMat = MakeMaterial(new Color(1.00f, 0.85f, 0.30f), "MAT_Hourglass", emissive: true, emissiveColor: new Color(1.8f, 1.4f, 0.4f));
            Material invisMat = MakeMaterial(new Color(0.25f, 0.80f, 1.00f, 0.55f), "MAT_Invisibility", transparent: true);
            Material chestMat = MakeMaterial(new Color(0.50f, 0.30f, 0.15f), "MAT_Chest");
            Material chestRareMat = MakeMaterial(new Color(0.28f, 0.48f, 0.95f), "MAT_ChestRare", emissive: true, emissiveColor: new Color(0.4f, 0.8f, 1.6f));   // [0.4.0] Rare 蓝
            Material chestEpicMat = MakeMaterial(new Color(1.00f, 0.78f, 0.20f), "MAT_ChestEpic", emissive: true, emissiveColor: new Color(1.8f, 1.3f, 0.35f));  // [0.4.0] Epic 金
            Material chestLegendMat = MakeMaterial(new Color(0.95f, 0.45f, 0.20f), "MAT_ChestLegend", emissive: true, emissiveColor: new Color(2.2f, 1.0f, 0.35f));   // [0.8.0] Legendary 炎橙
            Material chestRelicMat = MakeMaterial(new Color(0.95f, 0.55f, 0.95f), "MAT_ChestRelic", emissive: true, emissiveColor: new Color(2.0f, 1.1f, 2.0f));    // [0.8.0] MidnightRelic 遗物粉紫
            Material fragmentMat = MakeMaterial(new Color(1.00f, 0.90f, 0.35f), "MAT_Fragment");
            Material clockFaceMat = MakeMaterial(new Color(1.00f, 0.76f, 0.25f), "MAT_ClockFace", emissive: true,
                emissiveColor: new Color(1.45f, 0.95f, 0.30f));
            Material gateMat = MakeMaterial(new Color(0.30f, 0.30f, 0.42f), "MAT_Gate");
            // 安全屋绿地板：亮绿 + 自发光（emission），夜晚月光下也恒定发光、一眼认出。
            // 此前用深绿反照率，月光下几乎黑成一片，用户反馈"找不到绿地板"。
            Material safeMat = MakeMaterial(new Color(0.12f, 0.72f, 0.34f), "MAT_SafeZone", emissive: true, emissiveColor: new Color(0.25f, 1.8f, 0.8f));
            // 普通建筑地板：暗红锈色，明确"这不是安全屋"（负向视觉传达，避免旧版玩家惯性进楼躲藏被抓）
            Material buildingMat = MakeMaterial(new Color(0.42f, 0.16f, 0.14f), "MAT_BuildingFloor");
            // 环境氛围 v2：道路比草地亮一个明度层级，建筑基座承担轮廓和台阶识别。
            Material roadMat = MakeMaterial(new Color(0.125f, 0.157f, 0.227f), "MAT_StonePath_Night");
            Material plazaMat = MakeMaterial(new Color(0.173f, 0.220f, 0.314f), "MAT_Plaza");
            // 半透明坡屋顶（暗蓝 alpha 0.45，俯视能看进建筑内部；玩家进建筑后 RoofFade 再淡出）
            Material roofMat = MakeMaterial(new Color(0.082f, 0.122f, 0.231f, 0.45f), "MAT_Roof_BlueGray", transparent: true);
            // 发光窗（暖黄教室灯光）/ 灯头 / 地面光晕 / 晨门框——夜间地标，全自发光
            Material windowMat = MakeMaterial(new Color(1.00f, 0.88f, 0.45f), "MAT_Window", emissive: true, emissiveColor: new Color(1.6f, 1.3f, 0.6f));
            Material lampHeadMat = MakeMaterial(new Color(1.00f, 0.878f, 0.639f), "MAT_LampHead", emissive: true, emissiveColor: new Color(1.9f, 1.55f, 0.9f));
            Material glowMat = MakeMaterial(new Color(1.00f, 0.82f, 0.50f, 0.26f), "MAT_Glow", emissive: true, emissiveColor: new Color(1.25f, 1.0f, 0.55f), transparent: true);
            Material poleMat = MakeMaterial(new Color(0.20f, 0.24f, 0.33f), "MAT_MetalGrid");
            Material bushMat = MakeMaterial(new Color(0.122f, 0.302f, 0.227f), "MAT_Bush");
            Material trunkMat = MakeMaterial(new Color(0.255f, 0.188f, 0.145f), "MAT_TreeTrunk");
            Material gateFrameMat = MakeMaterial(new Color(0.95f, 0.80f, 0.45f), "MAT_GateFrame", emissive: true, emissiveColor: new Color(1.5f, 1.2f, 0.6f));
            // 建筑内部摆设材质：桌椅=木色 / 床=床板色 / 黑板=墨绿（每栋建筑专属内饰，俯视一眼认出是啥楼）
            Material deskMat = MakeMaterial(new Color(0.42f, 0.30f, 0.18f), "MAT_Wood_Dark");
            Material bedMat = MakeMaterial(new Color(0.28f, 0.30f, 0.40f), "MAT_Bed");
            Material boardMat = MakeMaterial(new Color(0.10f, 0.22f, 0.14f), "MAT_Blackboard");
            // 校园 v2 新增建筑内饰：书架=深棕 / 实验台=银灰 / 球场=橙 / 舞台=暗红
            Material shelfMat = MakeMaterial(new Color(0.34f, 0.22f, 0.12f), "MAT_Shelf");        // 图书馆书架
            Material labMat = MakeMaterial(new Color(0.52f, 0.54f, 0.58f), "MAT_LabBench");       // 实验楼实验台
            Material gymMat = MakeMaterial(new Color(0.85f, 0.55f, 0.20f), "MAT_Gym");            // 体育馆地板/球架
            Material stageMat = MakeMaterial(new Color(0.58f, 0.18f, 0.18f), "MAT_Stage");         // 报告厅舞台
            // 开场宿舍房间新增：床毯=暖橙 / 窗玻璃=半透明（窗扇上有十字棂条+玻璃=真窗户，不再像门）
            Material blanketMat = MakeMaterial(new Color(0.75f, 0.45f, 0.28f), "MAT_Blanket");
            Material sheetMat = MakeMaterial(new Color(0.74f, 0.80f, 0.88f), "MAT_DormSheet");
            Material glassMat = MakeMaterial(new Color(0.80f, 0.86f, 0.95f, 0.28f), "MAT_WindowGlass", transparent: true);
            // [0.8.1] 回退：6 新道具材质 + 8 摆点事件材质已全部删除（道具/事件暂缓）。

            // 正式 Art Bible 贴图优先；缺失时才保留旧程序贴图作为安全回退。
            ApplySurfaceTexture(groundMat, LoadArtTexture("TEX_Grass_Night", "PT_Grass_v2", ProceduralTextureLibrary.Pattern.Grass,
                new Color(0.88f, 0.88f, 0.88f), new Color(0.48f, 0.58f, 0.48f)), new Vector2(10f, 8f));
            ApplySurfaceTexture(roadMat, LoadArtTexture("TEX_StonePath_Night", "PT_Paving_v2", ProceduralTextureLibrary.Pattern.Paving,
                new Color(0.92f, 0.92f, 0.92f), new Color(0.40f, 0.43f, 0.52f)), new Vector2(3f, 3f));
            ApplySurfaceTexture(plazaMat, LoadArtTexture("TEX_StonePath_Night", "PT_Plaza_v2", ProceduralTextureLibrary.Pattern.Paving,
                new Color(0.94f, 0.94f, 0.94f), new Color(0.42f, 0.47f, 0.58f)), new Vector2(2f, 2f));
            ApplySurfaceTexture(wallMat, LoadArtTexture("TEX_Wall_BlueGray", "PT_WallPanels_v2", ProceduralTextureLibrary.Pattern.WallPanels,
                new Color(0.94f, 0.94f, 0.94f), new Color(0.38f, 0.44f, 0.58f)), new Vector2(2f, 2f));
            ApplySurfaceTexture(roofMat, LoadArtTexture("TEX_Roof_BlueGray", "PT_RoofTiles_v2", ProceduralTextureLibrary.Pattern.RoofTiles,
                new Color(0.90f, 0.90f, 0.90f), new Color(0.34f, 0.38f, 0.55f)), new Vector2(2f, 2f));
            ApplySurfaceTexture(deskMat, LoadArtTexture("TEX_Wood_Dark", "PT_Counter", ProceduralTextureLibrary.Pattern.WallPanels,
                new Color(0.70f, 0.56f, 0.42f), new Color(0.34f, 0.24f, 0.14f)), new Vector2(1f, 1f));
            ApplySurfaceTexture(poleMat, LoadArtTexture("TEX_MetalGrid", "PT_Metal", ProceduralTextureLibrary.Pattern.WallPanels,
                new Color(0.72f, 0.76f, 0.84f), new Color(0.22f, 0.26f, 0.34f)), new Vector2(1f, 2f));

            // 也保留为可复用的 .mat 资产，供后续宿舍、超市与模块化建筑直接引用。
            groundMat = PersistArtMaterial(groundMat);
            roadMat = PersistArtMaterial(roadMat);
            wallMat = PersistArtMaterial(wallMat);
            roofMat = PersistArtMaterial(roofMat);
            deskMat = PersistArtMaterial(deskMat);
            poleMat = PersistArtMaterial(poleMat);

            // ---- 地面 + 围墙（[0.3.0] 扩成 96×80 整片校园，容纳 13 栋建筑 + 广场；用户反馈"地图太小/游玩时间短"）----
            GameObject ground = CreateCube("Ground", new Vector3(0, -0.5f, 0), new Vector3(96, 1, 80), groundMat);
            CreateCube("Wall_N", new Vector3(0, 1.5f, -40), new Vector3(96, 3, 0.5f), wallMat);
            // 南墙开"翻窗"（用户反馈：以前的窗底太低像门 / 横条细长不像窗）：改成真窗户——
            // 更宽 1.8m + 窗台抬到 0.85（门通地、窗有窗台，这是"窗"不是"门"的关键）+ 四面发光窗框 +
            // 外凸白石窗台板。第一人称从宿舍房间里爬出到校园（WindowIntro 过场，宿舍房间在南墙外 z 40~45.5）。
            float winHalf = 0.9f;      // 窗半宽 → 窗宽 1.8m（够爬过去，相机干净穿过不贴框）
            float winBottom = 0.85f;   // 窗台顶高（有窗台 = 窗户；门是通地的）
            float winTop = 2.15f;      // 窗顶底高（窗洞高 1.3m，眼睛 1.65 探入有富余）
            CreateCube("Wall_S_L", new Vector3(-(48f + winHalf) * 0.5f, 1.5f, 40), new Vector3(48f - winHalf, 3, 0.5f), wallMat);
            CreateCube("Wall_S_R", new Vector3((48f + winHalf) * 0.5f, 1.5f, 40), new Vector3(48f - winHalf, 3, 0.5f), wallMat);
            CreateCube("Wall_S_WinSill", new Vector3(0, winBottom * 0.5f, 40), new Vector3(winHalf * 2f, winBottom, 0.5f), wallMat);
            CreateCube("Wall_S_WinTop", new Vector3(0, (winTop + 3f) * 0.5f, 40), new Vector3(winHalf * 2f, 3f - winTop, 0.5f), wallMat);
            // 四面发光窗框（左/右竖框 + 上横框）：暖黄发光围出"窗"的轮廓，夜色里一眼看到
            CreateCube("Wall_S_WinFrame_L", new Vector3(-winHalf - 0.05f, (winBottom + winTop) * 0.5f, 40), new Vector3(0.18f, winTop - winBottom, 0.45f), gateFrameMat).layer = 2;
            CreateCube("Wall_S_WinFrame_R", new Vector3(winHalf + 0.05f, (winBottom + winTop) * 0.5f, 40), new Vector3(0.18f, winTop - winBottom, 0.45f), gateFrameMat).layer = 2;
            CreateCube("Wall_S_WinFrame_T", new Vector3(0f, winTop + 0.09f, 40), new Vector3(winHalf * 2f + 0.36f, 0.18f, 0.45f), gateFrameMat).layer = 2;
            // 窗台板：白石条横架在窗下、向房间里凸出，是"窗台"——窗不像门的关键视觉
            CreateCube("Wall_S_WinSillBoard", new Vector3(0f, winBottom - 0.06f, 40.2f), new Vector3(winHalf * 2f + 0.6f, 0.12f, 0.5f), plazaMat).layer = 2;
            CreateCube("Wall_E", new Vector3(48, 1.5f, 0), new Vector3(0.5f, 3, 80), wallMat);
            CreateCube("Wall_W", new Vector3(-48, 1.5f, 0), new Vector3(0.5f, 3, 80), wallMat);

            // ---- 障碍墙（空地矮墙，潜行/绕行变化，规格书 103；[0.3.0] 3→6，覆盖新地图各区）----
            CreateCube("Obstacle_A", new Vector3(-6, 1.5f, 3), new Vector3(6, 3, 0.4f), wallMat);    // 中心广场
            CreateCube("Obstacle_B", new Vector3(12, 1.5f, 8), new Vector3(0.4f, 3, 5), wallMat);     // 小卖部东
            CreateCube("Obstacle_C", new Vector3(-14, 1.5f, 12), new Vector3(4, 3, 0.4f), wallMat);   // 图书馆↔宿舍 之间潜行掩体
            CreateCube("Obstacle_D", new Vector3(40, 1.5f, 2), new Vector3(5, 3, 0.4f), wallMat);     // 东区掩体
            CreateCube("Obstacle_E", new Vector3(-42, 1.5f, -6), new Vector3(4, 3, 0.4f), wallMat);   // 西区掩体
            CreateCube("Obstacle_F", new Vector3(6, 1.5f, 32), new Vector3(7, 3, 0.4f), wallMat);     // 南区掩体

            // ---- 校园建筑（[0.3.0] 扩成 13 栋，按分区覆盖整片 96×80 校园）。
            // 布局（俯视，北=晨门方向）：
            //   西北：图书馆2 / 教学楼(安全屋) / 报告厅 / 食堂
            //   中区：图书馆（西）/ 小卖部 / 实验楼（东）
            //   南区：宿舍 / 教学楼2 / 体育馆 / 实验楼2 / 食堂2 / 宿舍2
            // 每栋建筑内部用 NavMeshModifierVolume 挖空 NavMesh → 巡夜者无路可进。
            // 教学楼 = 唯一安全屋（绿自发光地板 + SafeZone 触发器：躲进去立即脱战免疫抓捕）；
            // 其余 = 普通建筑（暗红地板 + Building 触发器：躲进去拖延，巡夜者堵前门，可后门穿堂溜走）。
            // 建筑识别靠内部摆设：课桌椅=教学楼 / 阶梯舞台=报告厅 / 餐桌=食堂 / 书架=图书馆 / 实验台=实验楼 /
            //   双层床=宿舍 / 球场+球架=体育馆 / 货架=小卖部。
            Material[] allInteriors = { deskMat, bedMat, boardMat, shelfMat, labMat, gymMat, stageMat };
            // [0.4.3] 建筑根节点返回值：全部 13 栋收集，供 LayoutRandomizer 接线（随机布局用）。
            GameObject teachingRoot = CreateBuilding("Building_Teaching", new Vector3(-20, 0, -20), new Vector2(13, 8), wallMat, safeMat, roofMat, windowMat, plazaMat,
                safeHouse: true, interiorKind: "classroom", interiors: allInteriors);
            // 安全屋前门守卫禁区（用户反馈：巡夜者追到门口会穿模插进安全屋里面）。
            // 楼内 NavMesh 已挖空，守卫 agent 进不了楼，但追到门洞时身体会从 2.4m 门洞插进门槛。
            // 在南门前（z -17.5~-15.2，覆盖门洞宽 2.4）再挖一块 Not Walkable，守卫最近停在门外 ~1.5m，
            // 身体不再插进门槛；与楼内挖空区（z ≥ -15.5）重叠，保证门洞处完全无路。
            // [0.4.3] layer 2→0（层掩码矛盾修复）+ 挂安全屋根节点（随机化时随楼走）。
            GameObject safeDoorBlock = new GameObject("Teaching_SafeDoorBlock");
            safeDoorBlock.transform.position = new Vector3(-20f, 1.5f, -15.65f);
            safeDoorBlock.layer = 0;
            safeDoorBlock.transform.SetParent(teachingRoot.transform, true);
            NavMeshModifierVolume safeMod = safeDoorBlock.AddComponent<NavMeshModifierVolume>();
            safeMod.size = new Vector3(3.2f, 4f, 2.3f);          // 宽 3.2 > 门宽 2.4，纵深 2.3 门外
            safeMod.center = Vector3.up * 0f;                     // y -0.5~3.5 覆盖地面 NavMesh 面(y=0)
            safeMod.area = NavMesh.GetAreaFromName("Not Walkable");
            GameObject hallRoot = CreateBuilding("Building_Hall", new Vector3(-7, 0, -12), new Vector2(10, 7), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, rearDoor: true, interiorKind: "hall", interiors: allInteriors);
            GameObject canteenRoot = CreateBuilding("Building_Canteen", new Vector3(22, 0, -22), new Vector2(11, 7), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, rearDoor: true, interiorKind: "canteen", interiors: allInteriors);
            GameObject canteen2Root = CreateBuilding("Building_Canteen2", new Vector3(38, 0, -14), new Vector2(11, 7), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, rearDoor: true, interiorKind: "canteen", interiors: allInteriors);
            GameObject libraryRoot = CreateBuilding("Building_Library", new Vector3(-24, 0, 6), new Vector2(11, 7), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, rearDoor: true, interiorKind: "library", interiors: allInteriors);
            GameObject library2Root = CreateBuilding("Building_Library2", new Vector3(-38, 0, -34), new Vector2(11, 7), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, rearDoor: true, interiorKind: "library", interiors: allInteriors);
            GameObject labRoot = CreateBuilding("Building_Lab", new Vector3(28, 0, 4), new Vector2(10, 6), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, interiorKind: "lab", interiors: allInteriors);
            GameObject lab2Root = CreateBuilding("Building_Lab2", new Vector3(26, 0, 22), new Vector2(10, 6), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, interiorKind: "lab", interiors: allInteriors);
            GameObject dormRoot = CreateBuilding("Building_Dorm", new Vector3(-24, 0, 16), new Vector2(12, 6), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, rearDoor: true, interiorKind: "dorm", interiors: allInteriors);
            GameObject dorm2Root = CreateBuilding("Building_Dorm2", new Vector3(-40, 0, 22), new Vector2(12, 6), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, rearDoor: true, interiorKind: "dorm", interiors: allInteriors);
            GameObject teaching2Root = CreateBuilding("Building_Teaching2", new Vector3(8, 0, 22), new Vector2(11, 7), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, interiorKind: "classroom", interiors: allInteriors);
            GameObject gymRoot = CreateBuilding("Building_Gym", new Vector3(36, 0, 14), new Vector2(12, 9), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, rearDoor: true, interiorKind: "gym", interiors: allInteriors);
            GameObject shopRoot = CreateBuilding("Building_Shop", new Vector3(6, 0, 4), new Vector2(8, 5), wallMat, buildingMat, roofMat, windowMat, plazaMat,
                safeHouse: false, interiorKind: "shop", interiors: allInteriors);

            // ---- 玩家（Capsule + CharacterController + 相机 + 交互）----
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.layer = 2;   // Ignore Raycast：动态物体，不参与 NavMesh 烘焙
            player.transform.position = new Vector3(0, 0f, 34f);   // [0.3.0] 落地 Spot 附近出生（主大道南端），横穿校园北上去晨门逃离

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0, 1f, 0);
            cc.slopeLimit = 45f;   // 显式声明（默认即 45，防场景序列化偏差）
            cc.stepOffset = 0.3f;  // 显式声明（默认即 0.3，防场景序列化偏差；自动迈 30cm 以下台阶）

            // 画面 v1：胶囊 → 方块小人（头/身/腿 + 书包），腿随移动摆动
            CreateCharacterBody(player.transform, playerMat, player: true);

            PlayerController playerCtrl = player.AddComponent<PlayerController>();
            player.AddComponent<SafeZoneDetector>();   // 建筑检测：SafeZone=安全屋（免疫抓捕/立即弃追）；Building=普通建筑（感知屏蔽，拖延）
            player.AddComponent<MobileControls>();   // [0.5] 手游虚拟摇杆 + 右半屏按钮（挂 Player，Intro 过场期间随 Player 一起禁用）

            GameObject cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 20f, 11f);
            UnityEngine.Camera c = cam.AddComponent<UnityEngine.Camera>();
            c.orthographic = true;          // 2.5D 正交投影
            c.orthographicSize = 15f;       // [0.3.0] 地图扩到 96×80 后视野相应加大（覆盖整片校园）
            c.farClipPlane = 120f;
            c.backgroundColor = new Color(0.055f, 0.082f, 0.141f); // #0E1524
            cam.AddComponent<AudioListener>();
            CameraController camCtrl = cam.AddComponent<CameraController>();
            camCtrl.Target = player.transform;
            camCtrl.OrthoSize = 15f;        // 相机组件自身也同步（LateUpdate 每帧刷新 orthoSize）

            InteractionSystem inter = player.AddComponent<InteractionSystem>();
            inter.CameraRoot = cam.transform;

            // ---- 巡夜者 ×7（[0.3.0] GAME_DESIGN 巡夜者类型；巡逻区域按象限分区互不重叠，覆盖整片 96×80 校园）----
            // [0.4.3] guards 收集所有守卫返回值；守卫场景存 inactive，LayoutRandomizer 随机化+重烘焙后激活。
            List<PatrolController> guards = new List<PatrolController>();
            // Scout_A 北中（教学楼/报告厅/食堂环）：均衡巡逻（视野 11m/65°、听力 7m、追击 6.5）。
            guards.Add(CreatePatroller("Patrol_Scout_A",
                new Vector3(-25, 0f, -15f),
                new Vector3[] {
                    new Vector3(-25, 0.5f, -15f),   // 原 -24,-16 在教学楼南墙上，南移 1m
                    new Vector3(-13, 0.5f, -10f),   // 原 -12,-10 在报告厅西墙上，西移 1m
                    new Vector3(0, 0.5f, -12f),
                    new Vector3(14, 0.5f, -16f),
                    new Vector3(10, 0.5f, -22f),
                    new Vector3(-8, 0.5f, -24f),
                },
                patrolMat,
                patrolSpeed: 3f, chaseSpeed: 6.5f, visionRange: 11f, visionAngle: 65f, hearingRange: 7f,
                isTracker: false, tall: false, wide: false, player: player.transform,
                coneColor: new Color(0.40f, 0.60f, 1.00f, 0.18f)).GetComponent<PatrolController>());   // 蓝锥：均衡

            // Scout_B 南中（教学楼2/小卖部/体育馆环）
            guards.Add(CreatePatroller("Patrol_Scout_B",
                new Vector3(-8, 0f, 10f),
                new Vector3[] {
                    new Vector3(-8, 0.5f, 10f),
                    new Vector3(4, 0.5f, 12f),
                    new Vector3(16, 0.5f, 10f),
                    new Vector3(26, 0.5f, 14f),
                    new Vector3(16, 0.5f, 20f),
                },
                patrolMat,
                patrolSpeed: 3f, chaseSpeed: 6.5f, visionRange: 11f, visionAngle: 65f, hearingRange: 7f,
                isTracker: false, tall: false, wide: false, player: player.transform,
                coneColor: new Color(0.40f, 0.60f, 1.00f, 0.18f)).GetComponent<PatrolController>());   // 蓝锥：均衡

            // Runner_A 东南（实验楼2/实验楼/体育馆走廊）：高速巡逻(5.5) / 视野极窄极短(6.5m/30°) / 听力近(4.5m) / 反应快(3)。
            guards.Add(CreatePatroller("Patrol_Runner_A",
                new Vector3(26, 0f, 12f),
                new Vector3[] {
                    new Vector3(26, 0.5f, 12f),
                    new Vector3(36, 0.5f, 20f),
                    new Vector3(28, 0.5f, 30f),
                    new Vector3(22, 0.5f, 10f),
                },
                runnerMat,
                patrolSpeed: 5.5f, chaseSpeed: 7.6f, visionRange: 6.5f, visionAngle: 30f, hearingRange: 4.5f,
                isTracker: false, tall: true, wide: false, player: player.transform,
                detectRate: 3f, coneColor: new Color(1.00f, 0.35f, 0.30f, 0.16f)).GetComponent<PatrolController>());   // 红锥：快但盲，擦身躲开

            // Runner_B 东北（食堂/食堂2走廊）
            guards.Add(CreatePatroller("Patrol_Runner_B",
                new Vector3(16, 0f, -26f),
                new Vector3[] {
                    new Vector3(16, 0.5f, -26f),
                    new Vector3(30, 0.5f, -20f),
                    new Vector3(36, 0.5f, -10f),
                },
                runnerMat,
                patrolSpeed: 5.5f, chaseSpeed: 7.6f, visionRange: 6.5f, visionAngle: 30f, hearingRange: 4.5f,
                isTracker: false, tall: true, wide: false, player: player.transform,
                detectRate: 3f, coneColor: new Color(1.00f, 0.35f, 0.30f, 0.16f)).GetComponent<PatrolController>());   // 红锥：快但盲，擦身躲开

            // Tracker_A 西南（宿舍/图书馆/宿舍2）：慢速(1.8) / 视野极大(18m/105°) / 发现后持续追踪，靠奔跑拉开 25m 甩掉。
            guards.Add(CreatePatroller("Patrol_Tracker_A",
                new Vector3(-36, 0f, 26f),
                new Vector3[] {
                    new Vector3(-36, 0.5f, 26f),
                    new Vector3(-31, 0.5f, 12f),   // 原 -28,16 在宿舍建筑内（NavMesh 挖空区，守卫会卡死），移出
                    new Vector3(-34, 0.5f, 6f),
                    new Vector3(-42, 0.5f, 10f),
                },
                trackerMat,
                patrolSpeed: 1.8f, chaseSpeed: 5f, visionRange: 18f, visionAngle: 105f, hearingRange: 8f,
                isTracker: true, tall: false, wide: true, player: player.transform,
                coneColor: new Color(0.80f, 0.40f, 1.00f, 0.16f)).GetComponent<PatrolController>());   // 紫锥：慢但看得远，发现了别指望甩掉

            // Guardian 守护者（GAME_DESIGN 第 4 种）：驻守固定区域，360° 圈感知（金色警戒圈常亮，被引走时圈留原地提示"危险区空了"）。
            // 守碎片2（食堂2 南门）——chaseSpeed 6 < 玩家奔跑 7，引开后能靠奔跑甩掉再折返偷碎片。
            guards.Add(CreatePatroller("Patrol_Guardian_A",
                new Vector3(38, 0f, -8f),
                new Vector3[] { new Vector3(38, 0.5f, -8f) },
                guardianMat,
                patrolSpeed: 3f, chaseSpeed: 6f, visionRange: 12f, visionAngle: 360f, hearingRange: 8f,
                isTracker: false, tall: true, wide: false, player: player.transform,
                isGuardian: true, guardRadius: 12f, ringMat: guardRingMat).GetComponent<PatrolController>());

            // Guardian 守护者：守碎片3（实验楼2 西门）
            guards.Add(CreatePatroller("Patrol_Guardian_B",
                new Vector3(19, 0f, 22f),
                new Vector3[] { new Vector3(19, 0.5f, 22f) },
                guardianMat,
                patrolSpeed: 3f, chaseSpeed: 6f, visionRange: 12f, visionAngle: 360f, hearingRange: 8f,
                isTracker: false, tall: true, wide: false, player: player.transform,
                isGuardian: true, guardRadius: 12f, ringMat: guardRingMat).GetComponent<PatrolController>());

            // ---- [0.8.6] 段位增援守卫 ×5（Scout 均衡型；场景存 inactive，ReinforceDirector 按段位到点激活）----
            // 巡逻点簇只定区域（LayoutRandomizer 每局在区域内采样重排）；分布在现有 7 守卫覆盖较少的空区。
            List<PatrolController> reserves = new List<PatrolController>();
            Vector3[][] reinforceZones = {
                new[] { new Vector3(-38, 0.5f, 18), new Vector3(-33, 0.5f, 8), new Vector3(-42, 0.5f, 2) },   // 西南空区
                new[] { new Vector3(2, 0.5f, -20), new Vector3(8, 0.5f, -14), new Vector3(-2, 0.5f, -26) },   // 中央大道东
                new[] { new Vector3(38, 0.5f, -22), new Vector3(44, 0.5f, -14), new Vector3(32, 0.5f, -30) }, // 东北空区
                new[] { new Vector3(-16, 0.5f, 32), new Vector3(-6, 0.5f, 28), new Vector3(-22, 0.5f, 24) },  // 西北空区
                new[] { new Vector3(30, 0.5f, 0), new Vector3(38, 0.5f, 6), new Vector3(26, 0.5f, -6) },     // 东中
            };
            for (int i = 0; i < reinforceZones.Length; i++)
            {
                reserves.Add(CreatePatroller("Reinforce_Scout_" + (i + 1),
                    reinforceZones[i][0],
                    reinforceZones[i],
                    patrolMat,
                    patrolSpeed: 3f, chaseSpeed: 6.5f, visionRange: 11f, visionAngle: 65f, hearingRange: 7f,
                    isTracker: false, tall: false, wide: false, player: player.transform,
                    coneColor: new Color(0.40f, 0.60f, 1.00f, 0.18f)).GetComponent<PatrolController>());
            }

            // ---- 宝箱 ×3（[0.4.0] 品质分配：风险越高奖励越高——食堂=Common / 小卖部=Rare / 体育馆=Epic）----
            // [0.4.3] chests 列表收集；LayoutRandomizer 把 5 品质宝箱随机到互异建筑。
            // [0.8.0] 3→5：加 Legendary（炎橙）+ MidnightRelic（遗物粉紫，开出午夜遗物）
            List<LootChest> chests = new List<LootChest>();
            Vector3[] chestPos = { new Vector3(22f, 0.7f, -22f), new Vector3(36f, 0.7f, 14f), new Vector3(6f, 0.7f, 4f), new Vector3(-30f, 0.7f, -28f), new Vector3(40f, 0.7f, -30f) };
            LootChest.ChestQuality[] chestQ = { LootChest.ChestQuality.Common, LootChest.ChestQuality.Epic, LootChest.ChestQuality.Rare, LootChest.ChestQuality.Legendary, LootChest.ChestQuality.MidnightRelic };
            Material[] chestMats = { chestMat, chestEpicMat, chestRareMat, chestLegendMat, chestRelicMat };
            string[] chestNames = { "Chest_Common_1", "Chest_Epic_1", "Chest_Rare_1", "Chest_Legendary_1", "Chest_MidnightRelic_1" };
            for (int i = 0; i < chestPos.Length; i++)
            {
                GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chest.name = chestNames[i];
                chest.layer = 2;   // Ignore Raycast：不参与 NavMesh 烘焙
                chest.transform.position = chestPos[i];   // 食堂(22,-22) / 体育馆(36,14) / 小卖部(6,4) 净空处
                chest.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
                chest.GetComponent<Renderer>().sharedMaterial = chestMats[i];
                // 低多边形宝箱：根方块保留碰撞与 LootChest，装饰件全部无碰撞并继承根物体回弹。
                CreatePropPart(chest.transform, "Chest_Lid", PrimitiveType.Cube,
                    new Vector3(0f, 0.58f, 0f), new Vector3(0.92f, 0.18f, 0.92f), chestMats[i]);
                CreatePropPart(chest.transform, "Chest_Band", PrimitiveType.Cube,
                    new Vector3(0f, 0.08f, -0.51f), new Vector3(0.12f, 0.86f, 0.08f), gateFrameMat);
                CreatePropPart(chest.transform, "Chest_Band_Back", PrimitiveType.Cube,
                    new Vector3(0f, 0.08f, 0.51f), new Vector3(0.12f, 0.86f, 0.08f), gateFrameMat);
                CreatePropPart(chest.transform, "Chest_Lock", PrimitiveType.Cube,
                    new Vector3(0f, 0.08f, -0.61f), new Vector3(0.22f, 0.28f, 0.08f), gateFrameMat);
                var chestComp = chest.AddComponent<LootChest>();
                chestComp.Quality = chestQ[i];
                chests.Add(chestComp);
            }

            // [0.8.1] 回退：随机事件实体（时间裂缝×2 / 诱饵宝箱 / 8 摆点事件）已全部删除——随机事件暂缓。
            // 事件脚本（MerchantEvent/VendingEvent 等）保留为死代码，后期完善后在此重建实体。

            // ---- 时间碎片 ×3（[0.3.0] 摆成广三角，迫使玩家横穿校园）----
            // 碎片1 教学楼内（安全屋，低风险保底）/ 碎片2 食堂2 内（远东北，Guardian_A 守）/ 碎片3 实验楼2 内（远东南，Guardian_B 守）。
            // 位置避开内饰（对照 CreateInteriorProp 桌椅/实验台布局）：教室中列过道、餐桌行列空隙、实验台列间过道。
            // [0.4.3] fragments 列表收集（顺序 Frag1 安全屋 / Frag2 / Frag3）；LayoutRandomizer 重排。
            List<TimeFragment> fragments = new List<TimeFragment>();
            Vector3[] fragPos = { new Vector3(-20f, 0.7f, -20f), new Vector3(38f, 0.7f, -14f), new Vector3(24.75f, 0.7f, 22f) };
            for (int i = 0; i < fragPos.Length; i++)
            {
                GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                frag.name = $"TimeFragment_{i + 1}";
                frag.layer = 2;   // Ignore Raycast：不参与 NavMesh 烘焙
                frag.transform.position = fragPos[i];
                frag.transform.localScale = Vector3.one * 0.5f;
                frag.GetComponent<Renderer>().enabled = false;
                CreateClockFragmentVisual(frag.transform, clockFaceMat, gateFrameMat, poleMat);
                frag.GetComponent<SphereCollider>().isTrigger = true;
                frag.AddComponent<TimeFragment>();
                fragments.Add(frag.GetComponent<TimeFragment>());
            }

            // ---- 灯油道具 ×8（[0.3.0] 5→8 覆盖新地图，TorchItem）：拾取扩大手电筒光圈半径（用户反馈：吃道具扩视野）----
            // 散落校园各处的暖黄发光小罐（旋转漂浮提示可拾取），分布在建筑旁/路旁
            // [0.4.3] pickups 收集全部 14 个外道具（灯油8+加速2+沙漏2+隐身2），LayoutRandomizer 随机重排。
            List<Transform> pickups = new List<Transform>();
            Vector3[] oilPos = {
                new Vector3(-10f, 0.3f, -30f),   // 教学楼北
                new Vector3(8f, 0.3f, -28f),     // 报告厅北
                new Vector3(28f, 0.3f, -28f),    // 食堂2北
                new Vector3(-6f, 0.3f, -7f),     // 报告厅南（原 -6,-10 在报告厅内，南移出建筑）
                new Vector3(6f, 0.3f, -6f),      // 小卖部北
                new Vector3(26f, 0.3f, 14f),     // 实验楼南
                new Vector3(-32f, 0.3f, 16f),    // 宿舍西（原 -30,16 卡在宿舍西墙上，西移 2m）
                new Vector3(0f, 0.3f, 26f)       // 南区主大道
            };
            for (int i = 0; i < oilPos.Length; i++)
            {
                GameObject oil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                oil.name = $"TorchOil_{i + 1}";
                oil.layer = 2;   // Ignore Raycast：不参与 NavMesh 烘焙
                oil.transform.position = oilPos[i];
                oil.transform.localScale = new Vector3(0.28f, 0.4f, 0.28f);
                CreateTorchOilVisual(oil.transform, lampHeadMat, poleMat, gateFrameMat);
                Collider oilCol = oil.GetComponent<Collider>();
                if (oilCol != null) oilCol.isTrigger = true;
                oil.AddComponent<TorchItem>();
                pickups.Add(oil.transform);
            }

            // ---- [0.3.0] 新道具：加速药剂 / 时间沙漏 / 隐身斗篷（自动拾取，效果注入 PlayerController/RunManager）----
            Vector3[] speedDrinkPos = { new Vector3(4f, 0.3f, 16f), new Vector3(-34f, 0.3f, -8f) };
            for (int i = 0; i < speedDrinkPos.Length; i++)
            {
                GameObject drink = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                drink.name = "SpeedDrink_" + (i + 1);
                drink.layer = 2;   // Ignore Raycast：不参与 NavMesh 烘焙
                drink.transform.position = speedDrinkPos[i];
                drink.transform.localScale = new Vector3(0.28f, 0.5f, 0.28f);
                CreateSpeedDrinkVisual(drink.transform, speedDrinkMat, poleMat, gateFrameMat);
                Collider dc = drink.GetComponent<Collider>();
                if (dc != null) dc.isTrigger = true;
                drink.AddComponent<SpeedDrink>();
                pickups.Add(drink.transform);
            }
            Vector3[] hourglassPos = { new Vector3(-40f, 0.3f, 0f), new Vector3(16f, 0.3f, -16f) };
            for (int i = 0; i < hourglassPos.Length; i++)
            {
                GameObject hg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hg.name = "TimeHourglass_" + (i + 1);
                hg.layer = 2;
                hg.transform.position = hourglassPos[i];
                hg.transform.localScale = new Vector3(0.3f, 0.45f, 0.3f);
                hg.GetComponent<Renderer>().sharedMaterial = hourglassMat;
                CreateHourglassVisual(hg.transform, gateFrameMat, fragmentMat);
                Collider hc = hg.GetComponent<Collider>();
                if (hc != null) hc.isTrigger = true;
                hg.AddComponent<TimeHourglass>();
                pickups.Add(hg.transform);
            }
            Vector3[] invisPos = { new Vector3(44f, 0.3f, 8f), new Vector3(-40f, 0.3f, 12f) };
            for (int i = 0; i < invisPos.Length; i++)
            {
                GameObject invis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                invis.name = "Invisibility_" + (i + 1);
                invis.layer = 2;
                invis.transform.position = invisPos[i];
                invis.transform.localScale = Vector3.one * 0.5f;
                invis.GetComponent<Renderer>().enabled = false;
                CreateInvisibilityVialVisual(invis.transform, invisMat, hourglassMat, gateFrameMat);
                invis.GetComponent<SphereCollider>().isTrigger = true;
                invis.AddComponent<Invisibility>();
                pickups.Add(invis.transform);
            }

            // [0.8.1] 回退：6 新道具放置（声学诱饵×2/探测器×2/干扰器/传送器/夜视仪/假卡）已全部删除——恢复 4 旧道具（14 个）。

            // ---- 晨门 ----
            GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "ExitGate";
            gate.transform.position = new Vector3(0, 1.5f, -40f);   // 北墙中央，集齐碎片后逃离
            gate.transform.localScale = new Vector3(4f, 3f, 0.6f);
            gate.GetComponent<Renderer>().sharedMaterial = gateMat;
            ExitGate gateComp = gate.AddComponent<ExitGate>();
            gateComp.GateRenderer = gate.GetComponent<Renderer>();

            // ---- 画面 v1：晨门发光框（暖黄夜间地标，一眼看出逃离方向）----
            CreateCube("Gate_Frame_L", new Vector3(-1.6f, 2f, -40f), new Vector3(0.25f, 4f, 0.25f), gateFrameMat).layer = 2;
            CreateCube("Gate_Frame_R", new Vector3(1.6f, 2f, -40f), new Vector3(0.25f, 4f, 0.25f), gateFrameMat).layer = 2;
            CreateCube("Gate_Frame_T", new Vector3(0f, 4f, -40f), new Vector3(3.6f, 0.25f, 0.25f), gateFrameMat).layer = 2;
            CreateExitGateDetails(new Vector3(0f, 0f, -40f), gateFrameMat, poleMat);

            // ---- 月光（午夜蓝紫氛围）----
            GameObject moon = new GameObject("Moonlight");
            Light light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(58f, -35f, 0f); // 左上 → 右下的低强度月光
            light.color = new Color(0.545f, 0.635f, 0.780f);              // #8BA2C7
            light.intensity = 0.75f;   // [修复] 恢复原亮度：Codex 降到 0.52 后地图整体过暗

            // ---- 画面 v1：夜景氛围（路灯 + 石板路 + 灌木）----
            // 石板路井字形路网（晨门→中心→南区 + 东西贯穿 + 南北两排服务路），绕开全部建筑
            CreateRoad(roadMat);
            // 路灯沿路/建筑前布置（[0.3.0] 8→14 盏覆盖整片 96×80 校园），暖黄光晕照亮夜路；灯柱参与烘焙成障碍，巡夜者绕行
            CreateLamp(new Vector3(-20f, 0f, -28f), poleMat, lampHeadMat, glowMat);  // 教学楼北
            CreateLamp(new Vector3(-4f, 0f, -24f), poleMat, lampHeadMat, glowMat);   // 报告厅北
            CreateLamp(new Vector3(12f, 0f, -18f), poleMat, lampHeadMat, glowMat);   // 食堂南
            CreateLamp(new Vector3(34f, 0f, -10f), poleMat, lampHeadMat, glowMat);   // 食堂2南
            CreateLamp(new Vector3(-34f, 0f, -20f), poleMat, lampHeadMat, glowMat);  // 图书馆2东
            CreateLamp(new Vector3(2f, 0f, -2f), poleMat, lampHeadMat, glowMat);     // 中心广场
            CreateLamp(new Vector3(22f, 0f, 2f), poleMat, lampHeadMat, glowMat);     // 实验楼南
            CreateLamp(new Vector3(36f, 0f, 10f), poleMat, lampHeadMat, glowMat);    // 体育馆南
            CreateLamp(new Vector3(-30f, 0f, 6f), poleMat, lampHeadMat, glowMat);    // 图书馆南
            CreateLamp(new Vector3(-38f, 0f, 26f), poleMat, lampHeadMat, glowMat);   // 宿舍2南
            CreateLamp(new Vector3(-6f, 0f, 18f), poleMat, lampHeadMat, glowMat);    // 教学楼2西
            CreateLamp(new Vector3(18f, 0f, 26f), poleMat, lampHeadMat, glowMat);    // 实验楼2北
            CreateLamp(new Vector3(46f, 0f, -4f), poleMat, lampHeadMat, glowMat);    // 东区
            CreateLamp(new Vector3(4f, 0f, 32f), poleMat, lampHeadMat, glowMat);     // 南区主大道
            // 灌木点缀场地四角 + 路边（[0.3.0] 8→14）
            CreateBush(new Vector3(-46f, 0.3f, -36f), bushMat);
            CreateBush(new Vector3(-46f, 0.3f, 36f), bushMat);
            CreateBush(new Vector3(46f, 0.3f, 36f), bushMat);
            CreateBush(new Vector3(46f, 0.3f, -36f), bushMat);
            CreateBush(new Vector3(-30f, 0.3f, -26f), bushMat);
            CreateBush(new Vector3(-10f, 0.3f, -4f), bushMat);
            CreateBush(new Vector3(10f, 0.3f, 14f), bushMat);
            CreateBush(new Vector3(30f, 0.3f, -24f), bushMat);
            CreateBush(new Vector3(-14f, 0.3f, 32f), bushMat);
            CreateBush(new Vector3(28f, 0.3f, 32f), bushMat);
            CreateBush(new Vector3(-44f, 0.3f, -4f), bushMat);
            CreateBush(new Vector3(44f, 0.3f, 14f), bushMat);
            CreateBush(new Vector3(-2f, 0.3f, 8f), bushMat);
            CreateBush(new Vector3(14f, 0.3f, -32f), bushMat);
            CreateCampusTrees(trunkMat, bushMat);
            CreateCampusPathProps(poleMat, plazaMat, bushMat);

            // ---- Run 系统（场景内放置，重载场景时自动重建）----
            GameObject runGo = new GameObject("RunManager");
            runGo.AddComponent<RunManager>();
            runGo.AddComponent<RunHUD>();   // [0.4.0] 运行 HUD：时间/碎片/金币/危险时段
            runGo.AddComponent<ItemUseController>();   // [0.5] PC 数字键 1-4 使用背包道具
            GameObject rewardGo = new GameObject("RewardSystem");
            rewardGo.AddComponent<RewardSystem>();
            rewardGo.AddComponent<CollectionView>();   // [0.4.5] 图鉴面板（RewardSystem.Start get-or-add 兜底）
            // [0.8.1] 回退：RandomEvents（RandomEventSystem）已删——随机事件全部暂缓。

            // ---- 探索迷雾（Fog of War）：全图暗幕 quad，走过的地方被点亮（用户反馈：视野太大，昏暗环境下应慢慢探索）----
            // shader 加载双重兜底：Shader.Find 找不到时回退到 AssetDatabase 按路径加载；仍找不到则日志报错
            //（材质会显示为错误色，即用户反馈的"很大黄色遮挡"），好定位。
            Shader fogShader = Shader.Find("Before8AM/FogOfWar");
            if (fogShader == null) fogShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/FogOfWar.shader");
            Debug.Log($"[Before8AM] FogOfWar shader: {(fogShader != null ? fogShader.name : "NULL!")}");
            Material fogMat = new Material(fogShader);
            fogMat.SetVector("_BoundsMin", new Vector4(-48f, -40f, 0f, 0f));   // [0.3.0] 迷雾边界同步 96×80
            fogMat.SetVector("_BoundsMax", new Vector4(48f, 40f, 0f, 0f));
            fogMat.SetColor("_DarkColor", Color.black);
            fogMat.SetFloat("_MaxDarkness", 1f);   // 光圈外纯黑，无探索记忆。
            fogMat.SetFloat("_TorchRadius", 9f);
            fogMat.SetFloat("_TorchSoft", 1.2f);
            fogMat.SetVector("_TorchPos", new Vector4(0f, 0f, 34f, 0f));   // 玩家出生 (0,0,34)
            fogMat.renderQueue = 3000;

            GameObject fogPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fogPlane.name = "FogOfWarPlane";
            fogPlane.layer = 2;   // Ignore Raycast：不参与 NavMesh 烘焙
            fogPlane.transform.position = new Vector3(0f, 10f, 0f);          // 屋顶(≈3m)上方，相机(20m)下方
            fogPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);     // 面朝上
            fogPlane.transform.localScale = new Vector3(192f, 160f, 1f);     // 覆盖 96×80 地图
            var fogCollider = fogPlane.GetComponent<Collider>();
            if (fogCollider != null) Object.DestroyImmediate(fogCollider);
            fogPlane.GetComponent<Renderer>().sharedMaterial = fogMat;
            fogPlane.AddComponent<ExplorationFog>().Init(player.transform, fogMat);
            fogPlane.SetActive(false);   // 开场翻窗过场期间禁用（否则全黑），WindowIntro 结束后恢复

            // ---- 2.5D ↔ 第一人称一键切换（V 键，游玩中随时可切——用户反馈最大的更新点）。
            // 挂主相机：首视 = 相机贴玩家眼睛 + 透视 + 朝玩家朝向（PlayerController 已按相机前向移动，
            // WASD 自动变相对移动）；隐藏玩家身体 + 禁用迷雾平面 + FogHide 首视豁免。----
            ViewToggle viewToggle = cam.AddComponent<ViewToggle>();
            viewToggle.Player = player.transform;
            viewToggle.FogPlane = fogPlane;

            // ---- 南墙窗扇（WindowSash）：真窗户窗扇——木框 + 十字棂条 + 4 块半透明玻璃（夜里透出校园灯光），
            // 开场时绕上缘铰链向墙外"掀上去"（上悬窗式：从里往外推，下缘向外挑高像雨棚，不再是从左侧开门式侧开）。
            // 十字棂 + 玻璃 = "窗"，不再像一扇光秃秃的门板（用户反馈：窗户像门）。
            // pivot 在缺口上缘中心 (0, 2.15, 39.7)，子窗扇向下伸展 1.3m 覆盖竖窗 x±0.9、高 0.85~2.15；
            // 窗扇 z=39.7 与窗框 z=40 错开，修掉"窗扇/窗框 z-fighting 闪"（用户反馈：开场一闪一闪）。
            GameObject sashPivot = new GameObject("WindowSashPivot");
            sashPivot.transform.position = new Vector3(0f, winTop, 39.7f);   // 上缘中心铰链（[0.3.0] 南墙 z=40）
            sashPivot.layer = 2;
            {
                float w = winHalf;                                  // 窗半宽 → 窗扇宽 winHalf*2
                float h = (winTop - winBottom) * 0.5f;              // 窗半高 → 窗扇高 h*2
                float bar = 0.08f;                                  // 棂条宽
                float pw = (w * 2f - bar * 3f) * 0.5f;              // 单格玻璃宽（内区减中棂）
                float ph = (h * 2f - bar * 3f) * 0.5f;              // 单格玻璃高
                float innerL = -w + bar;                            // 玻璃区左缘（水平居中，外框内）
                float innerB = -h * 2f + bar;                       // 玻璃区下缘（上缘铰链 → 向下伸展）
                // 外框四边（上框贴 pivot，下框在最低，左右框竖直居中）
                CreateInteriorProp(sashPivot.transform, "Sash_Frame_T", new Vector3(0f, -bar * 0.5f, 0f), new Vector3(w * 2f, bar, 0.06f), gateFrameMat);
                CreateInteriorProp(sashPivot.transform, "Sash_Frame_B", new Vector3(0f, -h * 2f + bar * 0.5f, 0f), new Vector3(w * 2f, bar, 0.06f), gateFrameMat);
                CreateInteriorProp(sashPivot.transform, "Sash_Frame_L", new Vector3(-w + bar * 0.5f, -h, 0f), new Vector3(bar, h * 2f, 0.06f), gateFrameMat);
                CreateInteriorProp(sashPivot.transform, "Sash_Frame_R", new Vector3(w - bar * 0.5f, -h, 0f), new Vector3(bar, h * 2f, 0.06f), gateFrameMat);
                // 十字棂条
                CreateInteriorProp(sashPivot.transform, "Sash_Mullion_V", new Vector3(0f, -h, 0f), new Vector3(bar, h * 2f, 0.05f), gateFrameMat);
                CreateInteriorProp(sashPivot.transform, "Sash_Mullion_H", new Vector3(0f, -h, 0f), new Vector3(w * 2f, bar, 0.05f), gateFrameMat);
                // 4 块半透明玻璃（透出校园夜景）
                for (int gy = 0; gy < 2; gy++)
                    for (int gx = 0; gx < 2; gx++)
                    {
                        float gx0 = innerL + pw * 0.5f + gx * (pw + bar);
                        float gy0 = innerB + ph * 0.5f + gy * (ph + bar);
                        CreateInteriorProp(sashPivot.transform, "Sash_Glass", new Vector3(gx0, gy0, 0f), new Vector3(pw, ph, 0.02f), glassMat);
                    }
            }
            WindowSash sashComp = sashPivot.AddComponent<WindowSash>();

            // ---- 开场翻窗过场（WindowIntro）：宿舍房间里醒来 → 环视 → 爬窗 → 落地校园 → 黑场切回 2.5D 俯视 → StartRun。
            // 宿舍房间由 CreateIntroDormRoom 搭在南墙外（z 40~45.5），第一人称相机从这里开始。----
            GameObject introGo = new GameObject("IntroCamera");
            introGo.transform.position = new Vector3(0f, 1.5f, 43f);   // [0.3.0] 宿舍房间里（房间 z 40~45.5，床在左、桌在右）
            UnityEngine.Camera intro = introGo.AddComponent<UnityEngine.Camera>();
            intro.orthographic = false;
            intro.fieldOfView = 65f;
            intro.nearClipPlane = 0.3f;     // 原来 0.05 贴着 1.2m 窄窗框穿过 → 近裁剪闪烁；放宽 + 加宽窗洞 = 干净穿过
            intro.farClipPlane = 120f;
            intro.clearFlags = CameraClearFlags.SolidColor;
            intro.backgroundColor = new Color(0.02f, 0.04f, 0.08f);   // 深蓝黑夜空
            // 不挂 AudioListener（主相机已有）；不给 MainCamera tag（Camera.main 必须始终是 2.5D 主相机）
            WindowIntro introComp = introGo.AddComponent<WindowIntro>();
            introComp.MainCamera = c;
            introComp.Player = player;
            introComp.FogPlane = fogPlane;
            introComp.Run = runGo.GetComponent<RunManager>();
            introComp.Sash = sashComp;

            // ---- 开场规则解说面板（IntroRules，[0.4.2]）：每次进游戏黑屏先弹规则，看完点开始才播翻窗过场。
            // 挂在独立 GameObject（纯 OnGUI 面板，不依赖相机/Transform/碰撞），WindowIntro.RulesGate 等它 Dismissed。----
            GameObject rulesGo = new GameObject("IntroRulesPanel");
            IntroRules rules = rulesGo.AddComponent<IntroRules>();
            introComp.RulesGate = rules;

            // ---- 开场宿舍房间（用户反馈：要从"宿舍"里爬出去，宿舍是独立空间——南墙外搭一间封闭宿舍，窗就是那扇翻窗）----
            CreateIntroDormRoom(deskMat, bedMat, blanketMat, sheetMat, boardMat, wallMat, glowMat, gateFrameMat, poleMat);

            // ---- NavMesh 烘焙（覆盖地面 + 障碍）----
            NavMeshSurface navSurface = ground.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navSurface.layerMask = ~(1 << 2);   // 排除 Ignore Raycast 层：玩家/巡夜者/宝箱/碎片不烘成静态障碍
            navSurface.BuildNavMesh();

            // ---- [0.4.3] LayoutRandomizer 接线：每把随机布局（房屋/碎片/道具/守卫巡逻点）----
            // 运行时洗牌现有物体（生成逻辑在 Editor 程序集，运行时无法重建），引用构建时接线不靠 Find。
            // BuildingInfo 照建筑表 center/size/safeHouse/rearDoor；守卫已存 inactive，随机化+烘焙后激活。
            GameObject layoutGo = new GameObject("LayoutRandomizer");
            LayoutRandomizer lr = layoutGo.AddComponent<LayoutRandomizer>();
            lr.Buildings = new LayoutRandomizer.BuildingInfo[] {
                new LayoutRandomizer.BuildingInfo { Root = teachingRoot.transform,  Size = new Vector2(13, 8), SafeHouse = true,  RearDoor = false },
                new LayoutRandomizer.BuildingInfo { Root = hallRoot.transform,      Size = new Vector2(10, 7), SafeHouse = false, RearDoor = true },
                new LayoutRandomizer.BuildingInfo { Root = canteenRoot.transform,   Size = new Vector2(11, 7), SafeHouse = false, RearDoor = true },
                new LayoutRandomizer.BuildingInfo { Root = canteen2Root.transform,  Size = new Vector2(11, 7), SafeHouse = false, RearDoor = true },
                new LayoutRandomizer.BuildingInfo { Root = libraryRoot.transform,   Size = new Vector2(11, 7), SafeHouse = false, RearDoor = true },
                new LayoutRandomizer.BuildingInfo { Root = library2Root.transform,  Size = new Vector2(11, 7), SafeHouse = false, RearDoor = true },
                new LayoutRandomizer.BuildingInfo { Root = labRoot.transform,       Size = new Vector2(10, 6), SafeHouse = false, RearDoor = false },
                new LayoutRandomizer.BuildingInfo { Root = lab2Root.transform,      Size = new Vector2(10, 6), SafeHouse = false, RearDoor = false },
                new LayoutRandomizer.BuildingInfo { Root = dormRoot.transform,      Size = new Vector2(12, 6), SafeHouse = false, RearDoor = true },
                new LayoutRandomizer.BuildingInfo { Root = dorm2Root.transform,     Size = new Vector2(12, 6), SafeHouse = false, RearDoor = true },
                new LayoutRandomizer.BuildingInfo { Root = teaching2Root.transform, Size = new Vector2(11, 7), SafeHouse = false, RearDoor = false },
                new LayoutRandomizer.BuildingInfo { Root = gymRoot.transform,       Size = new Vector2(12, 9), SafeHouse = false, RearDoor = true },
                new LayoutRandomizer.BuildingInfo { Root = shopRoot.transform,      Size = new Vector2(8, 5),  SafeHouse = false, RearDoor = false },
            };
            lr.Guards = guards.ToArray();
            lr.ReserveGuards = reserves.ToArray();   // [0.8.6] 段位增援守卫（inactive，ReinforceDirector 到点激活）
            ReinforceDirector rd = layoutGo.AddComponent<ReinforceDirector>();
            rd.Reserve = reserves.ToArray();
            rd.Notice = layoutGo.AddComponent<ScrollingNotice>();   // [0.8.6] 增援滚动播报（挂 LayoutRandomizer 同物体）
            lr.Surface = navSurface;
            lr.Fragments = fragments.ToArray();
            lr.Chests = chests.ToArray();
            lr.Pickups = pickups.ToArray();   // 14 个旧道具（[0.8.1] 回退：事件/新道具已不接线）

            // ---- 保存并注册进 Build Settings ----
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            MainMenuBuilder.BuildMainMenu();   // [0.5] 生成主菜单场景 + 重排 build settings = [MainMenu, VS_MidnightCampus]（主菜单 buildIndex 0）

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);

            Debug.Log($"[Before8AM] Vertical Slice 场景已生成并保存: {ScenePath}。打开后点 Play 即可游玩。");
        }

        // ---------- 配置辅助 ----------

        static void EnsurePlayerTag()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            EnsureTag(tags, "Player");
            EnsureTag(tags, "SafeZone");   // 安全屋（教学楼）触发器
            EnsureTag(tags, "Building");   // 普通建筑（食堂/宿舍）触发器
            tagManager.ApplyModifiedProperties();
        }

        static void EnsureTag(SerializedProperty tags, string tag)
        {
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        }

        static void SetupURP()
        {
            const string path = "Assets/Settings/URP_Asset.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset != null && !HasValidStandaloneRenderer(asset))
            {
                // 无效（无 Renderer）或 renderer 内嵌为 sub-asset（启动加载顺序不可靠，会触发 CreatePipeline NRE）：
                // 删除重建为官方菜单格式（renderer 独立 asset 文件）。删除前先解除管线引用。
                if (GraphicsSettings.defaultRenderPipeline == asset) GraphicsSettings.defaultRenderPipeline = null;
                if (QualitySettings.renderPipeline == asset) QualitySettings.renderPipeline = null;
                AssetDatabase.DeleteAsset(path);
                asset = null;
            }
            if (asset == null)
            {
                // 复刻 URP 官方菜单流程（UniversalRenderPipelineAsset.cs:633）：
                // renderer 保存为独立 asset 文件 + 填充 postProcessData + shader 资源，
                // 再创建 pipeline asset 关联它。避免内嵌 sub-asset 启动加载顺序问题。
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                // 复刻 URP internal PostProcessData.GetDefaultPostProcessData()
                // （URP Runtime/Data/PostProcessData.cs:38）：加载 URP 包内置默认后处理资源
                rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    Path.Combine(UniversalRenderPipelineAsset.packagePath, "Runtime/Data/PostProcessData.asset"));
                string rendererPath = Path.Combine(
                    Path.GetDirectoryName(path),
                    Path.GetFileNameWithoutExtension(path) + "_Renderer.asset");
                AssetDatabase.CreateAsset(rendererData, rendererPath);
                ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, path);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            AssetDatabase.SaveAssets();
        }

        static bool HasValidStandaloneRenderer(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return false;
            UnityEngine.Object rendererRef = list.GetArrayElementAtIndex(0).objectReferenceValue;
            if (rendererRef == null) return false;
            string rendererPath = AssetDatabase.GetAssetPath(rendererRef);
            string assetPath = AssetDatabase.GetAssetPath(asset);
            // renderer 必须是独立 asset 文件（路径 ≠ 主 asset 路径，排除内嵌 sub-asset）
            return !string.IsNullOrEmpty(rendererPath) && rendererPath != assetPath;
        }

        static void SetupActiveInput()
        {
            // Input System 1.7 无公共 API（PlayerSettings.activeInputHandler 已不存在）。
            // 采用包内部 EditorPlayerSettingHelpers 同款逻辑：直接写 PlayerSettings 序列化字段。
            // 值：0=旧输入, 1=新输入, 2=两者。设为 2(Both) 保证新旧输入都可用。
            var playerSettings = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (playerSettings == null || playerSettings.Length == 0)
            {
                Debug.LogWarning("[Before8AM] 未找到 PlayerSettings 对象，跳过输入后端设置");
                return;
            }
            var so = new SerializedObject(playerSettings[0]);
            SerializedProperty prop = so.FindProperty("activeInputHandler");
            if (prop == null)
            {
                Debug.LogWarning("[Before8AM] PlayerSettings 缺少 activeInputHandler 属性，跳过输入后端设置");
                return;
            }
            prop.intValue = 2;   // InputHandler.InputBoth
            so.ApplyModifiedProperties();
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == path) return;
            var list = new List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(path, true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ---------- 灰盒素材辅助 ----------

        /// <summary>
        /// 2.5D 灰盒建筑 = 玩家掩体：地板 + 四面墙（门洞）+ 无顶。
        /// 建筑内部一律用 NavMeshModifierVolume 挖空 NavMesh → 巡夜者无路可进（可靠，见下注释）。
        /// safeHouse=true 时为唯一安全屋（教学楼）：地板亮绿自发光 + SafeZone 触发器（躲进去免疫抓捕、巡夜者立即放弃追击）。
        /// safeHouse=false 时为普通建筑（食堂/宿舍）：暗红地板 + Building 触发器（躲进去只是拖延——巡夜者堵前门，
        ///   但建筑内感知被屏蔽 → 20s 脱战倒计时走完即离开）；rearDoor=true 时北墙加后门，被堵可穿堂溜走。
        /// 玩家用 CharacterController 不依赖 NavMesh，可自由进出；巡夜者只能走到门口。
        /// </summary>
        static GameObject CreateBuilding(string name, Vector3 center, Vector2 size, Material wall, Material floor,
            Material roofMat, Material windowMat, Material plazaMat, bool safeHouse = false, bool rearDoor = false,
            string label = "", string interiorKind = "", Material[] interiors = null)
        {
            // [0.4.3] 建筑根节点：全部子物体挂到 root 下，运行时 LayoutRandomizer 移动 root = 整楼移动（每把随机布局）。
            // SetParent(root, true) 保留世界坐标（worldPositionStays），root 位于建筑中心。
            GameObject root = new GameObject(name + "_Root");
            root.transform.position = center;

            GetBuildingStyle(name, interiorKind, wall, roofMat, out Material styledWall, out Material styledRoof, out Material facadeAccent);

            float x = size.x, z = size.y;
            const float wallThick = 0.4f;
            const float wallH = 4f;
            const float floorTop = 0.12f;   // 地板顶面：低门槛（< CharacterController.stepOffset 0.3），玩家可走进去

            // 地板（薄，高 0.12，中心 y = floorTop - 0.06）。
            // layer=2(Ignore Raycast)：不参与 NavMesh 烘焙（NavMesh 挖空由下方 NavMeshModifierVolume 负责）
            GameObject floorGo = CreateCube(name + "_Floor", center + Vector3.up * (floorTop - 0.06f), new Vector3(x, 0.12f, z), floor);
            floorGo.layer = 2;
            floorGo.transform.SetParent(root.transform, true);

            // 内部触发器：安全屋 → SafeZone tag（免疫抓捕 + 立即弃追）；普通建筑 → Building tag（感知屏蔽，拖延）。
            // 覆盖整栋建筑内部，玩家进入即标记。
            string zoneTag = safeHouse ? "SafeZone" : "Building";
            GameObject zone = new GameObject(name + "_" + zoneTag);
            zone.transform.position = center + Vector3.up * (floorTop + 1f);
            zone.tag = zoneTag;
            zone.layer = 2;   // 不参与 NavMesh 烘焙
            zone.transform.SetParent(root.transform, true);
            BoxCollider zc = zone.AddComponent<BoxCollider>();
            zc.isTrigger = true;
            zc.size = new Vector3(x - 0.6f, 2f, z - 0.6f);

            // 内部 NavMesh 挖空：ModifierVolume 覆盖建筑内径（每侧缩 0.5 > 墙厚，不延伸到墙体/门洞外）。
            // 修 critical：此前靠 layer2 地板排除烘焙，但下方 Ground 大平面仍在室内产生 NavMesh，
            // 巡夜者可能经门洞走进楼（0.12m 台阶的物理阻挡不可靠）——"巡夜者进不来"因此并不成立。
            // [0.4.3] 修 layer 矛盾：NavMeshSurface.layerMask=~(1<<2) 排除 layer2，而 ModifierVolume 在 layer2
            // 会被 NavMeshSurface.cs:345 按自身层过滤 → 挖空从未生效。改 layer0（volume 无 mesh/collider，
            // 不会被几何收集，只作为 ModifierBox 源，被 layerMask 包含 → 挖空真正生效）。
            GameObject navMod = new GameObject(name + "_NavMod");
            navMod.transform.position = center;
            navMod.layer = 0;
            navMod.transform.SetParent(root.transform, true);
            NavMeshModifierVolume mod = navMod.AddComponent<NavMeshModifierVolume>();
            mod.size = new Vector3(x - 1f, wallH + 1f, z - 1f);   // 内径略缩，不覆盖墙与门洞外的 NavMesh
            mod.center = Vector3.up * (wallH * 0.5f - 0.5f);      // 体积 y 范围覆盖地面 NavMesh 面(y=0)
            // area=Not Walkable(1) 直接在 NavMesh 上挖洞（API 文档：值为 1 即"not walkable"，产生洞；无 overrideArea 属性）
            mod.area = NavMesh.GetAreaFromName("Not Walkable");

            Vector3 wallC = center + Vector3.up * (floorTop + wallH * 0.5f);

            // 北墙（-Z）：普通建筑留后门（穿堂逃跑路线，被前门堵可从后门溜）；安全屋保持单前门据点
            float door = 2.4f;
            if (rearDoor)
            {
                float half = (x - door) * 0.5f;
                CreateCube(name + "_Wall_N_L", wallC + new Vector3(-(half + door) * 0.5f, 0, -z * 0.5f), new Vector3(half, wallH, wallThick), styledWall).transform.SetParent(root.transform, true);
                CreateCube(name + "_Wall_N_R", wallC + new Vector3((half + door) * 0.5f, 0, -z * 0.5f), new Vector3(half, wallH, wallThick), styledWall).transform.SetParent(root.transform, true);
            }
            else
            {
                CreateCube(name + "_Wall_N", wallC + new Vector3(0, 0, -z * 0.5f), new Vector3(x, wallH, wallThick), styledWall).transform.SetParent(root.transform, true);
            }

            // 东墙 / 西墙
            CreateCube(name + "_Wall_E", wallC + new Vector3(x * 0.5f, 0, 0), new Vector3(wallThick, wallH, z), styledWall).transform.SetParent(root.transform, true);
            CreateCube(name + "_Wall_W", wallC + new Vector3(-x * 0.5f, 0, 0), new Vector3(wallThick, wallH, z), styledWall).transform.SetParent(root.transform, true);

            // 南墙（+Z）留门洞（宽 2.4，居中），玩家前门出入口
            float halfWall = (x - door) * 0.5f;
            CreateCube(name + "_Wall_S_L", wallC + new Vector3(-(halfWall + door) * 0.5f, 0, z * 0.5f), new Vector3(halfWall, wallH, wallThick), styledWall).transform.SetParent(root.transform, true);
            CreateCube(name + "_Wall_S_R", wallC + new Vector3((halfWall + door) * 0.5f, 0, z * 0.5f), new Vector3(halfWall, wallH, wallThick), styledWall).transform.SetParent(root.transform, true);

            // ---- 建筑外观 v2：低成本结构细节（只做视觉层，不增加碰撞） ----
            // 同一墙体材质派生一档更深的收边色，避免新增全局材质表和运行时资源。
            Material trimMat = new Material(styledWall) { name = name + "_WallTrim" };
            Color trimColor = styledWall.color * 0.68f;
            trimColor.a = 1f;
            trimMat.color = trimColor;
            AddBuildingFacadeDetails(name, center, size, wallH, floorTop, door, rearDoor, styledWall, trimMat, root.transform);

            // ---- 画面 v1：坡屋顶（半透明，俯视可看进内部）+ 发光窗 + 建筑基座 ----
            AddRoof(name, center, size, styledRoof, wallH, floorTop, root.transform);
            AddWindows(name, center, size, wallH, floorTop, windowMat, root.transform);
            AddBuildingPlaza(name, center, size, plazaMat, root.transform);
            AddBuildingIdentityDetails(name, center, size, wallH, floorTop, interiorKind, facadeAccent, windowMat, root.transform);

            // ---- 识别度（用户反馈"食堂/教学楼都没做出来"）：门前发光招牌 + 内部摆设 ----
            // 招牌浮在屋顶上方（面朝上，俯视可读）；内部摆设按类型生成（教学楼=桌椅黑板 / 食堂=餐桌 / 宿舍=双层床）。
            if (!string.IsNullOrEmpty(label) && interiors != null && interiors.Length >= 3)
                AddBuildingLabel(name, center + new Vector3(0f, wallH + 1.5f, size.y * 0.5f - 0.8f), label);
            if (!string.IsNullOrEmpty(interiorKind) && interiors != null && interiors.Length >= 3)
                AddBuildingInterior(name, center, size, interiorKind, interiors, root.transform);

            return root;
        }

        /// <summary>同一贴图体系下按建筑功能生成低饱和材质变体；复用 Base Map，只改变色调和贴图节奏。</summary>
        static void GetBuildingStyle(string name, string kind, Material wallSource, Material roofSource,
            out Material wall, out Material roof, out Material accent)
        {
            Color wallTint = new Color(1f, 1f, 1f, 1f);
            Color roofTint = new Color(1f, 1f, 1f, 1f);
            Color accentColor = new Color(0.42f, 0.54f, 0.76f);
            Vector2 tiling = Vector2.one;
            switch (kind)
            {
                case "classroom":
                    wallTint = new Color(0.88f, 0.96f, 1.08f); roofTint = new Color(0.78f, 0.86f, 1.08f);
                    accentColor = new Color(0.38f, 0.62f, 0.90f); tiling = new Vector2(1.0f, 1.2f); break;
                case "canteen":
                    wallTint = new Color(1.08f, 0.91f, 0.80f); roofTint = new Color(1.12f, 0.78f, 0.70f);
                    accentColor = new Color(0.88f, 0.48f, 0.22f); tiling = new Vector2(1.4f, 0.9f); break;
                case "dorm":
                    wallTint = new Color(0.96f, 0.88f, 1.08f); roofTint = new Color(0.84f, 0.72f, 1.08f);
                    accentColor = new Color(0.68f, 0.46f, 0.86f); tiling = new Vector2(0.8f, 1.3f); break;
                case "library":
                    wallTint = new Color(0.80f, 0.96f, 0.90f); roofTint = new Color(0.68f, 0.92f, 0.82f);
                    accentColor = new Color(0.25f, 0.68f, 0.52f); tiling = new Vector2(1.3f, 1.1f); break;
                case "lab":
                    wallTint = new Color(0.82f, 1.00f, 1.06f); roofTint = new Color(0.72f, 0.94f, 1.08f);
                    accentColor = new Color(0.30f, 0.74f, 0.82f); tiling = new Vector2(1.1f, 0.8f); break;
                case "gym":
                    wallTint = new Color(1.06f, 0.92f, 0.76f); roofTint = new Color(1.15f, 0.72f, 0.58f);
                    accentColor = new Color(0.94f, 0.52f, 0.20f); tiling = new Vector2(1.5f, 1.2f); break;
                case "shop":
                    wallTint = new Color(0.82f, 0.94f, 1.08f); roofTint = new Color(0.68f, 0.82f, 1.08f);
                    accentColor = new Color(0.30f, 0.72f, 0.92f); tiling = new Vector2(0.9f, 1.4f); break;
                case "hall":
                    wallTint = new Color(1.08f, 0.82f, 0.90f); roofTint = new Color(1.10f, 0.62f, 0.72f);
                    accentColor = new Color(0.88f, 0.30f, 0.42f); tiling = new Vector2(1.35f, 0.85f); break;
            }

            // 同类第二栋也有小幅色调差，避免整排建筑像复制粘贴。
            float duplicateShift = name.EndsWith("2") ? 0.92f : 1f;
            wall = CreateBuildingMaterialVariant(wallSource, name + "_Wall", wallTint * duplicateShift, tiling);
            roof = CreateBuildingMaterialVariant(roofSource, name + "_Roof", roofTint * duplicateShift, tiling);
            accent = MakeMaterial(accentColor * duplicateShift, name + "_Accent", emissive: true,
                emissiveColor: accentColor * 1.05f * duplicateShift);
        }

        static Material CreateBuildingMaterialVariant(Material source, string name, Color tint, Vector2 tiling)
        {
            Material variant = new Material(source) { name = name };
            Color color = source.color;
            color.r *= tint.r;
            color.g *= tint.g;
            color.b *= tint.b;
            variant.color = color;
            if (variant.mainTexture != null) variant.mainTextureScale = Vector2.Scale(variant.mainTextureScale, tiling);
            if (variant.HasProperty("_BaseMap")) variant.SetTextureScale("_BaseMap", variant.mainTextureScale);
            return variant;
        }

        /// <summary>不同功能建筑的无碰撞入口特征，确保从俯视也能读出用途。</summary>
        static void AddBuildingIdentityDetails(string name, Vector3 center, Vector2 size, float wallH, float floorTop,
            string kind, Material accentMat, Material windowMat, Transform parent)
        {
            float frontZ = center.z + size.y * 0.5f + 0.30f;
            float y = floorTop + 2.2f;
            switch (kind)
            {
                case "classroom":
                    for (int i = -1; i <= 1; i++)
                        CreateVisualCube(name + "_ClassBanner_" + i, new Vector3(center.x + i * 1.25f, y, frontZ), new Vector3(0.22f, 1.25f, 0.06f), accentMat, parent);
                    break;
                case "canteen":
                    CreateVisualCube(name + "_CanteenAwning", new Vector3(center.x, floorTop + 2.75f, frontZ + 0.22f), new Vector3(size.x * 0.62f, 0.16f, 0.75f), accentMat, parent);
                    break;
                case "dorm":
                    for (int i = -1; i <= 1; i++)
                    {
                        float bx = center.x + i * size.x * 0.24f;
                        CreateVisualCube(name + "_DormBalcony_" + i, new Vector3(bx, floorTop + 2.05f, frontZ + 0.12f), new Vector3(size.x * 0.18f, 0.10f, 0.46f), accentMat, parent);
                        CreateVisualCube(name + "_DormRail_" + i, new Vector3(bx, floorTop + 2.42f, frontZ + 0.31f), new Vector3(size.x * 0.18f, 0.45f, 0.06f), accentMat, parent);
                    }
                    break;
                case "library":
                    CreateVisualCube(name + "_LibraryColumn_L", new Vector3(center.x - size.x * 0.28f, floorTop + 1.45f, frontZ), new Vector3(0.32f, 2.7f, 0.20f), accentMat, parent);
                    CreateVisualCube(name + "_LibraryColumn_R", new Vector3(center.x + size.x * 0.28f, floorTop + 1.45f, frontZ), new Vector3(0.32f, 2.7f, 0.20f), accentMat, parent);
                    break;
                case "lab":
                    for (int i = -1; i <= 1; i++)
                        StylizedLowPolyFactory.CreateTaperedPrism(name + "_LabVent_" + i,
                            new Vector3(center.x + i * 1.2f, floorTop + wallH + 0.35f, center.z), new Vector2(0.42f, 0.42f), new Vector2(0.24f, 0.24f), 0.70f, 6, accentMat, parent);
                    break;
                case "gym":
                    CreateVisualCube(name + "_GymHighWindow", new Vector3(center.x, floorTop + 3.0f, frontZ), new Vector3(size.x * 0.55f, 0.52f, 0.06f), windowMat, parent);
                    break;
                case "shop":
                    CreateVisualCube(name + "_ShopCanopy", new Vector3(center.x, floorTop + 2.45f, frontZ + 0.18f), new Vector3(size.x * 0.52f, 0.13f, 0.65f), accentMat, parent);
                    break;
                case "hall":
                    CreateVisualCube(name + "_HallMarquee", new Vector3(center.x, floorTop + 3.1f, frontZ), new Vector3(size.x * 0.52f, 0.30f, 0.08f), accentMat, parent);
                    break;
            }
        }

        /// <summary>
        /// 建筑外观结构细节：四角立柱 + 墙顶压边 + 门洞两侧收边。
        /// 全部为 layer2 无碰撞装饰，避免改变玩家/守卫的碰撞与 NavMesh。
        /// </summary>
        static void AddBuildingFacadeDetails(string name, Vector3 center, Vector2 size, float wallH, float floorTop,
            float door, bool rearDoor, Material wallMat, Material trimMat, Transform parent)
        {
            float x = size.x, z = size.y;
            float wallY = floorTop + wallH * 0.5f;
            float topY = floorTop + wallH - 0.10f;
            float corner = 0.28f;
            float trimH = 0.22f;
            float wallThick = 0.46f;

            // 可见外壳使用收腰斜面，而原 BoxCollider 墙仍负责门洞、碰撞与 NavMesh。
            // 南北门洞依旧留空，避免美术层把可通行入口盖回去。
            float sideLen = Mathf.Max(0.5f, (x - door) * 0.5f);
            float sideOffset = (sideLen + door) * 0.5f;
            CreateFacadeSegment(name + "_Facade_S_L", center + new Vector3(-sideOffset, wallY, z * 0.5f), new Vector2(sideLen, 0.52f), wallH, wallMat, parent);
            CreateFacadeSegment(name + "_Facade_S_R", center + new Vector3(sideOffset, wallY, z * 0.5f), new Vector2(sideLen, 0.52f), wallH, wallMat, parent);
            if (rearDoor)
            {
                CreateFacadeSegment(name + "_Facade_N_L", center + new Vector3(-sideOffset, wallY, -z * 0.5f), new Vector2(sideLen, 0.52f), wallH, wallMat, parent);
                CreateFacadeSegment(name + "_Facade_N_R", center + new Vector3(sideOffset, wallY, -z * 0.5f), new Vector2(sideLen, 0.52f), wallH, wallMat, parent);
            }
            else
            {
                CreateFacadeSegment(name + "_Facade_N", center + new Vector3(0f, wallY, -z * 0.5f), new Vector2(x, 0.52f), wallH, wallMat, parent);
            }
            CreateFacadeSegment(name + "_Facade_E", center + new Vector3(x * 0.5f, wallY, 0f), new Vector2(0.52f, z), wallH, wallMat, parent);
            CreateFacadeSegment(name + "_Facade_W", center + new Vector3(-x * 0.5f, wallY, 0f), new Vector2(0.52f, z), wallH, wallMat, parent);

            // 四角竖向收边：让建筑从远处也有明确轮廓。
            CreateVisualCube(name + "_Trim_Corner_NE", center + new Vector3(x * 0.5f, wallY, -z * 0.5f), new Vector3(corner, wallH + 0.16f, corner), trimMat, parent);
            CreateVisualCube(name + "_Trim_Corner_NW", center + new Vector3(-x * 0.5f, wallY, -z * 0.5f), new Vector3(corner, wallH + 0.16f, corner), trimMat, parent);
            CreateVisualCube(name + "_Trim_Corner_SE", center + new Vector3(x * 0.5f, wallY, z * 0.5f), new Vector3(corner, wallH + 0.16f, corner), trimMat, parent);
            CreateVisualCube(name + "_Trim_Corner_SW", center + new Vector3(-x * 0.5f, wallY, z * 0.5f), new Vector3(corner, wallH + 0.16f, corner), trimMat, parent);

            // 墙顶压边分段避开前后门洞，不把门重新封成一面墙。
            CreateVisualCube(name + "_Trim_Top_S_L", center + new Vector3(-sideOffset, topY, z * 0.5f), new Vector3(sideLen, trimH, wallThick), trimMat, parent);
            CreateVisualCube(name + "_Trim_Top_S_R", center + new Vector3(sideOffset, topY, z * 0.5f), new Vector3(sideLen, trimH, wallThick), trimMat, parent);
            CreateVisualCube(name + "_Trim_Top_N_L", center + new Vector3(-sideOffset, topY, -z * 0.5f), new Vector3(sideLen, trimH, wallThick), trimMat, parent);
            CreateVisualCube(name + "_Trim_Top_N_R", center + new Vector3(sideOffset, topY, -z * 0.5f), new Vector3(sideLen, trimH, wallThick), trimMat, parent);
            CreateVisualCube(name + "_Trim_Top_E", center + new Vector3(x * 0.5f, topY, 0f), new Vector3(wallThick, trimH, z), trimMat, parent);
            CreateVisualCube(name + "_Trim_Top_W", center + new Vector3(-x * 0.5f, topY, 0f), new Vector3(wallThick, trimH, z), trimMat, parent);

            // 门洞两侧短收边，强化入口位置但不添加门、文字或玩法提示。
            float doorSideX = door * 0.5f + 0.16f;
            CreateVisualCube(name + "_Trim_Door_S_L", center + new Vector3(-doorSideX, wallY, z * 0.5f + 0.02f), new Vector3(0.16f, wallH - 0.25f, 0.12f), trimMat, parent);
            CreateVisualCube(name + "_Trim_Door_S_R", center + new Vector3(doorSideX, wallY, z * 0.5f + 0.02f), new Vector3(0.16f, wallH - 0.25f, 0.12f), trimMat, parent);
            CreateVisualCube(name + "_Trim_Door_N_L", center + new Vector3(-doorSideX, wallY, -z * 0.5f - 0.02f), new Vector3(0.16f, wallH - 0.25f, 0.12f), trimMat, parent);
            CreateVisualCube(name + "_Trim_Door_N_R", center + new Vector3(doorSideX, wallY, -z * 0.5f - 0.02f), new Vector3(0.16f, wallH - 0.25f, 0.12f), trimMat, parent);
        }

        static void CreateFacadeSegment(string name, Vector3 pos, Vector2 baseSize, float height, Material mat, Transform parent)
        {
            Vector2 topSize = new Vector2(baseSize.x * 0.91f, baseSize.y * 0.90f);
            StylizedLowPolyFactory.CreateTaperedPrism(name, pos, baseSize, topSize, height - 0.12f, 4, mat, parent);
        }

        static GameObject CreateVisualCube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = 2;
            go.transform.position = pos;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            if (parent != null) go.transform.SetParent(parent, true);
            return go;
        }

        /// <summary>门前招牌：世界空间 Canvas + 中文 Text（SimHei 字体，含中文字形），面朝上浮在屋顶上方，俯视可读。
        /// 字号调校：世界高度 ≈ fontSize × localScale × 1.2 / 100 ≈ 0.7m（3 字宽 ~2m）。</summary>
        static void AddBuildingLabel(string parentName, Vector3 pos, string text)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasGo = new GameObject(parentName + "_Label");
            canvasGo.transform.position = pos;
            canvasGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 面朝上（俯视可读）
            canvasGo.layer = 2;   // 装饰层，不参与 NavMesh

            Canvas cv = canvasGo.AddComponent<Canvas>();
            cv.renderMode = RenderMode.WorldSpace;
            RectTransform crt = canvasGo.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(6f, 1f);
            crt.localScale = new Vector3(0.6f, 0.6f, 1f);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            RectTransform trt = textGo.AddComponent<RectTransform>();
            trt.sizeDelta = crt.sizeDelta;
            var tm = textGo.AddComponent<UnityEngine.UI.Text>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 100;
            tm.alignment = TextAnchor.MiddleCenter;
            tm.horizontalOverflow = HorizontalWrapMode.Overflow;
            tm.verticalOverflow = VerticalWrapMode.Overflow;
            tm.color = new Color(1f, 0.93f, 0.7f);
            tm.raycastTarget = false;
        }

        /// <summary>内部摆设：教学楼=桌椅黑板 / 食堂=餐桌椅 / 宿舍=双层床。层2 不参与 NavMesh；
        /// 桌椅/床/书架/实验台/座位保留 BoxCollider（玩家撞上绕行，家具是实体掩体）；
        /// 黑板/球场/球架/舞台为贴墙贴地装饰，无碰撞（不产生隐形墙）。</summary>
        static void AddBuildingInterior(string name, Vector3 center, Vector2 size, string kind, Material[] m, Transform parent = null)
        {
            GameObject root = new GameObject(name + "_Interior");
            root.transform.position = center;
            root.layer = 2;
            if (parent != null) root.transform.SetParent(parent, true);   // [0.4.3] 挂建筑根，整楼移动

            float x = size.x, z = size.y;
            switch (kind)
            {
                case "classroom":   // 教学楼：黑板（北墙内侧）+ 3排×2列课桌椅
                    CreateInteriorProp(root.transform, "Blackboard", new Vector3(0f, 2.2f, -z * 0.5f + 0.25f), new Vector3(3.2f, 1.4f, 0.1f), m[2], collidable: false);   // 高位贴墙装饰，不挡路
                    int rows = 3;
                    for (int r = 0; r < rows; r++)
                        for (int col = 0; col < 2; col++)
                        {
                            float px = -x * 0.25f + col * x * 0.5f;
                            float pz = -z * 0.12f + r * 1.4f;
                            CreateInteriorProp(root.transform, "Desk", new Vector3(px, 0.45f, pz), new Vector3(0.9f, 0.4f, 0.6f), m[0]);
                            CreateInteriorProp(root.transform, "Chair", new Vector3(px, 0.28f, pz + 0.7f), new Vector3(0.5f, 0.28f, 0.5f), m[0]);
                        }
                    break;
                case "canteen":     // 食堂：2排×3列餐桌 + 两侧椅子
                    for (int r = 0; r < 2; r++)
                        for (int col = 0; col < 3; col++)
                        {
                            float px = -x * 0.3f + col * x * 0.3f;
                            float pz = -z * 0.15f + r * 2.2f;
                            CreateInteriorProp(root.transform, "Table", new Vector3(px, 0.55f, pz), new Vector3(1.4f, 0.5f, 0.9f), m[0]);
                            CreateInteriorProp(root.transform, "Chair", new Vector3(px - 0.9f, 0.28f, pz), new Vector3(0.45f, 0.28f, 0.45f), m[0]);
                            CreateInteriorProp(root.transform, "Chair", new Vector3(px + 0.9f, 0.28f, pz), new Vector3(0.45f, 0.28f, 0.45f), m[0]);
                        }
                    break;
                case "dorm":        // 宿舍：3 张双层床（上下铺 + 四角立柱）
                    for (int i = 0; i < 3; i++)
                    {
                        float px = -x * 0.3f + i * x * 0.3f;
                        CreateInteriorProp(root.transform, "BunkBottom", new Vector3(px, 0.55f, 0f), new Vector3(0.9f, 0.12f, 1.8f), m[1]);
                        CreateInteriorProp(root.transform, "BunkTop", new Vector3(px, 1.35f, 0f), new Vector3(0.9f, 0.12f, 1.8f), m[1]);
                        CreateInteriorProp(root.transform, "BunkPillar", new Vector3(px - 0.35f, 0.9f, 0.9f), new Vector3(0.08f, 1.8f, 0.08f), m[1]);
                        CreateInteriorProp(root.transform, "BunkPillar", new Vector3(px + 0.35f, 0.9f, -0.9f), new Vector3(0.08f, 1.8f, 0.08f), m[1]);
                    }
                    break;
                case "library":     // 图书馆：南北两侧大书架 + 中间阅读桌（俯视一排排书脊 = 图书馆）
                    // 书架沿南北墙各拆两段、中间留门洞（宽 2.4 = 门宽）：整条书架横贯墙面会堵死前门和后门
                    //（用户反馈：门前后都被挡板挡住进不去）。段长 = 墙边到门边，中心 ±(墙边+门边)/2。
                    {
                        float doorHalf = 1.2f;                          // 门半宽（南墙门洞宽 2.4）
                        float shelfInner = x * 0.5f - 0.35f;            // 书架内缘（贴近内墙面）
                        float shelfLen = shelfInner - doorHalf;         // 单段书架长（墙边→门边）
                        float shelfC = (shelfInner + doorHalf) * 0.5f;  // 西段中心 x（东段为 +）
                        CreateInteriorProp(root.transform, "Bookshelf_N_W", new Vector3(-shelfC, 1.0f, -z * 0.5f + 0.45f), new Vector3(shelfLen, 2.0f, 0.35f), m[3]);
                        CreateInteriorProp(root.transform, "Bookshelf_N_E", new Vector3(shelfC, 1.0f, -z * 0.5f + 0.45f), new Vector3(shelfLen, 2.0f, 0.35f), m[3]);
                        CreateInteriorProp(root.transform, "Bookshelf_S_W", new Vector3(-shelfC, 1.0f, z * 0.5f - 0.45f), new Vector3(shelfLen, 2.0f, 0.35f), m[3]);
                        CreateInteriorProp(root.transform, "Bookshelf_S_E", new Vector3(shelfC, 1.0f, z * 0.5f - 0.45f), new Vector3(shelfLen, 2.0f, 0.35f), m[3]);
                    }
                    for (int r = 0; r < 2; r++)
                        for (int col = 0; col < 3; col++)
                        {
                            float px = -x * 0.3f + col * x * 0.3f;
                            float pz = -z * 0.15f + r * 1.8f;
                            CreateInteriorProp(root.transform, "ReadingTable", new Vector3(px, 0.5f, pz), new Vector3(1.5f, 0.4f, 0.8f), m[0]);
                            CreateInteriorProp(root.transform, "Chair", new Vector3(px, 0.3f, pz + 0.8f), new Vector3(0.4f, 0.3f, 0.4f), m[0]);
                        }
                    break;
                case "lab":         // 实验楼：三列长实验台 + 两侧凳
                    for (int col = 0; col < 3; col++)
                    {
                        float px = -x * 0.25f + col * x * 0.25f;
                        CreateInteriorProp(root.transform, "LabBench", new Vector3(px, 0.55f, 0f), new Vector3(0.8f, 0.5f, z - 1.4f), m[4]);
                        CreateInteriorProp(root.transform, "Stool", new Vector3(px, 0.3f, -z * 0.25f), new Vector3(0.3f, 0.3f, 0.3f), m[0]);
                        CreateInteriorProp(root.transform, "Stool", new Vector3(px, 0.3f, z * 0.25f), new Vector3(0.3f, 0.3f, 0.3f), m[0]);
                    }
                    break;
                case "gym":         // 体育馆：低球场平台 + 两端篮球架（橙）
                    // 球场/球架是贴地或高位装饰，不产生碰撞（否则球架细柱会挡成一堵隐形墙）
                    CreateInteriorProp(root.transform, "Court", new Vector3(0f, 0.05f, 0f), new Vector3(x - 2.4f, 0.08f, z - 2.4f), m[5], collidable: false);
                    CreateInteriorProp(root.transform, "Hoop_N", new Vector3(0f, 2.4f, -z * 0.5f + 1.2f), new Vector3(0.3f, 2.0f, 0.3f), m[5], collidable: false);
                    CreateInteriorProp(root.transform, "Backboard_N", new Vector3(0f, 2.6f, -z * 0.5f + 0.9f), new Vector3(1.2f, 0.15f, 0.15f), m[5], collidable: false);
                    CreateInteriorProp(root.transform, "Hoop_S", new Vector3(0f, 2.4f, z * 0.5f - 1.2f), new Vector3(0.3f, 2.0f, 0.3f), m[5], collidable: false);
                    CreateInteriorProp(root.transform, "Backboard_S", new Vector3(0f, 2.6f, z * 0.5f - 0.9f), new Vector3(1.2f, 0.15f, 0.15f), m[5], collidable: false);
                    break;
                case "shop":        // 小卖部：两侧货架 + 北端收银台（小空间，货架贴墙留中央过道；南门正对过道）
                    CreateInteriorProp(root.transform, "Shelf_W", new Vector3(-x * 0.5f + 0.6f, 1.0f, 0f), new Vector3(0.5f, 2.0f, z - 0.9f), m[3]);
                    CreateInteriorProp(root.transform, "Shelf_E", new Vector3(x * 0.5f - 0.6f, 1.0f, 0f), new Vector3(0.5f, 2.0f, z - 0.9f), m[3]);
                    CreateInteriorProp(root.transform, "Counter", new Vector3(0f, 0.55f, -z * 0.5f + 0.7f), new Vector3(x - 1.6f, 0.5f, 0.7f), m[0]);
                    break;
                case "hall":        // 报告厅：北端舞台 + 阶梯座位（逐排抬高，俯视像剧场）
                    // 舞台无碰撞：报告厅北墙有后门，舞台挡在北墙前，有碰撞会堵死后门穿堂逃生（用户反馈过的设计）
                    CreateInteriorProp(root.transform, "Stage", new Vector3(0f, 0.35f, -z * 0.5f + 1.2f), new Vector3(x - 1.6f, 0.3f, 1.6f), m[6], collidable: false);
                    for (int r = 0; r < 4; r++)
                        for (int col = 0; col < 3; col++)
                        {
                            // 最南一排正对南门：中间列留空（门正对位）——否则中间座位把门洞堵死，玩家进不去（用户反馈）
                            if (r == 3 && col == 1) continue;
                            float px = -x * 0.3f + col * x * 0.3f;
                            float pz = -z * 0.1f + r * 1.3f;
                            CreateInteriorProp(root.transform, "Seat", new Vector3(px, 0.35f + r * 0.14f, pz), new Vector3(0.9f, 0.12f, 0.6f), m[0]);
                        }
                    break;
            }
        }

        /// <summary>
        /// 建筑内部摆设（层 2：不参与 NavMesh 烘焙；但玩家在 Default 层，碰撞矩阵默认全开，会与它碰撞）。
        /// collidable=true（默认）保留 BoxCollider —— 桌椅/床/书架等家具是实体，玩家 CharacterController
        /// 撞上会绕行（用户反馈：之前能直接穿模走过去，摆设没有意义）。collidable=false 用于贴墙/贴地/高位
        /// 装饰件（黑板/球场/球架/舞台），不产生隐形墙挡路。
        /// </summary>
        static void CreateInteriorProp(Transform root, string name, Vector3 localPos, Vector3 scale, Material mat, bool collidable = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.layer = 2;
            if (!collidable)
                Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            // 旧 Cube 是室内家具的物理承载层。桌椅有各自的清晰轮廓；其他物体继续使用收腰低模。
            go.GetComponent<Renderer>().enabled = false;
            if (name.Contains("Desk") || name.Contains("Table") || name.Contains("Bench") || name == "Counter")
            {
                CreateTableVisual(root, name + "_Visual", localPos, scale, mat);
            }
            else if (name == "Chair" || name == "Stool" || name == "Seat")
            {
                CreateChairVisual(root, name + "_Visual", localPos, scale, mat, name == "Stool");
            }
            else
            {
                StylizedLowPolyFactory.CreateLocalTaperedPrism(root, name + "_Visual", localPos,
                    new Vector2(scale.x, scale.z), new Vector2(scale.x * 0.84f, scale.z * 0.84f), scale.y, 4, mat);
            }
        }

        /// <summary>桌面和四条细腿保持矩形家具的稳定感；视觉层不带碰撞，承载层仍由 CreateInteriorProp 保留。</summary>
        static void CreateTableVisual(Transform root, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            float topThickness = Mathf.Clamp(scale.y * 0.24f, 0.08f, 0.13f);
            float topY = localPos.y + scale.y * 0.5f - topThickness * 0.5f;
            float legHeight = Mathf.Max(0.24f, scale.y - topThickness);
            float legY = topY - topThickness * 0.5f - legHeight * 0.5f;
            float legWidth = Mathf.Clamp(Mathf.Min(scale.x, scale.z) * 0.10f, 0.07f, 0.12f);
            float insetX = Mathf.Max(0.10f, scale.x * 0.5f - legWidth);
            float insetZ = Mathf.Max(0.10f, scale.z * 0.5f - legWidth);

            CreateInteriorVisualCube(root, name + "_Top", new Vector3(localPos.x, topY, localPos.z),
                new Vector3(scale.x * 1.04f, topThickness, scale.z * 1.04f), mat);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    CreateInteriorVisualCube(root, name + "_Leg_" + x + "_" + z,
                        new Vector3(localPos.x + insetX * x, legY, localPos.z + insetZ * z),
                        new Vector3(legWidth, legHeight, legWidth), mat);
        }

        /// <summary>座面、四腿和后靠背让课椅/餐椅在俯视和第一人称都能一眼读出用途。</summary>
        static void CreateChairVisual(Transform root, string name, Vector3 localPos, Vector3 scale, Material mat, bool isStool)
        {
            float seatThickness = Mathf.Clamp(scale.y * 0.36f, 0.08f, 0.13f);
            float seatY = localPos.y + scale.y * 0.28f;
            float legHeight = Mathf.Max(0.18f, seatY - seatThickness * 0.5f);
            float legY = seatY - seatThickness * 0.5f - legHeight * 0.5f;
            float legWidth = Mathf.Clamp(Mathf.Min(scale.x, scale.z) * 0.16f, 0.06f, 0.10f);
            float insetX = Mathf.Max(0.07f, scale.x * 0.5f - legWidth);
            float insetZ = Mathf.Max(0.07f, scale.z * 0.5f - legWidth);

            CreateInteriorVisualCube(root, name + "_Seat", new Vector3(localPos.x, seatY, localPos.z),
                new Vector3(scale.x * 1.05f, seatThickness, scale.z * 1.05f), mat);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    CreateInteriorVisualCube(root, name + "_Leg_" + x + "_" + z,
                        new Vector3(localPos.x + insetX * x, legY, localPos.z + insetZ * z),
                        new Vector3(legWidth, legHeight, legWidth), mat);
            if (!isStool)
            {
                float backHeight = Mathf.Max(0.32f, scale.y * 1.55f);
                CreateInteriorVisualCube(root, name + "_Back", new Vector3(localPos.x, seatY + backHeight * 0.45f, localPos.z - insetZ),
                    new Vector3(scale.x * 0.92f, backHeight, Mathf.Max(0.06f, scale.z * 0.16f)), mat);
            }
        }

        static void CreateInteriorVisualCube(Transform root, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = CreateVisualCube(name, root.TransformPoint(localPos), scale, mat, root);
            go.transform.localPosition = localPos;
        }

        /// <summary>开场宿舍房间：南墙外 z 24~29.5 的一间封闭宿舍（地板 + 三面墙，北面就是南墙那扇翻窗）。
        /// 内部：精细单人床（床架/床头尾板/被褥）+ 带抽屉的书桌台灯 + 衣柜 + 窗前地毯。
        /// 玩家在房间里醒来看向床，环视后爬出窗户到校园（WindowIntro 过场）。全部 layer 2 装饰，无碰撞不参与寻路。</summary>
        static void CreateIntroDormRoom(Material deskMat, Material bedMat, Material blanketMat, Material sheetMat, Material boardMat,
            Material wallMat, Material glowMat, Material gateFrameMat, Material metalMat)
        {
            // 地板（深木色，独立空间）+ 三面墙（北面=南墙 z=40 已存在，这面是后墙/两侧墙）[0.3.0] 全部 z+16
            CreateCube("DormRoom_Floor", new Vector3(0f, 0.02f, 42.75f), new Vector3(8f, 0.15f, 5.6f), deskMat).layer = 2;
            CreateCube("DormRoom_Wall_S", new Vector3(0f, 1.5f, 45.45f), new Vector3(8f, 3f, 0.35f), wallMat).layer = 2;
            CreateCube("DormRoom_Wall_W", new Vector3(-4f, 1.5f, 42.75f), new Vector3(0.35f, 3f, 5.6f), wallMat).layer = 2;
            CreateCube("DormRoom_Wall_E", new Vector3(4f, 1.5f, 42.75f), new Vector3(0.35f, 3f, 5.6f), wallMat).layer = 2;
            // 窗前暖色地毯（爬窗前看到的"宿舍感"地面）
            CreateCube("DormRoom_Rug", new Vector3(0f, 0.10f, 41.3f), new Vector3(2.6f, 0.05f, 2.8f), blanketMat).layer = 2;
            // 床和书桌是开场镜头首先停留的对象，使用独立的细节模型，避免在近景里读成几个方块。
            CreateIntroDormBed(new Vector3(-2.9f, 0f, 42.3f), bedMat, sheetMat, blanketMat, boardMat);
            CreateIntroDormDesk(new Vector3(2.9f, 0f, 42.0f), deskMat, bedMat, blanketMat, boardMat, gateFrameMat, glowMat, metalMat);
            // 衣柜（靠后墙）+ 侧墙贴画（暖黄小方块，朝房间这面，环视时一眼看到）
            CreateCube("DormRoom_Wardrobe", new Vector3(0f, 1.0f, 44.6f), new Vector3(1.6f, 2.0f, 0.5f), bedMat).layer = 2;
            CreateCube("DormRoom_Poster", new Vector3(-3.9f, 1.9f, 41.8f), new Vector3(0.05f, 0.6f, 0.8f), gateFrameMat).layer = 2;
        }

        /// <summary>开场近景床：完整床架、床头尾板、床垫、折叠被褥和双枕头；从相机起点看去仍保留清晰的大轮廓。</summary>
        static void CreateIntroDormBed(Vector3 center, Material frameMat, Material sheetMat, Material blanketMat, Material accentMat)
        {
            GameObject root = new GameObject("DormRoom_Bed");
            root.layer = 2;
            Transform t = root.transform;
            const float halfW = 0.72f;
            const float halfL = 1.05f;

            CreateVisualCube("DormRoom_Bed_Base", center + new Vector3(0f, 0.38f, 0f), new Vector3(1.42f, 0.14f, 2.05f), frameMat, t);
            CreateVisualCube("DormRoom_Bed_Rail_L", center + new Vector3(-halfW, 0.53f, 0f), new Vector3(0.12f, 0.32f, 2.12f), frameMat, t);
            CreateVisualCube("DormRoom_Bed_Rail_R", center + new Vector3(halfW, 0.53f, 0f), new Vector3(0.12f, 0.32f, 2.12f), frameMat, t);
            CreateVisualCube("DormRoom_Bed_Headboard", center + new Vector3(0f, 0.93f, -halfL), new Vector3(1.58f, 1.16f, 0.14f), frameMat, t);
            CreateVisualCube("DormRoom_Bed_Footboard", center + new Vector3(0f, 0.67f, halfL), new Vector3(1.58f, 0.64f, 0.14f), frameMat, t);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    CreateVisualCube("DormRoom_Bed_Leg_" + x + "_" + z,
                        center + new Vector3(x * 0.62f, 0.20f, z * 0.92f), new Vector3(0.14f, 0.40f, 0.14f), frameMat, t);

            CreateVisualCube("DormRoom_Bed_Mattress", center + new Vector3(0f, 0.58f, 0f), new Vector3(1.34f, 0.22f, 1.93f), sheetMat, t);
            CreateVisualCube("DormRoom_Bed_Blanket", center + new Vector3(0f, 0.74f, 0.48f), new Vector3(1.32f, 0.15f, 0.90f), blanketMat, t);
            CreateVisualCube("DormRoom_Bed_BlanketFold", center + new Vector3(0f, 0.82f, 0.08f), new Vector3(1.32f, 0.08f, 0.13f), blanketMat, t);
            CreateVisualCube("DormRoom_Bed_Pillow_L", center + new Vector3(-0.34f, 0.75f, -0.63f), new Vector3(0.53f, 0.15f, 0.46f), sheetMat, t);
            CreateVisualCube("DormRoom_Bed_Pillow_R", center + new Vector3(0.34f, 0.75f, -0.63f), new Vector3(0.53f, 0.15f, 0.46f), sheetMat, t);
            CreateVisualCube("DormRoom_Bed_Book", center + new Vector3(0.30f, 0.85f, 0.20f), new Vector3(0.34f, 0.05f, 0.42f), accentMat, t);
        }

        /// <summary>开场近景书桌：四腿桌、右侧抽屉柜、台灯、书本与真实暖光；环视时能明确读为可使用的学习区。</summary>
        static void CreateIntroDormDesk(Vector3 center, Material woodMat, Material drawerMat, Material bookMat, Material paperMat,
            Material lampMat, Material glowMat, Material metalMat)
        {
            GameObject root = new GameObject("DormRoom_Desk");
            root.layer = 2;
            Transform t = root.transform;
            const float halfW = 0.78f;
            const float halfD = 0.45f;

            CreateVisualCube("DormRoom_Desk_Top", center + new Vector3(0f, 0.72f, 0f), new Vector3(1.62f, 0.14f, 0.96f), woodMat, t);
            CreateVisualCube("DormRoom_Desk_Apron", center + new Vector3(0f, 0.61f, 0.43f), new Vector3(1.52f, 0.13f, 0.08f), woodMat, t);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    CreateVisualCube("DormRoom_Desk_Leg_" + x + "_" + z,
                        center + new Vector3(x * (halfW - 0.07f), 0.34f, z * (halfD - 0.07f)), new Vector3(0.11f, 0.66f, 0.11f), woodMat, t);

            CreateVisualCube("DormRoom_Desk_DrawerUnit", center + new Vector3(0.55f, 0.34f, -0.02f), new Vector3(0.38f, 0.64f, 0.78f), drawerMat, t);
            for (int i = 0; i < 2; i++)
            {
                float y = 0.28f + i * 0.26f;
                CreateVisualCube("DormRoom_Desk_Drawer_" + i, center + new Vector3(0.55f, y, 0.39f), new Vector3(0.32f, 0.20f, 0.05f), woodMat, t);
                CreateVisualCube("DormRoom_Desk_Handle_" + i, center + new Vector3(0.55f, y, 0.425f), new Vector3(0.12f, 0.035f, 0.035f), metalMat, t);
            }

            CreateVisualCube("DormRoom_Desk_Notebook", center + new Vector3(-0.20f, 0.81f, 0.02f), new Vector3(0.42f, 0.045f, 0.55f), bookMat, t);
            CreateVisualCube("DormRoom_Desk_Paper", center + new Vector3(0.16f, 0.815f, -0.18f), new Vector3(0.34f, 0.025f, 0.44f), paperMat, t);
            CreateVisualCube("DormRoom_Desk_Pencil", center + new Vector3(0.10f, 0.845f, 0.19f), new Vector3(0.05f, 0.025f, 0.38f), lampMat, t);

            CreateVisualCube("DormRoom_Desk_LampBase", center + new Vector3(-0.57f, 0.82f, -0.12f), new Vector3(0.28f, 0.06f, 0.28f), metalMat, t);
            CreateVisualCube("DormRoom_Desk_LampStem", center + new Vector3(-0.57f, 1.03f, -0.12f), new Vector3(0.06f, 0.40f, 0.06f), metalMat, t);
            GameObject arm = CreateVisualCube("DormRoom_Desk_LampArm", center + new Vector3(-0.49f, 1.20f, -0.12f), new Vector3(0.05f, 0.38f, 0.05f), metalMat, t);
            arm.transform.rotation = Quaternion.Euler(0f, 0f, -28f);
            StylizedLowPolyFactory.CreateTaperedPrism("DormRoom_Desk_Lampshade", center + new Vector3(-0.35f, 1.31f, -0.12f),
                new Vector2(0.30f, 0.30f), new Vector2(0.18f, 0.18f), 0.20f, 6, lampMat, t);
            CreateVisualCube("DormRoom_Desk_LampGlow", center + new Vector3(-0.35f, 1.18f, -0.12f), new Vector3(0.20f, 0.08f, 0.20f), glowMat, t);

            GameObject lightGo = new GameObject("DormRoom_Desk_Light");
            lightGo.layer = 2;
            lightGo.transform.position = center + new Vector3(-0.35f, 1.18f, -0.12f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.70f, 0.42f);
            light.intensity = 1.2f;
            light.range = 3.8f;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>坡屋顶：两片半透明斜板 + 顶脊 + RoofFade（玩家进建筑时屋顶淡出，不再遮挡俯视的"我"）。</summary>
        static void AddRoof(string name, Vector3 center, Vector2 size, Material roofMat, float wallH, float floorTop, Transform parent = null)
        {
            float x = size.x, z = size.y;
            const float roofH = 1.1f;
            float yBase = floorTop + wallH;

            // 屋顶容器（层2 不参与烘焙；RoofFade 挂这里统一控制所有斜面）。
            GameObject roof = new GameObject(name + "_Roof");
            roof.transform.position = center;
            roof.layer = 2;
            if (parent != null) roof.transform.SetParent(parent, true);   // [0.4.3] 挂建筑根，整楼移动

            // 单一程序网格替代两块旋转 Cube：立面、屋檐和山墙的轮廓完整，斜俯视下更像绘本低模建筑。
            StylizedLowPolyFactory.CreateGabledRoof(name + "_GableMesh", center + Vector3.up * yBase,
                x + 0.62f, z + 0.72f, roofH, roofMat, roof.transform);
            StylizedLowPolyFactory.CreateTaperedPrism(name + "_Ridge", center + new Vector3(0f, yBase + roofH, 0f),
                new Vector2(x + 0.72f, 0.25f), new Vector2(x + 0.48f, 0.16f), 0.18f, 4, roofMat, roof.transform);

            // 玩家进建筑 → 屋顶淡出；离开 → 恢复半透明
            roof.AddComponent<RoofFade>().Setup(center, size);
        }

        /// <summary>发光窗：贴外墙面的暖黄小方块（自发光，夜间教室灯光）。俯视 60° 从上方斜看可见。</summary>
        static void AddWindows(string name, Vector3 center, Vector2 size, float wallH, float floorTop, Material windowMat, Transform parent = null)
        {
            float x = size.x, z = size.y;
            const float wallThick = 0.4f;
            float y = floorTop + 1.9f;
            // 东/西墙（长边）：窗沿墙面（z 方向）排布，薄厚朝墙法线（x）
            CreateWindow(name + "_Win_E1", center + new Vector3(x * 0.5f + wallThick * 0.5f, y, -z * 0.25f), new Vector3(0.06f, 0.9f, 0.62f), windowMat, parent);
            CreateWindow(name + "_Win_E2", center + new Vector3(x * 0.5f + wallThick * 0.5f, y, z * 0.25f), new Vector3(0.06f, 0.9f, 0.62f), windowMat, parent);
            CreateWindow(name + "_Win_W1", center + new Vector3(-x * 0.5f - wallThick * 0.5f, y, -z * 0.25f), new Vector3(0.06f, 0.9f, 0.62f), windowMat, parent);
            CreateWindow(name + "_Win_W2", center + new Vector3(-x * 0.5f - wallThick * 0.5f, y, z * 0.25f), new Vector3(0.06f, 0.9f, 0.62f), windowMat, parent);
            // 南墙（前门面，避开中间门洞）
            CreateWindow(name + "_Win_S1", center + new Vector3(-x * 0.25f, y, z * 0.5f + wallThick * 0.5f), new Vector3(0.62f, 0.9f, 0.06f), windowMat, parent);
            CreateWindow(name + "_Win_S2", center + new Vector3(x * 0.25f, y, z * 0.5f + wallThick * 0.5f), new Vector3(0.62f, 0.9f, 0.06f), windowMat, parent);
        }

        static void CreateWindow(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent = null)
        {
            GameObject go = CreateCube(name, pos, scale, mat);
            go.layer = 2;   // 不参与 NavMesh 烘焙
            if (parent != null) go.transform.SetParent(parent, true);   // [0.4.3] 挂建筑根
        }

        /// <summary>创建道具的低多边形视觉部件。部件无碰撞，只随根物体移动/旋转/漂浮。</summary>
        public static GameObject CreatePropPart(Transform parent, string name, PrimitiveType primitive,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.layer = 2; // Ignore Raycast：不参与 NavMesh 烘焙，也不抢拾取碰撞
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = mat;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return go;
        }

        /// <summary>时间碎片：俯视可读的怀表盘，外圈、刻度和指针比抽象球体更直接传达“时间”。</summary>
        public static void CreateClockFragmentVisual(Transform parent, Material faceMat, Material rimMat, Material handMat)
        {
            CreatePropPart(parent, "Clock_Bezel", PrimitiveType.Cylinder,
                new Vector3(0f, 0.04f, 0f), new Vector3(1.52f, 0.10f, 1.52f), rimMat);
            CreatePropPart(parent, "Clock_Face", PrimitiveType.Cylinder,
                new Vector3(0f, 0.15f, 0f), new Vector3(1.30f, 0.07f, 1.30f), faceMat);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f * Mathf.Deg2Rad;
                float radius = 0.48f;
                GameObject tick = CreatePropPart(parent, "Clock_Tick_" + i, PrimitiveType.Cube,
                    new Vector3(Mathf.Sin(angle) * radius, 0.25f, Mathf.Cos(angle) * radius),
                    new Vector3(i % 3 == 0 ? 0.07f : 0.04f, 0.035f, i % 3 == 0 ? 0.15f : 0.09f), handMat);
                tick.transform.localRotation = Quaternion.Euler(0f, -i * 30f, 0f);
            }
            GameObject minuteHand = CreatePropPart(parent, "Clock_MinuteHand", PrimitiveType.Cube,
                new Vector3(0.13f, 0.28f, 0.12f), new Vector3(0.055f, 0.035f, 0.60f), handMat);
            minuteHand.transform.localRotation = Quaternion.Euler(0f, 42f, 0f);
            GameObject hourHand = CreatePropPart(parent, "Clock_HourHand", PrimitiveType.Cube,
                new Vector3(-0.10f, 0.30f, 0.06f), new Vector3(0.065f, 0.04f, 0.36f), handMat);
            hourHand.transform.localRotation = Quaternion.Euler(0f, -66f, 0f);
            CreatePropPart(parent, "Clock_Crown", PrimitiveType.Cylinder,
                new Vector3(0f, 0.10f, 0.82f), new Vector3(0.22f, 0.08f, 0.22f), rimMat);
        }

        /// <summary>时间沙漏：两个相向的收腰六角杯和顶部/底部框，不再是三根抽象圆柱。</summary>
        public static void CreateHourglassVisual(Transform parent, Material frameMat, Material sandMat)
        {
            Renderer rootRenderer = parent.GetComponent<Renderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;
            StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Hourglass_Upper", new Vector3(0f, 0.30f, 0f),
                new Vector2(0.24f, 0.24f), new Vector2(0.68f, 0.68f), 0.52f, 6, sandMat);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Hourglass_Lower", new Vector3(0f, -0.30f, 0f),
                new Vector2(0.68f, 0.68f), new Vector2(0.24f, 0.24f), 0.52f, 6, sandMat);
            CreatePropPart(parent, "Hourglass_FrameTop", PrimitiveType.Cylinder,
                new Vector3(0f, 0.62f, 0f), new Vector3(0.76f, 0.07f, 0.76f), frameMat);
            CreatePropPart(parent, "Hourglass_FrameBottom", PrimitiveType.Cylinder,
                new Vector3(0f, -0.62f, 0f), new Vector3(0.76f, 0.07f, 0.76f), frameMat);
            CreatePropPart(parent, "Hourglass_SandStream", PrimitiveType.Cylinder,
                new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.38f, 0.07f), sandMat);
        }

        /// <summary>隐身道具：可辨认的青色药瓶，瓶身、瓶颈和瓶盖明确区分于时间碎片与沙漏。</summary>
        public static void CreateInvisibilityVialVisual(Transform parent, Material glassMat, Material liquidMat, Material capMat)
        {
            CreatePropPart(parent, "Vial_Body", PrimitiveType.Sphere,
                new Vector3(0f, -0.04f, 0f), new Vector3(0.92f, 0.92f, 0.92f), glassMat);
            CreatePropPart(parent, "Vial_Liquid", PrimitiveType.Sphere,
                new Vector3(0f, -0.10f, 0f), new Vector3(0.62f, 0.48f, 0.62f), liquidMat);
            CreatePropPart(parent, "Vial_Neck", PrimitiveType.Cylinder,
                new Vector3(0f, 0.58f, 0f), new Vector3(0.34f, 0.32f, 0.34f), glassMat);
            CreatePropPart(parent, "Vial_Cap", PrimitiveType.Cylinder,
                new Vector3(0f, 0.91f, 0f), new Vector3(0.42f, 0.12f, 0.42f), capMat);
        }

        /// <summary>灯油：暖光油罐，有明显罐身、盖子和提把轮廓。</summary>
        public static void CreateTorchOilVisual(Transform parent, Material oilMat, Material metalMat, Material accentMat)
        {
            Renderer rootRenderer = parent.GetComponent<Renderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;
            CreatePropPart(parent, "Oil_Canister", PrimitiveType.Cylinder,
                new Vector3(0f, 0f, 0f), new Vector3(0.86f, 0.92f, 0.86f), oilMat);
            CreatePropPart(parent, "Oil_Cap", PrimitiveType.Cylinder,
                new Vector3(0f, 0.55f, 0f), new Vector3(0.48f, 0.16f, 0.48f), metalMat);
            CreatePropPart(parent, "Oil_Band", PrimitiveType.Cylinder,
                new Vector3(0f, 0.12f, 0f), new Vector3(0.94f, 0.06f, 0.94f), accentMat);
            CreatePropPart(parent, "Oil_Handle_Left", PrimitiveType.Cube,
                new Vector3(-0.30f, 0.75f, 0f), new Vector3(0.08f, 0.38f, 0.10f), metalMat);
            CreatePropPart(parent, "Oil_Handle_Right", PrimitiveType.Cube,
                new Vector3(0.30f, 0.75f, 0f), new Vector3(0.08f, 0.38f, 0.10f), metalMat);
            CreatePropPart(parent, "Oil_Handle_Top", PrimitiveType.Cube,
                new Vector3(0f, 0.92f, 0f), new Vector3(0.66f, 0.08f, 0.10f), metalMat);
        }

        /// <summary>加速饮料：细长瓶身、瓶盖、标签和斜向速度条，避免与药瓶或灯油混淆。</summary>
        public static void CreateSpeedDrinkVisual(Transform parent, Material drinkMat, Material capMat, Material labelMat)
        {
            Renderer rootRenderer = parent.GetComponent<Renderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;
            CreatePropPart(parent, "Drink_Bottle", PrimitiveType.Capsule,
                new Vector3(0f, 0f, 0f), new Vector3(0.82f, 1.22f, 0.82f), drinkMat);
            CreatePropPart(parent, "Drink_Cap", PrimitiveType.Cylinder,
                new Vector3(0f, 0.68f, 0f), new Vector3(0.44f, 0.14f, 0.44f), capMat);
            CreatePropPart(parent, "Drink_Label", PrimitiveType.Cube,
                new Vector3(0f, -0.05f, -0.43f), new Vector3(0.62f, 0.34f, 0.06f), labelMat);
            GameObject speedStripe = CreatePropPart(parent, "Drink_SpeedStripe", PrimitiveType.Cube,
                new Vector3(0f, -0.03f, -0.47f), new Vector3(0.10f, 0.48f, 0.07f), drinkMat);
            speedStripe.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);
        }

        /// <summary>建筑基座：石板薄片铺在建筑四周一圈，让建筑"落"在地面上而不是悬空立方。</summary>
        static void AddBuildingPlaza(string name, Vector3 center, Vector2 size, Material plazaMat, Transform parent = null)
        {
            GameObject plaza = CreateCube(name + "_Plaza",
                center + new Vector3(0, 0.006f, 0),
                new Vector3(size.x + 2f, 0.05f, size.y + 2f), plazaMat);
            plaza.layer = 2;
            if (parent != null) plaza.transform.SetParent(parent, true);   // [0.4.3] 挂建筑根
        }

        public static Material MakeMaterial(Color c, string name, bool emissive = false, Color? emissiveColor = null, bool transparent = false)   // [0.8.0] public：ParkingLotBuilder 复用
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(sh) { name = name };
            mat.color = c;
            // TUNIC 式低模基线：大面保持哑光，用颜色块和光照塑造形体，不依赖写实高光。
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.20f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (emissive)
            {
                // 自发光：物体颜色不受场景光照影响，恒定发光——夜间月光下也能一眼看清安全屋
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissiveColor ?? c * 1.5f);
            }
            if (transparent) MakeTransparent(mat);
            return mat;
        }

        const string ArtReferenceDirectory = "Assets/ArtReference";
        const string MaterialDirectory = "Assets/Materials";

        /// <summary>Art Bible 的表面贴图一律是 Default、Repeat、Bilinear，而不是 UI Sprite。</summary>
        static void ConfigureArtReferenceImports()
        {
            string[] textureNames = {
                "TEX_Grass_Night", "TEX_StonePath_Night", "TEX_Wall_BlueGray",
                "TEX_Wood_Dark", "TEX_MetalGrid", "TEX_Roof_BlueGray"
            };
            foreach (string textureName in textureNames)
            {
                string path = ArtReferenceDirectory + "/" + textureName + ".png";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool changed = importer.textureType != TextureImporterType.Default ||
                    importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Bilinear;
                if (!changed) continue;
                importer.textureType = TextureImporterType.Default;
                importer.spriteImportMode = SpriteImportMode.None;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        /// <summary>优先加载正式贴图；旧程序贴图只作为资产尚未同步时的回退。</summary>
        static Texture2D LoadArtTexture(string artTextureName, string fallbackName,
            ProceduralTextureLibrary.Pattern fallbackPattern, Color baseColor, Color detailColor)
        {
            Texture2D artTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ArtReferenceDirectory + "/" + artTextureName + ".png");
            return artTexture != null
                ? artTexture
                : ProceduralTextureLibrary.GetOrCreate(fallbackName, fallbackPattern, baseColor, detailColor);
        }

        /// <summary>把核心六种材质存为项目资产；重建场景时更新同名资产，不产生重复文件。</summary>
        static Material PersistArtMaterial(Material material)
        {
            if (material == null) return null;
            string path = MaterialDirectory + "/" + material.name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
            EditorUtility.CopySerialized(material, existing);
            Object.DestroyImmediate(material);
            return existing;
        }

        /// <summary>为 URP Lit 的 Base Map 设置共享平铺贴图，Standard 回退也保持兼容。</summary>
        public static void ApplySurfaceTexture(Material material, Texture2D texture, Vector2 tiling)
        {
            if (material == null || texture == null) return;
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", tiling);
            }
        }

        /// <summary>URP Lit 切到 Transparent surface：半透明屋顶（俯视可看进建筑内部）/ 路灯地面光晕。</summary>
        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
        }

        /// <summary>创建巡夜者（Scout/Runner/Tracker/Guardian 共用）：capsule 视觉 + 碰撞/抓人触发 + NavMeshAgent + 巡逻点 + PatrolController。
        /// 参数差异实现 GAME_DESIGN 巡夜者类型——同一套状态机，靠速度/视野/听力/IsTracker/IsGuardian 区分行为。
        /// Guardian 守卫者：单点驻守 + 360° 圈感知，不画三角视野锥，改放独立金色警戒圈（被引走时圈留原地提示"危险区空了"）。</summary>
        public static GameObject CreatePatroller(string goName, Vector3 spawnPos, Vector3[] patrolPos, Material mat,   // [0.8.0] public：ParkingLotBuilder 复用
            float patrolSpeed, float chaseSpeed, float visionRange, float visionAngle, float hearingRange,
            bool isTracker, bool tall, bool wide, Transform player,
            float detectRate = 2.5f, Color? coneColor = null,
            bool isGuardian = false, float guardRadius = 12f, Material ringMat = null)
        {
            GameObject go = new GameObject(goName);
            go.layer = 2;   // Ignore Raycast：动态物体，不参与 NavMesh 烘焙
            go.transform.position = spawnPos;   // 空地上，出生即进巡逻路径

            // 画面 v1：胶囊 → 方块小人。体型区分巡夜者：Runner 瘦高（tall）/ Tracker 宽胖（wide）/ Scout 标准。
            // 头/身/腿随移动摆动（SimpleWalker）；身体方块做变色反馈（追击变红）。
            Renderer bodyRenderer = CreateCharacterBody(go.transform, mat, tall: tall, wide: wide, visualKind: goName);

            // 物理碰撞体：solid capsule 做阻挡（玩家撞上触发 PlayerController.OnControllerColliderHit 被抓；
            // NavMeshAgent 也需要 Collider 才能正确避障）。此前 Body 的 Collider 被删且根无 Collider，
            // 导致玩家直接穿过巡夜者、抓人完全失效。
            CapsuleCollider body = go.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0, 1f, 0);
            body.height = 2f;
            body.radius = 0.4f;

            // 抓人触发器：覆盖「巡夜者主动撞上静止玩家」的方向（PatrolController.OnTriggerEnter 检测）
            SphereCollider catchTrigger = go.AddComponent<SphereCollider>();
            catchTrigger.center = new Vector3(0, 1f, 0);
            catchTrigger.radius = 0.6f;
            catchTrigger.isTrigger = true;

            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
            agent.speed = patrolSpeed;
            agent.stoppingDistance = 0.1f;   // 追到尽量贴近，配合距离抓捕

            Transform[] points = new Transform[patrolPos.Length];
            for (int i = 0; i < patrolPos.Length; i++)
            {
                var pt = new GameObject($"{goName}_PatrolPoint_{i + 1}");
                pt.transform.position = patrolPos[i];
                points[i] = pt.transform;
            }

            PatrolController ctrl = go.AddComponent<PatrolController>();
            ctrl.Player = player;
            ctrl.PatrolPoints = points;
            ctrl.IsTracker = isTracker;
            ctrl.IsGuardian = isGuardian;
            ctrl.GuardRadius = guardRadius;
            // [0.4.5] 图鉴守卫类型：按名字前缀解析（goName 是 builder 逐字写死的字面量，唯一权威）
            ctrl.Kind = goName.StartsWith("Patrol_Tracker_") ? GuardType.Tracker
                : goName.StartsWith("Patrol_Guardian_") ? GuardType.Guardian
                : goName.StartsWith("Patrol_Runner_") ? GuardType.Runner
                : GuardType.Scout;
            ctrl.BodyRenderer = bodyRenderer;   // 追击变色反馈（身体方块变色，头/腿保持原色）
            ctrl.PatrolSpeed = patrolSpeed;
            ctrl.ChaseSpeed = chaseSpeed;
            ctrl.VisionRange = visionRange;
            ctrl.VisionAngle = visionAngle;
            ctrl.HearingRange = hearingRange;
            ctrl.DetectRate = detectRate;

            if (isGuardian)
            {
                // 金色警戒圈：独立于守卫的常亮地面标识（不 parent、不挂 FogHide）——守卫被引走追人时
                // 圈留在原地，玩家一眼看出"危险区空出来了，快偷"；守卫脱战回岗站在圈上（360° 圈感知语义）。
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = goName + "_GuardRing";
                ring.layer = 2;   // 装饰层，不参与 NavMesh
                ring.transform.position = spawnPos + Vector3.up * 0.03f;
                ring.transform.localScale = new Vector3(guardRadius * 2f, 0.02f, guardRadius * 2f);
                Collider ringCol = ring.GetComponent<Collider>();
                if (ringCol != null) Object.DestroyImmediate(ringCol);
                if (ringMat != null) ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            }
            else if (coneColor.HasValue)
            {
                // 视野锥可视化：贴地半透明三角（长度=视野距离/张角=视野角度），一眼看出巡夜者技能差异
                VisionCone cone = go.AddComponent<VisionCone>();
                cone.Init(visionRange, visionAngle, coneColor.Value);
            }

            // 守卫隐身（手电筒光圈）：玩家光圈外完全隐藏，靠近才现形（用户反馈：守夜者不该自带发光）
            go.AddComponent<FogHide>();

            // [0.4.3] 守卫场景存 inactive：LayoutRandomizer 运行时随机化+重烘焙后统一激活。
            // 否则 Start 的 agent.Warp 按旧巡逻点出生，且随机化时守卫可能穿楼卡死。
            go.SetActive(false);
            return go;
        }

        public static GameObject CreateCube(string name, Vector3 pos, Vector3 scale, Material mat)   // [0.8.0] public：ParkingLotBuilder 复用
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ---------- 画面 v1：方块小人 / 夜景 ----------

        /// <summary>方块小人（低多边形人物）：头 + 身体 + 双腿，腿随移动摆动（SimpleWalker）。
        /// 体型差异实现巡夜者特征：tall=瘦高(Runner) / wide=宽胖(Tracker)；player=true 时加暖橙书包。
        /// 返回身体 Renderer（变色反馈：追击变红只改身体方块，头/腿保持原色）。</summary>
        public static Renderer CreateCharacterBody(Transform parent, Material bodyMat, bool tall = false, bool wide = false,
            bool player = false, string visualKind = "")   // [0.8.0] public：ParkingLotBuilder 复用
        {
            if (player)
                return CreateOriginalBlockPlayer(parent, bodyMat);

            Vector3 bodyScale = tall ? new Vector3(0.30f, 0.68f, 0.22f)
                          : wide ? new Vector3(0.56f, 0.50f, 0.34f)
                                 : new Vector3(0.42f, 0.55f, 0.26f);

            // 身体（主材质，变色反馈；走路时上下起伏，SimpleWalker.Body）
            GameObject body = StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Body", new Vector3(0f, 0.80f, 0f),
                new Vector2(bodyScale.x, bodyScale.z), new Vector2(bodyScale.x * 0.76f, bodyScale.z * 0.82f), bodyScale.y, 4, bodyMat);
            Renderer r = body.GetComponent<Renderer>();

            // 头：六边形收腰，而不是纯方块；从俯视与第一人称都能读出角色方向。
            float headS = tall ? 0.24f : wide ? 0.34f : 0.30f;
            GameObject head = StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Head",
                new Vector3(0f, 0.80f + (bodyScale.y + headS) * 0.5f, 0.015f),
                new Vector2(headS, headS), new Vector2(headS * 0.74f, headS * 0.74f), headS, 6, bodyMat);

            // 四肢 pivot 结构：pivot 在髋/肩关节（SimpleWalker 摆 pivot = 前后摆腿/摆臂），肢体 cube 向下延伸
            float legGap = wide ? 0.20f : 0.09f;
            float legWidth = wide ? 0.20f : 0.15f;
            Transform leftLegPivot = CreateLeg(parent, "LeftLeg", -legGap, legWidth, bodyMat);
            Transform rightLegPivot = CreateLeg(parent, "RightLeg", legGap, legWidth, bodyMat);

            float armLen = tall ? 0.50f : 0.42f;
            float armW = wide ? 0.13f : 0.10f;
            float bodyHalfW = bodyScale.x * 0.5f;
            Transform leftElbow, rightElbow;
            Transform leftArmPivot = CreateArm(parent, "LeftArm", -(bodyHalfW + 0.03f), armW, armLen, bodyMat, out leftElbow);
            Transform rightArmPivot = CreateArm(parent, "RightArm", bodyHalfW + 0.03f, armW, armLen, bodyMat, out rightElbow);

            SimpleWalker walker = parent.GetComponent<SimpleWalker>();
            if (walker == null) walker = parent.gameObject.AddComponent<SimpleWalker>();
            walker.LeftLegPivot = leftLegPivot;
            walker.RightLegPivot = rightLegPivot;
            walker.LeftArmPivot = leftArmPivot;
            walker.RightArmPivot = rightArmPivot;
            walker.LeftElbowPivot = leftElbow;
            walker.RightElbowPivot = rightElbow;
            walker.Body = body.transform;

            // 角色装饰层：低多边形制服细节（无碰撞，仅用于俯视识别）。
            // 类型由 Builder 的守卫对象名显式传入，不依赖 Unity 材质运行时名称。
            Color accentColor = CharacterAccentColor(visualKind, player);
            Material accentMat = MakeMaterial(accentColor, bodyMat.name + "_Accent", emissive: player || visualKind.Contains("Guardian"),
                emissiveColor: player ? new Color(0.35f, 0.65f, 1.0f) : accentColor * 1.2f);
            AddCharacterUniformDetails(parent, bodyScale, headS, accentMat, player);

            // 玩家书包（深蓝，背后；俯视便于识别玩家方位）
            Renderer bagR = null;
            if (player)
            {
                Material bagMat = MakeMaterial(new Color(0.18f, 0.30f, 0.52f), "MAT_Bag");
                GameObject bag = StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Bag", new Vector3(0f, 0.80f, -0.22f),
                    new Vector2(0.28f, 0.15f), new Vector2(0.20f, 0.12f), 0.32f, 4, bagMat);
                bagR = bag.GetComponent<Renderer>();
            }

            // 基础型号：挂 CharacterVisual 注册所有部位（未来皮肤系统按部位名换材质，不动模型）
            CharacterVisual cv = parent.GetComponent<CharacterVisual>();
            if (cv == null) cv = parent.gameObject.AddComponent<CharacterVisual>();
            cv.RegisterPart("Body", r);
            cv.RegisterPart("Head", head.GetComponent<Renderer>());
            cv.RegisterPart("LeftLeg", leftLegPivot.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("RightLeg", rightLegPivot.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("LeftArm", leftArmPivot.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("RightArm", rightArmPivot.GetChild(0).GetComponent<Renderer>());
            if (bagR != null) cv.RegisterPart("Bag", bagR);

            return r;
        }

        /// <summary>
        /// 主角固定为项目最初的方块小人：Cube 头、身体、四肢和一个小书包。
        /// 后续美术只替换这套固定部件的材质/程序贴图，不替换轮廓或导入骨骼模型。
        /// </summary>
        static Renderer CreateOriginalBlockPlayer(Transform parent, Material bodyMat)
        {
            Vector3 bodyScale = new Vector3(0.42f, 0.55f, 0.26f);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, 0.80f, 0f);
            body.transform.localScale = bodyScale;
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = bodyMat;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(parent, false);
            head.transform.localPosition = new Vector3(0f, 1.225f, 0f);
            head.transform.localScale = Vector3.one * 0.30f;
            Object.DestroyImmediate(head.GetComponent<BoxCollider>());
            Renderer headRenderer = head.GetComponent<Renderer>();
            headRenderer.sharedMaterial = bodyMat;

            Transform leftLeg = CreateBlockLeg(parent, "LeftLeg", -0.09f, bodyMat);
            Transform rightLeg = CreateBlockLeg(parent, "RightLeg", 0.09f, bodyMat);
            Transform leftElbow;
            Transform rightElbow;
            Transform leftArm = CreateBlockArm(parent, "LeftArm", -0.24f, bodyMat, out leftElbow);
            Transform rightArm = CreateBlockArm(parent, "RightArm", 0.24f, bodyMat, out rightElbow);

            Material bagMat = MakeMaterial(new Color(0.85f, 0.55f, 0.20f), "MAT_Bag");
            GameObject bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bag.name = "Bag";
            bag.transform.SetParent(parent, false);
            bag.transform.localPosition = new Vector3(0f, 0.80f, -0.22f);
            bag.transform.localScale = new Vector3(0.26f, 0.30f, 0.12f);
            Object.DestroyImmediate(bag.GetComponent<BoxCollider>());
            Renderer bagRenderer = bag.GetComponent<Renderer>();
            bagRenderer.sharedMaterial = bagMat;

            SimpleWalker walker = parent.GetComponent<SimpleWalker>();
            if (walker == null) walker = parent.gameObject.AddComponent<SimpleWalker>();
            walker.LeftLegPivot = leftLeg;
            walker.RightLegPivot = rightLeg;
            walker.LeftArmPivot = leftArm;
            walker.RightArmPivot = rightArm;
            walker.LeftElbowPivot = leftElbow;
            walker.RightElbowPivot = rightElbow;
            walker.Body = body.transform;
            walker.UseRestPose = false;
            walker.CaptureRestPose();

            CharacterVisual cv = parent.GetComponent<CharacterVisual>();
            if (cv == null) cv = parent.gameObject.AddComponent<CharacterVisual>();
            cv.RegisterPart("Body", bodyRenderer);
            cv.RegisterPart("Head", headRenderer);
            cv.RegisterPart("LeftLeg", leftLeg.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("RightLeg", rightLeg.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("LeftArm", leftArm.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("RightArm", rightArm.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("Bag", bagRenderer);
            return bodyRenderer;
        }

        static Transform CreateBlockLeg(Transform parent, string name, float xOffset, Material material)
        {
            GameObject pivot = new GameObject(name + "_Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(xOffset, 0.52f, 0f);

            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = name;
            leg.transform.SetParent(pivot.transform, false);
            leg.transform.localPosition = new Vector3(0f, -0.19f, 0f);
            leg.transform.localScale = new Vector3(0.15f, 0.38f, 0.20f);
            Object.DestroyImmediate(leg.GetComponent<BoxCollider>());
            leg.GetComponent<Renderer>().sharedMaterial = material;
            return pivot.transform;
        }

        static Transform CreateBlockArm(Transform parent, string name, float xOffset, Material material, out Transform elbowPivot)
        {
            GameObject pivot = new GameObject(name + "_Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(xOffset, 1.05f, 0f);

            GameObject upper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            upper.name = name + "_Upper";
            upper.transform.SetParent(pivot.transform, false);
            upper.transform.localPosition = new Vector3(0f, -0.11f, 0f);
            upper.transform.localScale = new Vector3(0.10f, 0.22f, 0.10f);
            Object.DestroyImmediate(upper.GetComponent<BoxCollider>());
            upper.GetComponent<Renderer>().sharedMaterial = material;

            GameObject elbow = new GameObject(name + "_Elbow");
            elbow.transform.SetParent(pivot.transform, false);
            elbow.transform.localPosition = new Vector3(0f, -0.22f, 0f);

            GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            forearm.name = name + "_Forearm";
            forearm.transform.SetParent(elbow.transform, false);
            forearm.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            forearm.transform.localScale = new Vector3(0.085f, 0.20f, 0.085f);
            Object.DestroyImmediate(forearm.GetComponent<BoxCollider>());
            forearm.GetComponent<Renderer>().sharedMaterial = material;

            GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hand.name = name + "_Hand";
            hand.transform.SetParent(elbow.transform, false);
            hand.transform.localPosition = new Vector3(0f, -0.20f, 0f);
            hand.transform.localScale = Vector3.one * 0.15f;
            Object.DestroyImmediate(hand.GetComponent<BoxCollider>());
            hand.GetComponent<Renderer>().sharedMaterial = material;
            elbowPivot = elbow.transform;
            return pivot.transform;
        }

        /// <summary>
        /// 主角专用外观：参考已确认的大学生概念图，用大块低模折面表达夹克、书包和发型。
        /// 它保持 CharacterVisual 的七部位与 SimpleWalker 的关节接线，因此不触碰移动、隐身或换皮肤机制。
        /// </summary>
        static Renderer CreatePlayerCharacterBody(Transform parent)
        {
            const string modelPath = "Assets/Art/Characters/QuaterniusUltimateModularMen/Selected/Casual.fbx";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                Debug.LogError("[Before8AM] 玩家角色模型未导入: " + modelPath);
                return CreateProceduralPlayerFallback(parent);
            }

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            model.name = "Player_Quaternius_Casual";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            DisableImportedModelHelpers(model);
            FitPlayerModelToController(model, parent);

            Material skinMat = MakeMaterial(new Color(0.70f, 0.48f, 0.38f), "MAT_Player_Skin");
            Material jacketMat = MakeMaterial(new Color(0.055f, 0.118f, 0.275f), "MAT_Player_Jacket");
            Material pantsMat = MakeMaterial(new Color(0.095f, 0.115f, 0.165f), "MAT_Player_Trousers");
            Material shoeMat = MakeMaterial(new Color(0.15f, 0.18f, 0.25f), "MAT_Player_Sneakers");
            Material soleMat = MakeMaterial(new Color(0.52f, 0.57f, 0.65f), "MAT_Player_Soles");
            Material hairMat = MakeMaterial(new Color(0.025f, 0.030f, 0.070f), "MAT_Player_Hair");
            Material eyeMat = MakeMaterial(new Color(0.11f, 0.075f, 0.060f), "MAT_Player_Eyes");
            ApplyPlayerModelPalette(model, skinMat, jacketMat, pantsMat, shoeMat, soleMat, hairMat, eyeMat);

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = GetPlayerAnimatorController();
            HumanoidAnimationDriver animationDriver = parent.GetComponent<HumanoidAnimationDriver>();
            if (animationDriver == null) animationDriver = parent.gameObject.AddComponent<HumanoidAnimationDriver>();
            animationDriver.Animator = animator;

            Renderer bodyRenderer = FindPlayerRenderer(model, "Casual_Body");
            CharacterVisual cv = parent.GetComponent<CharacterVisual>();
            if (cv == null) cv = parent.gameObject.AddComponent<CharacterVisual>();
            cv.RegisterPart("Body", bodyRenderer);
            cv.RegisterPart("Head", FindPlayerRenderer(model, "Casual_Head"));
            cv.RegisterPart("Legs", FindPlayerRenderer(model, "Casual_Legs"));
            cv.RegisterPart("Feet", FindPlayerRenderer(model, "Casual_Feet"));
            return bodyRenderer;
        }

        /// <summary>
        /// FBX 的单位和导出缩放不可假定。按实际渲染边界将角色校准为 CharacterController 的可见人高，
        /// 并使最低点严格落在玩家根节点的地面上，避免出现导入后数十米高或悬空的角色。
        /// </summary>
        static void FitPlayerModelToController(GameObject model, Transform playerRoot)
        {
            // 2.5D 镜头下主角应与原灰盒小方人同量级，而非填满 2m CharacterController。
            const float targetVisibleHeight = 1.28f;
            if (!TryGetVisibleModelBounds(model, out Bounds bounds) || bounds.size.y < 0.0001f)
            {
                Debug.LogWarning("[Before8AM] 无法读取玩家模型边界，跳过自动定标。");
                return;
            }

            float scale = targetVisibleHeight / bounds.size.y;
            model.transform.localScale *= scale;
            if (!TryGetVisibleModelBounds(model, out bounds)) return;

            Vector3 worldPosition = model.transform.position;
            worldPosition.y -= bounds.min.y - playerRoot.position.y;
            model.transform.position = worldPosition;
            Debug.Log("[Before8AM] 玩家模型已按边界定标: " + bounds.size.y.ToString("F2") + "m");
        }

        static bool TryGetVisibleModelBounds(GameObject model, out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer.gameObject.name == "Cube") continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return hasBounds;
        }

        static void DisableImportedModelHelpers(GameObject model)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                if (renderer.gameObject.name == "Cube") renderer.enabled = false;
            foreach (Light light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (UnityEngine.Camera camera in model.GetComponentsInChildren<UnityEngine.Camera>(true)) camera.enabled = false;
        }

        static void ApplyPlayerModelPalette(GameObject model, Material skinMat, Material jacketMat, Material pantsMat,
            Material shoeMat, Material soleMat, Material hairMat, Material eyeMat)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (renderer.gameObject.name == "Casual_Body" && materials.Length >= 2)
                {
                    materials[0] = skinMat;
                    materials[1] = jacketMat;
                }
                else if (renderer.gameObject.name == "Casual_Legs" && materials.Length >= 2)
                {
                    materials[0] = skinMat;
                    materials[1] = pantsMat;
                }
                else if (renderer.gameObject.name == "Casual_Feet" && materials.Length >= 2)
                {
                    materials[0] = shoeMat;
                    materials[1] = soleMat;
                }
                else if (renderer.gameObject.name == "Casual_Head" && materials.Length >= 4)
                {
                    materials[0] = skinMat;
                    materials[1] = hairMat;
                    materials[2] = eyeMat;
                    materials[3] = hairMat;
                }
                renderer.sharedMaterials = materials;
            }
        }

        static Renderer FindPlayerRenderer(GameObject model, string name)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                if (renderer.gameObject.name == name) return renderer;
            return null;
        }

        static Transform FindChildTransform(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildTransform(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static RuntimeAnimatorController GetPlayerAnimatorController()
        {
            const string controllerPath = "Assets/Art/Characters/QuaterniusUltimateModularMen/PlayerHumanoid.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller != null) return controller;

            const string animationPath = "Assets/Art/Characters/QuaterniusUltimateModularMen/Source/Animations/Animations.fbx";
            AnimationClip idle = FindAnimationClip(animationPath, "Idle");
            AnimationClip walk = FindAnimationClip(animationPath, "Walk");
            if (idle == null || walk == null)
            {
                Debug.LogError("[Before8AM] 未找到 Quaternius Idle/Walk 动画，请等待 Animations.fbx 导入完成后重建场景。");
                return null;
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState("Idle");
            idleState.motion = idle;
            AnimatorState walkState = stateMachine.AddState("Walk");
            walkState.motion = walk;
            stateMachine.defaultState = idleState;

            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.08f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.12f, "Speed");
            AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.10f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.12f, "Speed");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static AnimationClip FindAnimationClip(string assetPath, string stateName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip == null) continue;
                if (clip.name == stateName || clip.name.EndsWith("|" + stateName)) return clip;
            }
            return null;
        }

        static Renderer CreateProceduralPlayerFallback(Transform parent)
        {
            Material jacketMat = MakeMaterial(new Color(0.055f, 0.118f, 0.275f), "MAT_Player_Jacket");
            Material shirtMat = MakeMaterial(new Color(0.60f, 0.65f, 0.73f), "MAT_Player_Shirt");
            Material pantsMat = MakeMaterial(new Color(0.095f, 0.115f, 0.165f), "MAT_Player_Trousers");
            Material skinMat = MakeMaterial(new Color(0.70f, 0.48f, 0.38f), "MAT_Player_Skin");
            Material hairMat = MakeMaterial(new Color(0.025f, 0.030f, 0.070f), "MAT_Player_Hair");
            Material shoeMat = MakeMaterial(new Color(0.15f, 0.18f, 0.25f), "MAT_Player_Sneakers");
            Material bagMat = MakeMaterial(new Color(0.34f, 0.39f, 0.48f), "MAT_Player_Backpack");
            Material accentMat = MakeMaterial(new Color(0.95f, 0.52f, 0.20f), "MAT_Player_Zipper", emissive: true,
                emissiveColor: new Color(0.55f, 0.20f, 0.06f));

            // 夹克的肩宽和收腰让俯视剪影先读到"人"，而不是一块竖直方柱。
            GameObject body = StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Body", new Vector3(0f, 0.84f, 0f),
                new Vector2(0.48f, 0.30f), new Vector2(0.40f, 0.26f), 0.62f, 6, jacketMat);
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Player_ShirtFront", new Vector3(0f, 0.82f, 0.165f),
                new Vector2(0.24f, 0.045f), new Vector2(0.20f, 0.045f), 0.40f, 4, shirtMat);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Player_Collar", new Vector3(0f, 1.18f, 0.035f),
                new Vector2(0.34f, 0.25f), new Vector2(0.25f, 0.19f), 0.16f, 6, jacketMat);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Player_ZipperTab", new Vector3(0.13f, 0.80f, 0.192f),
                new Vector2(0.055f, 0.03f), new Vector2(0.045f, 0.03f), 0.12f, 4, accentMat);

            // 头与五块大发片：保留短碎发的辨识，不引入 AI 图中移动端不可见的密集发丝。
            GameObject head = StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Head", new Vector3(0f, 1.34f, 0.02f),
                new Vector2(0.31f, 0.29f), new Vector2(0.25f, 0.23f), 0.32f, 7, skinMat);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Hair_Cap", new Vector3(0f, 1.54f, 0.005f),
                new Vector2(0.34f, 0.31f), new Vector2(0.20f, 0.18f), 0.16f, 7, hairMat);
            CreatePlayerHairLock(parent, "Hair_Front_C", new Vector3(0f, 1.48f, 0.155f), 0.075f, 0.15f, hairMat);
            CreatePlayerHairLock(parent, "Hair_Front_L", new Vector3(-0.11f, 1.47f, 0.125f), 0.070f, 0.13f, hairMat);
            CreatePlayerHairLock(parent, "Hair_Front_R", new Vector3(0.11f, 1.47f, 0.125f), 0.070f, 0.13f, hairMat);
            CreatePlayerHairLock(parent, "Hair_Side_L", new Vector3(-0.17f, 1.42f, 0.02f), 0.060f, 0.15f, hairMat);
            CreatePlayerHairLock(parent, "Hair_Side_R", new Vector3(0.17f, 1.42f, 0.02f), 0.060f, 0.14f, hairMat);

            Transform leftLeg = CreatePlayerLeg(parent, "LeftLeg", -0.14f, pantsMat, shoeMat);
            Transform rightLeg = CreatePlayerLeg(parent, "RightLeg", 0.14f, pantsMat, shoeMat);
            Transform leftElbow;
            Transform rightElbow;
            Transform leftArm = CreatePlayerArm(parent, "LeftArm", -0.29f, jacketMat, skinMat, out leftElbow);
            Transform rightArm = CreatePlayerArm(parent, "RightArm", 0.29f, jacketMat, skinMat, out rightElbow);

            // 背包与两条肩带是俯视镜头的永久识别点；橙色只留给功能性拉链头。
            GameObject bag = StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "Bag", new Vector3(0f, 0.91f, -0.22f),
                new Vector2(0.34f, 0.17f), new Vector2(0.25f, 0.13f), 0.44f, 6, bagMat);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "BagStrap_L", new Vector3(-0.19f, 1.00f, 0.07f),
                new Vector2(0.045f, 0.035f), new Vector2(0.040f, 0.030f), 0.44f, 4, bagMat);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, "BagStrap_R", new Vector3(0.19f, 1.00f, 0.07f),
                new Vector2(0.045f, 0.035f), new Vector2(0.040f, 0.030f), 0.44f, 4, bagMat);

            SimpleWalker walker = parent.GetComponent<SimpleWalker>();
            if (walker == null) walker = parent.gameObject.AddComponent<SimpleWalker>();
            walker.LeftLegPivot = leftLeg;
            walker.RightLegPivot = rightLeg;
            walker.LeftArmPivot = leftArm;
            walker.RightArmPivot = rightArm;
            walker.LeftElbowPivot = leftElbow;
            walker.RightElbowPivot = rightElbow;
            walker.Body = body.transform;

            CharacterVisual cv = parent.GetComponent<CharacterVisual>();
            if (cv == null) cv = parent.gameObject.AddComponent<CharacterVisual>();
            cv.RegisterPart("Body", bodyRenderer);
            cv.RegisterPart("Head", head.GetComponent<Renderer>());
            cv.RegisterPart("LeftLeg", leftLeg.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("RightLeg", rightLeg.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("LeftArm", leftArm.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("RightArm", rightArm.GetChild(0).GetComponent<Renderer>());
            cv.RegisterPart("Bag", bag.GetComponent<Renderer>());
            return bodyRenderer;
        }

        static void CreatePlayerHairLock(Transform parent, string name, Vector3 localPosition, float width, float height, Material mat)
        {
            StylizedLowPolyFactory.CreateLocalTaperedPrism(parent, name, localPosition,
                new Vector2(width, 0.045f), new Vector2(width * 0.55f, 0.030f), height, 4, mat);
        }

        static Transform CreatePlayerLeg(Transform parent, string name, float xOff, Material pantsMat, Material shoeMat)
        {
            GameObject pivot = new GameObject(name + "_Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(xOff, 0.52f, 0f);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(pivot.transform, name, new Vector3(0f, -0.20f, 0f),
                new Vector2(0.19f, 0.20f), new Vector2(0.15f, 0.16f), 0.40f, 5, pantsMat);
            GameObject shoe = StylizedLowPolyFactory.CreateLocalTaperedPrism(pivot.transform, name + "_Shoe", new Vector3(0f, -0.43f, 0.07f),
                new Vector2(0.20f, 0.15f), new Vector2(0.16f, 0.11f), 0.16f, 5, shoeMat);
            shoe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return pivot.transform;
        }

        static Transform CreatePlayerArm(Transform parent, string name, float xOff, Material jacketMat, Material skinMat,
            out Transform elbowPivot)
        {
            GameObject pivot = new GameObject(name + "_Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(xOff, 1.06f, 0f);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(pivot.transform, name + "_Jacket", new Vector3(0f, -0.12f, 0f),
                new Vector2(0.14f, 0.14f), new Vector2(0.105f, 0.105f), 0.25f, 5, jacketMat);
            GameObject elbow = new GameObject(name + "_Elbow");
            elbow.transform.SetParent(pivot.transform, false);
            elbow.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(elbow.transform, name + "_Sleeve", new Vector3(0f, -0.10f, 0f),
                new Vector2(0.105f, 0.105f), new Vector2(0.075f, 0.075f), 0.20f, 5, jacketMat);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(elbow.transform, name + "_Hand", new Vector3(0f, -0.24f, 0.01f),
                new Vector2(0.095f, 0.080f), new Vector2(0.070f, 0.060f), 0.14f, 5, skinMat);
            elbowPivot = elbow.transform;
            return pivot.transform;
        }

        static Color CharacterAccentColor(string visualKind, bool player)
        {
            if (player) return new Color(0.72f, 0.84f, 1.00f);       // 玩家浅蓝校牌/肩带
            if (visualKind.Contains("Guardian")) return new Color(1.00f, 0.86f, 0.42f);
            if (visualKind.Contains("Runner")) return new Color(0.95f, 0.42f, 0.36f);
            if (visualKind.Contains("Tracker")) return new Color(0.72f, 0.42f, 0.88f);
            return new Color(0.32f, 0.45f, 0.70f);                  // Scout 冷蓝灰识别条
        }

        /// <summary>角色制服视觉细节：领口、胸前识别条、校牌/面罩和肩部色块。</summary>
        static void AddCharacterUniformDetails(Transform parent, Vector3 bodyScale, float headS, Material accentMat, bool player)
        {
            float bodyTop = 0.80f + bodyScale.y * 0.5f;
            float frontZ = bodyScale.z * 0.5f + 0.025f;
            float bodyHalfW = bodyScale.x * 0.5f;

            // 领口：薄条压在身体顶部，俯视能读出制服结构。
            CreateCharacterVisualCube(parent, "Uniform_Collar", new Vector3(0f, bodyTop - 0.06f, 0f),
                new Vector3(bodyScale.x * 0.72f, 0.08f, bodyScale.z * 1.05f), accentMat);

            // 胸前识别条：玩家为浅蓝，守卫为类型色；不使用文字，避免字体/视角依赖。
            CreateCharacterVisualCube(parent, "Uniform_ChestStripe", new Vector3(0f, 0.84f, frontZ),
                new Vector3(bodyScale.x * 0.62f, 0.09f, 0.035f), accentMat);

            // 校牌/监控面罩：小矩形，强化角色朝向但不改变模型结构。
            float badgeY = 0.98f + (bodyScale.y - 0.50f) * 0.18f;
            CreateCharacterVisualCube(parent, player ? "Student_Badge" : "Patrol_Visor", new Vector3(0f, badgeY, frontZ + 0.012f),
                new Vector3(player ? 0.11f : 0.16f, 0.10f, 0.035f), accentMat);

            // 肩部小色块：只加在高/宽角色的外轮廓上，避免所有小人变成同一 silhouette。
            float shoulderY = 1.05f;
            float shoulderX = bodyHalfW + 0.035f;
            CreateCharacterVisualCube(parent, "Uniform_Shoulder_L", new Vector3(-shoulderX, shoulderY, 0f),
                new Vector3(0.07f, 0.10f, bodyScale.z * 0.78f), accentMat);
            CreateCharacterVisualCube(parent, "Uniform_Shoulder_R", new Vector3(shoulderX, shoulderY, 0f),
                new Vector3(0.07f, 0.10f, bodyScale.z * 0.78f), accentMat);
        }

        static GameObject CreateCharacterVisualCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            return go;
        }

        /// <summary>腿：pivot 在髋关节（摆动点），腿 cube 向下延伸。返回 pivot（SimpleWalker 摆它，模拟前后摆腿）。</summary>
        static Transform CreateLeg(Transform parent, string name, float xOff, float width, Material mat)
        {
            GameObject pivot = new GameObject(name + "_Pivot");
            pivot.transform.SetParent(parent);
            pivot.transform.localPosition = new Vector3(xOff, 0.52f, 0);   // 髋关节高度

            StylizedLowPolyFactory.CreateLocalTaperedPrism(pivot.transform, name, new Vector3(0f, -0.19f, 0f),
                new Vector2(width, 0.20f), new Vector2(width * 0.72f, 0.16f), 0.38f, 4, mat);
            return pivot.transform;
        }

        /// <summary>
        /// 手臂（两节 + 手）：肩 pivot（SimpleWalker 摆它，与腿反相）→ 上臂 → 肘 pivot（前臂折叠）→ 前臂 → 手。
        /// 俯视 60° 下单节细手臂摆动看不清（用户反馈"手臂不摆动"），两节+手让摆臂明显可读。
        /// 返回肩 pivot；肘 pivot 经 out 传回（SimpleWalker 摆肘）。</summary>
        static Transform CreateArm(Transform parent, string name, float xOff, float width, float len, Material mat, out Transform elbowPivot)
        {
            GameObject pivot = new GameObject(name + "_Pivot");
            pivot.transform.SetParent(parent);
            pivot.transform.localPosition = new Vector3(xOff, 1.05f, 0);   // 肩关节高度

            // 上臂（肩→肘）
            StylizedLowPolyFactory.CreateLocalTaperedPrism(pivot.transform, name + "_Upper", new Vector3(0f, -0.11f, 0f),
                new Vector2(width, width), new Vector2(width * 0.76f, width * 0.76f), 0.22f, 4, mat);

            // 肘 pivot（前臂折叠点）
            GameObject elbow = new GameObject(name + "_Elbow");
            elbow.transform.SetParent(pivot.transform);
            elbow.transform.localPosition = new Vector3(0, -0.22f, 0);   // 上臂底端

            // 前臂（略细）
            float foreW = width * 0.85f;
            StylizedLowPolyFactory.CreateLocalTaperedPrism(elbow.transform, name + "_Forearm", new Vector3(0f, -0.10f, 0f),
                new Vector2(foreW, foreW), new Vector2(foreW * 0.76f, foreW * 0.76f), 0.20f, 4, mat);

            // 手（小方块：摆臂时末端移动，俯视一眼可读）
            float handS = Mathf.Max(0.13f, width * 1.5f);
            StylizedLowPolyFactory.CreateLocalTaperedPrism(elbow.transform, name + "_Hand", new Vector3(0f, -0.20f, 0f),
                new Vector2(handS, handS), new Vector2(handS * 0.70f, handS * 0.70f), handS, 5, mat);

            elbowPivot = elbow.transform;
            return pivot.transform;
        }

        static void CreateRoad(Material roadMat)
        {
            // 石板路井字路网（层2 贴地装饰，不参与烘焙）：纵路×2（晨门主大道 + 西侧沿馆路）+ 横路×2（中庭服务路 + 北区服务路）。
            // 每条路坐标都逐一绕开 7 栋建筑（校园从 40×36 扩到 56×48 后，路网也要成体系，否则只有一条路找不着北）。
            // 四条路 y 递增（0.005/0.009/0.013）避免交叉处 Z 冲突。
            CreateRoadStrip("Road_NS_Main", new Vector3(0f, 0.005f, -3f), new Vector3(3f, 0.05f, 74f), roadMat);   // [0.3.0] 晨门(z-40)→南区(z34) 中央主大道
            CreateRoadStrip("Road_EW_Main", new Vector3(0f, 0.009f, -2f), new Vector3(88f, 0.05f, 3f), roadMat);   // 中庭横穿
            CreateRoadStrip("Road_EW_North", new Vector3(0f, 0.013f, -28f), new Vector3(84f, 0.05f, 3f), roadMat); // 北区横穿
            CreateRoadStrip("Road_EW_South", new Vector3(0f, 0.017f, 28f), new Vector3(84f, 0.05f, 3f), roadMat);  // 南区横穿
        }

        static void CreateRoadStrip(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            StylizedLowPolyFactory.CreateTaperedPrism(name, pos,
                new Vector2(scale.x, scale.z), new Vector2(scale.x * 0.96f, scale.z * 0.96f), scale.y, 4, mat);
        }

        static void CreateLamp(Vector3 pos, Material poleMat, Material headMat, Material glowMat)
        {
            // 灯柱（层0：参与 NavMesh 烘焙成静态障碍，巡夜者绕行路灯）
            GameObject pole = CreateCube("Lamp_Pole", pos + Vector3.up * 1.5f, new Vector3(0.12f, 3f, 0.12f), poleMat);
            pole.name = $"Lamp_Pole_{pos.x:0}_{pos.z:0}";
            pole.GetComponent<Renderer>().enabled = false;
            // 灯头（层2：暖黄自发光）
            GameObject head = CreateCube("Lamp_Head", pos + Vector3.up * 3f, new Vector3(0.30f, 0.30f, 0.30f), headMat);
            head.name = $"Lamp_Head_{pos.x:0}_{pos.z:0}";
            head.layer = 2;
            head.GetComponent<Renderer>().enabled = false;
            StylizedLowPolyFactory.CreateLantern("Lamp_Stylized", pos, 3f, poleMat, headMat);
            // 只保留真实灯光，不绘制俯视可见的黄色地面盘，避免灯下像一块领地区域。
            // 微暖低饱和光只让附近的地面稍亮，不抢占迷雾和玩法标记的视觉层级。
            GameObject lightGo = new GameObject($"Lamp_Light_{pos.x:0}_{pos.z:0}");
            lightGo.transform.position = pos + Vector3.up * 3f;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.847f, 0.698f, 0.388f); // #D8B263
            light.intensity = 0.68f;
            light.range = 6.0f;
            light.shadows = LightShadows.None;
        }

        static void CreateBush(Vector3 pos, Material bushMat)
        {
            GameObject root = new GameObject("GrassClump");
            root.layer = 2;
            root.transform.position = pos;
            const int bladeCount = 7;
            for (int i = 0; i < bladeCount; i++)
            {
                float angle = i * Mathf.PI * 2f / bladeCount;
                float radius = i == 0 ? 0f : 0.22f;
                float height = 0.48f + (i % 3) * 0.10f;
                GameObject blade = StylizedLowPolyFactory.CreateLocalTaperedPrism(root.transform, "Blade_" + i,
                    new Vector3(Mathf.Cos(angle) * radius, height * 0.5f, Mathf.Sin(angle) * radius),
                    new Vector2(0.20f, 0.14f), new Vector2(0.025f, 0.018f), height, 4, bushMat);
                blade.transform.localRotation = Quaternion.Euler((i % 2 == 0 ? 1f : -1f) * 10f, i * 51f, 0f);
            }
        }

        static void CreateCampusTrees(Material trunkMat, Material foliageMat)
        {
            Vector3[] treePos = {
                new Vector3(-44f, 0f, -31f), new Vector3(-43f, 0f, 31f),
                new Vector3(43f, 0f, -31f), new Vector3(43f, 0f, 31f),
                new Vector3(-42f, 0f, -11f), new Vector3(42f, 0f, 22f),
                new Vector3(-14f, 0f, 35f), new Vector3(30f, 0f, 34f)
            };
            for (int i = 0; i < treePos.Length; i++)
                StylizedLowPolyFactory.CreateLowPolyTree("CampusTree_" + i, treePos[i], 2.55f, trunkMat, foliageMat);
        }

        /// <summary>晨门视觉层：柱脚、顶部压边和内侧光条，保留原门体的交互与状态变色。</summary>
        static void CreateExitGateDetails(Vector3 center, Material glowMat, Material stoneMat)
        {
            CreateVisualCube("Gate_Base_L", center + new Vector3(-1.6f, 0.18f, 0f), new Vector3(0.58f, 0.36f, 0.72f), stoneMat, null);
            CreateVisualCube("Gate_Base_R", center + new Vector3(1.6f, 0.18f, 0f), new Vector3(0.58f, 0.36f, 0.72f), stoneMat, null);
            CreateVisualCube("Gate_Crown", center + new Vector3(0f, 4.26f, 0f), new Vector3(4.15f, 0.22f, 0.46f), stoneMat, null);
            CreateVisualCube("Gate_Glow_L", center + new Vector3(-1.23f, 2.05f, -0.33f), new Vector3(0.08f, 2.75f, 0.05f), glowMat, null);
            CreateVisualCube("Gate_Glow_R", center + new Vector3(1.23f, 2.05f, -0.33f), new Vector3(0.08f, 2.75f, 0.05f), glowMat, null);
        }

        /// <summary>主路视觉地标：仅使用无碰撞低模部件，避免影响随机布局、路径和 NavMesh。</summary>
        static void CreateCampusPathProps(Material metalMat, Material stoneMat, Material plantMat)
        {
            Vector3[] benchPos = {
                new Vector3(-11f, 0f, -28f), new Vector3(11f, 0f, -2f), new Vector3(-12f, 0f, 28f)
            };
            for (int i = 0; i < benchPos.Length; i++)
            {
                Vector3 p = benchPos[i];
                CreateVisualCube("Bench_Seat_" + i, p + new Vector3(0f, 0.45f, 0f), new Vector3(1.5f, 0.12f, 0.42f), stoneMat, null);
                CreateVisualCube("Bench_Back_" + i, p + new Vector3(0f, 0.78f, 0.16f), new Vector3(1.5f, 0.42f, 0.10f), stoneMat, null);
                CreateVisualCube("Bench_Leg_L_" + i, p + new Vector3(-0.55f, 0.24f, 0f), new Vector3(0.10f, 0.48f, 0.30f), metalMat, null);
                CreateVisualCube("Bench_Leg_R_" + i, p + new Vector3(0.55f, 0.24f, 0f), new Vector3(0.10f, 0.48f, 0.30f), metalMat, null);
            }

            Vector3[] planterPos = {
                new Vector3(-6f, 0f, -2f), new Vector3(6f, 0f, -2f), new Vector3(-6f, 0f, 28f), new Vector3(6f, 0f, 28f)
            };
            for (int i = 0; i < planterPos.Length; i++)
            {
                Vector3 p = planterPos[i];
                CreateVisualCube("Planter_" + i, p + new Vector3(0f, 0.18f, 0f), new Vector3(0.78f, 0.36f, 0.78f), stoneMat, null);
                GameObject shrub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shrub.name = "Planter_Shrub_" + i;
                shrub.layer = 2;
                shrub.transform.position = p + new Vector3(0f, 0.62f, 0f);
                shrub.transform.localScale = new Vector3(0.58f, 0.72f, 0.58f);
                Object.DestroyImmediate(shrub.GetComponent<SphereCollider>());
                shrub.GetComponent<Renderer>().sharedMaterial = plantMat;
            }

            Vector3[] signPos = { new Vector3(-3.8f, 0f, -28f), new Vector3(3.8f, 0f, 28f) };
            for (int i = 0; i < signPos.Length; i++)
            {
                Vector3 p = signPos[i];
                CreateVisualCube("PathMarker_Pole_" + i, p + new Vector3(0f, 0.75f, 0f), new Vector3(0.10f, 1.5f, 0.10f), metalMat, null);
                CreateVisualCube("PathMarker_Plate_" + i, p + new Vector3(0f, 1.38f, 0f), new Vector3(0.72f, 0.32f, 0.08f), stoneMat, null);
            }
        }
    }
}
