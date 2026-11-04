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
        IReadOnlyList<CombatUnit> Units
    );

    public record CrisisShiftResult(
        PlayerProfile UpdatedProfile,
        FactionType PreviousFaction,
        FactionType NewFaction,
        bool IsCrisis,
        bool IsAscension
    );

    public interface IFactionRuleset
    {
        FactionType Archetype { get; }
        bool IsLanded { get; }
        int CalculateUpkeepRevenue(int controlledProvinces);
    }

    public interface ICrisisDirector
    {
        bool CanTriggerCrisis(PlayerProfile player, int controlledProvinces);
        bool CanTriggerAscension(PlayerProfile player, int controlledProvinces);
        CrisisShiftResult ExecuteForcedCrisis(PlayerProfile player);
        CrisisShiftResult ExecuteAscension(PlayerProfile player, FactionType targetLandedFaction);

        event Action<CrisisShiftResult> OnRulesetShifted;
    }
}