using UnityEngine;
using Before8AM.Run;
using Before8AM.Visual;

namespace Before8AM.Core
{
    /// <summary>
    /// [0.8.0] 无开场过场的关卡（午夜超市）：场景加载后自动开跑 + 激活探索迷雾。
    /// 校园关卡由 WindowIntro 过场驱动 StartRun，这里提供无 intro 的直接开跑入口。
    /// 守卫已在场景激活（固定布局无需随机化），RunManager.Awake 先于本组件 Start 注册 Instance。
    /// </summary>
    public class GameAutoStart : MonoBehaviour
    {
        void Start()
        {
            StartCoroutine(AutoRun());   // [审查] 延后一帧再开跑：StartRun 会同步触发 OnRunStarted，若 RandomEventSystem.Start 尚未订阅，定时随机事件整局不排程
        }

        System.Collections.IEnumerator AutoRun()
        {
            yield return null;   // 等所有组件的 Start 先跑完（含 RandomEventSystem 的 OnRunStarted 订阅）

            RunManager run = RunManager.Instance;
            if (run != null) run.StartRun();
            else Debug.LogWarning("[AutoStart] RunManager 未就绪，未开跑");

            var fog = Object.FindObjectOfType<ExplorationFog>();
            if (fog != null) fog.gameObject.SetActive(true);   // 校园由 WindowIntro 激活；这里直接开
        }
    }
}
