using Core.Combat;

namespace Core.Crisis
{
    public class MilitantRuleset : IFactionRuleset
    {
        public FactionType Archetype => FactionType.Militants;
        public bool IsLanded => true;
        public bool ReceivesFortificationBonus => true;
        public int CalculateUpkeepRevenue(int controlledProvinces) => controlledProvinces;
    }

    public class ZealotRuleset : IFactionRuleset
    {
        public FactionType Archetype => FactionType.Zealots;
        public bool IsLanded => true;
        public bool ReceivesFortificationBonus => false;
        public int CalculateUpkeepRevenue(int controlledProvinces) => controlledProvinces;
    }

    public class GuerrillaRuleset : IFactionRuleset
    {
        public FactionType Archetype => FactionType.Guerrillas;
        public bool IsLanded => false;
        public bool ReceivesFortificationBonus => false;
        public int CalculateUpkeepRevenue(int controlledProvinces) => 0;
    }

    public class GuildRuleset : IFactionRuleset
    {
        public FactionType Archetype => FactionType.Guilds;
        public bool IsLanded => false;
        public bool ReceivesFortificationBonus => false;
        public int CalculateUpkeepRevenue(int controlledProvinces) => 0;
    }
}