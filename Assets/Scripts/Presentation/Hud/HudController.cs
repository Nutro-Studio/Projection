using System.Collections.Generic;
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
        private const int ActionButtonCount = 10;

        private static readonly UnitType[] UnitTypes = (UnitType[])System.Enum.GetValues(typeof(UnitType));

        private static readonly FactionType[] ShiftTargets =
        {
            FactionType.Militants, FactionType.Zealots, FactionType.Guilds
        };

        private readonly List<HudAction> _actions = new(ActionButtonCount);
        private readonly HudAction[] _shownActions = new HudAction[ActionButtonCount];
        private readonly FactionType[] _shownShifts = new FactionType[3];

        private PrototypeGameSession _session;
        private HudInteraction _interaction;
        private CombatCoordinator _coordinator;
        private DiceRollView _diceRoll;

        private VisualElement _root;
        private VisualElement _playerFlag;
        private Label _playerLabel;
        private Label _goldLabel;
        private Label _vpLabel;
        private Label _apLabel;
        private Label _turnLabel;
        private VisualElement _provincePanel;
        private Label _provinceName;
        private Label _provinceOwner;
        private Label _provinceUnits;
        private Label _provinceStructures;
        private Button _moveButton;
        private VisualElement[] _splitRows;
        private Label[] _moveCountLabels;
        private Button[] _moveFewerButtons;
        private Button[] _moveMoreButtons;
        private Button[] _actionButtons;
        private Button[] _shiftButtons;
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
            _apLabel = root.Q<Label>("ap-label");
            _turnLabel = root.Q<Label>("turn-label");
            _provincePanel = root.Q<VisualElement>("province-panel");
            _provinceName = root.Q<Label>("province-name");
            _provinceOwner = root.Q<Label>("province-owner");
            _provinceUnits = root.Q<Label>("province-units");
            _provinceStructures = root.Q<Label>("province-structures");
            _moveButton = root.Q<Button>("move-button");
            _endTurnButton = root.Q<Button>("end-turn-button");
            _statusLabel = root.Q<Label>("status-label");
            _banner = root.Q<Label>("ruleset-banner");
            _victoryOverlay = root.Q<VisualElement>("victory-overlay");
            _victoryLabel = root.Q<Label>("victory-label");
            _diceRoll = new DiceRollView(root);

            BindSplitRows(root);
            _actionButtons = CreateButtonPool(root.Q<VisualElement>("action-list"), ActionButtonCount, null,
                OnActionClicked);
            _shiftButtons = CreateButtonPool(root.Q<VisualElement>("shift-list"), _shownShifts.Length,
                "shift-button", OnShiftClicked);

            _moveButton.clicked += _interaction.BeginMove;
            _endTurnButton.clicked += _interaction.EndTurn;

            _banner.style.display = DisplayStyle.None;
            _victoryOverlay.style.display = DisplayStyle.None;
        }

        private void BindSplitRows(VisualElement root)
        {
            int count = UnitTypes.Length;
            _splitRows = new VisualElement[count];
            _moveCountLabels = new Label[count];
            _moveFewerButtons = new Button[count];
            _moveMoreButtons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                var type = UnitTypes[i];
                _splitRows[i] = root.Q<VisualElement>($"split-row-{i}");
                _moveCountLabels[i] = root.Q<Label>($"move-count-label-{i}");
                _moveFewerButtons[i] = root.Q<Button>($"move-fewer-button-{i}");
                _moveMoreButtons[i] = root.Q<Button>($"move-more-button-{i}");
                root.Q<Label>($"split-label-{i}").text = HudText.UnitTypeName(type);
                _moveFewerButtons[i].clicked += () => _interaction.DecreaseUnitsToMove(type);
                _moveMoreButtons[i].clicked += () => _interaction.IncreaseUnitsToMove(type);
            }
        }

        private static Button[] CreateButtonPool(
            VisualElement container,
            int count,
            string extraClass,
            System.Action<int> onClick)
        {
            var buttons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var button = new Button(() => onClick(index)) { focusable = false };
                button.AddToClassList("action-button");
                if (extraClass != null) button.AddToClassList(extraClass);
                button.style.display = DisplayStyle.None;
                container.Add(button);
                buttons[i] = button;
            }

            return buttons;
        }

        private void OnActionClicked(int index) => _interaction.Execute(_shownActions[index]);

        private void OnShiftClicked(int index) => _interaction.RequestShift(_shownShifts[index]);

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
            _apLabel.text = HudText.ActionPoints(player.ActionPoints);
            _turnLabel.text = HudText.Turn(_session.TurnNumber);

            RefreshProvincePanel();
            RefreshShifts();
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
            _provinceUnits.text = HudText.Garrison(province, _session);
            _provinceStructures.text = HudText.Structures(province, _session);

            for (int i = 0; i < UnitTypes.Length; i++)
            {
                var type = UnitTypes[i];
                int movable = _interaction.GetMovableUnitCount(type);
                int unitsToMove = _interaction.GetUnitsToMove(type);
                _splitRows[i].style.display = movable > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _moveCountLabels[i].text = HudText.UnitsToMove(unitsToMove, movable);
                _moveFewerButtons[i].SetEnabled(unitsToMove > 0);
                _moveMoreButtons[i].SetEnabled(unitsToMove < movable);
            }

            int totalToMove = _interaction.UnitsToMove;
            _moveButton.text = HudText.MoveButton(totalToMove);
            _moveButton.SetEnabled(totalToMove > 0 && _session.ValidateMoveOrigin(selected) == CommandResult.Ok);

            _interaction.GetAvailableActions(_actions);
            for (int i = 0; i < _actionButtons.Length; i++)
            {
                bool isShown = i < _actions.Count;
                _actionButtons[i].style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!isShown) continue;

                _shownActions[i] = _actions[i];
                _actionButtons[i].text = HudText.Action(_actions[i], province, _session);
            }
        }

        private void RefreshShifts()
        {
            bool isGuerrilla = _session.CurrentPlayer.Faction == FactionType.Guerrillas;
            int shown = 0;
            for (int i = 0; i < ShiftTargets.Length; i++)
            {
                var target = ShiftTargets[i];
                bool available = isGuerrilla ? _session.CanDeclareAscension() : _session.CanShiftRuleset(target);
                if (!available) continue;

                bool isPending = _interaction.PendingShift == target;
                _shownShifts[shown] = target;
                _shiftButtons[shown].text = HudText.Shift(target, isGuerrilla, isPending);
                _shiftButtons[shown].EnableInClassList("shift-button--pending", isPending);
                _shiftButtons[shown].style.display = DisplayStyle.Flex;
                shown++;
            }

            for (int i = shown; i < _shiftButtons.Length; i++)
            {
                _shiftButtons[i].style.display = DisplayStyle.None;
            }
        }

        private void RefreshStatus()
        {
            string failure = HudText.Command(_interaction.LastResult);
            _statusLabel.text = failure.Length > 0 ? failure : HudText.Hint(_interaction);
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
