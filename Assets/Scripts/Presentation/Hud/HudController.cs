using Core.Combat;
using Core.Crisis;
using Presentation.Map;
using Presentation.Prototype;
using UnityEngine;
using UnityEngine.UIElements;

namespace Presentation.Hud
{
    [RequireComponent(typeof(UIDocument))]
    public class HudController : MonoBehaviour
    {
        private const float BannerSeconds = 4f;

        private PrototypeGameSession _session;
        private HudInteraction _interaction;
        private CombatCoordinator _coordinator;
        private DiceRollView _diceRoll;

        private VisualElement _root;
        private VisualElement _playerFlag;
        private Label _playerLabel;
        private Label _goldLabel;
        private Label _vpLabel;
        private Label _turnLabel;
        private VisualElement _provincePanel;
        private Label _provinceName;
        private Label _provinceOwner;
        private Label _provinceUnits;
        private Label _provinceFort;
        private Button _buyButton;
        private Button _moveButton;
        private Button _endTurnButton;
        private Label _statusLabel;
        private Label _banner;
        private VisualElement _victoryOverlay;
        private Label _victoryLabel;
        private float _bannerLeft;

        public void Initialize(
            PrototypeGameSession session,
            HudInteraction interaction,
            CombatCoordinator coordinator)
        {
            _session = session;
            _interaction = interaction;
            _coordinator = coordinator;

            BindElements(GetComponent<UIDocument>().rootVisualElement);

            _session.StateChanged += Refresh;
            _session.BattleResolved += _diceRoll.Play;
            _interaction.Changed += Refresh;
            _coordinator.OnRulesetShifted += ShowRulesetShift;
            _coordinator.OnGameWon += ShowVictory;

            Refresh();
        }

        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            var panel = _root.panel;
            var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, flipped)) != null;
        }

        /// <summary>
        /// Space first finishes the dice presentation, so an open battle can't end the turn by accident.
        /// </summary>
        public void ConfirmOrEndTurn()
        {
            if (_diceRoll.IsVisible)
            {
                _diceRoll.Dismiss();
                return;
            }

            _interaction.EndTurn();
        }

        private void BindElements(VisualElement root)
        {
            _root = root;
            _root.pickingMode = PickingMode.Ignore;
            _playerFlag = root.Q<VisualElement>("player-flag");
            _playerLabel = root.Q<Label>("player-label");
            _goldLabel = root.Q<Label>("gold-label");
            _vpLabel = root.Q<Label>("vp-label");
            _turnLabel = root.Q<Label>("turn-label");
            _provincePanel = root.Q<VisualElement>("province-panel");
            _provinceName = root.Q<Label>("province-name");
            _provinceOwner = root.Q<Label>("province-owner");
            _provinceUnits = root.Q<Label>("province-units");
            _provinceFort = root.Q<Label>("province-fort");
            _buyButton = root.Q<Button>("buy-button");
            _moveButton = root.Q<Button>("move-button");
            _endTurnButton = root.Q<Button>("end-turn-button");
            _statusLabel = root.Q<Label>("status-label");
            _banner = root.Q<Label>("ruleset-banner");
            _victoryOverlay = root.Q<VisualElement>("victory-overlay");
            _victoryLabel = root.Q<Label>("victory-label");
            _diceRoll = new DiceRollView(root);

            _buyButton.text = HudText.BuyButton();
            _buyButton.clicked += _interaction.BuyUnit;
            _moveButton.clicked += _interaction.BeginMove;
            _endTurnButton.clicked += _interaction.EndTurn;

            _banner.style.display = DisplayStyle.None;
            _victoryOverlay.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            if (_session == null) return;

            _diceRoll.Tick(Time.deltaTime);

            if (_bannerLeft > 0f)
            {
                _bannerLeft -= Time.deltaTime;
                if (_bannerLeft <= 0f)
                {
                    _banner.style.display = DisplayStyle.None;
                }
            }
        }

        private void Refresh()
        {
            var player = _session.CurrentPlayer;
            _playerLabel.text = HudText.Player(player);
            _playerFlag.style.unityBackgroundImageTintColor = FactionPalette.For(player.Faction);
            _goldLabel.text = HudText.Gold(player.Gold);
            _vpLabel.text = HudText.VictoryPoints(player.VictoryPoints);
            _turnLabel.text = HudText.Turn(_session.TurnNumber);

            RefreshProvincePanel();
            RefreshStatus();
        }

        private void RefreshProvincePanel()
        {
            int selected = _interaction.SelectedProvinceId;
            bool hasSelection = selected != HudInteraction.NoSelection;
            _provincePanel.style.display = hasSelection ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasSelection) return;

            var province = _session.Provinces[selected];
            _provinceName.text = province.Name;
            _provinceOwner.text = HudText.Owner(_session.GetPlayer(province.OwnerId));
            _provinceUnits.text = HudText.Units(province.Units.Count, _session.CountMovableUnits(selected));
            _provinceFort.text = HudText.Fort(province.HasFort);

            bool isOwnProvince = province.OwnerId == _session.CurrentPlayer.PlayerId;
            _buyButton.SetEnabled(isOwnProvince && _session.CurrentPlayer.Gold >= PrototypeGameSession.UnitCost);
            _moveButton.SetEnabled(_session.ValidateMoveOrigin(selected) == CommandResult.Ok);
        }

        private void RefreshStatus()
        {
            string failure = HudText.Command(_interaction.LastResult);
            _statusLabel.text = failure.Length > 0
                ? failure
                : HudText.Hint(_interaction.SelectedProvinceId != HudInteraction.NoSelection,
                    _interaction.IsTargetingMove);
        }

        private void ShowRulesetShift(CrisisShiftResult shift)
        {
            _banner.text = HudText.RulesetShift(shift);
            _banner.style.display = DisplayStyle.Flex;
            _bannerLeft = BannerSeconds;
        }

        private void ShowVictory(PlayerProfile winner)
        {
            _victoryLabel.text = HudText.Victory(winner);
            _victoryOverlay.style.display = DisplayStyle.Flex;
        }

        private void OnDestroy()
        {
            if (_session == null) return;

            _session.StateChanged -= Refresh;
            _session.BattleResolved -= _diceRoll.Play;
            _interaction.Changed -= Refresh;
            _coordinator.OnRulesetShifted -= ShowRulesetShift;
            _coordinator.OnGameWon -= ShowVictory;
        }
    }
}
