# What's a Blade Vortex?

> Track every individual Blade Vortex stack, your Minion Pact snapshot state, and get alerted the moment your damage falls off.

Part of the **WhatsA** plugin family for ExileApi.

## What It Does

- **Total stack counter** with the Blade Vortex skill icon as a background -- no more guessing how many blades are out
- **Snapshot timer bar** showing when the oldest cluster expires, with cluster grouping so Greater Unleash releases read as one cast
- **Unleash seal pips** -- a live readout of your `anticipation` charges so you know when the next cast will release seals
- **Snapshot-lost alert** -- red edge flash, "LOST" text, and an optional `.wav` sound fire the instant every blade has expired
- **Minion Pact "ZONE" warning** -- when you're running a single spectre for stacked minion life, the plugin detects when your snapshot is gone and the spectre is dead, so you know to zone before recasting

## Detection

The plugin reads the `new_new_blade_vortex` buff entries from your player buffs component -- each BV blade is a separate buff with its own timer, which is what lets the plugin distinguish individual stacks and cluster them by cast time. Minion Pact is detected via the `SupportMinionPactSacrificeNearbyDamageableMinionToGainPermillageOfMaxLifeAsPhysicalDamage` stat on the `blade_vortex` ActorSkill. Spectre alive state uses the `number_of_orb_spectres` buff (present = dead wisp, absent = alive).

## Failsafes

- Grace period guard: all processing is skipped during the zone-in invulnerability window, no false snapshot transitions
- `AreaChange` clears all tracked state so nothing carries over between zones
- Object-pooled cluster rebuild: zero GC allocations per tick, bounded at 10 blades / 10 clusters
- Spectre HP is only cached when `Life.MaxHP > 1` (the component returns 1 during load races)
- Snapshot sound alert silently no-ops if the `.wav` is missing; visual alerts still fire
- Plugin sends no input and makes no memory writes -- read-only observer

## Getting Started

1. Download and place in `Plugins/Source/Whats A Blade Vortex/`
2. HUD auto-compiles on next launch
3. Enable "Whats A Blade Vortex" in the plugin list
4. Cast Blade Vortex once in a zone with a Raise Spectre minion alive -- the panel will populate

## Settings

| Setting | What it does |
|---------|--------------|
| Show Unleash seals | Greater Unleash seal pip meter at the top of the compact panel |
| Show snapshot bar | Timer bar tracking the oldest blade cluster's remaining duration |
| Show full cluster panel | Expand to a detailed per-cluster view with individual blade bars |
| Show duration | Display the oldest cluster's remaining time in seconds next to the bar (off by default) |
| Max Unleash seals | Your character's max seal count (Greater Unleash = 4, +1 from Caster Mastery) |
| Cluster window (ms) | How close in time two casts have to be to count as one cluster |
| Panel X / Y / Width | Overlay position and size |
| Snapshot-lost: red flash | Decaying red border flash when every blade expires |
| Snapshot-lost: red header | Flip the header to red "SNAPSHOT LOST" text |
| Snapshot-lost: sound alert | Play a `.wav` when the snapshot drops |
| Sound file | Wav file name (without extension) from `PoeHelper/Sounds/` |
| Open Folder | Opens `PoeHelper/Sounds/` in Explorer, creating it if missing |

<details>
<summary>Technical Details</summary>

### Architecture

1. **Tick()** -- reads `Buffs`, `Actor`, and `Life` components, rebuilds blade clusters via `BladeTracker.Rebuild()`, runs the snapshot state machine, and builds a `BladeVortexRenderState` DTO. No drawing.
2. **Render()** -- delegates to `SnapshotPanelRenderer` using the cached DTO. Draws the compact or full panel plus the edge-flash overlay. No component access.
3. **DrawSettings()** -- delegates to `BladeVortexSettingsUi` for a custom tabbed ImGui panel with a live status bar.

### Snapshot State Machine

- `Ready` -- waiting for a spectre sacrifice
- `Active` -- spectre died while blades were present (snapshot captured)
- `Lost` -- all blades expired (added phys damage is gone)
- Back to `Ready` -- spectre resurrected (only happens on zone change with Minion Pact single-spectre setups)

Transition to `Active` requires `_prevSpectreAlive && !spectreAlive && hasBlades`, which captures the exact frame the spectre was sacrificed.

### Cluster Grouping

Blades are sorted by timer descending (newest first), then grouped by timer gap: any two blades whose timers differ by less than the configured cluster window are in the same cluster. This captures Greater Unleash releases where seal + base casts fire in the same tick but may land in slightly different frames.

### Skill Icon

The plugin ships a 64x64 PNG of the Blade Vortex skill icon sourced from PoEDB. It's loaded once in `Initialise()` via `Graphics.InitImage()` and drawn as a semi-transparent background behind the total stack count -- zero per-frame texture loading cost. The attempted path through `ActorSkill.EffectsPerLevel.SkillGemWrapper.ActiveSkill.Icon` returns garbage memory at the current offset, so the local PNG approach is both faster and reliable.

### Gotchas

- `Buff.Charges` is obsolete; use `Buff.BuffCharges`
- `anticipation` buff charges are Unleash seals, NOT blade counts
- Spectre alive detection uses `number_of_orb_spectres` buff absence, not `DeployedObjects.Entity.IsAlive` (which is unreliable)
- `Life.MaxHP` returns 1 when the component address is still loading; cache only when `> 1`
- `Color` defaults to `System.Drawing.Color` with `using SharpDX;` -- renderer files alias `using Color = SharpDX.Color;`

</details>

## About This Project

These plugins are built with AI-assisted development using Claude Code and the
ExileApiScaffolding (private development workspace) workspace.

The developer works professionally in cybersecurity and high-risk software --
AI compensates for a C# knowledge gap specifically, not engineering judgment.
Plugin data comes from the PoE Wiki and PoEDB data mining.

The focus is on UX: friction points and missing expected features that the
existing plugin ecosystem doesn't address. Every hour spent developing is an
hour not spent on league progression, so feedback is the best way to support
the project.

## WhatsA Plugin Family

| Plugin | Description |
|--------|-------------|
| **What's a Blade Vortex?** | Blade Vortex stack tracker with Minion Pact snapshot detection |
| [What's a Breakpoint?](https://github.com/ParogDev/WhatsABreakpoint) | Kinetic Fusillade attack speed breakpoint visualizer |
| [What's a Crowd Control?](https://github.com/ParogDev/WhatsACrowdControl) | OmniCC-style CC effect overlay with timers |
| [What's a Mirage?](https://github.com/ParogDev/WhatsAMirage) | League mechanic overlay for spawners, chests, and wishes |
| [What's a Tincture?](https://github.com/ParogDev/WhatsATincture) | Automated tincture management with burn stack tracking |
| [What's a Tooltip?](https://github.com/ParogDev/WhatsATooltip) | Shared rich tooltip service for WhatsA plugins |
| [What's an AI Bridge?](https://github.com/ParogDev/WhatsAnAiBridge) | File-based IPC for AI-assisted plugin development |
| [What's an Unbound Avatar?](https://github.com/ParogDev/WhatsAnUnboundAvatar) | Auto-activation for Avatar of the Wilds at 100 fury |

Built with ExileApiScaffolding (private development workspace)
