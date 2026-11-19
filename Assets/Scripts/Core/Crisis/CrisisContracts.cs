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
        IReadOnlyList<UnitInstance> Units,
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
        bool IsVoluntary,
        int FortificationsLost = 0,
        int ChurchesLost = 0
    );

    /// <summary>
    /// Asset losses an archetype pays when it voluntarily leaves its ruleset (Transformation Matrix).
    /// Each value is the share lost; losses round down.
    /// </summary>
    public record ShiftPenalty(
        double MilitaryUnitLoss = 0,
        double MissionaryLoss = 0,
        double FortificationLoss = 0,
        double ChurchLoss = 0,
        double TreasuryLoss = 0
    )
    {
        public static readonly ShiftPenalty None = new();
    }

    /// <summary>Board structures a player holds when a voluntary shift is priced.</summary>
    public record ShiftHoldings(int Fortifications, int Churches)
    {
        public static readonly ShiftHoldings None = new(0, 0);
    }

    public interface IFactionRuleset
    {
        FactionType Archetype { get; }
        bool IsLanded { get; }
        bool ReceivesFortificationBonus { get; }
        int CalculateUpkeepRevenue(int controlledProvinces);
        ShiftPenalty VoluntaryShiftPenalty { get; }
    }

    public interface ICrisisDirector
    {
        bool CanTriggerCrisis(PlayerProfile player, int controlledProvinces);
        bool CanTriggerAscension(PlayerProfile player, int controlledProvinces);
        bool CanTriggerVoluntaryShift(PlayerProfile player, FactionType targetFaction, int controlledProvinces);
        bool CanCapitulate(PlayerProfile player);
        bool CheckVictory(PlayerProfile player);

        CrisisShiftResult ExecuteForcedCrisis(PlayerProfile player);
        CrisisShiftResult ExecuteCapitulation(PlayerProfile player);
        CrisisShiftResult ExecuteAscension(PlayerProfile player, FactionType targetLandedFaction);
        CrisisShiftResult ExecuteVoluntaryShift(PlayerProfile player, FactionType targetFaction, ShiftHoldings holdings);

        event Action<CrisisShiftResult> OnRulesetShifted;
        event Action<PlayerProfile> OnVictoryAchieved;
    }
}