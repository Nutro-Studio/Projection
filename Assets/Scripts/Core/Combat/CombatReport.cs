namespace Core.Combat
{
    public sealed class CombatReport
    {
        public string ProvinceName { get; }
        public int AttackerId { get; }
        public int DefenderId { get; }
        public int AttackerCount { get; }
        public int DefenderCount { get; }
        public bool ProvinceCaptured { get; }
        public CombatResult Result { get; }

        public int AttackerCasualties => AttackerCount - Result.SurvivingAttackers.Count;
        public int DefenderCasualties => DefenderCount - Result.SurvivingDefenders.Count;

        public CombatReport(
            string provinceName,
            int attackerId,
            int defenderId,
            int attackerCount,
            int defenderCount,
            bool provinceCaptured,
            CombatResult result)
        {
            ProvinceName = provinceName;
            AttackerId = attackerId;
            DefenderId = defenderId;
            AttackerCount = attackerCount;
            DefenderCount = defenderCount;
            ProvinceCaptured = provinceCaptured;
            Result = result;
        }
    }

    public sealed class ConversionReport
    {
        public string ProvinceName { get; }
        public int ProvinceId { get; }
        public int ZealotPlayerId { get; }
        public int MissionariesExpended { get; }
        public int ConvertedCount { get; }
        public int ZealotRawSum { get; }
        public int DefenderRawSum { get; }
        public (int roll1, int roll2, int roll3) ZealotRolls { get; }
        public (int roll1, int roll2, int roll3) DefenderRolls { get; }

        public ConversionReport(
            string provinceName,
            int provinceId,
            int zealotPlayerId,
            int missionariesExpended,
            int convertedCount,
            int zealotRawSum,
            int defenderRawSum,
            (int roll1, int roll2, int roll3) zealotRolls,
            (int roll1, int roll2, int roll3) defenderRolls)
        {
            ProvinceName = provinceName;
            ProvinceId = provinceId;
            ZealotPlayerId = zealotPlayerId;
            MissionariesExpended = missionariesExpended;
            ConvertedCount = convertedCount;
            ZealotRawSum = zealotRawSum;
            DefenderRawSum = defenderRawSum;
            ZealotRolls = zealotRolls;
            DefenderRolls = defenderRolls;
        }
    }
}