using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;
using Color = SharpDX.Color;

namespace WhatsABladeVortex;

public sealed class WhatsABladeVortexSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    // -- Display --
    [Menu("Display", 100)]
    public EmptyNode DisplayHeader { get; set; }

    [Menu("Show Unleash seals", "Show the Greater Unleash seal pips.", 101, 100)]
    public ToggleNode ShowSeals { get; set; } = new(true);

    [Menu("Show snapshot bar", "Show a single bar for the freshest blade -- tracks when ALL stacks expire.", 102, 100)]
    public ToggleNode ShowSnapshotBar { get; set; } = new(true);

    [Menu("Show full cluster panel", "Show all clusters with per-blade bars. If off, only seals + snapshot bar are drawn.", 103, 100)]
    public ToggleNode ShowFullPanel { get; set; } = new(false);

    [Menu("Max Unleash seals", "Your max seals (Greater Unleash = 4, Caster Mastery +1, etc).", 104, 100)]
    public RangeNode<int> MaxSeals { get; set; } = new(4, 1, 10);

    [Menu("Cluster window (ms)", "Blades spawned within this window are grouped into the same cast cluster.", 105, 100)]
    public RangeNode<int> ClusterWindowMs { get; set; } = new(150, 30, 600);

    // -- Position --
    [Menu("Position", 200)]
    public EmptyNode PositionHeader { get; set; }

    [Menu("Panel X", 201, 200)]
    public RangeNode<int> PanelX { get; set; } = new(520, 0, 3840);

    [Menu("Panel Y", 202, 200)]
    public RangeNode<int> PanelY { get; set; } = new(200, 0, 2160);

    [Menu("Panel width", 203, 200)]
    public RangeNode<int> PanelWidth { get; set; } = new(400, 260, 700);

    // -- Alerts --
    [Menu("Alerts", 300)]
    public EmptyNode AlertsHeader { get; set; }

    [Menu("Snapshot-lost: red flash", "Decaying red border flash when all blades expire.", 301, 300)]
    public ToggleNode AlertEdgeFlash { get; set; } = new(true);

    [Menu("Snapshot-lost: red header", "Flip the header to red SNAPSHOT LOST text.", 302, 300)]
    public ToggleNode AlertRedHeader { get; set; } = new(true);

    // -- Colors --
    [Menu("Colors", 400)]
    public EmptyNode ColorsHeader { get; set; }

    [Menu("Border", 401, 400)]
    public ColorNode BorderColor { get; set; } = new(new Color(90, 75, 26, 255));

    [Menu("Background", 402, 400)]
    public ColorNode BackgroundColor { get; set; } = new(new Color(10, 13, 20, 235));

    [Menu("Alive", 403, 400)]
    public ColorNode AliveColor { get; set; } = new(new Color(95, 224, 122, 255));

    [Menu("Warn", 404, 400)]
    public ColorNode WarnColor { get; set; } = new(new Color(224, 200, 58, 255));

    [Menu("Danger", 405, 400)]
    public ColorNode DangerColor { get; set; } = new(new Color(224, 64, 64, 255));

    [Menu("Muted text", 406, 400)]
    public ColorNode MutedColor { get; set; } = new(new Color(136, 146, 166, 255));

    [Menu("Text", 407, 400)]
    public ColorNode TextColor { get; set; } = new(new Color(232, 234, 240, 255));

    [Menu("Snapshot active", 408, 400)]
    public ColorNode SnapshotActiveColor { get; set; } = new(new Color(106, 169, 255, 255));

    [Menu("Snapshot ready", 409, 400)]
    public ColorNode SnapshotReadyColor { get; set; } = new(new Color(200, 168, 85, 255));

    // -- Debug --
    [Menu("Debug", 500)]
    public EmptyNode DebugHeader { get; set; }

    [Menu("Show debug window", "Show spectre, Minion Pact, and snapshot state diagnostics.", 501, 500)]
    public ToggleNode ShowDebugWindow { get; set; } = new(false);
}
