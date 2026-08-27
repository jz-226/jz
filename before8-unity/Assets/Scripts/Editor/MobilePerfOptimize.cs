using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Before8AM.EditorTools
{
    /// <summary>
    /// [0.9.2] 移动端性能一键优化：SerializedObject 改 URP asset 的序列化字段（部分属性只读，须走 serialized）。
    /// 真机卡顿主因（已排查）：
    ///   1) 手机原生分辨率（RenderScale=1）+ HDR 全开 → fill rate 压力大
    ///   2) 校园 13 盏路灯 + 宿舍灯全部逐像素（Per Pixel）附加光，每对象限 4 盏 → 移动端 GPU 重
    ///   3) 阴影距离 50 偏高
    /// 关闭 HDR 顺带解决「守卫发光材质过曝晃眼」：emissive intensity >1 会被 clamp 到 1，
    /// 守卫身体/金色警戒圈/道具材质不再爆亮。
    /// 幂等：重复点击无副作用（只降不升）。改完重新打包即生效，无需重新生成场景。
    /// </summary>
    public static class MobilePerfOptimize
    {
        [MenuItem("Tools/早八在逃/移动端性能优化")]
        static void Optimize()
        {
            const string path = "Assets/Settings/URP_Asset.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (urp == null)
            {
                EditorUtility.DisplayDialog("优化失败", $"找不到 URP asset：{path}", "OK");
                return;
            }

            var so = new SerializedObject(urp);
            var pHdr = so.FindProperty("m_SupportsHDR");
            var pScale = so.FindProperty("m_RenderScale");
            var pPerObj = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            var pDist = so.FindProperty("m_ShadowDistance");
            var pMainShadow = so.FindProperty("m_MainLightShadowmapResolution");
            if (pHdr == null || pScale == null || pPerObj == null || pDist == null || pMainShadow == null)
            {
                EditorUtility.DisplayDialog("优化失败", "URP asset 字段结构不符（Unity 版本不同？），请手动在 Project Settings 里调整", "OK");
                return;
            }

            bool oldHdr = pHdr.boolValue;
            float oldScale = pScale.floatValue;
            int oldPerObj = pPerObj.intValue;
            float oldDist = pDist.floatValue;

            pHdr.boolValue = false;                            // 无后处理 → 关 HDR（省带宽 + 去 emissive 过曝「晃眼」）
            pScale.floatValue = Mathf.Min(oldScale, 0.85f);    // 85% 渲染缩放（只降不升，幂等）
            pPerObj.intValue = 2;                              // 每对象逐像素灯 4→2（13 路灯场景移动端压力大）
            pDist.floatValue = Mathf.Min(oldDist, 25f);
            pMainShadow.intValue = 2;                          // 主光阴影 2048→1024（ShadowResolution._1024）
            so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 1;   // 2048→512

            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            UnityEngine.Debug.Log($"[PerfOptimize] HDR {oldHdr}→false, RenderScale {oldScale:0.00}→{pScale.floatValue:0.00}, " +
                      $"PerObjectLights {oldPerObj}→{pPerObj.intValue}, ShadowDist {oldDist:0}→{pDist.floatValue:0}（已优化；MSAA 本就是 1x 未动）");
            EditorUtility.DisplayDialog("移动端性能优化",
                "已应用：关 HDR、渲染 85%、每对象逐像素灯 4→2、阴影距离 25、阴影分辨率降低。\n\n重新打包 APK 后生效（无需改场景）。", "OK");
        }
    }
}
