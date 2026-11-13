using System;
using System.Collections.Generic;
using Core.Combat;

namespace Core.Crisis
{
    public class CrisisDirector : ICrisisDirector
    {
        public const int VictoryPointsThreshold = 50;
        public const int CrisisProvinceThreshold = 3;
        public const int AscensionProvinceThreshold = 5;

        private readonly IReadOnlyDictionary<FactionType, IFactionRuleset> _rulesets;

        public event Action<CrisisShiftResult> OnRulesetShifted;
        public event Action<PlayerProfile> OnVictoryAchieved;

        public CrisisDirector(IReadOnlyDictionary<FactionType, IFactionRuleset> rulesets)
        {
            _rulesets = rulesets ?? throw new ArgumentNullException(nameof(rulesets));
        }

        public bool CheckVictory(PlayerProfile player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            bool won = player.VictoryPoints >= VictoryPointsThreshold;
            if (won)
            {
                OnVictoryAchieved?.Invoke(player);
            }

            return won;
        }

        public bool CanTriggerCrisis(PlayerProfile player, int controlledProvinces)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_rulesets.TryGetValue(player.Faction, out var ruleset)) return false;

            return ruleset.IsLanded && controlledProvinces < CrisisProvinceThreshold;
        }

        public bool CanTriggerAscension(PlayerProfile player, int controlledProvinces)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return player.Faction == FactionType.Guerrillas && controlledProvinces >= AscensionProvinceThreshold;
        }

        public bool CanTriggerVoluntaryShift(PlayerProfile player, FactionType targetFaction, int controlledProvinces)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (player.Faction == targetFaction || player.Faction == FactionType.Guerrillas) return false;

            return player.Faction switch
            {
                FactionType.Militants => targetFaction == FactionType.Zealots || targetFaction == FactionType.Guilds,
                FactionType.Zealots => targetFaction == FactionType.Militants || targetFaction == FactionType.Guilds,
                FactionType.Guilds =>
                    (targetFaction == FactionType.Militants || targetFaction == FactionType.Zealots) &&
                    controlledProvinces >= 3,
                _ => false
            };
        }

        public CrisisShiftResult ExecuteForcedCrisis(PlayerProfile player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            int penalizedVp = (int)Math.Floor(player.VictoryPoints * 0.80);
            var retainedUnits = AssignUnitsToOwner(player.Units, player.PlayerId);

            var updatedProfile = player with
            {
                Faction = FactionType.Guerrillas,
                Gold = 0,
                VictoryPoints = Math.Max(0, penalizedVp),
                Units = retainedUnits
            };

            var shiftResult = new CrisisShiftResult(
                UpdatedProfile: updatedProfile,
                PreviousFaction: player.Faction,
                NewFaction: FactionType.Guerrillas,
                IsCrisis: true,
                IsAscension: false,
                IsVoluntary: false
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

            int boostedVp = (int)Math.Floor(player.VictoryPoints * 1.10);
            var retainedUnits = AssignUnitsToOwner(player.Units, player.PlayerId);

            var updatedProfile = player with
            {
                Faction = targetLandedFaction,
                VictoryPoints = boostedVp,
                Units = retainedUnits
            };

            var shiftResult = new CrisisShiftResult(
                UpdatedProfile: updatedProfile,
                PreviousFaction: FactionType.Guerrillas,
                NewFaction: targetLandedFaction,
                IsCrisis: false,
                IsAscension: true,
                IsVoluntary: false
            );

            OnRulesetShifted?.Invoke(shiftResult);
            CheckVictory(updatedProfile);
            return shiftResult;
        }

        public CrisisShiftResult ExecuteVoluntaryShift(PlayerProfile player, FactionType targetFaction)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            int penalizedVp = (int)Math.Floor(player.VictoryPoints * 0.80);
            int gold = player.Gold;

            // Guild shift penalty: lose 50% treasury
            if (player.Faction == FactionType.Guilds)
            {
                gold = (int)Math.Floor(gold * 0.50);
            }

            // Militant shift penalty: lose 50% military units
            IReadOnlyList<UnitInstance> finalUnits;
            if (player.Faction == FactionType.Militants)
            {
                int remainingCount = (int)Math.Ceiling(player.Units.Count * 0.50);
                var surviving = new List<UnitInstance>(remainingCount);
                for (int i = 0; i < remainingCount; i++)
                {
                    surviving.Add(player.Units[i] with { OwnerPlayerId = player.PlayerId });
                }

                finalUnits = surviving;
            }
            else
            {
                finalUnits = AssignUnitsToOwner(player.Units, player.PlayerId);
            }

            var updatedProfile = player with
            {
                Faction = targetFaction,
                Gold = gold,
                VictoryPoints = Math.Max(0, penalizedVp),
                Units = finalUnits
            };

            var shiftResult = new CrisisShiftResult(
                UpdatedProfile: updatedProfile,
                PreviousFaction: player.Faction,
                NewFaction: targetFaction,
                IsCrisis: false,
                IsAscension: false,
                IsVoluntary: true
            );

            OnRulesetShifted?.Invoke(shiftResult);
            return shiftResult;
        }

        private static List<UnitInstance> AssignUnitsToOwner(IReadOnlyList<UnitInstance> units, int ownerPlayerId)
        {
            var list = new List<UnitInstance>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                list.Add(units[i] with { OwnerPlayerId = ownerPlayerId });
            }

            return list;
        }
    }
}