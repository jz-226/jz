using UnityEngine;
using UnityEngine.AI;
using Before8AM.Run;
using Before8AM.Player;
using Before8AM.Collection;
using Before8AM.Audio;   // [0.8.9] 警戒/追击/搜索状态音

namespace Before8AM.Patrol
{
    /// <summary>
    /// 巡夜者 AI：NavMeshAgent + 状态机（PATROL→SUSPICIOUS→ALERT→CHASE→SEARCH→PATROL）。
    /// 视野判定：距离 + 角度 + LineOfSight(Raycast)，墙体阻挡（规格书 32/101/103）。
    /// 支持 Scout / Runner / Tracker 参数差异；Tracker 一旦发现持续追踪且速度低于玩家。
    /// </summary>
    /// <summary>[0.4.5] 守卫类型（图鉴用；由 VerticalSliceBuilder 按名字前缀写入）。</summary>
    public enum GuardType { Scout, Runner, Tracker, Guardian }

    [RequireComponent(typeof(NavMeshAgent))]
    public class PatrolController : MonoBehaviour
    {
        [Header("行为参数")]
        [Tooltip("巡逻速度（玩家走路 4.5）")]
        public float PatrolSpeed = 3f;
        [Tooltip("追击速度：>玩家走路(4.5)、<玩家奔跑(7)；贴得紧但逃跑有效")]
        public float ChaseSpeed = 6.5f;
        public float VisionRange = 12f;
        public float VisionAngle = 70f;
        [Tooltip("听力范围：跑出视线但仍在此范围内会被持续追踪（跑步声）")]
        public float HearingRange = 8f;
        [Tooltip("追击时长：完全失去感知（看不见也听不见）后继续追这么久才放弃")]
        public float ChaseDuration = 20f;
        public float SearchDuration = 5f;
        [Tooltip("每秒察觉积累（2.5 ≈ 视野内 1.2 秒进入追击）")]
        public float DetectRate = 2.5f;
        public float LoseRate = 1.5f;    // 每秒察觉流失
        [Tooltip("Tracker：发现后持续追踪，绝不放弃")]
        public bool IsTracker;
        [Tooltip("Tracker：玩家拉开到此距离外并持续 ChaseDuration 后放弃追踪（GAME_DESIGN：速度低于玩家，可甩掉）")]
        public float TrackerGiveUpDistance = 25f;
        [Tooltip("Guardian 守卫者（GAME_DESIGN 第 4 种）：驻守固定点 + 360° 圈感知。被引走后追完回岗（引开偷碎片玩法）")]
        public bool IsGuardian;
        [Tooltip("Guardian 感知半径（圈）：圈内 360° 全向可见（相对 Scout 的锥形视野）")]
        public float GuardRadius = 12f;
        [Tooltip("[0.4.5] 守卫类型（图鉴解锁用，builder 按名字写入）")]
        public GuardType Kind = GuardType.Scout;
        [Tooltip("距玩家多近视为抓捕（规格书 27/28）。用距离而非物理碰撞，覆盖 NavMeshAgent 避障把 Scout 挡在玩家旁 0.5~1m 不接触的情况。1.0m≈模型实际接触（守卫身体 0.4m 宽），避免「没碰到就被杀」（用户反馈）；配合视线检测，隔墙不能抓")]
        public float CatchDistance = 1.0f;

        [Header("引用")]
        public Transform Player;
        public Transform[] PatrolPoints;
        [Tooltip("视觉反馈：Chase 时变红、Search 变橙，让玩家一眼看出状态")]
        public Renderer BodyRenderer;
        Color idleColor = new Color(0.02f, 0.02f, 0.04f);   // 巡逻基础色：Start 记录创建时材质色（Scout 深黑 / Runner 暗红 / Tracker 暗紫）

        public PatrolState State { get; private set; } = PatrolState.Patrol;
        public float Suspicion { get; private set; }   // 0~3，对应 !/!!/!!!

        const float SUSPICIOUS_THRESHOLD = 1f;   // !
        const float ALERT_THRESHOLD = 2f;        // !!
        const float CHASE_THRESHOLD = 3f;        // !!!

        NavMeshAgent agent;
        int patrolIndex;
        float stateTimer;
        Vector3 searchPosition;
        Vector3 lastSeenPosition;   // 追击中最后看到/听到玩家的位置

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (Player == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) Player = go.transform;
            }
        }

        void Start()
        {
            agent.speed = PatrolSpeed;
            if (BodyRenderer != null) idleColor = BodyRenderer.material.color;   // 记录巡逻基础色
            if (PatrolPoints == null || PatrolPoints.Length == 0)
            {
                PatrolPoints = new Transform[0];
                return;
            }

            // 每把随机开局位置（用户反馈"巡夜者每次开头都刷在固定位置"）：
            // 从巡逻路线上随机挑一个点起步（巡逻区域不变、三条路线不重叠照旧），
            // 但每次开局的出生点不同。场景重载（按 R 重开）时 Start 重跑 → 每把都重新随机。
            patrolIndex = Random.Range(0, PatrolPoints.Length);
            agent.Warp(PatrolPoints[patrolIndex].position);
            lastSeenPosition = transform.position;
        }

        /// <summary>接触玩家：被抓（规格书 27/28）。配合场景生成器挂的 trigger collider 使用，
        /// 覆盖「Scout 主动撞上静止玩家」的方向（玩家侧 OnControllerColliderHit 只在自己移动时触发）。
        /// 统一走 CanCatch()：距离 + 视线通畅（隔墙不能抓，防止"没碰到就被杀"）。</summary>
        void OnTriggerEnter(Collider other)
        {
            // 玩家躲进建筑后免疫抓捕：安全屋（SafeZone）+ 普通建筑掩体（Building）都算，
            // 否则巡夜者堵前门时物理触碰会误抓（安全屋设定 + 普通建筑拖延机制）
            if (other.CompareTag("Player") && CanCatch() && !PlayerInSafeZone() && !PlayerInBuilding())
                RunManager.Instance?.Fail(RunState.Caught);
        }

        void Update()
        {
            RunManager run = RunManager.Instance;
            if (run == null || run.State != RunState.Running)
            {
                agent.isStopped = true;
                return;
            }
            agent.isStopped = false;

            // [0.8.0] 干扰器：全场守卫停摆（原地发呆，不巡逻不追不抓，察觉保留——到期恢复原状态）
            var jamPc = Player != null ? Player.GetComponent<PlayerController>() : null;
            if (jamPc != null && jamPc.JammerActive)
            {
                agent.isStopped = true;
                return;
            }

            // 近距离直接抓捕：距离判定比物理碰撞可靠（避障/碰撞事件可能不触发）。
            // 玩家躲在建筑内免疫抓捕：安全屋 + 普通建筑掩体都算（否则隔门洞/隔 0.4m 墙的直线距离
            // < CatchDistance 会隔空抓死楼内玩家——critical 漏洞）。
            // 统一走 CanCatch()：距离 + 视线通畅（隔墙不能抓，防止"没碰到就被杀"）
            if (Player != null && !PlayerInSafeZone() && !PlayerInBuilding() && CanCatch())
            {
                RunManager.Instance?.Fail(RunState.Caught);
                return;
            }

            switch (State)
            {
                case PatrolState.Patrol: TickPatrol(); break;
                case PatrolState.Suspicious:
                case PatrolState.Alert: TickSuspicious(); break;
                case PatrolState.Chase: TickChase(); break;
                case PatrolState.Search: TickSearch(); break;
            }

            TickVision();
            UpdateBodyColor();
        }

        // ---------- 视觉反馈 ----------

        void UpdateBodyColor()
        {
            if (BodyRenderer == null) return;
            Color c;
            switch (State)
            {
                case PatrolState.Chase: c = new Color(0.9f, 0.12f, 0.1f); break;
                case PatrolState.Search: c = new Color(0.75f, 0.45f, 0.1f); break;
                case PatrolState.Suspicious:
                case PatrolState.Alert: c = new Color(0.55f, 0.32f, 0.08f); break;
                default: c = idleColor; break;   // Patrol：恢复各自巡逻色（不再硬编码深黑，否则 Runner/Tracker 原色被刷掉）
            }
            if (BodyRenderer.material.color != c)
                BodyRenderer.material.color = c;
        }

        // ---------- 视野与察觉 ----------

        void TickVision()
        {
            if (Player == null) return;

            // 玩家躲在建筑安全屋：巡夜者彻底失去目标（看不见/听不见），察觉快速清零，
            // 追击/搜索自然结束后回巡逻（安全屋设定的核心）。
            if (PlayerInSafeZone())
            {
                Suspicion = Mathf.Max(0f, Suspicion - LoseRate * Time.deltaTime * 2f);
                UpdateStateFromSuspicion();
                return;
            }

            // 普通建筑掩体：建筑内部屏蔽视线/听力（巡夜者感知不到楼内玩家），察觉正常流失。
            // 不立即放弃追击（区别于安全屋）——由 TickChase 的 20s 脱战倒计时自然结束，
            // 巡夜者堵前门一会儿就走（普通建筑=拖延不是绝对安全）。
            if (PlayerInBuilding())
            {
                Suspicion = Mathf.Max(0f, Suspicion - LoseRate * Time.deltaTime);
                UpdateStateFromSuspicion();
                return;
            }

            float dist = Vector3.Distance(transform.position, Player.position);
            // Guardian：用圈半径做距离判定 + 360° 全向（inAngle 恒 true）——区域感知，不是锥形视野
            bool inRange = dist <= (IsGuardian ? GuardRadius : VisionRange);
            bool inAngle = true;
            if (inRange && !IsGuardian)
            {
                Vector3 toPlayer = (Player.position - transform.position).normalized;
                inAngle = Vector3.Angle(transform.forward, toPlayer) <= VisionAngle * 0.5f;
            }
            bool hasLOS = false;
            if (inRange && inAngle)
            {
                Vector3 eye = transform.position + Vector3.up * 1.4f;
                Vector3 target = Player.position + Vector3.up * 1.0f;
                // 目标点在玩家自身 CharacterController 碰撞体内部，Linecast 必先撞到玩家自己，
                // 命中玩家自身 = 中间无其他障碍，仍视为可见（critical：此前 hasLOS 恒 false，Scout 永远看不见玩家）。
                hasLOS = !Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                         || hit.collider.transform == Player;
            }

            // [0.4.1] 隐身道具：完全隐身——守卫看不见也听不见（visible/heard 都被 IsInvisible 拦截）。
            // 以前保留奔跑声（设计成"不是无敌"），试玩反馈"隐身没用"：逃跑时守卫靠跑步声实时追，
            // 开了等于没开。现在彻底隐身：时长短(5s)+ 限量，靠窗口拉开距离，到期后被发现仍会挨抓——不是无敌。
            // [0.8.0] 假学生卡：守卫把玩家当成本校学生——看不见也听不见（等同完全脱离感知，但比隐身更稳：不依赖状态切换）
            bool hasFakeCard = PlayerHasFakeCard();
            bool visible = inRange && inAngle && hasLOS && !PlayerIsInvisible() && !hasFakeCard;
            bool heard = dist <= HearingRange && IsPlayerMoving() && !PlayerIsInvisible() && !hasFakeCard;

            if (visible)
            {
                // 看见即追（用户反馈"守夜者不抓我"：此前窄视野守卫察觉攒不满 CHASE_THRESHOLD 就不追）
                Suspicion = CHASE_THRESHOLD;
                stateTimer = 0f;
            }
            else if (heard)
            {
                // 听见玩家奔跑也会积累到追捕（从 0 到满约 1.2s 持续奔跑声），不再卡在警戒上限
                Suspicion = Mathf.Min(CHASE_THRESHOLD, Suspicion + DetectRate * Time.deltaTime);
            }
            else
            {
                Suspicion = Mathf.Max(0f, Suspicion - LoseRate * Time.deltaTime);
            }

            UpdateStateFromSuspicion();
        }

        /// <summary>
        /// [0.8.9] 玩家奔跑是否出声（听力判定）：跑（>5.5m/s，跑速 7/加速更快）出声，走（4.5m/s）无声——
        /// 潜行靠慢走即可，不再要求站定。视觉判定不受影响：被守卫视野照见照常被发现追捕。
        /// </summary>
        bool IsPlayerMoving()
        {
            var pc = Player != null ? Player.GetComponent<PlayerController>() : null;
            if (pc == null) return true;   // 拿不到玩家组件时保守视为在动
            return pc.CurrentSpeed > 5.5f;
        }

        /// <summary>玩家是否在隐身道具效果中（[0.3.0]：守卫看不见，但奔跑声仍可被听力感知——平衡）。</summary>
        bool PlayerIsInvisible()
        {
            var pc = Player != null ? Player.GetComponent<PlayerController>() : null;
            return pc != null && pc.IsInvisible;
        }

        /// <summary>[0.8.0] 假学生卡：玩家持卡期间守卫无视（不抓不追看不见）。</summary>
        bool PlayerHasFakeCard()
        {
            var pc = Player != null ? Player.GetComponent<PlayerController>() : null;
            return pc != null && pc.FakeCardActive;
        }

        /// <summary>玩家是否在建筑安全屋内（SafeZone trigger，SafeZoneDetector 检测）。</summary>
        bool PlayerInSafeZone()
        {
            if (Player == null) return false;
            var detector = Player.GetComponent<SafeZoneDetector>();
            return detector != null && detector.InSafeZone;
        }

        /// <summary>玩家是否在普通建筑掩体内（Building trigger，SafeZoneDetector 检测；不含安全屋教学楼）。
        /// 普通建筑=拖延不是绝对安全：躲进去免疫抓捕、感知被屏蔽（巡夜者脱战倒计时正常走），
        /// 巡夜者会堵前门直到脱战，玩家可等它走或从后门溜。</summary>
        bool PlayerInBuilding()
        {
            if (Player == null) return false;
            var detector = Player.GetComponent<SafeZoneDetector>();
            return detector != null && detector.InBuilding && !detector.InSafeZone;
        }

        void UpdateStateFromSuspicion()
        {
            if (Suspicion >= CHASE_THRESHOLD && State != PatrolState.Chase)
            {
                EnterState(PatrolState.Chase);
                return;
            }

            // 巡逻阶段逐级升级；警戒阶段逐级降级——否则察觉掉光后仍停在 Alert，
            // 变成永远面向玩家的"警戒木桩"，巡逻闭环断裂。
            switch (State)
            {
                case PatrolState.Patrol:
                    if (Suspicion >= ALERT_THRESHOLD) EnterState(PatrolState.Alert);
                    else if (Suspicion >= SUSPICIOUS_THRESHOLD) EnterState(PatrolState.Suspicious);
                    break;
                case PatrolState.Suspicious:
                    if (Suspicion >= ALERT_THRESHOLD) EnterState(PatrolState.Alert);
                    else if (Suspicion < SUSPICIOUS_THRESHOLD) EnterState(PatrolState.Patrol);
                    break;
                case PatrolState.Alert:
                    if (Suspicion < ALERT_THRESHOLD)
                        EnterState(Suspicion >= SUSPICIOUS_THRESHOLD ? PatrolState.Suspicious : PatrolState.Patrol);
                    break;
                // Chase/Search 由各自 Tick 计时驱动，不由察觉值降级，避免打断追击节奏
            }
        }

        void EnterState(PatrolState newState)
        {
            if (State == newState) return;
            State = newState;
            stateTimer = 0f;
            agent.speed = newState == PatrolState.Chase ? ChaseSpeed : PatrolSpeed;

            // [0.4.5] 图鉴：守卫进入 警觉/追击 = 被该守卫追捕 → 解锁对应类型。
            // 只在状态切换瞬间执行（上面 State==newState 早退 + Unlock 幂等），重复遭遇不重复计。
            // LureChest.AlertAt 引开的守卫直接进 Search，不经过 Alert/Chase —— 与"被追捕才解锁"决策一致。
            // [0.8.0] 同位置标记"本局被守卫发现"（每日任务"无被发现撤离"）；AlertAt 不经过这里，诱饵不误标。
            if (newState == PatrolState.Alert || newState == PatrolState.Chase)
            {
                CollectionSystem.Unlock(EntryForKind());
                Before8AM.Mission.MissionSystem.MarkDetected();
            }

            // [0.8.9] 状态警报音（潜行核心反馈）：警觉→追击升级时两音顺延各响一次 = 警报升级；
            // Search 只在追击脱战/诱饵引开时进，搜索音区别于警报
            switch (newState)
            {
                case PatrolState.Alert: SFXManager.Instance.Play("twoTone1", 0.9f); break;
                case PatrolState.Chase: SFXManager.Instance.Play("laser1", 0.9f); break;
                case PatrolState.Search: SFXManager.Instance.Play("question_001", 0.9f); break;
            }

            if (newState == PatrolState.Chase)
            {
                // 追人时少避让 + 贴到最近：守卫能真正接触玩家被抓。
                // NavMeshAgent 默认 HighQuality 避障会把守卫挡在玩家旁 0.5~1m 就停住，
                // 配合收紧后的 CatchDistance=1.0 会抓不到（"守卫又不抓我"回归）
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.stoppingDistance = 0.05f;
            }
            else
            {
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                agent.stoppingDistance = 0.1f;
            }

            if (newState == PatrolState.Search)
                searchPosition = lastSeenPosition;   // 去最后目击位置搜索，而非玩家实时位置
        }

        /// <summary>[0.4.5] 守卫类型 → 图鉴条目。映射放守卫侧，避免 Collection↔Patrol 双向依赖。</summary>
        CollectionEntry EntryForKind()
        {
            switch (Kind)
            {
                case GuardType.Runner: return CollectionEntry.Runner;
                case GuardType.Tracker: return CollectionEntry.Tracker;
                case GuardType.Guardian: return CollectionEntry.Guardian;
                default: return CollectionEntry.Scout;
            }
        }

        // ---------- 各状态 Tick ----------

        void TickPatrol()
        {
            if (PatrolPoints.Length == 0) return;

            // Guardian 守卫者：不巡游，驻守 PatrolPoints[0]（驻守点）。被引走追人后回岗——
            // 玩家趁机偷碎片的核心玩法（"危险区空出来了"由驻守点的金色警戒圈提示）。
            if (IsGuardian)
            {
                Vector3 post = PatrolPoints[0].position;
                if (Vector3.Distance(transform.position, post) > 0.5f)
                    agent.SetDestination(post);
                return;
            }

            if (agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                patrolIndex = (patrolIndex + 1) % PatrolPoints.Length;
                agent.SetDestination(PatrolPoints[patrolIndex].position);
            }
        }

        void TickSuspicious()
        {
            if (Player == null) return;
            Vector3 look = Player.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 6f * Time.deltaTime);
        }

        void TickChase()
        {
            if (Player == null) return;

            // 玩家躲进建筑安全屋：立即放弃追击，去最后目击位置搜索（安全屋设定）
            if (PlayerInSafeZone())
            {
                EnterState(PatrolState.Search);
                return;
            }

            // [0.4.4] 玩家躲进普通建筑掩体：感知被屏蔽——守卫当场失去目标，去最后目击点搜索。
            // 修复"堵门口 20s 出不去"：不无限堵门，Search 约 5s 即回巡逻（玩家等它走或从后门穿堂溜）。
            if (PlayerInBuilding())
            {
                lastSeenPosition = Player.position;
                EnterState(PatrolState.Search);
                return;
            }

            // [0.4.1] 隐身：守卫当场失去目标（看不见也听不见），去玩家消失点搜索。
            // 以前隐身时奔跑声仍在 → 守卫实时追玩家本人，药水"开了等于没开"（用户反馈"隐身没用"）。
            // 现在隐身=完全脱离：守卫搜 5s 找不到就回巡逻；药水到期后若还在附近仍会被重新发现（不是无敌）。
            if (PlayerIsInvisible())
            {
                lastSeenPosition = Player.position;   // 消失点 = 最后目击位置，守卫去那搜索
                EnterState(PatrolState.Search);
                return;
            }

            if (IsTracker)
            {
                // 持续追踪实时位置，绝不主动放弃（规格书：追踪者）。但速度低于玩家，
                // 玩家靠奔跑把距离拉到 TrackerGiveUpDistance 外并持续 ChaseDuration 后
                // 才算真正甩掉（否则永不放弃 + 纯速度追不上 = 被粘到死）。
                float dist = Vector3.Distance(transform.position, Player.position);
                if (dist > TrackerGiveUpDistance)
                {
                    stateTimer += Time.deltaTime;
                    if (stateTimer >= ChaseDuration)
                    {
                        lastSeenPosition = Player.position;   // 甩掉瞬间记录最后位置，去那附近搜索
                        EnterState(PatrolState.Search);
                    }
                }
                else
                {
                    agent.SetDestination(Player.position);
                    stateTimer = 0f;
                }
                return;
            }

            // 还能感知到玩家（看见或听见）→ 追实时位置，倒计时清零；
            // 完全失去感知（跑出视野 + 超出听力范围）→ 追"最后目击位置"并倒计时，到点去那里搜索。
            // （若失去感知仍追实时位置，躲墙/绕建筑也甩不掉，潜行反制失效。）
            // 修复：此前用 Suspicion < CHASE_THRESHOLD 判定放弃，玩家一跑出视线察觉值
            // 0.07s 内跌破 3，追击计时立即启动，导致"一逃走就不追"。
            bool canPerceive = CanPerceivePlayer();
            if (canPerceive)
            {
                lastSeenPosition = Player.position;
                stateTimer = 0f;
            }
            else
            {
                stateTimer += Time.deltaTime;
            }
            agent.SetDestination(canPerceive ? Player.position : lastSeenPosition);

            if (stateTimer >= ChaseDuration)
                EnterState(PatrolState.Search);
        }

        /// <summary>追击中是否还能感知到玩家（看见 或 听见），用于判定是否该放弃。</summary>
        bool CanPerceivePlayer()
        {
            if (Player == null) return false;
            if (PlayerHasFakeCard()) return false;   // [0.8.0] 假学生卡：追捕中直接视为失去目标
            if (PlayerInBuilding()) return false;   // 普通建筑内感知被屏蔽（楼内隐身），脱战倒计时正常走
            float dist = Vector3.Distance(transform.position, Player.position);
            if (dist <= HearingRange && IsPlayerMoving() && !PlayerIsInvisible()) return true;   // 听见（移动声）；[0.4.1] 隐身时奔跑也不出声
            if (PlayerIsInvisible()) return false;   // [0.3.0] 隐身道具：看不见（只能靠听力感知）
            if (dist > (IsGuardian ? GuardRadius : VisionRange)) return false;   // 超出视野距离（Guardian 用圈半径）
            Vector3 toPlayer = (Player.position - transform.position).normalized;
            if (!IsGuardian && Vector3.Angle(transform.forward, toPlayer) > VisionAngle * 0.5f) return false;  // 不在视野角内
            // 视线通畅：命中玩家自身碰撞体 = 中间无其他障碍，仍视为可见（同 TickVision 修复）
            return !Physics.Linecast(
                transform.position + Vector3.up * 1.4f,
                Player.position + Vector3.up * 1f,
                out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                || hit.collider.transform == Player;
        }

        /// <summary>能否当场抓捕：距离在 CatchDistance 内 + 视线通畅。
        /// **隔墙不能抓**（用户反馈"没碰到就被杀"：此前 1.4m 距离判定+光线穿墙，玩家看不见守卫就被杀；
        /// 现在必须真正接触且能看见）。命中玩家自身 / 守卫自身碰撞体不算遮挡。</summary>
        bool CanCatch()
        {
            if (Player == null) return false;
            if (PlayerIsInvisible()) return false;   // [0.4.1] 隐身中守卫贴身也不抓捕（药水窗口内完全安全）
            if (PlayerHasFakeCard()) return false;   // [0.8.0] 假学生卡：守卫把你当学生，贴身也不抓
            if (Before8AM.Events.SafeZoneEvent.PlayerProtected) return false;   // [0.8.0] 临时安全屋：区域内守卫抓不到
            if (Vector3.Distance(transform.position, Player.position) > CatchDistance) return false;
            Vector3 eye = transform.position + Vector3.up * 1.2f;
            Vector3 target = Player.position + Vector3.up * 0.9f;
            if (Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform != Player && !hit.collider.transform.IsChildOf(transform))
                    return false;   // 墙/障碍挡在中间 → 视线不通 → 不能隔墙抓
            }
            return true;
        }

        /// <summary>[0.4.4] 外部警觉入口：诱饵宝箱开箱报警 → 去 pos 点搜索（引开守卫玩法，与 Guardian 金圈同构）。
        /// 复用现有 Search 机制：记最后目击点 → 进 Search → 搜 SearchDuration 后回巡逻/回岗。
        /// 正在追击玩家的守卫不受影响（避免开箱反而把追着玩家的守卫瞬间拉走）。</summary>
        public void AlertAt(Vector3 pos)
        {
            RunManager run = RunManager.Instance;
            if (run == null || run.State != RunState.Running) return;
            if (State == PatrolState.Chase) return;   // 不打断已锁定玩家的守卫
            lastSeenPosition = pos;                   // EnterState(Search) 会把 searchPosition 指向这里
            searchPosition = pos;                     // 双保险：已在 Search 时 EnterState 早退仍重定向
            stateTimer = 0f;                          // 重搜新点：续 Search 计时
            Suspicion = ALERT_THRESHOLD;              // =2（!! 橙色）；Search 不被察觉值降级，稳定搜满 SearchDuration
            EnterState(PatrolState.Search);
        }

        void TickSearch()
        {
            agent.SetDestination(searchPosition);
            stateTimer += Time.deltaTime;
            if (stateTimer >= SearchDuration || (agent.remainingDistance <= agent.stoppingDistance + 0.3f && stateTimer > 2f))
            {
                Suspicion = 0f;
                EnterState(PatrolState.Patrol);
            }
        }
    }
}
