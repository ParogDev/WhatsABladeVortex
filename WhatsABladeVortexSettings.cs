using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;
using Color = SharpDX.Color;

namespace WhatsABladeVortex;

// Drawn by SettingsMenu (SettingsMenu.cs) on the Overlay tab: every EmptyNode is a section header and
// [Menu(index, parentIndex)] hangs rows under it. [Menu] gives the label and the description, [MenuUnit] the
// slider suffix. The flat layout is kept so saved settings files still load.
public sealed class WhatsABladeVortexSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    // -- Display --
    [Menu("Display", "What the overlay shows", 100)]
    public EmptyNode DisplayHeader { get; set; } = new();

    [Menu("Show Unleash seals", "Show the Greater Unleash seal pips.", 101, 100)]
    public ToggleNode ShowSeals { get; set; } = new(true);

    [Menu("Show snapshot bar", "Show a single bar for the freshest blade -- tracks when ALL stacks expire.", 102, 100)]
    public ToggleNode ShowSnapshotBar { get; set; } = new(true);

    [Menu("Show full cluster panel", "Show all clusters with per-blade bars. If off, only seals + snapshot bar are drawn.", 103, 100)]
    public ToggleNode ShowFullPanel { get; set; } = new(false);

    [Menu("Max Unleash seals", "Your max seals (Greater Unleash = 4, Caster Mastery +1, etc).", 104, 100)]
    public RangeNode<int> MaxSeals { get; set; } = new(4, 1, 10);

    [Menu("Cluster window", "Blades spawned within this window are grouped into the same cast cluster.", 105, 100)]
    [MenuUnit("ms")]
    public RangeNode<int> ClusterWindowMs { get; set; } = new(150, 30, 600);

    [Menu("Show duration", "Show remaining time for the oldest blade cluster.", 106, 100)]
    public ToggleNode ShowDuration { get; set; } = new(false);

    // -- Position --
    [Menu("Position", "Where the panel sits on screen", 200)]
    public EmptyNode PositionHeader { get; set; } = new();

    [Menu("Panel X", "Left edge of the panel, in screen px.", 201, 200)]
    [MenuUnit("px")]
    public RangeNode<int> PanelX { get; set; } = new(520, 0, 3840);

    [Menu("Panel Y", "Top edge of the panel, in screen px.", 202, 200)]
    [MenuUnit("px")]
    public RangeNode<int> PanelY { get; set; } = new(200, 0, 2160);

    [Menu("Panel width", "Width of the full cluster panel, in px.", 203, 200)]
    [MenuUnit("px")]
    public RangeNode<int> PanelWidth { get; set; } = new(400, 260, 700);

    // -- Colors --
    [Menu("Colors", "Overlay panel colours", 400)]
    public EmptyNode ColorsHeader { get; set; } = new();

    [Menu("Border", "Panel border.", 401, 400)]
    public ColorNode BorderColor { get; set; } = new(new Color(90, 75, 26, 255));

    [Menu("Background", "Panel background (alpha sets its transparency).", 402, 400)]
    public ColorNode BackgroundColor { get; set; } = new(new Color(10, 13, 20, 235));

    [Menu("Alive", "Full timer bars, the SNAPSHOT ACTIVE chip and a living spectre.", 403, 400)]
    public ColorNode AliveColor { get; set; } = new(new Color(95, 224, 122, 255));

    [Menu("Warn", "Timers under 1.5 s and half-empty bars.", 404, 400)]
    public ColorNode WarnColor { get; set; } = new(new Color(224, 200, 58, 255));

    [Menu("Danger", "Nearly empty bars, SNAPSHOT LOST and the ZONE warning.", 405, 400)]
    public ColorNode DangerColor { get; set; } = new(new Color(224, 64, 64, 255));

    [Menu("Muted text", "Secondary text (labels, counts).", 406, 400)]
    public ColorNode MutedColor { get; set; } = new(new Color(136, 146, 166, 255));

    [Menu("Text", "Main text.", 407, 400)]
    public ColorNode TextColor { get; set; } = new(new Color(232, 234, 240, 255));

    [Menu("Snapshot active", "Minion Pact line while it is linked.", 408, 400)]
    public ColorNode SnapshotActiveColor { get; set; } = new(new Color(106, 169, 255, 255));

    [Menu("Snapshot ready", "Snapshot state in the debug window while waiting for the spectre sacrifice.", 409, 400)]
    public ColorNode SnapshotReadyColor { get; set; } = new(new Color(200, 168, 85, 255));

    // -- Debug --
    [Menu("Debug", "Diagnostics (live values are on the Debug tab)", 500)]
    public EmptyNode DebugHeader { get; set; } = new();

    [Menu("Show debug window", "Show spectre, Minion Pact, and snapshot state diagnostics.", 501, 500)]
    public ToggleNode ShowDebugWindow { get; set; } = new(false);

    // -- Alerts -- (last, so the Sound test buttons drawn after the settings sit right under them)
    [Menu("Alerts", "What happens when the snapshot is lost (all blades expired)", 300)]
    public EmptyNode AlertsHeader { get; set; } = new();

    [Menu("Snapshot-lost: red flash", "Decaying red border flash when all blades expire.", 301, 300)]
    public ToggleNode AlertEdgeFlash { get; set; } = new(true);

    [Menu("Snapshot-lost: red header", "Flip the header to red SNAPSHOT LOST text.", 302, 300)]
    public ToggleNode AlertRedHeader { get; set; } = new(true);

    [Menu("Snapshot-lost: sound alert", "Play a .wav sound when all blades expire.", 303, 300)]
    public ToggleNode AlertSound { get; set; } = new(true);

    [Menu("Sound volume", "Volume for the snapshot-lost sound (0 = mute, 100 = full).", 304, 300)]
    [MenuUnit("%")]
    public RangeNode<int> SoundVolume { get; set; } = new(80, 0, 100);

    [Menu("Sound file", "Wav file in PoeHelper/Sounds/ (without extension).", 305, 300)]
    public TextNode SoundFile { get; set; } = new("snapshot_lost");
}
