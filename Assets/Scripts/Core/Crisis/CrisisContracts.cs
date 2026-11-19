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
        bool IsVoluntary
    );

    /// <summary>What a player holds at upkeep, as far as Victory Point scoring is concerned.</summary>
    public record UpkeepState(
        int ControlledProvinces,
        int TradingPosts,
        int Gold,
        int PresenceTokens = 0,
        int OpenRebellions = 0
    );

    /// <summary>Board events that score Victory Points the moment they happen.</summary>
    public enum ScoringEvent
    {
        ProvinceCaptured,
        RebellionCrushed,
        FortificationBuilt,
        ChurchBuilt,
        RebellionDefended,
        TradeRouteCompleted
    }

    public interface IFactionRuleset
    {
        FactionType Archetype { get; }
        bool IsLanded { get; }
        bool ReceivesFortificationBonus { get; }
        int CalculateUpkeepRevenue(int controlledProvinces);
        int CalculateUpkeepVictoryPoints(UpkeepState state);
        int ScoreEvent(ScoringEvent scoringEvent);
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