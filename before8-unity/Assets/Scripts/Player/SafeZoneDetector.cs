using UnityEngine;

namespace Before8AM.Player
{
    /// <summary>
    /// 建筑检测（挂在玩家上）：进入建筑内部触发器时记录状态。
    /// - SafeZone tag（教学楼）→ InSafeZone：安全屋，躲进去免疫抓捕、巡夜者立即放弃追击。
    /// - Building tag（食堂/宿舍）→ InBuilding：普通建筑掩体，躲进去只是拖延（巡夜者堵前门，
    ///   但建筑内感知被屏蔽 → 20s 脱战倒计时走完即离开），可等它走或从后门穿堂溜走。
    /// PatrolController 据此判定玩家的建筑状态。
    /// [0.4.4] 移除屏幕大字提示：规则已由 IntroRules 开场面板讲清，进建筑不再刷字（用户反馈）。
    /// </summary>
    public class SafeZoneDetector : MonoBehaviour
    {
        public bool InSafeZone { get; private set; }   // 安全屋（教学楼）
        public bool InBuilding { get; private set; }   // 普通建筑（食堂/宿舍）

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("SafeZone")) InSafeZone = true;
            else if (other.CompareTag("Building")) InBuilding = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("SafeZone")) InSafeZone = false;
            else if (other.CompareTag("Building")) InBuilding = false;
        }

        // [0.4.4] 屏幕大字提示已移除（安全屋/普通建筑）：规则由 IntroRules 开场面板讲清，
        // 进建筑再刷字重复打扰。以下只剩状态检测——守卫/建筑逻辑依赖 InSafeZone/InBuilding。
    }
}
