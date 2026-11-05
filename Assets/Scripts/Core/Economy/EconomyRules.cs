namespace Projection.Core.Economy
{
    public enum BuildingType
    {
        Fortification,
        Church,
        TradingPost
    }

    /// <summary>Numbers from the rulebook (section 3) in one place, so balancing touches only this file.</summary>
    public static class EconomyRules
    {
        public const int StartingGold = 10;
        public const int IncomePerProvince = 1;
        public const int ChurchTithePerUnit = 1;

        public const int MilitaryUnitCost = 3;
        public const int MissionaryCost = 3;
        public const int TraderCost = 2;
        public const int RebellionCellCost = 2;

        public const int FortificationCost = 5;
        public const int ChurchCost = 4;
        public const int TradingPostCost = 4;

        public const int FortificationVp = 1;
        public const int ChurchVp = 2;
        public const int TradingPostVp = 1;

        public static int UnitCost(Units.UnitType type)
        {
            switch (type)
            {
                case Units.UnitType.Military: return MilitaryUnitCost;
                case Units.UnitType.Missionary: return MissionaryCost;
                case Units.UnitType.Trader: return TraderCost;
                case Units.UnitType.RebellionCell: return RebellionCellCost;
                default: return int.MaxValue;
            }
        }

        public static int BuildingCost(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Fortification: return FortificationCost;
                case BuildingType.Church: return ChurchCost;
                case BuildingType.TradingPost: return TradingPostCost;
                default: return int.MaxValue;
            }
        }

        public static int BuildingVp(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Fortification: return FortificationVp;
                case BuildingType.Church: return ChurchVp;
                case BuildingType.TradingPost: return TradingPostVp;
                default: return 0;
            }
        }

        /// <summary>Which archetype may recruit which unit (rulebook table "Built By").</summary>
        public static bool CanRecruit(FactionArchetype archetype, Units.UnitType type)
        {
            switch (type)
            {
                case Units.UnitType.Military:
                    return archetype == FactionArchetype.Militants || archetype == FactionArchetype.Zealots;
                case Units.UnitType.Missionary:
                    return archetype == FactionArchetype.Zealots;
                case Units.UnitType.Trader:
                    return archetype == FactionArchetype.Guilds;
                case Units.UnitType.RebellionCell:
                    return archetype == FactionArchetype.Guerrillas;
                default:
                    return false;
            }
        }

        public static bool CanBuild(FactionArchetype archetype, BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Fortification: return archetype == FactionArchetype.Militants;
                case BuildingType.Church: return archetype == FactionArchetype.Zealots;
                case BuildingType.TradingPost: return archetype == FactionArchetype.Guilds;
                default: return false;
            }
        }
    }
}
