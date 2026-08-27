using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Before8AM.EditorTools
{
    /// <summary>
    /// [0.9.2] 一键打包 Android APK（上架 TapTap 用）。
    /// 编辑器内运行（不依赖命令行 batchmode，规避许可证限制）。
    /// 三步自动完成：
    ///   1) PlayerSettings 固化上架参数——包名 com.before8am.escape、版本 1.0.0、
    ///      架构 ARM64（TapTap 2023 起强制 64 位）、versionCode 1
    ///   2) 签名 keystore——Build/before8am.keystore 存在则复用，缺则用 Unity 内置
    ///      JDK 的 keytool 生成（RSA 2048 / 100 年有效），密码写 KEYSTORE_INFO.txt 防丢
    ///   3) BuildPipeline 出包 → Build/Before8AM_v1.0.0.apk
    /// [0.9.3] 输出文件名改 ASCII：Android 工具链（Gradle/IL2CPP）不接受非 ASCII 路径，
    ///   项目路径与输出路径都必须纯 ASCII（项目根 F:\Before8AM；F:\早八在逃 是 junction 别名，打包会报 Invalid project path）。
    /// 注意：包名即商店身份标识，上架后不可改。
    /// </summary>
    public static class BuildAPK
    {
        const string PACKAGE = "com.before8am.escape";
        const string VERSION = "1.0.0";
        const int VERSION_CODE = 1;
        const string KEYSTORE_REL = "Build/before8am.keystore";
        const string KEY_ALIAS = "before8am";
        // 公开源码不保存商店签名密码。请在本地私有配置中替换。
        const string KEY_PASS = "YOUR_LOCAL_KEYSTORE_PASSWORD";
        const string KEY_DNAME = "CN=before8am, OU=Dev, O=Before8AM, C=CN";

        [MenuItem("Tools/早八在逃/一键打包 Android APK")]
        static void Build()
        {
            // ---------- 1) 固化上架参数 ----------
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PACKAGE);
            PlayerSettings.bundleVersion = VERSION;
            PlayerSettings.Android.bundleVersionCode = VERSION_CODE;
            // 后端切 IL2CPP（原生编译，商店主流验收标准）：Mono + 架构设置的组合在这个 Unity 版本
            // 曾在 Build 时读到「Target architecture not specified」，IL2CPP + 纯 ARM64 是官方标准组合
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // [0.9.2] 固定横屏（玩家反馈真机竖屏）：LandscapeLeft 常见默认，手机转横即用
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            UnityEngine.Debug.Log($"[BuildAPK] package={PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)} " +
                      $"backend={PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)} " +
                      $"arch={PlayerSettings.Android.targetArchitectures}");

            // ---------- 2) 签名 keystore ----------
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            string ksPath = Path.Combine(root, KEYSTORE_REL).Replace('\\', '/');
            Directory.CreateDirectory(Path.GetDirectoryName(ksPath));
            if (!File.Exists(ksPath))
            {
                var keytool = Path.Combine(
                    Path.GetDirectoryName(EditorApplication.applicationPath),
                    "Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe");
                var psi = new ProcessStartInfo(keytool)
                {
                    Arguments = string.Format(
                        "-genkeypair -v -keystore \"{0}\" -alias {1} -keyalg RSA -keysize 2048 " +
                        "-validity 36500 -storepass {2} -keypass {2} -dname \"{3}\"",
                        ksPath, KEY_ALIAS, KEY_PASS, KEY_DNAME),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var proc = Process.Start(psi);
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    EditorUtility.DisplayDialog("打包失败", "keytool 生成签名失败，请查看 Console", "OK");
                    return;
                }
                File.WriteAllText(Path.Combine(root, "Build/KEYSTORE_INFO.txt"),
                    "早八在逃 Android 签名信息（务必保存，丢了无法更新商店包）\n" +
                    "keystore : " + ksPath + "\n" +
                    "alias    : " + KEY_ALIAS + "\n" +
                    "password : " + KEY_PASS + "\n" +
                    "有效期   : 100 年（RSA 2048）\n");
            }
            PlayerSettings.Android.useCustomKeystore = true;   // [关键] 不设此开关 Unity 会用默认 debug keystore 签名，签名 MD5 永远对不上
            PlayerSettings.Android.keystoreName = ksPath;
            PlayerSettings.Android.keyaliasName = KEY_ALIAS;
            PlayerSettings.Android.keystorePass = KEY_PASS;
            PlayerSettings.Android.keyaliasPass = KEY_PASS;

            // ---------- 3) 出包（场景按 Build Settings 顺序：主菜单 → 校园 → 停车场） ----------
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("打包失败", "Build Settings 里没有启用任何场景", "OK");
                return;
            }
            string apk = Path.Combine(root, "Build", $"Before8AM_v{VERSION}.apk");   // [0.9.3] ASCII 文件名（Android 工具链拒非 ASCII）
            var report = BuildPipeline.BuildPlayer(scenes, apk, BuildTarget.Android, BuildOptions.None);

            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.DisplayDialog("打包完成",
                    $"APK 已生成：\n{apk}\n\n大小 {report.summary.totalSize / 1024f / 1024f:0.0} MB", "OK");
            else
                EditorUtility.DisplayDialog("打包失败", "请查看 Console 窗口的报错信息", "OK");
        }
    }
}
