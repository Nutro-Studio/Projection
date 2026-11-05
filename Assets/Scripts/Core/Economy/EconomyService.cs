using Core.Combat;
using Core.Map;
using Core.Units;

namespace Core.Economy
{
    /// <summary>Income, recruitment, construction and tithes. All gold flows through faction treasuries.</summary>
    public class EconomyService
    {
        private readonly GameState _state;

        public EconomyService(GameState state)
        {
            _state = state;
        }

        /// <summary>Phase 1: revenue from the faction's ruleset (landed factions: +1 Gold per province).</summary>
        public int CollectIncome(Faction faction)
        {
            int provinces = _state.Map.CountControlledBy(faction);
            int income = faction.Ruleset.CalculateUpkeepRevenue(provinces);
            if (income <= 0) return 0;

            faction.Treasury.Deposit(income, $"Revenue from {provinces} provinces");
            _state.Write($"{faction}: +{income}G revenue ({provinces} provinces)");
            return income;
        }

        public ActionResult CanRecruit(Faction faction, Province province, UnitType type)
        {
            if (!EconomyRules.CanRecruit(faction.Archetype, type))
                return ActionResult.Fail($"{faction.Archetype} cannot recruit {type}.");
            if (province.Owner != faction)
                return ActionResult.Fail($"{province} is not controlled by {faction}.");
            if (_state.HostileOccupant(province, faction) != null)
                return ActionResult.Fail($"{province} is occupied by enemy troops.");

            int cost = EconomyRules.UnitCost(type);
            if (!faction.Treasury.CanAfford(cost))
                return ActionResult.Fail($"Not enough gold: {type} costs {cost}G, treasury has {faction.Treasury.Gold}G.");

            return ActionResult.Ok();
        }

        public ActionResult Recruit(Faction faction, Province province, UnitType type)
        {
            var check = CanRecruit(faction, province, type);
            if (!check.Success) return check;

            int cost = EconomyRules.UnitCost(type);
            faction.Treasury.TrySpend(cost, $"Recruit {type} in {province}");
            _state.SpawnUnit(type, faction, province);

            string message = $"{faction}: recruited {type} in {province} (-{cost}G)";
            _state.Write(message);
            return ActionResult.Ok(message);
        }

        public ActionResult CanBuild(Faction faction, Province province, BuildingType type)
        {
            if (!EconomyRules.CanBuild(faction.Archetype, type))
                return ActionResult.Fail($"{faction.Archetype} cannot build {type}.");
            if (province.Owner != faction)
                return ActionResult.Fail($"{province} is not controlled by {faction}.");
            if (HasBuilding(province, type))
                return ActionResult.Fail($"{province} already has a {type}.");

            int cost = EconomyRules.BuildingCost(type);
            if (!faction.Treasury.CanAfford(cost))
                return ActionResult.Fail($"Not enough gold: {type} costs {cost}G, treasury has {faction.Treasury.Gold}G.");

            return ActionResult.Ok();
        }

        public ActionResult Build(Faction faction, Province province, BuildingType type)
        {
            var check = CanBuild(faction, province, type);
            if (!check.Success) return check;

            int cost = EconomyRules.BuildingCost(type);
            faction.Treasury.TrySpend(cost, $"Build {type} in {province}");
            PlaceBuilding(faction, province, type);

            int vp = EconomyRules.BuildingVp(type);
            faction.VictoryPoints += vp;

            string message = $"{faction}: built {type} in {province} (-{cost}G, +{vp} VP)";
            _state.Write(message);
            return ActionResult.Ok(message);
        }

        /// <summary>
        /// Church tithes, applied when <paramref name="endingFaction"/> ends its turn: every one of its units
        /// standing in a province with a foreign church pays 1G to the church owner. Guerrillas are exempt.
        /// Returns the total paid.
        /// </summary>
        public int ApplyChurchTithes(Faction endingFaction)
        {
            if (endingFaction.Archetype == FactionType.Guerrillas) return 0;

            int total = 0;
            var provinces = _state.Map.Provinces;
            for (int i = 0; i < provinces.Count; i++)
            {
                var province = provinces[i];
                if (!province.HasChurch || province.ChurchOwner == endingFaction) continue;

                int units = _state.CountUnits(endingFaction, province);
                if (units == 0) continue;

                int paid = Treasury.Transfer(endingFaction.Treasury, province.ChurchOwner.Treasury,
                    units * EconomyRules.ChurchTithePerUnit, $"Church tithe in {province}");
                total += paid;
                if (paid > 0) _state.Write($"{endingFaction}: paid {paid}G tithe to {province.ChurchOwner} in {province}");
            }

            return total;
        }

        public static bool HasBuilding(Province province, BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Fortification: return province.HasFortification;
                case BuildingType.Church: return province.HasChurch;
                case BuildingType.TradingPost: return province.HasTradingPost;
                default: return false;
            }
        }

        /// <summary>Places a building without paying for it (map setup, scenario scripts).</summary>
        public static void PlaceBuilding(Faction owner, Province province, BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Fortification: province.HasFortification = true; break;
                case BuildingType.Church: province.ChurchOwner = owner; break;
                case BuildingType.TradingPost: province.TradingPostOwner = owner; break;
            }
        }
    }
}
