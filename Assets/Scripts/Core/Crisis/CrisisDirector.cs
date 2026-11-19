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
        public const double ShiftVictoryPointsMultiplier = 0.80;

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

        /// <summary>
        /// Any landed faction may give up its state during its turn and collapse into Guerrillas.
        /// </summary>
        public bool CanCapitulate(PlayerProfile player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return _rulesets.TryGetValue(player.Faction, out var ruleset) && ruleset.IsLanded;
        }

        public CrisisShiftResult ExecuteForcedCrisis(PlayerProfile player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return Collapse(player, isVoluntary: false);
        }

        public CrisisShiftResult ExecuteCapitulation(PlayerProfile player)
        {
            if (!CanCapitulate(player))
            {
                throw new InvalidOperationException("Only a landed faction can capitulate.");
            }

            return Collapse(player, isVoluntary: true);
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

        /// <summary>
        /// Applies the leaving archetype's <see cref="ShiftPenalty"/> and -20% VP.
        /// The earliest units in <see cref="PlayerProfile.Units"/> are the ones kept.
        /// </summary>
        public CrisisShiftResult ExecuteVoluntaryShift(PlayerProfile player, FactionType targetFaction,
            ShiftHoldings holdings)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (holdings == null) throw new ArgumentNullException(nameof(holdings));

            var penalty = _rulesets[player.Faction].VoluntaryShiftPenalty;
            int militaryToKeep = Remaining(CountUnits(player.Units, UnitType.RegularMilitary), penalty.MilitaryUnitLoss);
            int missionariesToKeep = Remaining(CountUnits(player.Units, UnitType.Missionary), penalty.MissionaryLoss);

            var finalUnits = new List<UnitInstance>(player.Units.Count);
            for (int i = 0; i < player.Units.Count; i++)
            {
                var unit = player.Units[i];
                if (unit.Type == UnitType.RegularMilitary)
                {
                    if (militaryToKeep == 0) continue;
                    militaryToKeep--;
                }
                else if (unit.Type == UnitType.Missionary)
                {
                    if (missionariesToKeep == 0) continue;
                    missionariesToKeep--;
                }

                finalUnits.Add(unit with { OwnerPlayerId = player.PlayerId });
            }

            var updatedProfile = player with
            {
                Faction = targetFaction,
                Gold = (int)Math.Floor(player.Gold * (1 - penalty.TreasuryLoss)),
                VictoryPoints = PenalizedVictoryPoints(player),
                Units = finalUnits
            };

            var shiftResult = new CrisisShiftResult(
                UpdatedProfile: updatedProfile,
                PreviousFaction: player.Faction,
                NewFaction: targetFaction,
                IsCrisis: false,
                IsAscension: false,
                IsVoluntary: true,
                FortificationsLost: Lost(holdings.Fortifications, penalty.FortificationLoss),
                ChurchesLost: Lost(holdings.Churches, penalty.ChurchLoss)
            );

            OnRulesetShifted?.Invoke(shiftResult);
            return shiftResult;
        }

        /// <summary>
        /// Crisis or capitulation: the treasury is wiped, VP drop by 20% and the player's units stay on as
        /// Guerrilla insurgents.
        /// </summary>
        private CrisisShiftResult Collapse(PlayerProfile player, bool isVoluntary)
        {
            var updatedProfile = player with
            {
                Faction = FactionType.Guerrillas,
                Gold = 0,
                VictoryPoints = PenalizedVictoryPoints(player),
                Units = AssignUnitsToOwner(player.Units, player.PlayerId)
            };

            var shiftResult = new CrisisShiftResult(
                UpdatedProfile: updatedProfile,
                PreviousFaction: player.Faction,
                NewFaction: FactionType.Guerrillas,
                IsCrisis: true,
                IsAscension: false,
                IsVoluntary: isVoluntary
            );

            OnRulesetShifted?.Invoke(shiftResult);
            return shiftResult;
        }

        private static int PenalizedVictoryPoints(PlayerProfile player) =>
            Math.Max(0, (int)Math.Floor(player.VictoryPoints * ShiftVictoryPointsMultiplier));

        private static int Lost(int count, double share) => (int)Math.Floor(count * share);

        private static int Remaining(int count, double share) => count - Lost(count, share);

        private static int CountUnits(IReadOnlyList<UnitInstance> units, UnitType type)
        {
            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Type == type)
                {
                    count++;
                }
            }

            return count;
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