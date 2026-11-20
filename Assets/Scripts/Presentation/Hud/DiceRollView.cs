using System;
using Presentation.Prototype;
using UnityEngine.UIElements;

namespace Presentation.Hud
{
    /// <summary>
    /// Tumbles the 3d4 of both sides (faces 0-3), then settles on the rolls the combat service produced.
    /// </summary>
    public sealed class DiceRollView
    {
        private const float TumbleSeconds = 0.9f;
        private const float FaceChangeSeconds = 0.07f;
        private const int DiceCount = 3;
        private const int DieSides = 4;

        private readonly VisualElement _overlay;
        private readonly Label _title;
        private readonly Label _attackerTotal;
        private readonly Label _defenderTotal;
        private readonly Label _summary;
        private readonly Button _continue;
        private readonly Label[] _attackerDice = new Label[DiceCount];
        private readonly Label[] _defenderDice = new Label[DiceCount];
        private readonly Random _tumbleRandom = new();

        private BattleReport _report;
        private float _tumbleLeft;
        private float _nextFaceChange;

        public bool IsVisible => _report != null;

        public DiceRollView(VisualElement root)
        {
            _overlay = root.Q<VisualElement>("combat-overlay");
            _title = root.Q<Label>("combat-title");
            _attackerTotal = root.Q<Label>("attacker-total");
            _defenderTotal = root.Q<Label>("defender-total");
            _summary = root.Q<Label>("combat-summary");
            _continue = root.Q<Button>("combat-continue");
            _continue.clicked += Dismiss;

            for (int i = 0; i < DiceCount; i++)
            {
                _attackerDice[i] = root.Q<Label>($"attacker-die-{i + 1}");
                _defenderDice[i] = root.Q<Label>($"defender-die-{i + 1}");
            }

            _overlay.style.display = DisplayStyle.None;
        }

        public void Play(BattleReport report)
        {
            _report = report;
            _tumbleLeft = TumbleSeconds;
            _nextFaceChange = 0f;
            _title.text = HudText.BattleTitle(report);
            _attackerTotal.text = string.Empty;
            _defenderTotal.text = string.Empty;
            _summary.text = string.Empty;
            _continue.style.visibility = Visibility.Hidden;
            _overlay.style.display = DisplayStyle.Flex;
        }

        public void Tick(float deltaTime)
        {
            if (_report == null || _tumbleLeft <= 0f) return;

            _tumbleLeft -= deltaTime;
            if (_tumbleLeft <= 0f)
            {
                Settle();
                return;
            }

            _nextFaceChange -= deltaTime;
            if (_nextFaceChange <= 0f)
            {
                _nextFaceChange = FaceChangeSeconds;
                ShowRandomFaces(_attackerDice);
                ShowRandomFaces(_defenderDice);
            }
        }

        public void Dismiss()
        {
            if (_tumbleLeft > 0f)
            {
                _tumbleLeft = 0f;
                Settle();
                return;
            }

            _report = null;
            _overlay.style.display = DisplayStyle.None;
        }

        private void Settle()
        {
            var result = _report.Result;
            ShowFaces(_attackerDice, result.AttackerRolls);
            ShowFaces(_defenderDice, result.DefenderRolls);
            _attackerTotal.text = HudText.DiceTotal(result.AttackerRawSum);
            _defenderTotal.text = HudText.DiceTotal(result.DefenderRawSum);
            _summary.text = HudText.BattleSummary(_report);
            _continue.style.visibility = Visibility.Visible;
        }

        private void ShowRandomFaces(Label[] dice)
        {
            for (int i = 0; i < dice.Length; i++)
            {
                dice[i].text = _tumbleRandom.Next(0, DieSides).ToString();
            }
        }

        private static void ShowFaces(Label[] dice, (int roll1, int roll2, int roll3) rolls)
        {
            dice[0].text = rolls.roll1.ToString();
            dice[1].text = rolls.roll2.ToString();
            dice[2].text = rolls.roll3.ToString();
        }
    }
}