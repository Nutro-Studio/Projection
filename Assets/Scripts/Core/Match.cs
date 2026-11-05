using Projection.Core.Combat;
using Projection.Core.Economy;
using Projection.Core.Map;
using Projection.Core.Turns;
using Projection.Core.Units;

namespace Projection.Core
{
    /// <summary>Wires the systems together and sets up the standard 2-faction start (Militants 8 / Zealots 8 / Neutral 5, 10G each).</summary>
    public class Match
    {
        public GameState State { get; }
        public EconomyService Economy { get; }
        public MovementService Movement { get; }
        public TurnManager Turns { get; }

        public Faction Militants { get; }
        public Faction Zealots { get; }

        public Match(ICombatResolver combat = null)
        {
            Militants = new Faction(0, "Militants", FactionArchetype.Militants, EconomyRules.StartingGold);
            Zealots = new Faction(1, "Zealots", FactionArchetype.Zealots, EconomyRules.StartingGold);

            State = new GameState(StandardMap.Create(Militants, Zealots), new[] { Militants, Zealots });
            Economy = new EconomyService(State);
            Movement = new MovementService(State, combat ?? new PlaceholderCombatResolver());
            Turns = new TurnManager(State, Economy, Movement);

            PlaceStartingForces();
        }

        public Province Province(int x, int y) => State.Map.Get(StandardMap.IdOf(x, y));

        private void PlaceStartingForces()
        {
            var bastion = Province(1, 1);
            EconomyService.PlaceBuilding(Militants, bastion, BuildingType.Fortification);
            Spawn(Militants, UnitType.Military, bastion, 2);
            Spawn(Militants, UnitType.Military, Province(2, 0), 1);
            Spawn(Militants, UnitType.Military, Province(2, 2), 1);

            var sanctum = Province(5, 1);
            EconomyService.PlaceBuilding(Zealots, sanctum, BuildingType.Church);
            Spawn(Zealots, UnitType.Military, sanctum, 2);
            Spawn(Zealots, UnitType.Missionary, sanctum, 1);
            Spawn(Zealots, UnitType.Military, Province(4, 0), 1);
            Spawn(Zealots, UnitType.Military, Province(4, 2), 1);
        }

        private void Spawn(Faction owner, UnitType type, Province province, int count)
        {
            for (int i = 0; i < count; i++) State.SpawnUnit(type, owner, province);
        }
    }
}
