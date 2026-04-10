# CLAUDE.md - WhatsABladeVortex

## Purpose

Tracks Blade Vortex blades via player buffs and visualizes the Minion Pact snapshot lifetime. The default HUD cannot distinguish individual BV casts or show per-blade timers -- each blade is a separate `new_new_blade_vortex` buff with its own timer. The plugin groups blades into temporal clusters, tracks the snapshot state machine (spectre sacrifice), and shows Unleash seal charges from the `anticipation` buff.

## Key Behavior

- BV blade detection: reads all `new_new_blade_vortex` buff entries from the player's `Buffs` component. Each entry is one blade with its own `Timer` and `MaxTime`.
- Cluster grouping: blades with timers within `ClusterWindowMs` (default 150ms) are grouped into the same cast cluster. This captures Greater Unleash releases where seals + base cast fire in the same tick.
- BladeTracker: stateless per-frame rebuild from buff data using object pooling. Pre-allocates pools of 10 `BladeInfo` and 10 `BladeCluster` instances to avoid GC pressure. Sorts blades by timer descending and groups into clusters by timer gap.
- Snapshot state machine: `Ready` (waiting for spectre sacrifice) -> `Active` (spectre died with blades present) -> `Lost` (all blades expired) -> `Ready` (spectre resurrected). Transition to Active requires `_prevSpectreAlive && !spectreAlive && hasBlades`.
- Spectre tracking: alive state determined by absence of `number_of_orb_spectres` buff (buff present = dead spectre). HP read from `raise_spectre` skill's `DeployedObjects` when alive.
- Minion Pact detection: reads `SupportMinionPactSacrificeNearbyDamageableMinionToGainPermillageOfMaxLifeAsPhysicalDamage` stat from the `blade_vortex` ActorSkill. Calculates added physical damage as `spectreMaxHp * permillage / 1000`.
- Unleash seal meter: reads `anticipation` buff's `BuffCharges`. Purely informational -- not BV stacks.
- Render state DTO: all Tick data is packed into a `BladeVortexRenderState` object, passed to both the overlay renderer and the custom settings UI.
- AreaChange clears all tracked state. Grace period guard skips Tick processing. Plugin sends no input.

## Architecture

Follows the established plugin pattern (`/buff-tracker`, `/player-monitor` skills):

1. **Tick()** -- reads all components (Buffs, Actor, Life on deployed minions), rebuilds blade clusters, runs snapshot state machine, builds `BladeVortexRenderState` DTO. No drawing.
2. **Render()** -- delegates to `SnapshotPanelRenderer` using cached render state. No component access.
3. **DrawSettings()** -- delegates to `BladeVortexSettingsUi` for custom ImGui settings panel with live data display.

## Files

- `WhatsABladeVortex.cs` -- main plugin. Tick (buff read, cluster rebuild, state machine), Render (calls renderer + edge flash), DrawSettings, AreaChange.
- `BladeTracker.cs` -- `BladeInfo`, `BladeCluster`, `BladeTracker` with object-pooled Rebuild, FindClusterBars, FindNextLoss.
- `SnapshotPanelRenderer.cs` -- pure draw of overlay panel. Accepts `BladeVortexRenderState` DTO. DrawCompact (seals + bars), DrawFullPanel (header, clusters, footer), DrawDebugWindow.
- `BladeVortexSettingsUi.cs` -- custom ImGui settings panel with tabbed layout (Overlay, Colors, Debug), pill toggles, sliders, color pickers, live state status bar.
- `WhatsABladeVortexSettings.cs` -- all settings with `[Menu]` parentIndex hierarchy: Display, Position, Alerts, Colors, Debug.
- `design/mock.html` -- frontend-design HTML reference showing alive and lost states.
- `Sounds/` -- drop your `snapshot_lost.wav` here.

## Gotchas

- `Color` defaults to `System.Drawing.Color` when `using SharpDX;` is present -- file must `using Color = SharpDX.Color;`.
- `ExileCore.Graphics` collides with `System.Drawing.Graphics`; renderer does `using Graphics = ExileCore.Graphics;`.
- `Buff.Charges` is obsolete; use `Buff.BuffCharges`.
- Do NOT treat the `anticipation` buff charges as a blade count -- it is Unleash seals.
- Spectre alive detection uses buff absence (`number_of_orb_spectres` not present = alive). `DeployedObjects.Entity.IsAlive` is unreliable.
- `Life.MaxHP` returns 1 when the component address is null; only update cached HP when value > 1.

## Verification

1. Restart the HUD after building (no auto-recompile).
2. Open plugin settings panel -- should show tabbed UI with Overlay/Colors/Debug tabs and live status bar.
3. Enter a map with at least one Raise Spectre minion alive.
4. Cast BV once -> panel should show blades in a cluster, snapshot state transitions correctly.
5. Let blades expire -> panel flips to LOST state, red edge flash decays.
6. Settings tab live data should update in real-time (blade count, seal count, spectre status, Minion Pact info).
