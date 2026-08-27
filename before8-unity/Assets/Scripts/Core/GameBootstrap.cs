using UnityEngine;
using Before8AM.Run;
using Before8AM.Reward;

namespace Before8AM.Core
{
    /// <summary>
    /// 启动引导：进入 Play Mode 时清空服务注册表。
    /// RunManager / RewardSystem 等由场景显式放置（VerticalSliceBuilder 生成），
    /// 这样场景重载（按 R 重开）时它们会随场景自动重建。
    /// 注意：RuntimeInitializeOnLoadMethod 仅在首次进入 Play 时执行，不能用于场景级系统。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            GameServices.Clear();
        }
    }
}
