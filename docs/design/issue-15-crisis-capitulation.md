# #15 Crisis Escalation, Capitulation & Voluntary Abdication Matrix: Design Decisions

Branch: `feature/15-crisis-capitulation` (from `week3`).
Sources: issue #15 (scope), Rulebook §2 and §6 (details), Game Concept (§03, §04, §06).

## Decisions

| Topic | Decision | Source / reason |
|---|---|---|
| Crisis threshold | A landed faction collapses with **< 3** provinces. The issue says `≤ 3`; the code keeps `< 3`. | The Rulebook (§2, §6), the Game Concept (3×), the existing code, the tests and the testing guide all say `< 3`. With `≤ 3`, a Guild that shifts to a landed archetype at the allowed minimum of 3 provinces would collapse at the end of that same turn. |
| `Capitulate()` | Only landed factions (Militants, Zealots) can capitulate. It costs 1 AP, does not end the turn, and the result is flagged `IsCrisis = true, IsVoluntary = true`. | Issue + Rulebook §6 ("Any Landed Faction… or Capitulates"). The 1 AP cost is consistent with `VoluntaryShift` and `Ascend`. |
| Collapse consequences | Treasury → 0, −20% VP, and **every** unit stays on the board under the Guerrilla player as an insurgent. `OriginalFaction` is kept. No Rebellion cells are spawned. | Issue: "units become insurgents". Spawning rebellions appears only in the Game Concept, not in the issue, so it is left out. |
| Voluntary shift penalties | Militants lose 50% of military units and 30% of Fortifications; Zealots lose all Missionaries and 50% of Churches; Guilds lose 50% of the treasury. All shifts cost −20% VP. | Rulebook §6 matrix. |
| Where penalties live | `IFactionRuleset.VoluntaryShiftPenalty` (`ShiftPenalty` record). `CrisisDirector` no longer branches on `FactionType`. | `docs/architecture_engineering_guidelines.md`: "Polymorphism over Branching". |
| Rounding | Unit, Fortification and Church losses round down (3 troops lose 1, 7 forts lose 2). The Guild treasury keeps `floor(50%)`, as before. | The Rulebook does not say; this keeps the pre-existing Militant and Guild behaviour unchanged. |
| Which assets are lost | Units: the earliest in `PlayerProfile.Units` (province order) are kept. Fortifications and Churches: the lowest province id is razed first. | Deterministic and testable; the Rulebook does not say. |
| Board sync | After any shift, and after a crisis at `EndTurn`, the board matches the player's profile: dropped units leave, kept units take their updated record, and forts and churches are razed. | Issue: "execute all transformations seamlessly with board unit conversion". Before this, a crisis at `EndTurn` changed only the profile. |
| HUD | A `Capitulate -> Guerrillas` button sits next to the shift buttons. | No other issue covers UI for capitulation (#28 exposed shifts before `Capitulate` existed). |

## Open questions

1. **Guild → landed shift must also lose all Trading Posts** (Rulebook §6). Trading Posts arrive in #14, so this is added when #14 is rebased onto merged #15.
2. **"Mercenaries convert to permanent units"** (Rulebook §6, Guild shift). There are no Mercenaries: War Financing is not in any issue.
3. **Timing of voluntary shifts.** The Rulebook says "Voluntary (any upkeep)"; the pre-existing code allows them at any point of the turn for 1 AP. Is that intended?
4. **Should the player choose which units, Fortifications and Churches to lose** instead of the deterministic order?
5. **Verification.** EditMode tests were run through NUnit against the Unity 6000.3.28f1 DLLs (`dotnet test`), not yet in the Unity Test Runner, and the button has not been play-tested in the scene.
