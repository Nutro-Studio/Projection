using Core.Combat;
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
        [SerializeField] private CombatCoordinator _coordinator;
        [SerializeField] private HudController _hud;
        [SerializeField] private MapInputController _mapInput;
        [SerializeField] private Camera _mapCamera;
        [SerializeField] private ProvinceView[] _provinceViews;
        [SerializeField] private bool _crisisEnabled;

        private ProvinceMapPresenter _mapPresenter;

        private void Start()
        {
            var players = PrototypeSetup.CreatePlayers();
            var seeds = new ProvinceSeed[_provinceViews.Length];
            for (int i = 0; i < _provinceViews.Length; i++)
            {
                seeds[i] = _provinceViews[i].ToSeed();
            }

            var session = new PrototypeGameSession(new CoordinatorGameRules(_coordinator), players,
                PrototypeSetup.BuildProvinces(seeds, players), _crisisEnabled);
            var interaction = new HudInteraction(session);

            _hud.Initialize(session, interaction, _coordinator);
            _mapPresenter = new ProvinceMapPresenter(session, interaction, _provinceViews);
            _mapInput.Initialize(interaction, _hud, _mapCamera);
        }

        private void OnDestroy()
        {
            _mapPresenter?.Dispose();
        }
    }
}