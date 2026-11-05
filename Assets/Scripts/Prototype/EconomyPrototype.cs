using System.Collections.Generic;
using System.Linq;
using Projection.Core;
using Projection.Core.Economy;
using Projection.Core.Map;
using Projection.Core.Turns;
using Projection.Core.Units;
using UnityEngine;

namespace Projection.Prototype
{
    /// <summary>
    /// Playable prototype of the economy / map / movement systems with a throwaway IMGUI front-end.
    /// Hotseat: Militants and Zealots alternate. Click a province to select it; in the Movement phase click a
    /// highlighted neighbour to move units there. Space advances the phase.
    /// The real HUD will replace this; only the Core systems are meant to stay.
    /// </summary>
    public class EconomyPrototype : MonoBehaviour
    {
        private const float RefWidth = 1280f;
        private const float RefHeight = 720f;
        private const float Cell = 120f;
        private const float Gap = 8f;
        private const float MapLeft = 16f;
        private const float MapTop = 56f;
        private const float PanelLeft = 920f;
        private const int LogLines = 9;

        private static readonly Color MilitantColor = new Color(0.30f, 0.45f, 0.75f);
        private static readonly Color ZealotColor = new Color(0.80f, 0.62f, 0.20f);
        private static readonly Color NeutralColor = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color SelectedBorder = Color.white;
        private static readonly Color TargetBorder = new Color(0.35f, 0.95f, 0.45f);

        private Match _match;
        private Province _selected;
        private int _moveCount = 1;
        private UnitType _moveType = UnitType.Military;
        private string _feedback = "";
        private bool _feedbackIsError;
        private readonly List<string> _log = new List<string>();

        private Texture2D _white;
        private GUIStyle _cellStyle, _titleStyle, _labelStyle, _smallStyle, _errorStyle, _okStyle;

        private void Awake()
        {
            _match = new Match();
            _match.State.Log += AddLog;
            _match.Turns.TurnStarted += _ => _selected = null;
            _match.Turns.StartGame();
        }

        private void AddLog(string line)
        {
            _log.Add(line);
            if (_log.Count > 200) _log.RemoveAt(0);
            Debug.Log("[Projection] " + line);
        }

        private void OnGUI()
        {
            EnsureStyles();

            float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            HandleKeys();
            DrawRect(new Rect(0, 0, RefWidth, RefHeight), new Color(0.11f, 0.12f, 0.14f));

            var turns = _match.Turns;
            GUI.Label(new Rect(MapLeft, 12, 880, 32),
                $"PROJECTION - economy & movement prototype   |   Round {turns.Round}   |   {turns.Current} - {PhaseLabel(turns.Phase)}",
                _titleStyle);

            foreach (var province in _match.State.Map.Provinces) DrawProvince(province);

            DrawPanel();
            DrawLog();
        }

        private void HandleKeys()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
            {
                NextPhase();
                e.Use();
            }
        }

        // ---------- map ----------

        private Rect CellRect(Province p) =>
            new Rect(MapLeft + p.GridX * (Cell + Gap), MapTop + p.GridY * (Cell + Gap), Cell, Cell);

        private void DrawProvince(Province p)
        {
            var rect = CellRect(p);
            var current = _match.Turns.Current;

            Color border = Color.black;
            if (p == _selected) border = SelectedBorder;
            else if (IsMoveTarget(p)) border = TargetBorder;

            DrawRect(rect, border);
            DrawRect(Inset(rect, 3), Color.Lerp(OwnerColor(p.Owner), Color.black, 0.35f));

            var units = _match.State.UnitsIn(p).ToList();
            var text = new System.Text.StringBuilder();
            text.Append("<b>").Append(p.Name).Append(p.IsCapital ? " (capital)" : "").Append("</b>\n");
            text.Append("<size=10>").Append(p.Owner != null ? p.Owner.Name : "Neutral").Append("</size>\n");

            if (p.HasFortification) text.Append("[Fort] ");
            if (p.HasChurch) text.Append("[Church] ");
            if (p.HasFortification || p.HasChurch) text.Append('\n');

            foreach (var group in units.GroupBy(u => (u.Owner, u.Type)))
            {
                int ready = group.Count(u => !u.HasMovedThisTurn);
                string tag = group.Key.Type == UnitType.Military ? "troops" : "missionary";
                text.Append($"{group.Key.Owner.Name.Substring(0, 3)} {tag} x{group.Count()}");
                if (group.Key.Owner == current && _match.Turns.Phase == TurnPhase.Movement)
                    text.Append($" ({ready} ready)");
                text.Append('\n');
            }

            GUI.Label(Inset(rect, 7), text.ToString(), _cellStyle);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) OnProvinceClicked(p);
        }

        private bool IsMoveTarget(Province p) =>
            _selected != null && _match.Turns.Phase == TurnPhase.Movement &&
            _match.State.Map.AreAdjacent(_selected, p) &&
            _match.Movement.ReadyUnits(_match.Turns.Current, _selected, _moveType).Count > 0;

        private void OnProvinceClicked(Province p)
        {
            if (IsMoveTarget(p))
            {
                Report(_match.Turns.Move(_selected, p, _moveCount, _moveType));
                _selected = p.Owner == _match.Turns.Current ? p : _selected;
                ClampMoveCount();
                return;
            }

            _selected = p == _selected ? null : p;
            ClampMoveCount();
        }

        // ---------- side panel ----------

        private void DrawPanel()
        {
            var turns = _match.Turns;
            var faction = turns.Current;
            float x = PanelLeft, y = MapTop, w = RefWidth - PanelLeft - 16;

            DrawRect(new Rect(x - 8, y - 8, w + 16, 400), new Color(0.17f, 0.18f, 0.21f));

            GUI.Label(new Rect(x, y, w, 24), $"<b><color=#{ColorUtility.ToHtmlStringRGB(OwnerColor(faction))}>{faction}</color></b>", _titleStyle);
            y += 28;
            GUI.Label(new Rect(x, y, w, 20), $"Treasury: <b>{faction.Treasury.Gold} G</b>     VP: <b>{faction.VictoryPoints}</b>     Provinces: {_match.State.Map.CountControlledBy(faction)}", _labelStyle);
            y += 22;
            foreach (var other in _match.State.Factions.Where(f => f != faction))
            {
                GUI.Label(new Rect(x, y, w, 18), $"{other}: {other.Treasury.Gold} G, {other.VictoryPoints} VP, {_match.State.Map.CountControlledBy(other)} prov.", _smallStyle);
                y += 18;
            }
            y += 8;

            GUI.Label(new Rect(x, y, w, 20), _selected == null ? "Select a province on the map." : $"Selected: <b>{_selected}</b>", _labelStyle);
            y += 26;

            if (turns.Phase == TurnPhase.Actions) y = DrawActions(x, y, w, faction);
            else if (turns.Phase == TurnPhase.Movement) y = DrawMovement(x, y, w);

            y = Mathf.Max(y + 8, MapTop + 300);
            if (GUI.Button(new Rect(x, y, w, 32), turns.Phase == TurnPhase.Actions ? "To Movement phase  [Space]" : "End turn  [Space]"))
                NextPhase();
            y += 40;

            if (!string.IsNullOrEmpty(_feedback))
                GUI.Label(new Rect(x, y, w, 44), _feedback, _feedbackIsError ? _errorStyle : _okStyle);

            DrawLedger(faction, PanelLeft, MapTop + 410, w);
        }

        private float DrawActions(float x, float y, float w, Faction faction)
        {
            GUI.Label(new Rect(x, y, w, 18), "Spend gold (in your selected province):", _smallStyle);
            y += 20;

            if (_selected == null) return y;

            y = ActionButton(x, y, w, $"Recruit military unit ({EconomyRules.MilitaryUnitCost} G)", faction, UnitType.Military);
            if (faction.Archetype == FactionArchetype.Zealots)
                y = ActionButton(x, y, w, $"Recruit missionary ({EconomyRules.MissionaryCost} G)", faction, UnitType.Missionary);
            if (faction.Archetype == FactionArchetype.Militants)
                y = BuildButton(x, y, w, $"Build fortification ({EconomyRules.FortificationCost} G, +{EconomyRules.FortificationVp} VP)", faction, BuildingType.Fortification);
            if (faction.Archetype == FactionArchetype.Zealots)
                y = BuildButton(x, y, w, $"Build church ({EconomyRules.ChurchCost} G, +{EconomyRules.ChurchVp} VP)", faction, BuildingType.Church);
            return y;
        }

        private float ActionButton(float x, float y, float w, string label, Faction faction, UnitType type)
        {
            GUI.enabled = _match.Economy.CanRecruit(faction, _selected, type).Success;
            if (GUI.Button(new Rect(x, y, w, 28), label)) Report(_match.Turns.Recruit(_selected, type));
            GUI.enabled = true;
            return y + 32;
        }

        private float BuildButton(float x, float y, float w, string label, Faction faction, BuildingType type)
        {
            GUI.enabled = _match.Economy.CanBuild(faction, _selected, type).Success;
            if (GUI.Button(new Rect(x, y, w, 28), label)) Report(_match.Turns.Build(_selected, type));
            GUI.enabled = true;
            return y + 32;
        }

        private float DrawMovement(float x, float y, float w)
        {
            if (_selected == null)
            {
                GUI.Label(new Rect(x, y, w, 36), "Select a province with your units, then click a green neighbour.", _smallStyle);
                return y + 40;
            }

            var faction = _match.Turns.Current;
            int military = _match.Movement.ReadyUnits(faction, _selected, UnitType.Military).Count;
            int missionaries = _match.Movement.ReadyUnits(faction, _selected, UnitType.Missionary).Count;

            if (missionaries > 0)
            {
                string[] options = { $"Military ({military})", $"Missionary ({missionaries})" };
                int chosen = GUI.Toolbar(new Rect(x, y, w, 26), _moveType == UnitType.Military ? 0 : 1, options);
                _moveType = chosen == 0 ? UnitType.Military : UnitType.Missionary;
                y += 32;
            }
            else
            {
                _moveType = UnitType.Military;
            }

            int ready = _moveType == UnitType.Military ? military : missionaries;
            ClampMoveCount();

            GUI.Label(new Rect(x, y + 4, 165, 20), $"Units to move: <b>{_moveCount}</b> / {ready}", _labelStyle);
            if (GUI.Button(new Rect(x + 170, y, 40, 26), "-")) _moveCount = Mathf.Max(1, _moveCount - 1);
            if (GUI.Button(new Rect(x + 214, y, 40, 26), "+")) _moveCount = Mathf.Min(Mathf.Max(1, ready), _moveCount + 1);
            if (GUI.Button(new Rect(x + 258, y, w - 258, 26), "All")) _moveCount = Mathf.Max(1, ready);
            y += 32;

            GUI.Label(new Rect(x, y, w, 36),
                ready > 0 ? "Click a green neighbour to move. Entering enemy troops starts a battle (placeholder 3d4)."
                          : "No units here can move this turn.", _smallStyle);
            return y + 40;
        }

        private void ClampMoveCount()
        {
            if (_selected == null) { _moveCount = 1; return; }
            int ready = _match.Movement.ReadyUnits(_match.Turns.Current, _selected, _moveType).Count;
            _moveCount = Mathf.Clamp(_moveCount, 1, Mathf.Max(1, ready));
        }

        private void DrawLedger(Faction faction, float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 18), "<b>Treasury ledger</b>", _labelStyle);
            y += 20;
            foreach (var tx in faction.Treasury.Ledger.Reverse().Take(8))
            {
                GUI.Label(new Rect(x, y, w, 16), tx.ToString(), _smallStyle);
                y += 16;
            }
        }

        // ---------- log ----------

        private void DrawLog()
        {
            float top = MapTop + 3 * (Cell + Gap) + 4;
            var rect = new Rect(MapLeft, top, 7 * (Cell + Gap) - Gap, RefHeight - top - 12);
            DrawRect(rect, new Color(0.07f, 0.07f, 0.09f));

            float y = rect.y + 4;
            foreach (var line in _log.Skip(Mathf.Max(0, _log.Count - LogLines)))
            {
                GUI.Label(new Rect(rect.x + 8, y, rect.width - 16, 17), line, _smallStyle);
                y += 17;
            }
        }

        // ---------- helpers ----------

        private void NextPhase()
        {
            _match.Turns.AdvancePhase();
            _feedback = "";
            ClampMoveCount();
        }

        private void Report(ActionResult result)
        {
            _feedback = result.Message;
            _feedbackIsError = !result.Success;
        }

        private Color OwnerColor(Faction f)
        {
            if (f == null) return NeutralColor;
            return f.Archetype == FactionArchetype.Militants ? MilitantColor : ZealotColor;
        }

        private static string PhaseLabel(TurnPhase phase) =>
            phase == TurnPhase.Actions ? "Actions & Recruitment" : phase == TurnPhase.Movement ? "Movement & Combat" : phase.ToString();

        private static Rect Inset(Rect r, float d) => new Rect(r.x + d, r.y + d, r.width - 2 * d, r.height - 2 * d);

        private void DrawRect(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (_white != null) return;

            _white = Texture2D.whiteTexture;
            _cellStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12, wordWrap = true, alignment = TextAnchor.UpperLeft };
            _cellStyle.normal.textColor = Color.white;
            _titleStyle = new GUIStyle(_cellStyle) { fontSize = 17, wordWrap = false };
            _labelStyle = new GUIStyle(_cellStyle) { fontSize = 13 };
            _smallStyle = new GUIStyle(_cellStyle) { fontSize = 11 };
            _smallStyle.normal.textColor = new Color(0.8f, 0.8f, 0.85f);
            _errorStyle = new GUIStyle(_labelStyle);
            _errorStyle.normal.textColor = new Color(1f, 0.45f, 0.4f);
            _okStyle = new GUIStyle(_labelStyle);
            _okStyle.normal.textColor = new Color(0.55f, 0.95f, 0.6f);
        }
    }
}
