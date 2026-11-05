using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;

namespace Presentation.Prototype
{
    public enum CommandResult
    {
        Ok,
        GameOver,
        NotYourProvince,
        NoUnits,
        NotEnoughGold,
        NotAdjacent,
        UnitsAlreadyMoved
    }

    /// <summary>
    /// Rules owned by the Core systems. The session only reaches them through this seam.
    /// </summary>
    public interface IGameRules
    {
        CombatResult ResolveSkirmish(
            IReadOnlyList<CombatUnit> attackers,
            IReadOnlyList<CombatUnit> defenders,
            FactionType defenderFaction,
            bool hasFortification);

        PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces);
        int CalculateRevenue(FactionType faction, int controlledProvinces);
    }

    public sealed class ProvinceState
    {
        public const int NeutralOwner = -1;

        public int Id { get; }
        public string Name { get; }
        public bool HasFort { get; }
        public IReadOnlyList<int> Neighbours { get; }
        public int OwnerId { get; }
        public IReadOnlyList<CombatUnit> Units { get; }

        public ProvinceState(
            int id,
            string name,
            bool hasFort,
            IReadOnlyList<int> neighbours,
            int ownerId,
            IReadOnlyList<CombatUnit> units)
        {
            Id = id;
            Name = name;
            HasFort = hasFort;
            Neighbours = neighbours;
            OwnerId = ownerId;
            Units = units;
        }

        public ProvinceState WithOccupation(int ownerId, IReadOnlyList<CombatUnit> units) =>
            new(Id, Name, HasFort, Neighbours, ownerId, units);

        public bool IsAdjacentTo(int provinceId)
        {
            for (int i = 0; i < Neighbours.Count; i++)
            {
                if (Neighbours[i] == provinceId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class BattleReport
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

        public BattleReport(
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
