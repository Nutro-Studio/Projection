using Core.Combat;

namespace Core.Crisis
{
    public class MilitantRuleset : IFactionRuleset
    {
        public const int ProvincesPerVictoryPoint = 3;
        public const int FortificationVictoryPoints = 1;
        public const int CaptureVictoryPoints = 2;

        public FactionType Archetype => FactionType.Militants;
        public bool IsLanded => true;
        public bool ReceivesFortificationBonus => true;
        public int CalculateUpkeepRevenue(int controlledProvinces) => controlledProvinces;

        /// <summary>+1 VP for every 3 controlled provinces.</summary>
        public int CalculateUpkeepVictoryPoints(UpkeepState state) =>
            state.ControlledProvinces / ProvincesPerVictoryPoint;

        /// <summary>+1 VP per Fortification built, +2 VP per captured province or crushed rebellion.</summary>
        public int ScoreEvent(ScoringEvent scoringEvent) => scoringEvent switch
        {
            ScoringEvent.FortificationBuilt => FortificationVictoryPoints,
            ScoringEvent.ProvinceCaptured => CaptureVictoryPoints,
            ScoringEvent.RebellionCrushed => CaptureVictoryPoints,
            _ => 0
        };
    }

    public class ZealotRuleset : IFactionRuleset
    {
        public const int ChurchVictoryPoints = 2;

        public FactionType Archetype => FactionType.Zealots;
        public bool IsLanded => true;
        public bool ReceivesFortificationBonus => false;
        public int CalculateUpkeepRevenue(int controlledProvinces) => controlledProvinces;

        /// <summary>+1 VP per province holding the Zealots' Religious Presence token.</summary>
        public int CalculateUpkeepVictoryPoints(UpkeepState state) => state.PresenceTokens;

        /// <summary>+2 VP per Church built.</summary>
        public int ScoreEvent(ScoringEvent scoringEvent) =>
            scoringEvent == ScoringEvent.ChurchBuilt ? ChurchVictoryPoints : 0;
    }

    public class GuerrillaRuleset : IFactionRuleset
    {
        public const int RebellionDefenseVictoryPoints = 2;

        public FactionType Archetype => FactionType.Guerrillas;
        public bool IsLanded => false;
        public bool ReceivesFortificationBonus => false;
        public int CalculateUpkeepRevenue(int controlledProvinces) => 0;

        /// <summary>+1 VP per province in open Rebellion.</summary>
        public int CalculateUpkeepVictoryPoints(UpkeepState state) => state.OpenRebellions;

        /// <summary>+2 VP for each successful defence of an open Rebellion zone.</summary>
        public int ScoreEvent(ScoringEvent scoringEvent) =>
            scoringEvent == ScoringEvent.RebellionDefended ? RebellionDefenseVictoryPoints : 0;
    }

    public class GuildRuleset : IFactionRuleset
    {
        public const int CaravanGoldPerProvince = 2;
        public const int CaravanTransitFee = 1;
        public const int BuiltTradingPostsPerCaravanBonus = 2;
        public const int TradeRouteMinProvinces = 3;
        public const int TradeRouteVictoryPoints = 2;
        public const int TradingPostVictoryPoints = 1;
        public const int GoldPerVictoryPoint = 10;
        public const int BuyoutBasePrice = 5;
        public const int BuyoutPricePerUnit = 3;

        public FactionType Archetype => FactionType.Guilds;
        public bool IsLanded => false;
        public bool ReceivesFortificationBonus => false;
        public int CalculateUpkeepRevenue(int controlledProvinces) => 0;

        /// <summary>
        /// Gold a caravan earns per province: 2, plus 1 for every 2 Trading Posts built beyond the starting ones.
        /// </summary>
        public static int CaravanGoldPerProvinceFor(int builtTradingPosts) =>
            CaravanGoldPerProvince + builtTradingPosts / BuiltTradingPostsPerCaravanBonus;

        /// <summary>+1 VP per Trading Post and +1 VP per 10 Gold banked. Trade routes score when they complete.</summary>
        public int CalculateUpkeepVictoryPoints(UpkeepState state) =>
            state.TradingPosts * TradingPostVictoryPoints + state.Gold / GoldPerVictoryPoint;

        public int ScoreEvent(ScoringEvent scoringEvent) =>
            scoringEvent == ScoringEvent.TradeRouteCompleted ? TradeRouteVictoryPoints : 0;
    }
}