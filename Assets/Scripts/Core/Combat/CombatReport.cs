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
}