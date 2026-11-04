using System;
using System.Collections.Generic;
using System.Linq;
using Core.Combat;

namespace Core.Crisis
{
    public class CrisisDirector : ICrisisDirector
    {
        private const int CrisisProvinceThreshold = 3;
        private const int AscensionProvinceThreshold = 5;

        private readonly IReadOnlyDictionary<FactionType, IFactionRuleset> _rulesets;

        public event Action<CrisisShiftResult> OnRulesetShifted;

        public CrisisDirector(IReadOnlyDictionary<FactionType, IFactionRuleset> rulesets)
        {
            _rulesets = rulesets ?? throw new ArgumentNullException(nameof(rulesets));
        }

        public bool CanTriggerCrisis(PlayerProfile player, int controlledProvinces)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_rulesets.TryGetValue(player.Faction, out var ruleset)) return false;

            // Crisis triggers when a landed faction has fewer than 3 provinces
            return ruleset.IsLanded && controlledProvinces < CrisisProvinceThreshold;
        }

        public bool CanTriggerAscension(PlayerProfile player, int controlledProvinces)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return player.Faction == FactionType.Guerrillas && controlledProvinces >= AscensionProvinceThreshold;
        }

        public CrisisShiftResult ExecuteForcedCrisis(PlayerProfile player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            // Rule: -20% VP penalty (floor)
            int penalizedVp = (int)Math.Floor(player.VictoryPoints * 0.80);

            // Rule: Convert surviving military units to Guerrilla faction units
            var convertedUnits = player.Units.Select(u => u with { Faction = FactionType.Guerrillas }).ToList();

            // Rule: Treasury wiped to 0
            var updatedProfile = player with
            {
                Faction = FactionType.Guerrillas,
                Gold = 0,
                VictoryPoints = Math.Max(0, penalizedVp),
                Units = convertedUnits
            };

            var shiftResult = new CrisisShiftResult(
                UpdatedProfile: updatedProfile,
                PreviousFaction: player.Faction,
                NewFaction: FactionType.Guerrillas,
                IsCrisis: true,
                IsAscension: false
            );

            OnRulesetShifted?.Invoke(shiftResult);
            return shiftResult;
        }

        public CrisisShiftResult ExecuteAscension(PlayerProfile player, FactionType targetLandedFaction)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (player.Faction != FactionType.Guerrillas)
            {
                throw new InvalidOperationException("Only Guerrillas can declare Ascension.");
            }

            if (targetLandedFaction == FactionType.Guerrillas)
            {
                throw new ArgumentException("Target archetype must be a formal state.", nameof(targetLandedFaction));
            }

            // Rule: +10% VP Ascension Bonus (floor)
            int boostedVp = (int)Math.Floor(player.VictoryPoints * 1.10);

            // Rule: Convert insurgent units to target formal faction units
            var convertedUnits = player.Units.Select(u => u with { Faction = targetLandedFaction }).ToList();

            var updatedProfile = player with
            {
                Faction = targetLandedFaction,
                VictoryPoints = boostedVp,
                Units = convertedUnits
            };

            var shiftResult = new CrisisShiftResult(
                UpdatedProfile: updatedProfile,
                PreviousFaction: FactionType.Guerrillas,
                NewFaction: targetLandedFaction,
                IsCrisis: false,
                IsAscension: true
            );

            OnRulesetShifted?.Invoke(shiftResult);
            return shiftResult;
        }
    }
}