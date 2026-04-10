# CLAUDE.md - WhatsABladeVortex

## Purpose

Tracks Blade Vortex blades as individual **deployed Effect entities** and visualizes the Minion Pact snapshot lifetime. Exists because the default HUD cannot show per-blade timers: blades are not a stacking buff, each one is a separate entity with its own ~4s lifetime. The in-game `anticipation` player buff that looks like BV stacks is actually Greater Unleash seals (internal gem id `SupportGemHandcastAnticipation`).

## Key behavior

- BV blade detection: `entity.GetComponent<Animated>()?.BaseAnimatedObjectEntity?.Path` containing `blade_vortex` (case-insensitive, works for base skill and MTX variants).
- Per-blade lifetime is predicted from spawn time (`EntityAdded` -> `DateTime.UtcNow`) plus the configured base duration * (1 + increased duration %). No LimitedLifespan read.
- Cluster grouping: blades spawned within `ClusterWindowMs` of an existing cluster's first cast join that cluster. Default 150 ms captures Greater Unleash releases (seals + base cast fire in the same tick).
- Snapshot state: `Alive` while at least one blade is tracked; transitions to `Lost` on the frame `tracker.Count` drops to 0. On transition, plays `Sounds/<SoundFile>` (user-supplied WAV), flashes a red viewport border, and paints the header red.
- Unleash seal meter reads the `anticipation` buff's `BuffCharges` (not legacy `Charges`). Displayed as pips above the cluster list. Purely informational - not treated as BV stacks.
- AreaChange clears all tracked blades. Grace period guard is applied in Tick. Plugin sends no input.

## Files

- `WhatsABladeVortex.cs` - main plugin. Tick (buff read + prune + state), Render (calls renderer + edge flash), EntityAdded/Removed, AreaChange, sound playback.
- `BladeTracker.cs` - `TrackedBlade`, `BladeCluster`, `BladeTracker` with add/remove/prune/clear.
- `SnapshotPanelRenderer.cs` - pure draw of panel (header chip, seal pips, clusters with gradient bars, footer).
- `WhatsABladeVortexSettings.cs` - all settings including per-alert toggles and color palette.
- `design/mock.html` - frontend-design HTML reference showing alive and lost states side by side.
- `Sounds/` - drop your `snapshot_lost.wav` here.

## Gotchas

- `Color` defaults to `System.Drawing.Color` when `using SharpDX;` is present - file must `using Color = SharpDX.Color;`.
- `ExileCore.Graphics` collides with `System.Drawing.Graphics`; renderer does `using Graphics = ExileCore.Graphics;`.
- `Buff.Charges` is obsolete; use `Buff.BuffCharges`.
- Do NOT treat the `anticipation` buff charges as a blade count - it is Unleash seals.
- The plugin never tries to read `LimitedLifespan`; predicted lifetime works because `EntityRemoved` fires reliably when the blade despawns, and `Prune` cleans up anything that slipped past expected expiry by more than 0.5s.

## Verification

1. Restart the HUD after building (no auto-recompile).
2. Enter a map with at least one Raise Spectre minion alive.
3. Cast BV once -> panel should show 1 blade in Cluster 1, snapshot alive.
4. Cast several more BVs in rapid succession -> Unleashed releases create multi-blade clusters, bars tick down independently.
5. Stop casting -> watch clusters drain; the footer highlights "next loss" with seconds remaining.
6. Let everything expire -> panel flips to `SNAPSHOT LOST`, plays sound (if WAV present), red edge flash decays over ~0.6s.
7. Cross-check via AI Bridge `deep:Effects:200` that the blade count matches.
