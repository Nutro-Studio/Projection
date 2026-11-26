using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using Core.Setup;
using Core.Simulation;
using Presentation.Hud;
using Presentation.Map;
using UnityEngine;

namespace Presentation
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private HudController _hud;
        [SerializeField] private MapInputController _mapInput;
        [SerializeField] private Camera _mapCamera;
        [SerializeField] private ProvinceView[] _provinceViews;
        [SerializeField] private bool _useContinentalMap21 = true;
        [SerializeField] private bool _crisisEnabled = true;

        private ProvinceMapPresenter _mapPresenter;

        private void Start()
        {
            var rulesets = new Dictionary<FactionType, IFactionRuleset>
            {
                { FactionType.Militants, new MilitantRuleset() },
                { FactionType.Zealots, new ZealotRuleset() },
                { FactionType.Guerrillas, new GuerrillaRuleset() },
                { FactionType.Guilds, new GuildRuleset() }
            };

            var diceRoller = new SystemRandomDiceRoller();
            var combatService = new RootCombatService(diceRoller);
            var crisisDirector = new CrisisDirector(rulesets);

            var players = ContinentalMapBuilder.CreatePlayers();
            ProvinceSeed[] seeds;

            if (_useContinentalMap21)
            {
                seeds = ContinentalMapBuilder.CreateContinentalMap21();
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

            var session = new GameSession(
                combatService, crisisDirector, rulesets, diceRoller,
                players, ContinentalMapBuilder.BuildProvinces(seeds, players), _crisisEnabled);
            var interaction = new HudInteraction(session);

            _hud.Initialize(session, interaction, crisisDirector);
            _mapPresenter = new ProvinceMapPresenter(session, interaction, _provinceViews);
            _mapInput.Initialize(interaction, _hud, _mapCamera);
        }

        private void EnsureContinentalViews(ProvinceSeed[] seeds)
        {
            if (_provinceViews == null || _provinceViews.Length == 0) return;

            var positions = MapLayout.CreateContinentalHexPositions21();
            var views = new ProvinceView[seeds.Length];
            var template = _provinceViews[0];

            for (int i = 0; i < seeds.Length; i++)
            {
                ProvinceView view = (i < _provinceViews.Length && _provinceViews[i] != null)
                    ? _provinceViews[i]
                    : Instantiate(template, template.transform.parent);

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