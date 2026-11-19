# #18 Victory Point (VP) Economy Overhaul & Pacing Recalibration: Design Decisions

Branch: `feature/18-vp-economy` (on top of `feature/14-guild-mechanics`, because #18 also covers Guild scoring).
Sources: issue #18 (scope), Rulebook §2 (Phase 4) and §5 (faction scoring).

## Decisions

| Topic | Decision | Source / reason |
|---|---|---|
| Militants | Upkeep: +1 VP per 3 controlled provinces. Events: +2 VP per captured province, +2 VP per crushed open rebellion (Martial Law), +1 VP per Fortification built. | Issue + Rulebook §5.1. |
| Zealots | Upkeep: +1 VP per province holding their own Religious Presence token. Event: +2 VP per Church built. | Issue + Rulebook §5.2. |
| Guilds | Unchanged from #14: +1 VP per Trading Post, +1 VP per 10 Gold, +2 VP per route entering ≥ 3 provinces. | Issue + Rulebook §5.3. |
| Guerrillas | Upkeep: +1 VP per province in **open** Rebellion. Event: +2 VP when they hold an open Rebellion zone against an attack. | Issue + Rulebook §5.4 ("active Rebellion"). Undercover cells do not count as active. |
| Flat +1 VP per province | Removed for every faction. | Issue: the flat rule is the problem being fixed. |
| Where scoring lives | `IFactionRuleset.CalculateUpkeepVictoryPoints(UpkeepState)` and `IFactionRuleset.ScoreEvent(ScoringEvent)`. The session only reports what happened. | Architecture guide, no branching on `FactionType`. |
| When upkeep VP are scored | At `EndTurn` (Phase 4), after revenue, so "Gold banked" includes this turn's income. | Rulebook §2. |
| Capture | +2 VP when the current player takes hegemony of a province from another player (a move or a conversion). It also counts when the current player wins a battle in a province that co-existing armies had left neutral. Each capture is scored once. | Rulebook: "capturing a province from another player". With co-existence, moving into a defended province makes it neutral first, so without the battle case Militants would almost never score a capture. |
| Defending a rebellion | +2 VP to the defender when a battle happens in their own open Rebellion province and the defender is not forced to retreat. Any attacker counts. | Rulebook: "successfully defending an active Rebellion zone against an imperial assault". |
| Victory | Event VP end the game the moment a player reaches 50. | Rulebook §7 ("during any phase"). |

## Open questions

1. **"+1 VP per Fortification built" never fires.** There is no action that builds Fortifications, and no issue asks for one. The rule is in `MilitantRuleset` and will apply once a build action exists.
2. **Pacing (25–40 turns to 50 VP) is not yet verified.** The values are the issue's values. Honest tuning needs simulated games, which is the headless mode of #20. A rough estimate for the 21-province map:
   - Militants: about 2 VP per turn from 8 provinces, plus captures, so roughly 15–25 turns.
   - Zealots: a Consecrate costs 1 AP, so they can gain several tokens per turn. That could be **too fast** (10+ VP per turn after about 5–6 turns).
   - Guilds: about 2–3 VP per turn plus routes, so likely **too slow**.
   - Guerrillas: depends on open rebellions surviving Martial Law.
3. Should **undercover cells** score anything, or only open rebellions (current behaviour)?
4. **Verification.** EditMode tests were run through NUnit against the Unity DLLs (`dotnet test`), not yet in the Unity Test Runner.
