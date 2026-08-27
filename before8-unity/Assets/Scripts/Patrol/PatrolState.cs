namespace Before8AM.Patrol
{
    /// <summary>
    /// 巡夜者状态机（规格书 31）。
    /// 察觉升级由 PatrolController.Suspicion（0~3）驱动：! / !! / !!! → Chase。
    /// </summary>
    public enum PatrolState
    {
        Patrol,      // 沿路径巡逻
        Suspicious,  // 察觉（!）
        Alert,       // 警觉（!!）
        Chase,       // 追踪（!!!）
        Search       // 丢失目标后搜索最后出现点
    }
}
