using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;

namespace Presentation.Prototype
{
    public sealed class CoordinatorGameRules : IGameRules
    {
        private readonly CombatCoordinator _coordinator;

        public CoordinatorGameRules(CombatCoordinator coordinator) => _coordinator = coordinator;

        public CombatResult ResolveSkirmish(
            IReadOnlyList<CombatUnit> attackers,
            IReadOnlyList<CombatUnit> defenders,
            FactionType defenderFaction,
            bool hasFortification) =>
            _coordinator.ResolveSkirmish(attackers, defenders, defenderFaction, hasFortification);

        public PlayerProfile EvaluatePlayerTurnState(PlayerProfile player, int controlledProvinces) =>
            _coordinator.EvaluatePlayerTurnState(player, controlledProvinces);

        public int CalculateRevenue(FactionType faction, int controlledProvinces) =>
            _coordinator.GetRuleset(faction).CalculateUpkeepRevenue(controlledProvinces);
    }
}
