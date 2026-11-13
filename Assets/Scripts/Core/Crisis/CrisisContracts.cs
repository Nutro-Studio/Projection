using System;
using System.Collections.Generic;
using Core.Combat;

namespace Core.Crisis
{
    public record PlayerProfile(
        int PlayerId,
        FactionType Faction,
        int Gold,
        int VictoryPoints,
        IReadOnlyList<CombatUnit> Units,
        int ActionPoints = PlayerProfile.BaseActionPoints
    )
    {
        /// <summary>Every major board interaction costs one AP; the pool refills at the start of the turn.</summary>
        public const int BaseActionPoints = 4;
    }

    public record CrisisShiftResult(
        PlayerProfile UpdatedProfile,
        FactionType PreviousFaction,
        FactionType NewFaction,
        bool IsCrisis,
        bool IsAscension,
        bool IsVoluntary
    );

    public interface IFactionRuleset
    {
        FactionType Archetype { get; }
        bool IsLanded { get; }
        bool ReceivesFortificationBonus { get; }
        int CalculateUpkeepRevenue(int controlledProvinces);
    }

    public interface ICrisisDirector
    {
        bool CanTriggerCrisis(PlayerProfile player, int controlledProvinces);
        bool CanTriggerAscension(PlayerProfile player, int controlledProvinces);
        bool CanTriggerVoluntaryShift(PlayerProfile player, FactionType targetFaction, int controlledProvinces);
        bool CheckVictory(PlayerProfile player);

        CrisisShiftResult ExecuteForcedCrisis(PlayerProfile player);
        CrisisShiftResult ExecuteAscension(PlayerProfile player, FactionType targetLandedFaction);
        CrisisShiftResult ExecuteVoluntaryShift(PlayerProfile player, FactionType targetFaction);

        event Action<CrisisShiftResult> OnRulesetShifted;
        event Action<PlayerProfile> OnVictoryAchieved;
    }
}