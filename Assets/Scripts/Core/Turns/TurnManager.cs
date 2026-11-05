using System;
using System.Collections.Generic;
using Projection.Core.Economy;
using Projection.Core.Map;
using Projection.Core.Units;

namespace Projection.Core.Turns
{
    public enum TurnPhase
    {
        Upkeep,
        Actions,
        Movement,
        Scoring
    }

    /// <summary>
    /// Hotseat turn order and the four-phase turn. Player commands go through here so they are only allowed
    /// for the active faction and in the right phase (recruit/build in Actions, move in Movement).
    /// </summary>
    public class TurnManager
    {
        private readonly GameState _state;
        private readonly EconomyService _economy;
        private readonly MovementService _movement;
        private int _factionIndex;

        public TurnPhase Phase { get; private set; }
        public int Round { get; private set; }
        public Faction Current => _state.Factions[_factionIndex];

        public event Action<Faction> TurnStarted;

        /// <summary>Raised in the Scoring phase after tithes. Hook for VP scoring and the crisis check.</summary>
        public event Action<Faction> TurnEnding;

        public TurnManager(GameState state, EconomyService economy, MovementService movement)
        {
            _state = state;
            _economy = economy;
            _movement = movement;
        }

        public void StartGame()
        {
            _factionIndex = 0;
            Round = 1;
            BeginTurn();
        }

        /// <summary>Actions -> Movement -> (Scoring) -> next faction's turn.</summary>
        public void AdvancePhase()
        {
            switch (Phase)
            {
                case TurnPhase.Actions:
                    Phase = TurnPhase.Movement;
                    break;
                case TurnPhase.Movement:
                    EndTurn();
                    break;
            }
        }

        public ActionResult Recruit(Province province, UnitType type) =>
            Phase != TurnPhase.Actions ? WrongPhase(TurnPhase.Actions) : _economy.Recruit(Current, province, type);

        public ActionResult Build(Province province, BuildingType type) =>
            Phase != TurnPhase.Actions ? WrongPhase(TurnPhase.Actions) : _economy.Build(Current, province, type);

        public ActionResult Move(Province from, Province to, int count, UnitType type = UnitType.Military) =>
            Phase != TurnPhase.Movement ? WrongPhase(TurnPhase.Movement) : _movement.Move(Current, from, to, count, type);

        private void BeginTurn()
        {
            Phase = TurnPhase.Upkeep;
            foreach (var unit in _state.UnitsOf(Current)) unit.HasMovedThisTurn = false;
            _state.Write($"--- Round {Round}: {Current}'s turn ---");
            _economy.CollectIncome(Current);

            Phase = TurnPhase.Actions;
            TurnStarted?.Invoke(Current);
        }

        private void EndTurn()
        {
            Phase = TurnPhase.Scoring;
            _economy.ApplyChurchTithes(Current);
            TurnEnding?.Invoke(Current);

            _factionIndex = (_factionIndex + 1) % _state.Factions.Count;
            if (_factionIndex == 0) Round++;
            BeginTurn();
        }

        private static ActionResult WrongPhase(TurnPhase needed) =>
            ActionResult.Fail($"This action is only allowed in the {needed} phase.");
    }
}
