using Core.Combat;

namespace Core.Crisis
{
    public class MilitantRuleset : IFactionRuleset
    {
        public FactionType Archetype => FactionType.Militants;
        public bool IsLanded => true;
        public bool ReceivesFortificationBonus => true;
        public int CalculateUpkeepRevenue(int controlledProvinces) => controlledProvinces;
        public int CalculateUpkeepVictoryPoints(UpkeepState state) => state.ControlledProvinces;
    }

    public class ZealotRuleset : IFactionRuleset
    {
        public FactionType Archetype => FactionType.Zealots;
        public bool IsLanded => true;
        public bool ReceivesFortificationBonus => false;
        public int CalculateUpkeepRevenue(int controlledProvinces) => controlledProvinces;
        public int CalculateUpkeepVictoryPoints(UpkeepState state) => state.ControlledProvinces;
    }

    public class GuerrillaRuleset : IFactionRuleset
    {
        public FactionType Archetype => FactionType.Guerrillas;
        public bool IsLanded => false;
        public bool ReceivesFortificationBonus => false;
        public int CalculateUpkeepRevenue(int controlledProvinces) => 0;
        public int CalculateUpkeepVictoryPoints(UpkeepState state) => state.ControlledProvinces;
    }

    public class GuildRuleset : IFactionRuleset
    {
        public const int CaravanGoldPerProvince = 2;
        public const int CaravanTransitFee = 1;
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

        /// <summary>+1 VP per Trading Post and +1 VP per 10 Gold banked. Trade routes score when they complete.</summary>
        public int CalculateUpkeepVictoryPoints(UpkeepState state) =>
            state.TradingPosts * TradingPostVictoryPoints + state.Gold / GoldPerVictoryPoint;
    }
}