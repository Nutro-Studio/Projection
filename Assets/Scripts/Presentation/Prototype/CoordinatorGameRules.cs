using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;

namespace Presentation.Prototype
{
    public sealed class CoordinatorGameRules : IGameRules
    {
        private readonly CombatCoordinator _coordinator;
        private readonly IDiceRoller _contestDice = new SystemRandomDiceRoller();

        public CoordinatorGameRules(CombatCoordinator coordinator) => _coordinator = coordinator;

        public CombatResult ResolveSkirmish(
            IReadOnlyList<UnitInstance> attackers,
            IReadOnlyList<UnitInstance> defenders,
            FactionType defenderFaction,
            bool hasFortification) =>
            _coordinator.ResolveSkirmish(attackers, defenders, defenderFaction, hasFortification);

        public PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces) =>
            _coordinator.EvaluatePlayerTurnState(player, controlledProvinces);

        public int CalculateRevenue(FactionType faction, int controlledProvinces) =>
            _coordinator.GetRuleset(faction).CalculateUpkeepRevenue(controlledProvinces);

        public int RollContest()
        {
            var (roll1, roll2, roll3) = _contestDice.Roll3D4();
            return roll1 + roll2 + roll3;
        }
    }
}