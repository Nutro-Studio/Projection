namespace Core.Simulation
{
    public sealed class GameBalanceConfig
    {
        public static GameBalanceConfig Default { get; } = new();

        public int StartingGold { get; init; } = 10;
        public int UnitCost { get; init; } = 3;
        public int MissionaryCost { get; init; } = 3;
        public int TraderCost { get; init; } = 2;
        public int ChurchCost { get; init; } = 4;
        public int ChurchTithe { get; init; } = 1;
        public int RebellionCellCost { get; init; } = 2;
        public int ConversionLimit { get; init; } = 2;
        public int VictoryPointsPerProvince { get; init; } = 1;
        public int TradingPostCost { get; init; } = 4;
        public int StartingTradingPostCount { get; init; } = 2;
        public int CaravanBaseGainPerProvince { get; init; } = 2;
        public int CaravanTransitToll { get; init; } = 1;
        public int CaravanMinTraversedForVp { get; init; } = 3;
        public int HostileBuyoutBaseCost { get; init; } = 6;
        public int HostileBuyoutUnitPremium { get; init; } = 2;
        public int HostileBuyoutBuildingPremium { get; init; } = 2;
        public int DefaultMercenaryPrice { get; init; } = 4;
        public int FortCost { get; init; } = 5;
        public int BuildFortVp { get; init; } = 1;
        public int CaptureOrCrushRebellionVp { get; init; } = 2;
        public int BuildChurchVp { get; init; } = 2;
        public int DefendRebellionVp { get; init; } = 2;

        public int VictoryPointsThreshold { get; init; } = 50;
        public int CrisisProvinceThreshold { get; init; } = 3;
        public int AscensionProvinceThreshold { get; init; } = 5;
    }
}