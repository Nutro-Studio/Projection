# Unit Testing & Verification Guide: Core Gameplay Systems

This guide outlines how to execute and verify the automated unit test suite for the **Combat Subsystem (Root 3d4)** and
**Ruleset Shift State Machine (Crisis & Ascension)**.

---

## 1. Quick Execution (Unity Test Runner)

1. Open the project in Unity.
2. In the top menu bar, navigate to:
   $$\text{Window} \longrightarrow \text{General} \longrightarrow \text{Test Runner}$$
3. In the Test Runner panel, select the **EditMode** tab.
4. Click **Run All** (or expand `Projection.Tests.Editor` and run individual fixtures).

> **Expected Execution Time:** $< 0.1\text{ s}$  
> **Target Status:** All tests **Green** (Passed).

---

## 2. Test Suite Overview & Coverage

All unit tests are located under:  
`Assets/Tests/Editor/` (isolated within the `Projection.Tests.Editor` assembly).

### A. Combat Resolution (`CombatServiceTests.cs`)

| Test Name                                                     | Rulebook Reference               | Verification Condition                                                                              |
|:--------------------------------------------------------------|:---------------------------------|:----------------------------------------------------------------------------------------------------|
| `ResolveBattle_AppliesRootCap_WhenRollExceedsUnitCount`       | Root Cap Rule (§4)               | If attacker rolls sum $\ge 10$ with only 2 units, effective hits must be capped at 2.               |
| `ResolveBattle_DefenderReceivingFortBonus_TakesTwoHitsToKill` | Fortification Armor (+1 HP) (§4) | Fortified Militant defenders must absorb 1 hit and survive with 1 HP remaining.                     |
| `ResolveBattle_TiesAwardedToDefender_AttackerMustRetreat`     | Casualties & Retreat (§4)        | Equal casualties result in defender victory; attacker is flagged with `AttackerMustRetreat = true`. |

---

### B. Ruleset Shift & Crisis State Machine (`CrisisDirectorTests.cs`)

| Test Name                                                                | Rulebook Reference             | Verification Condition                                                                                                                      |
|:-------------------------------------------------------------------------|:-------------------------------|:--------------------------------------------------------------------------------------------------------------------------------------------|
| `CanTriggerCrisis_ReturnsTrue_WhenLandedFactionDropsBelowThreeProvinces` | Forced Crisis Trigger (§2, §6) | Landed faction with $< 3$ provinces returns `true`; $\ge 3$ provinces returns `false`.                                                      |
| `CanTriggerCrisis_ReturnsFalse_ForNonLandedFaction`                      | Faction Archetypes (§5)        | Non-landed factions (Guerrillas) never trigger landed crisis checks.                                                                        |
| `ExecuteForcedCrisis_AppliesTwentyPercentVpPenaltyAndZeroesGold`         | Crisis Consequences (§6)       | Treasury wiped to $0\text{ Gold}$, Victory Points reduced by $-20\%$ ($\lfloor\text{VP} \times 0.8\rfloor$), units converted to Guerrillas. |
| `ExecuteAscension_AppliesTenPercentBonusAndTransitionsToTargetFaction`   | Statehood Ascension (§2, §6)   | Guerrillas with $\ge 5$ provinces transition to target landed state with $+10\%$ VP bonus ($\lfloor\text{VP} \times 1.1\rfloor$).           |

---

## 3. Command-Line / Headless Execution (Optional / CI)

To run the unit tests directly from the terminal without opening the Unity Editor GUI:

### Windows (PowerShell)

```powershell
& "C:\Program Files\Unity\Hub\Editor\<YOUR_UNITY_VERSION>\Editor\Unity.exe" `
  -batchmode `
  -projectPath . `
  -runTests `
  -testPlatform EditMode `
  -testResults ./test-results.xml `
  -logFile ./unity-test.log
```

### macOS

```bash
/Applications/Unity/Hub/Editor/<YOUR_UNITY_VERSION>/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath . \
  -runTests \
  -testPlatform EditMode \
  -testResults ./test-results.xml \
  -logFile ./unity-test.log
```

---

## 4. Troubleshooting Checklist

* **Tests do not show up in Test Runner:**
    * Ensure `Projection.Tests.Editor.asmdef` references `Projection.Core` and has the `UNITY_INCLUDE_TESTS` define
      constraint enabled.
* **Compiler error on `IsExternalInit`:**
    * Confirm `Assets/Scripts/Core/System/Runtime/CompilerServices/IsExternalInit.cs` is present and contained inside
      the `Projection.Core.asmdef` folder hierarchy.
* **Test assertions fail on random values:**
    * All unit tests use a deterministic `MockDiceRoller` instead of `SystemRandomDiceRoller`. If writing new tests,
      never use random dice in assertion blocks.