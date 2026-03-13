# Copilot Instructions — CS2-Platter

## Project Overview
This is **Platter**, a Cities: Skylines 2 mod.
Platter introduces a new way for players to manage their city's zoning and building by implementing a custom "parcel" entity. Parcels are flexible, player-defined areas that can be freely placed, allowing for more precise control over zoning and building placement compared to the vanilla zone-block system.
In the base game, the zoning system is based on "blocks" that are automatically created alongside roads, each containing a grid of cells. Players can zone these blocks, but they have limited control over the position of the blocks. Parcels decouple the zoning system from the road network, allowing players to create custom-shaped zones that can be placed anywhere, even overlapping existing blocks. This enables more creative and efficient city layouts.

The mod has two major layers:

| Layer | Stack | Location |
|-------|-------|----------|
| **Backend (C#)** | .NET Framework 4.8, Unity ECS (Entities / Burst / Jobs), Harmony patching | `Platter/` |
| **Frontend (TypeScript)** | React 18, SCSS Modules, CS2 UI Modding SDK (`cs2/*` externals), webpack | `Platter/UI/` |

### Key Architectural Concepts

- **ECS (Entity Component System)**: All game-state data lives in ECS components (`Platter/Components/`). Systems (`Platter/Systems/`) run jobs to process entities. Many systems use Burst-compiled `IJob` / `IJobParallelForDefer` structs.
- **Parcels**: The core domain object. A parcel is an entity with `Parcel`, `ParcelData`, `ParcelSubBlock`, and related components. Parcels interact with vanilla `Block` / `Cell` / `ZoneType` entities.
- **Prefabs**: `ParcelPrefab` and `ParcelPlaceholderPrefab` (in `Game.Prefabs` namespace) define the prefab templates that the game instantiates.
- **Harmony Patches**: Located in `Platter/Patches/`, used to intercept and modify vanilla game systems (tool systems, bulldoze, etc.).
- **UI ↔ C# Bindings**: Two-way communication uses `ValueBindingHelper<T>` / `TriggerBinding` on the C# side and `TwoWayBinding<T>` / `TriggerBuilder` on the TS side, with the mod id `"Platter"` as the namespace.
- **Localization**: English strings are defined in `Platter/L10n/EnUsConfig.cs`.

---

## FAQ

**Q: What game is this mod for?**
A: Cities: Skylines II (CS2).

**Q: Why .NET Framework 4.8?**
A: CS2 mods target Unity's Mono runtime which requires `net48`. This is set in the `.csproj` and enforced by the CS2 modding toolchain (`Mod.props`).

**Q: How is the UI built?**
A: The UI is a React/TypeScript module built with webpack (`Platter/UI/`). It is compiled into a JS bundle and deployed alongside the C# DLL. The CS2 UI SDK injects it into the game's Coherent GT browser.

**Q: How do C# and TypeScript communicate?**
A: Through `ValueBinding` / `TriggerBinding` pairs. C# side: `ExtendedUISystemBase.CreateBinding` / `CreateTrigger`. TS side: `TwoWayBinding` class in `utils/bidirectionalBinding.ts`. The binding key convention is `"BINDING:<KEY>"` / `"TRIGGER:<KEY>"`.

**Q: What are the build configurations?**
A: `Debug` (profiler + debug defines), `Release` (Burst enabled), and `I18N` (debug + locale export).

---

## Goals

1. **Maintain ECS discipline**: All mutable game state must live in ECS components. Systems must use jobs for data processing wherever possible.
2. **Burst compatibility**: Code inside `[BurstCompile]` jobs must not use managed types, allocations, or virtual calls. Guard Burst-compiled structs with `#if USE_BURST` / `[BurstCompile]`.
3. **Minimal Harmony patches**: Only patch vanilla methods when absolutely necessary; prefer ECS-based solutions.
4. **Type-safe UI bindings**: Every new binding must have a matching C# `ValueBindingHelper<T>` and TS `TwoWayBinding<T>` declaration with the same key.
5. **Backward compatibility**: Serializable components implement `ISerializable`; changes to serialized data must be migration-safe.

---

## Guidelines

### C# Coding Conventions

- **Namespace per folder** — e.g., `Platter.Systems`, `Platter.Components`, `Platter.Utils`, `Platter.Constants`, `Platter.Patches`.
  - Exception: Prefabs live in `Game.Prefabs` namespace to integrate with the game's prefab system.
- **File header** — Every `.cs` file starts with the standard copyright header:
  ```csharp
  // <copyright file="FileName.cs" company="Luca Rager">
  // Copyright (c) Luca Rager. All rights reserved.
  // Licensed under the MIT license. See LICENSE file in the project root for full license information.
  // </copyright>
  ```
- **Using statements** — Wrapped in `#region Using Statements` / `#endregion` blocks at the top of the namespace.
- **Naming**:
  - Fields: `m_PascalCase` prefix (e.g., `m_ZoneSearchSystem`, `m_Log`).
  - ECS component fields: `m_camelCase` (e.g., `m_LotSize`, `m_RoadEdge`) following vanilla CS2 convention.
  - Constants: `PascalCase` or `UPPER_CASE` (match surrounding file).
  - System classes: `P_` prefix (e.g., `P_NewCellCheckSystem`, `P_UISystem`).
  - Partial class jobs: separate file per job struct, named `<SystemName>.<JobName>.cs`.
- **Brace style** — Opening brace on the **same line** as declaration (Egyptian/K&R style):
  ```csharp
  public class Foo : Bar {
      // ...
  }
  ```
- **Alignment** — The codebase uses column-aligned assignments where there are multiple declarations of the same kind. Respect existing alignment.
- **XML doc comments** — Use `<summary>` on public types and members. Use `<inheritdoc/>` for overrides.
- **StyleCop** — The project uses StyleCop with several suppressions (see `GlobalSuppressions.cs`). Do not add `this.` prefix, do not enforce `SA1308` / `SA1309` field naming rules, braceless single-line `if` blocks are permitted.

### TypeScript / UI Conventions

- **React 18** with **TypeScript** (`strict: true`, `ESNext` target).
- **CSS Modules** (`.module.scss`) for component styling.
- **File structure**: One component per file. Components in `Platter/UI/src/components/<feature>/`.
- **Bindings file**: All C#↔UI bindings are centralized in `gameBindings.ts`. Do not scatter `bindValue` / `trigger` calls.
- **Types**: Shared UI types in `Platter/UI/src/types.ts`.
- **Debug utilities**: Use `useValueWrap` (from `debug.ts`) instead of raw `useValue` during development for change tracking.
- **External modules** (`cs2/modding`, `cs2/api`, `cs2/ui`, `cs2/bindings`, `cs2/l10n`, `cs2/input`, `cs2/utils`) are provided by the game at runtime. They are declared as webpack externals — never bundle them.

### Testing

- **Unit tests** go in `Platter.Tests/` using NUnit 4. Test class names end with `Tests` (e.g., `ParcelUtilsTests`).
- **In-game integration tests** go in `Platter/Tests/Integration/` using the `TestScenario` base class and `TestRunner` helper. These are compile-gated behind `#if IS_DEBUG`.
- Tests that require Unity runtime APIs (e.g., `Hash128.Compute`) must be in-game tests, not unit tests.

---

## Context

### Folder Reference

| Folder | Purpose |
|--------|---------|
| `Platter/Components/` | ECS `IComponentData` structs (serializable game state) |
| `Platter/Constants/` | Static constants (dimensions, colors, mod metadata) |
| `Platter/Extensions/` | C# extension methods and UI binding helpers |
| `Platter/L10n/` | Localization dictionaries |
| `Platter/Patches/` | Harmony patches against vanilla game systems |
| `Platter/Prefabs/` | `PrefabBase` subclasses registered with the game |
| `Platter/Settings/` | `ModSetting` subclass for in-game options |
| `Platter/Systems/` | ECS `GameSystemBase` subclasses (core logic) |
| `Platter/Systems/Blocks/` | Systems managing zone block / cell interactions |
| `Platter/Systems/Buildings/` | Building↔parcel reference systems |
| `Platter/Systems/Parcels/` | Parcel lifecycle systems (init, update, search) |
| `Platter/Systems/Roads/` | Road connection systems |
| `Platter/Systems/Tool/` | Tool integration (snap, placeholder, zone generation) |
| `Platter/Systems/UI/` | UI-facing systems (bindings, overlays, info panels, tooltips) |
| `Platter/Tests/` | In-game integration tests and test utilities |
| `Platter/Utils/` | Pure utility classes (math, geometry, logging) |
| `Platter/UI/` | Frontend TypeScript/React project |
| `Platter/UI/src/components/` | React components organized by feature |
| `Platter/UI/src/utils/` | TS utility modules (binding helpers, triggers, CSS classes) |
| `Platter/UI/types/` | CS2 SDK type declarations (`.d.ts`) |
| `Platter.Tests/` | Out-of-game NUnit unit tests |

### External Dependencies

- **Game assemblies** — Referenced via `Mod.props` / `References.csproj` from the CS2 modding toolchain. These are not NuGet packages; they come from the local game installation.
- **Colossal.*** — Game framework libraries (logging, serialization, math, UI).
- **Unity.*** — Unity ECS packages (`Unity.Entities`, `Unity.Mathematics`, `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`).
- **HarmonyLib** — Used for runtime method patching.
- **CS2 UI SDK** — `cs2/modding`, `cs2/api`, `cs2/bindings`, `cs2/ui`, `cs2/l10n`, `cs2/input` — provided at runtime by the game.

---

## Workflow

### Making C# Changes

1. **Read the target file** and understand the existing patterns before editing.
2. **Add new components** in `Platter/Components/` with `ISerializable` if they carry persisted state.
3. **Add new systems** by subclassing `PlatterGameSystemBase`. Register them in `PlatterMod.RegisterSystems()`.
4. **Partial class jobs**: If adding a new job to an existing system, create a new file named `<SystemName>.<JobName>.cs`.
5. **Burst jobs**: Mark with `#if USE_BURST` / `[BurstCompile]`. Do not use managed types inside the job struct.
6. **Build verification**: After changes, build the solution to check for compilation errors. The project targets `net48` and uses `AllowUnsafeBlocks`.

### Making UI Changes

1. **Add bindings** in `gameBindings.ts` (TS) and the corresponding `P_UISystem` (C#) simultaneously.
2. **Add components** under `Platter/UI/src/components/<feature>/` with a matching `.module.scss` file.
3. **Register** new top-level components in `Platter/UI/src/index.tsx` using `moduleRegistry.append` or `moduleRegistry.extend`.
4. **Build the UI** with `npm run build` from the `Platter/UI/` directory (this is also triggered automatically as a post-build MSBuild target).

### Common Pitfalls

- Do not use managed types (strings, classes, LINQ) inside Burst-compiled job structs.
- The `Game.Prefabs` namespace is intentionally used for prefab classes so they integrate with the game's prefab discovery.
- Vanilla game APIs may change between CS2 patches — Harmony patches are the most fragile part of the codebase.
- When modifying serializable components, always maintain backward-compatible deserialization.
