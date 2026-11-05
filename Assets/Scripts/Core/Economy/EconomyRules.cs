using System.Collections.Generic;
using Core.Combat;
using Core.Units;

namespace Core.Economy
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
        public const int ChurchTithePerUnit = 1;

        private static readonly Dictionary<UnitType, int> UnitCosts = new Dictionary<UnitType, int>
        {
            { UnitType.Military, 3 },
            { UnitType.Missionary, 3 },
            { UnitType.Trader, 2 },
            { UnitType.RebellionCell, 2 }
        };

        private static readonly Dictionary<BuildingType, int> BuildingCosts = new Dictionary<BuildingType, int>
        {
            { BuildingType.Fortification, 5 },
            { BuildingType.Church, 4 },
            { BuildingType.TradingPost, 4 }
        };

        private static readonly Dictionary<BuildingType, int> BuildingVps = new Dictionary<BuildingType, int>
        {
            { BuildingType.Fortification, 1 },
            { BuildingType.Church, 2 },
            { BuildingType.TradingPost, 1 }
        };

        // Rulebook table "Built By".
        private static readonly Dictionary<UnitType, FactionType[]> UnitRecruiters = new Dictionary<UnitType, FactionType[]>
        {
            { UnitType.Military, new[] { FactionType.Militants, FactionType.Zealots } },
            { UnitType.Missionary, new[] { FactionType.Zealots } },
            { UnitType.Trader, new[] { FactionType.Guilds } },
            { UnitType.RebellionCell, new[] { FactionType.Guerrillas } }
        };

        private static readonly Dictionary<BuildingType, FactionType> BuildingBuilders = new Dictionary<BuildingType, FactionType>
        {
            { BuildingType.Fortification, FactionType.Militants },
            { BuildingType.Church, FactionType.Zealots },
            { BuildingType.TradingPost, FactionType.Guilds }
        };

        public static int UnitCost(UnitType type) => UnitCosts[type];
        public static int BuildingCost(BuildingType type) => BuildingCosts[type];
        public static int BuildingVp(BuildingType type) => BuildingVps[type];

        public static bool CanRecruit(FactionType faction, UnitType type)
        {
            var recruiters = UnitRecruiters[type];
            for (int i = 0; i < recruiters.Length; i++)
            {
                if (recruiters[i] == faction) return true;
            }

            return false;
        }

        public static bool CanBuild(FactionType faction, BuildingType type) => BuildingBuilders[type] == faction;
    }
}
