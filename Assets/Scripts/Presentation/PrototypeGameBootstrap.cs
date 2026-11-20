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
        [SerializeField] private bool _useContinentalMap21 = true;
        [SerializeField] private bool _crisisEnabled = true;

        private ProvinceMapPresenter _mapPresenter;

        private void Start()
        {
            var players = PrototypeSetup.CreatePlayers();
            ProvinceSeed[] seeds;

            if (_useContinentalMap21)
            {
                seeds = PrototypeSetup.CreateContinentalMap21();
                EnsureContinentalViews(seeds);
                _crisisEnabled = true;
            }
            else
            {
                seeds = new ProvinceSeed[_provinceViews.Length];
                for (int i = 0; i < _provinceViews.Length; i++)
                {
                    seeds[i] = _provinceViews[i].ToSeed();
                }
            }

            var session = new PrototypeGameSession(new CoordinatorGameRules(_coordinator), players,
                PrototypeSetup.BuildProvinces(seeds, players), _crisisEnabled);
            var interaction = new HudInteraction(session);

            _hud.Initialize(session, interaction, _coordinator);
            _mapPresenter = new ProvinceMapPresenter(session, interaction, _provinceViews);
            _mapInput.Initialize(interaction, _hud, _mapCamera);
        }

        private void EnsureContinentalViews(ProvinceSeed[] seeds)
        {
            if (_provinceViews == null || _provinceViews.Length == 0) return;

            var positions = PrototypeSetup.CreateContinentalHexPositions21();
            var views = new ProvinceView[seeds.Length];
            var template = _provinceViews[0];

            for (int i = 0; i < seeds.Length; i++)
            {
                ProvinceView view;
                if (i < _provinceViews.Length && _provinceViews[i] != null)
                {
                    view = _provinceViews[i];
                }
                else
                {
                    view = Instantiate(template, template.transform.parent);
                }

                view.name = $"Province_{i}";
                view.transform.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
                view.ConfigureFromSeed(seeds[i]);
                views[i] = view;
            }

            for (int i = seeds.Length; i < _provinceViews.Length; i++)
            {
                if (_provinceViews[i] != null) _provinceViews[i].gameObject.SetActive(false);
            }

            _provinceViews = views;
        }

        private void OnDestroy()
        {
            _mapPresenter?.Dispose();
        }
    }
}