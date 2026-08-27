using UnityEngine;
using Before8AM.Collection;

namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件·临时安全屋：一块发光庇护圆盘，站进区域内守卫抓不到（等同安全屋但限区域、不关门）。
    /// 数据驱动目录 EventCatalog.TempSafeHouse（World）。
    /// 全局静态 PlayerProtected 由 PatrolController.CanCatch 读取；进出区域自动开合。
    /// </summary>
    public class SafeZoneEvent : MonoBehaviour
    {
        /// <summary>玩家当前是否处于任一临时安全屋内（PatrolController.CanCatch 读取）。</summary>
        public static bool PlayerProtected;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            PlayerProtected = true;
            CollectionSystem.Unlock(CollectionEntry.TempSafeHouse);
            Debug.Log("[临时安全屋] 进入庇护区：守卫抓不到你（离开即失效）");
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            PlayerProtected = false;
            Debug.Log("[临时安全屋] 离开庇护区");
        }
    }
}
