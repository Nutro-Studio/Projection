using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using Presentation.Hud;
using Presentation.Map;
using Presentation.Prototype;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Composition root of the prototype scene.
    /// </summary>
    public class PrototypeGameBootstrap : MonoBehaviour
    {
        private const int MilitantsPlayerId = 1;
        private const int ZealotsPlayerId = 2;

        [SerializeField] private CombatCoordinator _coordinator;
        [SerializeField] private HudController _hud;
        [SerializeField] private MapInputController _mapInput;
        [SerializeField] private Camera _mapCamera;
        [SerializeField] private ProvinceView[] _provinceViews;
        [SerializeField] private bool _crisisEnabled;

        private ProvinceMapPresenter _mapPresenter;

        private void Start()
        {
            var players = new[]
            {
                new PlayerProfile(MilitantsPlayerId, FactionType.Militants, PrototypeGameSession.StartingGold, 0,
                    new List<UnitInstance>()),
                new PlayerProfile(ZealotsPlayerId, FactionType.Zealots, PrototypeGameSession.StartingGold, 0,
                    new List<UnitInstance>())
            };

            var session = new PrototypeGameSession(new CoordinatorGameRules(_coordinator), players,
                BuildProvinces(players), _crisisEnabled);
            var interaction = new HudInteraction(session);

            _hud.Initialize(session, interaction, _coordinator);
            _mapPresenter = new ProvinceMapPresenter(session, interaction, _provinceViews);
            _mapInput.Initialize(interaction, _hud, _mapCamera);
        }

        private void OnDestroy()
        {
            _mapPresenter?.Dispose();
        }

        private List<ProvinceState> BuildProvinces(PlayerProfile[] players)
        {
            var provinces = new List<ProvinceState>(_provinceViews.Length);
            int nextUnitId = 0;
            for (int i = 0; i < _provinceViews.Length; i++)
            {
                var view = _provinceViews[i];
                var faction = FactionOf(players, view.StartingOwnerId);
                var units = new List<UnitInstance>(view.StartingUnits);
                for (int u = 0; u < view.StartingUnits; u++)
                {
                    units.Add(new UnitInstance(
                        nextUnitId++,
                        view.StartingOwnerId,
                        faction,
                        UnitType.RegularMilitary,
                        1,
                        1));
                }

                provinces.Add(new ProvinceState(view.ProvinceId, view.DisplayName, view.HasFort, view.Neighbours,
                    view.StartingOwnerId, units));
            }

            return provinces;
        }

        private static FactionType FactionOf(PlayerProfile[] players, int playerId)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].PlayerId == playerId)
                {
                    return players[i].Faction;
                }
            }

            return FactionType.Guerrillas;
        }
    }
}