using System.Collections.Generic;
using UnityEngine;
using Before8AM.Run;
using Before8AM.Reward;
using Before8AM.UI;   // [0.8.6] ScrollingNotice 增援播报
using Before8AM.Audio;   // [0.8.9] 增援警报音

namespace Before8AM.Patrol
{
    /// <summary>
    /// [0.8.6] 段位增援：本局按段位族（GameProgress.RankFamilyIndex）生成增援计划——随 8 分钟倒计时
    /// （RunManager.ElapsedTime）推进，到点激活预置的休眠守卫（Reserve，场景存 inactive）。
    /// 段位越高首增越早、总数越多（"更快更密"）。
    /// 激活瞬间 PatrolController.Awake+Start 重跑 → agent.Warp 落 LayoutRandomizer 已重排的巡逻点，直接进入巡逻。
    /// </summary>
    public class ReinforceDirector : MonoBehaviour
    {
        /// <summary>预置增援守卫（5 只，inactive；VerticalSliceBuilder 接线）。</summary>
        public PatrolController[] Reserve;

        /// <summary>[0.8.6] 滚动播报（增援时「新增巡夜者（类型）」；VerticalSliceBuilder 接线）。</summary>
        public ScrollingNotice Notice;

        /// <summary>本局增援触发时刻（ElapsedTime 秒，升序）。</summary>
        readonly List<float> triggers = new List<float>();
        int next;   // 下一个待触发索引

        void Start()
        {
            BuildPlan();
        }

        void Update()
        {
            var run = RunManager.Instance;
            if (run == null || run.State != RunState.Running) return;
            while (next < triggers.Count && run.ElapsedTime >= triggers[next])
            {
                if (next < Reserve.Length && Reserve[next] != null)
                {
                    Reserve[next].gameObject.SetActive(true);
                    if (Notice != null)
                        Notice.Show($"新增巡夜者（{KindName(Reserve[next].Kind)}）");
                    SFXManager.Instance.Play("threeTone1", 1f);   // [0.8.9] 增援警报（每波播一次）
                }
                next++;
            }
        }

        /// <summary>[0.8.6] 守卫类型名（与图鉴 IntroRules 同源：Scout/Runner/Tracker/Guardian）。</summary>
        static string KindName(GuardType k) => k switch
        {
            GuardType.Runner => "Runner",
            GuardType.Tracker => "Tracker",
            GuardType.Guardian => "Guardian",
            _ => "Scout",
        };

        /// <summary>[0.8.6] 段位族 → 增援计划 (count, firstAt, interval)：新生无增援；夜行者 1 波（60s）；
        /// 此后每族首增提前 5s、总数 +1（王者 5 波：35/105/175/245/315s，间隔 70s 均匀铺满 8 分钟）。</summary>
        (int count, float firstAt, float interval) PlanFor(int fam) => fam switch
        {
            0 => (0, 0f, 0f),
            1 => (1, 60f, 0f),
            2 => (2, 55f, 70f),
            3 => (3, 50f, 70f),
            4 => (3, 45f, 70f),
            5 => (4, 40f, 70f),
            _ => (5, 35f, 70f),   // 午夜王者
        };

        void BuildPlan()
        {
            triggers.Clear();
            next = 0;
            (int count, float firstAt, float interval) cfg = PlanFor(GameProgress.RankFamilyIndex);
            for (int i = 0; i < cfg.count; i++)
                triggers.Add(cfg.firstAt + i * cfg.interval);
        }

        /// <summary>下一波增援剩余秒数（HUD 用）；无增援计划或已全出返回 -1。</summary>
        public float NextReinforceIn
        {
            get
            {
                var run = RunManager.Instance;
                if (run == null || next >= triggers.Count) return -1f;
                return Mathf.Max(0f, triggers[next] - run.ElapsedTime);
            }
        }
    }
}
