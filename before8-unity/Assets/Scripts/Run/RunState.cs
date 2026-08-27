namespace Before8AM.Run
{
    /// <summary>
    /// 单局状态（规格书 26/27：超时或被捕 = RUN FAILED，本局全部清空）。
    /// </summary>
    public enum RunState
    {
        Ready,    // 场景就绪，尚未开始
        Running,  // 倒计时进行中（翻窗后开始）
        Success,  // 已进入晨门撤离
        Caught,   // 被巡夜者抓捕
        Timeout   // 480 秒超时，未进晨门
    }
}
