using System;
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
        private Label _apLabel;
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
        private VisualElement _splitRow;
        private Button _filterTypeButton;
        private Label _moveCountLabel;
        private Button _moveFewerButton;
        private Button _moveMoreButton;
        private VisualElement _dynamicActionsContainer;
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

            _apLabel = root.Q<Label>("ap-label");
            if (_apLabel == null)
            {
                _apLabel = new Label { name = "ap-label" };
                _apLabel.style.marginLeft = 12;
                _apLabel.style.marginRight = 12;
                _apLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _goldLabel.parent?.Insert(_goldLabel.parent.IndexOf(_goldLabel), _apLabel);
            }

            _provincePanel = root.Q<VisualElement>("province-panel");
            _provinceName = root.Q<Label>("province-name");
            _provinceOwner = root.Q<Label>("province-owner");
            _provinceUnits = root.Q<Label>("province-units");
            _provinceFort = root.Q<Label>("province-fort");
            _buyButton = root.Q<Button>("buy-button");
            _moveButton = root.Q<Button>("move-button");
            _splitRow = root.Q<VisualElement>("split-row");
            _moveCountLabel = root.Q<Label>("move-count-label");
            _moveFewerButton = root.Q<Button>("move-fewer-button");
            _moveMoreButton = root.Q<Button>("move-more-button");
            _endTurnButton = root.Q<Button>("end-turn-button");
            _statusLabel = root.Q<Label>("status-label");
            _banner = root.Q<Label>("ruleset-banner");
            _victoryOverlay = root.Q<VisualElement>("victory-overlay");
            _victoryLabel = root.Q<Label>("victory-label");
            _diceRoll = new DiceRollView(root);

            _filterTypeButton = root.Q<Button>("filter-type-button");
            if (_filterTypeButton == null)
            {
                _filterTypeButton = new Button { name = "filter-type-button" };
                _filterTypeButton.style.marginLeft = 6;
                _splitRow.Add(_filterTypeButton);
            }

            _dynamicActionsContainer = root.Q<VisualElement>("dynamic-actions-container");
            if (_dynamicActionsContainer == null)
            {
                _dynamicActionsContainer = new VisualElement { name = "dynamic-actions-container" };
                _dynamicActionsContainer.style.marginTop = 6;
                _provincePanel.Add(_dynamicActionsContainer);
            }

            // Static buy-button is replaced by faction-aware buttons in _dynamicActionsContainer
            _buyButton.style.display = DisplayStyle.None;

            _filterTypeButton.clicked += _interaction.CycleMoveUnitType;
            _moveButton.clicked += _interaction.BeginMove;
            _moveFewerButton.clicked += _interaction.DecreaseUnitsToMove;
            _moveMoreButton.clicked += _interaction.IncreaseUnitsToMove;
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
            _apLabel.text = HudText.ActionPoints(player.ActionPoints, PlayerProfile.BaseActionPoints);
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

            string summary = HudText.Units(province.Units.Count, _session.CountMovableUnits(selected));
            string breakdown = HudText.GarrisonBreakdown(province, _session.GetPlayer);
            string markers = HudText.ProvinceMarkers(province, _session.GetPlayer);

            _provinceUnits.text = breakdown.Length > 0 ? $"{summary}\n{breakdown}" : summary;
            _provinceFort.text = markers.Length > 0
                ? $"{HudText.Fort(province.HasFort)} | {markers}"
                : HudText.Fort(province.HasFort);

            int totalMovable = _session.CountMovableUnits(selected);
            int filteredMovable = _interaction.MovableUnitCount;
            int unitsToMove = _interaction.UnitsToMove;

            _moveButton.SetEnabled(_session.ValidateMoveOrigin(selected) == CommandResult.Ok && filteredMovable > 0);
            _moveButton.text = HudText.MoveButton(unitsToMove);
            _splitRow.style.display = totalMovable > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _filterTypeButton.text = HudText.UnitFilter(_interaction.MoveUnitTypeFilter);
            _moveCountLabel.text = HudText.UnitsToMove(unitsToMove, filteredMovable);
            _moveFewerButton.SetEnabled(unitsToMove > 1);
            _moveMoreButton.SetEnabled(unitsToMove < filteredMovable);

            RebuildDynamicActions(selected, province);
        }

        private void RebuildDynamicActions(int selectedProvinceId, ProvinceState province)
        {
            _dynamicActionsContainer.Clear();

            var player = _session.CurrentPlayer;
            bool hasAp = !_session.IsGameOver && player.ActionPoints > 0;
            bool isOwnProvince = province.OwnerId == player.PlayerId;
            bool hasOwnMilitary =
                _session.GetUnitsForPlayer(selectedProvinceId, player.PlayerId, UnitType.RegularMilitary).Count > 0;

            // 1. Dedicated Battle Triggers (for each co-existing enemy force with military units)
            if (hasOwnMilitary)
            {
                var enemyIds = _session.GetCoexistingEnemyIds(selectedProvinceId);
                for (int i = 0; i < enemyIds.Count; i++)
                {
                    int enemyId = enemyIds[i];
                    var enemy = _session.GetPlayer(enemyId);
                    if (enemy == null) continue;

                    AddActionButton(
                        HudText.AttackButton(enemy),
                        hasAp,
                        () => _interaction.Attack(enemyId));
                }
            }

            // 2. Faction-Specific & Counter-Action Buttons
            switch (player.Faction)
            {
                case FactionType.Militants:
                    AddActionButton(
                        HudText.BuyButton(),
                        hasAp && isOwnProvince && player.Gold >= PrototypeGameSession.UnitCost,
                        _interaction.BuyUnit);

                    if (hasOwnMilitary && province.RebellionCell != null && province.RebellionCell.IsOpenRebellion)
                    {
                        AddActionButton(HudText.MartialLawButton(), hasAp, _interaction.MartialLaw);
                    }

                    if (hasOwnMilitary && province.PresenceToken != null)
                    {
                        AddActionButton(HudText.InquisitionButton(), hasAp, _interaction.Inquisition);
                    }

                    AddCleanseButtonIfApplicable(province, player.PlayerId, hasAp && hasOwnMilitary);
                    break;

                case FactionType.Zealots:
                    bool hasMissionary =
                        _session.GetUnitsForPlayer(selectedProvinceId, player.PlayerId, UnitType.Missionary).Count > 0;
                    bool hasEnemyMilitary = _session.GetCoexistingEnemyIds(selectedProvinceId).Count > 0;

                    AddActionButton(
                        HudText.BuyMissionaryButton(),
                        hasAp && isOwnProvince && player.Gold >= PrototypeGameSession.MissionaryCost,
                        _interaction.BuyMissionary);

                    AddActionButton(
                        HudText.BuildChurchButton(),
                        hasAp && isOwnProvince && !province.HasChurch && player.Gold >= PrototypeGameSession.ChurchCost,
                        _interaction.BuildChurch);

                    AddActionButton(
                        HudText.ConsecrateButton(),
                        hasAp && hasMissionary && province.PresenceToken == null,
                        _interaction.Consecrate);

                    AddActionButton(
                        HudText.DivineConversionButton(),
                        hasAp && hasMissionary && hasEnemyMilitary,
                        _interaction.DivineConversion);

                    AddCleanseButtonIfApplicable(province, player.PlayerId, hasAp && hasOwnMilitary);
                    break;

                case FactionType.Guerrillas:
                    int cellCost = _session.GetRebellionCellCost(selectedProvinceId);
                    AddActionButton(
                        HudText.PlaceCellButton(cellCost),
                        hasAp && province.RebellionCell == null && player.Gold >= cellCost,
                        _interaction.PlaceRebellionCell);

                    if (province.RebellionCell != null
                        && province.RebellionCell.OwnerId == player.PlayerId
                        && !province.RebellionCell.IsOpenRebellion)
                    {
                        AddActionButton(HudText.InciteRebellionButton(), hasAp, _interaction.InciteRebellion);
                    }

                    break;

                case FactionType.Guilds:
                    AddActionButton(
                        HudText.BuyTraderButton(),
                        hasAp && isOwnProvince && player.Gold >= PrototypeGameSession.TraderCost,
                        _interaction.BuyTrader);
                    break;
            }

            // 3. Ruleset Shifts & Ascension (from CrisisDirector eligibility)
            AddShiftButtonsIfEligible(hasAp);
        }

        private void AddCleanseButtonIfApplicable(ProvinceState province, int playerId, bool canExecute)
        {
            bool hasEnemyChurch = province.HasChurch && province.ChurchOwnerId != playerId && !province.HasFort;
            bool hasEnemyToken = province.PresenceToken != null && province.PresenceToken.OwnerId != playerId;
            if (canExecute && (hasEnemyChurch || hasEnemyToken))
            {
                AddActionButton(HudText.CleanseButton(), true, _interaction.CleanseProvince);
            }
        }

        private void AddShiftButtonsIfEligible(bool hasAp)
        {
            FactionType[] targets =
            {
                FactionType.Militants,
                FactionType.Zealots,
                FactionType.Guilds
            };

            if (_session.CanAscend())
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    AddActionButton(
                        HudText.ShiftButton(target, isAscension: true),
                        hasAp,
                        () => _interaction.Ascend(target));
                }

                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (_session.CanVoluntaryShift(target))
                {
                    AddActionButton(
                        HudText.ShiftButton(target, isAscension: false),
                        hasAp,
                        () => _interaction.VoluntaryShift(target));
                }
            }
        }

        private void AddActionButton(string label, bool enabled, Action onClick)
        {
            var button = new Button(onClick) { text = label };
            button.style.marginTop = 3;
            button.SetEnabled(enabled);
            _dynamicActionsContainer.Add(button);
        }

        private void RefreshStatus()
        {
            string failure = HudText.Command(_interaction.LastResult);
            _statusLabel.text = failure.Length > 0
                ? failure
                : HudText.Hint(
                    _interaction.SelectedProvinceId != HudInteraction.NoSelection,
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