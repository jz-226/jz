using UnityEngine;

namespace Before8AM.Core
{
    /// <summary>
    /// [0.9.2] 运行时启动项：真机锁定 60fps。
    /// 不锁时 Android 可能按屏幕刷新率跑但渲染跟不上反而抖动、或发热降频；锁 60 配合 vsync 节奏更稳。
    /// </summary>
    public static class RuntimeInit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Application.targetFrameRate = 60;
        }
    }
}
