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

        /// <summary>
        /// Landed factions reduced to &lt;= 3 provinces collapse into Guerrillas.
        /// </summary>
        public bool CanTriggerCrisis(PlayerProfile player, int controlledProvinces)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_rulesets.TryGetValue(player.Faction, out var ruleset)) return false;

            return ruleset.IsLanded && controlledProvinces <= CrisisProvinceThreshold;
        }

        /// <summary>
        /// Any non-Guerrilla faction may voluntarily capitulate into Guerrillas during its turn.
        /// </summary>
        public bool CanCapitulate(PlayerProfile player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return player.Faction != FactionType.Guerrillas;
        }

        /// <summary>
        /// Guerrillas controlling &gt;= 5 rebel provinces may proclaim statehood.
        /// </summary>
        public bool CanTriggerAscension(PlayerProfile player, int rebelProvinces)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return player.Faction == FactionType.Guerrillas && rebelProvinces >= AscensionProvinceThreshold;
        }

        public bool CanTriggerVoluntaryShift(PlayerProfile player, FactionType targetFaction, int controlledProvinces)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (player.Faction == targetFaction || player.Faction == FactionType.Guerrillas) return false;
            if (targetFaction == FactionType.Guerrillas) return false;

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

        public CrisisShiftResult ExecuteForcedCrisis(PlayerProfile player, bool isCapitulation = false)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            int penalizedVp = (int)Math.Floor(player.VictoryPoints * 0.80);
            var insurgentUnits = ConvertUnitsToInsurgents(player.Units, player.PlayerId);

            var updatedProfile = player with
            {
                Faction = FactionType.Guerrillas,
                Gold = 0,
                VictoryPoints = Math.Max(0, penalizedVp),
                Units = insurgentUnits
            };

            var shiftResult = new CrisisShiftResult(
                UpdatedProfile: updatedProfile,
                PreviousFaction: player.Faction,
                NewFaction: FactionType.Guerrillas,
                IsCrisis: true,
                IsAscension: false,
                IsVoluntary: isCapitulation
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
            var regularUnits = ConvertUnitsToArchetype(player.Units, player.PlayerId, targetLandedFaction);

            var updatedProfile = player with
            {
                Faction = targetLandedFaction,
                VictoryPoints = boostedVp,
                Units = regularUnits
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
            if (targetFaction == FactionType.Guerrillas)
            {
                return ExecuteForcedCrisis(player, isCapitulation: true);
            }

            int penalizedVp = (int)Math.Floor(player.VictoryPoints * 0.80);
            int gold = player.Gold;

            if (player.Faction == FactionType.Guilds)
            {
                gold = (int)Math.Floor(gold * 0.50);
            }

            IReadOnlyList<UnitInstance> finalUnits;
            if (player.Faction == FactionType.Militants)
            {
                // Militant shift penalty: lose 50% military units
                var military = new List<UnitInstance>();
                var nonMilitary = new List<UnitInstance>();
                for (int i = 0; i < player.Units.Count; i++)
                {
                    var u = player.Units[i] with { OwnerPlayerId = player.PlayerId };
                    if (u.Type == UnitType.RegularMilitary) military.Add(u);
                    else nonMilitary.Add(u);
                }

                int retainedMilitary = (int)Math.Ceiling(military.Count * 0.50);
                var surviving = new List<UnitInstance>(retainedMilitary + nonMilitary.Count);
                for (int i = 0; i < retainedMilitary; i++) surviving.Add(military[i]);
                surviving.AddRange(nonMilitary);
                finalUnits = surviving;
            }
            else if (player.Faction == FactionType.Zealots)
            {
                // Zealot shift penalty: lose all Missionaries
                var surviving = new List<UnitInstance>(player.Units.Count);
                for (int i = 0; i < player.Units.Count; i++)
                {
                    var u = player.Units[i];
                    if (u.Type != UnitType.Missionary)
                    {
                        surviving.Add(u with { OwnerPlayerId = player.PlayerId });
                    }
                }

                finalUnits = surviving;
            }
            else if (player.Faction == FactionType.Guilds)
            {
                // Guild shift penalty: lose all Traders/Trading Posts; Mercenaries convert to permanent units
                var surviving = new List<UnitInstance>(player.Units.Count);
                for (int i = 0; i < player.Units.Count; i++)
                {
                    var u = player.Units[i];
                    if (u.Type == UnitType.Trader) continue;

                    surviving.Add(u with
                    {
                        OwnerPlayerId = player.PlayerId,
                        OriginalFaction = targetFaction
                    });
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

        private static List<UnitInstance> ConvertUnitsToInsurgents(IReadOnlyList<UnitInstance> units, int ownerPlayerId)
        {
            var list = new List<UnitInstance>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                list.Add(units[i] with
                {
                    OwnerPlayerId = ownerPlayerId,
                    Type = UnitType.RegularMilitary
                });
            }

            return list;
        }

        private static List<UnitInstance> ConvertUnitsToArchetype(
            IReadOnlyList<UnitInstance> units,
            int ownerPlayerId,
            FactionType targetArchetype)
        {
            var list = new List<UnitInstance>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                list.Add(units[i] with
                {
                    OwnerPlayerId = ownerPlayerId,
                    OriginalFaction = targetArchetype,
                    Type = UnitType.RegularMilitary
                });
            }

            return list;
        }
    }
}