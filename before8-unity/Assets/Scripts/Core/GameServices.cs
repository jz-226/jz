using System.Collections.Generic;
using UnityEngine;

namespace Before8AM.Core
{
    /// <summary>
    /// 轻量服务定位器：由 GameBootstrap 在场景加载时注册，运行期按类型取用。
    /// 避免滥用全局单例（规格书 96：仅 GameManager/SaveManager/AudioManager/PlatformManager 用单例）。
    /// </summary>
    public static class GameServices
    {
        static readonly Dictionary<System.Type, object> services = new Dictionary<System.Type, object>();

        public static void Register<T>(T service) where T : class
        {
            services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out object s))
                return (T)s;
            Debug.LogError($"[GameServices] 未注册服务: {typeof(T).Name}");
            return null;
        }

        public static void Clear()
        {
            services.Clear();
        }
    }
}
