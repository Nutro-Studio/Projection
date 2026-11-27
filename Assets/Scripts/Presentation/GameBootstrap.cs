using System.Collections.Generic;
using Core.Combat;
using Core.Crisis;
using Core.Setup;
using Core.Simulation;
using Presentation.Config;
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
        [SerializeField] private ProvinceView _provincePrefab;
        [SerializeField] private Transform _mapRoot;
        [SerializeField] private bool _crisisEnabled = true;

        [Tooltip("Terrain tile per province, indexed by province id.")] [SerializeField]
        private Sprite[] _terrainTiles;

        private ProvinceMapPresenter _mapPresenter;

        private void Start()
        {
            var configAsset = Resources.Load<GameBalanceConfigAsset>("Config/GameBalanceConfig");
            var config = configAsset != null ? configAsset.ToCoreConfig() : GameBalanceConfig.Default;

            var rulesets = new Dictionary<FactionType, IFactionRuleset>
            {
                { FactionType.Militants, new MilitantRuleset() },
                { FactionType.Zealots, new ZealotRuleset() },
                { FactionType.Guerrillas, new GuerrillaRuleset() },
                { FactionType.Guilds, new GuildRuleset() }
            };

            var diceRoller = new SystemRandomDiceRoller();
            var combatService = new RootCombatService(diceRoller);
            var crisisDirector = new CrisisDirector(rulesets, config);

            var players = ContinentalMapBuilder.CreatePlayers(config);
            var seeds = ContinentalMapBuilder.CreateContinentalMap21();
            var views = SpawnContinentalViews(seeds);

            var session = new GameSession(
                combatService, crisisDirector, rulesets, diceRoller,
                players, ContinentalMapBuilder.BuildProvinces(seeds, players), _crisisEnabled, config);
            var interaction = new HudInteraction(session);

            _hud.Initialize(session, interaction, crisisDirector);
            _mapPresenter = new ProvinceMapPresenter(session, interaction, views);
            _mapInput.Initialize(interaction, _hud, _mapCamera);

            var edgeTarget = _mapRoot != null ? _mapRoot.gameObject : gameObject;
            var edgeRenderer = edgeTarget.GetComponent<MapEdgeRenderer>() ?? edgeTarget.AddComponent<MapEdgeRenderer>();
            edgeRenderer.BuildEdges(session.Provinces, views);

            var cameraFramer = GetComponent<CameraFramer>() ?? gameObject.AddComponent<CameraFramer>();
            cameraFramer.Frame(_mapCamera, views);
        }

        private ProvinceView[] SpawnContinentalViews(ProvinceSeed[] seeds)
        {
            var parent = _mapRoot != null ? _mapRoot : transform;
            var positions = MapLayout.CreateForceDirectedPositions(seeds);
            var views = new ProvinceView[seeds.Length];

            for (int i = 0; i < seeds.Length; i++)
            {
                var view = Instantiate(_provincePrefab, parent);
                view.name = $"Province_{i}";
                view.transform.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
                view.ConfigureFromSeed(seeds[i]);
                view.SetTerrain(_terrainTiles != null && i < _terrainTiles.Length ? _terrainTiles[i] : null);
                views[i] = view;
            }

            return views;
        }

        private void OnDestroy()
        {
            _mapPresenter?.Dispose();
        }
    }
}