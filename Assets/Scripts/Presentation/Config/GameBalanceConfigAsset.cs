using Core.Simulation;
using UnityEngine;

namespace Presentation.Config
{
    [CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "Config/Game Balance Config")]
    public sealed class GameBalanceConfigAsset : ScriptableObject
    {
        [Header("Economy & Costs")]
        public int startingGold = 10;
        public int unitCost = 3;
        public int missionaryCost = 3;
        public int traderCost = 2;
        public int churchCost = 4;
        public int churchTithe = 1;
        public int rebellionCellCost = 2;
        public int tradingPostCost = 4;
        public int fortCost = 5;
        public int defaultMercenaryPrice = 4;

        [Header("Caravans & Buyouts")]
        public int startingTradingPostCount = 2;
        public int caravanBaseGainPerProvince = 2;
        public int caravanTransitToll = 1;
        public int caravanMinTraversedForVp = 3;
        public int hostileBuyoutBaseCost = 6;
        public int hostileBuyoutUnitPremium = 2;
        public int hostileBuyoutBuildingPremium = 2;

        [Header("Victory Points & Thresholds")]
        public int victoryPointsPerProvince = 1;
        public int buildFortVp = 1;
        public int buildChurchVp = 2;
        public int captureOrCrushRebellionVp = 2;
        public int defendRebellionVp = 2;
        public int victoryPointsThreshold = 50;
        public int crisisProvinceThreshold = 3;
        public int ascensionProvinceThreshold = 5;

        public GameBalanceConfig ToCoreConfig() => new()
        {
            StartingGold = startingGold,
            UnitCost = unitCost,
            MissionaryCost = missionaryCost,
            TraderCost = traderCost,
            ChurchCost = churchCost,
            ChurchTithe = churchTithe,
            RebellionCellCost = rebellionCellCost,
            TradingPostCost = tradingPostCost,
            FortCost = fortCost,
            DefaultMercenaryPrice = defaultMercenaryPrice,
            StartingTradingPostCount = startingTradingPostCount,
            CaravanBaseGainPerProvince = caravanBaseGainPerProvince,
            CaravanTransitToll = caravanTransitToll,
            CaravanMinTraversedForVp = caravanMinTraversedForVp,
            HostileBuyoutBaseCost = hostileBuyoutBaseCost,
            HostileBuyoutUnitPremium = hostileBuyoutUnitPremium,
            HostileBuyoutBuildingPremium = hostileBuyoutBuildingPremium,
            VictoryPointsPerProvince = victoryPointsPerProvince,
            BuildFortVp = buildFortVp,
            BuildChurchVp = buildChurchVp,
            CaptureOrCrushRebellionVp = captureOrCrushRebellionVp,
            DefendRebellionVp = defendRebellionVp,
            VictoryPointsThreshold = victoryPointsThreshold,
            CrisisProvinceThreshold = crisisProvinceThreshold,
            AscensionProvinceThreshold = ascensionProvinceThreshold
        };
    }
}