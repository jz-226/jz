using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Before8AM.Collection;
using Before8AM.UI;

namespace Before8AM.EditorTools
{
    /// <summary>
    /// [0.5] 主菜单场景生成（照 VerticalSliceBuilder 范式）：空场景 + 深色相机 + MenuController（挂 MainMenuController + CollectionView）。
    /// 接到 BuildVerticalSlice 末尾——用户点一次 `Before8AM > 2. Build Vertical Slice Scene`
    /// 即重建游戏场景 + 主菜单 + 重排 build settings 为 [MainMenu, VS_MidnightCampus]（主菜单 buildIndex 0）。
    /// 独立 MenuItem 可单独重跑（幂等：NewScene 重建 + SaveScene 覆盖）。
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu/MainMenu.unity";

        [MenuItem("Before8AM/1.5 Build Main Menu Scene")]
        public static void BuildMainMenu()
        {
            // [0.5] Play 模式防护（同 BuildVerticalSlice）：NewScene 在 Play 中被 Unity 禁止。
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[构建] 请先退出 Play 模式，再重建主菜单场景（NewScene 在 Play 中被 Unity 禁止）。");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 主相机（深色 SolidColor 背景，菜单是纯 UI 无场景物体）
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();   // 全限定：Before8AM.Camera 命名空间与 UnityEngine.Camera 撞名（CS0118），同 VerticalSliceBuilder 写法
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.04f, 0.08f);
            camGo.AddComponent<AudioListener>();

            // 菜单控制器 + 图鉴/商店/设置面板（子面板各自 OnGUI visible 门控，主菜单切换）
            GameObject menuGo = new GameObject("MenuController");
            menuGo.AddComponent<MainMenuController>();
            menuGo.AddComponent<CollectionView>();
            menuGo.AddComponent<ShopController>();
            menuGo.AddComponent<SettingsController>();

            string dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ReorderBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[构建] 主菜单场景已生成：{ScenePath}（build settings = [MainMenu, VS_MidnightCampus]）");
        }

        /// <summary>build settings 重排为 [主菜单, 游戏场景]（主菜单 buildIndex 0，游戏场景 1）。
        /// [0.8.0] 停车场场景若已生成则追加（校园保持 buildIndex 1，停车场 2）。</summary>
        public static void ReorderBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(VerticalSliceBuilder.ScenePath, true),
            };
            if (File.Exists(ParkingLotBuilder.ScenePath))
                list.Add(new EditorBuildSettingsScene(ParkingLotBuilder.ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
