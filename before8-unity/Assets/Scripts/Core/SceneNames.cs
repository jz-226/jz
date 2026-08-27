namespace Before8AM.Core
{
    /// <summary>
    /// [0.5] 场景名常量：跨场景跳转一律用场景名而非 buildIndex（抗 EditorBuildSettings 重排）。
    /// buildIndex：0=主菜单，1=VS_MidnightCampus（由 MainMenuBuilder.ReorderBuildSettings 保证）。
    /// </summary>
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string Game = "VS_MidnightCampus";
        public const string Parking = "ParkingLot";   // [0.8.0] 午夜超市关卡（第二张地图，场景内部代号 ParkingLot 保留）
    }
}
