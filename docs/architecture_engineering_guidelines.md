# Projection: Architectural Governance & Coding Standards

This document serves as the mandatory engineering contract for all contributors and any AI coding assistants assisting
with the codebase. All code introduced into the repository must adhere strictly to these standards to maintain
performance, testability, and architectural integrity.

---

## 1. Architectural Philosophy: The Decoupled Core

The project enforces a strict boundary between pure business logic and the Unity engine runtime:

```
+-------------------------------------------------------------+
|                     PRESENTATION LAYER                      |
|   (Unity MonoBehaviours, UI Toolkit/UGUI, ParticleSystems)  |
+------------------------------+------------------------------+
                               |
                   Events & Method Invocations
                               |
                               v
+-------------------------------------------------------------+
|                   CORE DOMAIN LOGIC (POCO)                  |
|  - Zero UnityEngine imports (except Coordinator facade)     |
|  - Immutable records and explicit interfaces                |
|  - Pure math, deterministic state transformations           |
+-------------------------------------------------------------+
```

### Key Architectural Tenets:

1. **Core Domain Logic Must Be Engine-Agnostic:**
    - Classes residing under `Core/` must be pure C# (Plain Old CLR Objects / POCO).
    - Never import `UnityEngine` inside mathematical, rule-checking, or economic calculation services.
    - Core services must not depend on `GameObject`, `Transform`, `Time.deltaTime`, or `Coroutine`.
2. **MonoBehaviours as Adapters Only:**
    - `MonoBehaviour` scripts (such as `CombatCoordinator.cs`) function solely as bridges: they listen to engine
      inputs (clicks, colliders, animations), forward inputs to POCO services, and broadcast domain events to visual
      subscribers.
3. **No Direct Inter-System Coupling:**
    - The UI layer must never directly manipulate map tiles or economy balances. It listens to domain events (
      `event Action<T>`).
    - The Map/Movement layer must never instantiate UI alerts or calculate combat dice directly. It delegates combat to
      `ICombatService` and turn validation to `ICrisisDirector`.

---

## 2. Assembly Boundary Rules (`.asmdef`)

The project uses explicit Unity Assembly Definitions to enforce modularity and compile times:

1. **`Projection.Core` (`Assets/Scripts/Core/`):**
    - Contains all domain interfaces, pure calculation services, and state models.
    - `rootNamespace` is set to `""` to preserve direct path-to-namespace parity (`Core.Combat`, `Core.Crisis`).
    - Must never reference Editor assemblies or Test frameworks.
2. **`Projection.Tests.Editor` (`Assets/Tests/Editor/`):**
    - Platforms restricted to `Editor`.
    - Constrained to `UNITY_INCLUDE_TESTS`.
    - Never place game runtime logic inside this assembly.
3. **Engine/Gameplay Assembly (`Assembly-CSharp` or feature asmdef):**
    - References `Projection.Core`.
    - Contains scene-specific controllers, audio triggers, and view components.

---

## 3. Memory & Garbage Collection (GC) Optimization

Under the 60 FPS / turn-resolution budget, zero-allocation patterns during gameplay actions are mandatory:

### Rule 1: No LINQ in Hot Paths / Runtime Operations

Do not use `.Select()`, `.Where()`, `.ToList()`, `.OrderBy()`, or `.First()` during turn resolution, movement
pathfinding, or combat execution. LINQ creates heap-allocated enumerators and closures.

* **Incorrect:**
  ```csharp
  // Allocates enumerator + List on heap
  var wounded = units.Where(u => u.CurrentHp < u.MaxHp).ToList();
  ```
* **Correct:**
  ```csharp
  // Pre-allocate capacity, use indexed for-loop
  var wounded = new List<CombatUnit>(units.Count);
  for (int i = 0; i < units.Count; i++)
  {
      if (units[i].CurrentHp < units[i].MaxHp)
      {
          wounded.Add(units[i]);
      }
  }
  ```

### Rule 2: Pre-allocate Collections

Always specify initial capacity when creating lists inside services:

```csharp
var results = new List<CombatUnit>(capacity: incomingUnits.Count);
```

### Rule 3: Use Value Types and Immutability

- Represent transient operational parameters using `readonly struct` or immutable `record`.
- Keep records stateless; use the `with` expression to return updated snapshots rather than mutating in-memory models
  across multiple threads or phases.

---

## 4. Coding Standards & Conventions

### 1. Naming & Structure

- **Namespaces:** Strictly follow directory structure (`namespace Core.Combat`, `namespace Core.Economy`).
- **Interfaces:** Prefix with `I` (e.g., `ICombatService`, `IFactionRuleset`). Use Interface Segregation—prefer small,
  focused interfaces over monolithic ones.
- **Dependency Injection:** Inject dependencies via constructors for all Core services:
  ```csharp
  public class RootCombatService : ICombatService
  {
      private readonly IDiceRoller _diceRoller;
      public RootCombatService(IDiceRoller diceRoller) => _diceRoller = diceRoller;
  }
  ```

### 2. Polymorphism over Branching

Never write hardcoded conditional checks based on faction types when behavior differences can be represented in
rulesets.

* **Incorrect:**
  ```csharp
  if (player.Faction == FactionType.Militants) { fortDefense = 2; }
  ```
* **Correct:**
  ```csharp
  if (ruleset.ReceivesFortificationBonus) { fortDefense = 2; }
  ```

### 3. Compiler Polyfills & Compiler Diagnostics

- The project targets C# 9+ on Unity's .NET Standard runtime.
- Do not remove or alter `Assets/Scripts/Core/System/Runtime/CompilerServices/IsExternalInit.cs`.
- The directive `// ReSharper disable once CheckNamespace` in that file is intentional to support compiler requirements
  while suppressing IDE namespace inspections.

---

## 5. Instructions for AI Code Generation (Prompt Template)

When prompting Claude, ChatGPT, Cursor, GitHub Copilot, or Gemini to write C# code for this project, prepend the
following prompt snippet:

> **System Prompt Directive for AI:**
> "You are writing code for the turn-based strategy game 'Projection' in Unity.
> 1. Adhere to Clean Architecture: keep domain services decoupled from `UnityEngine`.
> 2. Zero-GC rule: do NOT use LINQ (`System.Linq`) in gameplay or combat calculations. Use indexed `for` loops with
     pre-allocated list capacities.
> 3. Use immutable `record` or `readonly struct` for state snapshots and DTOs.
> 4. Polymorphism over conditionals: use `IFactionRuleset` traits instead of branching on `FactionType`.
> 5. All tests must be written in NUnit for Unity Test Runner (EditMode)."

---

## 6. Pre-Commit Checklist

Before pushing any commit or opening a Pull Request:

- [ ] No `using System.Linq;` in hot-path calculation services.
- [ ] Core services have zero direct references to `MonoBehaviour` or `GameObject`.
- [ ] All new logic is verified via an EditMode unit test in `Assets/Tests/Editor/`.
- [ ] Unity Test Runner passes 100% green without entering Play Mode.
- [ ] Code compiles without any warnings or namespace resolution errors.