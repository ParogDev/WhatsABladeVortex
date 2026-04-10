using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;
using Color = SharpDX.Color;

namespace WhatsABladeVortex;

public sealed class WhatsABladeVortexSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    // Display mode
    [Menu("Show Unleash seals", "Show the Greater Unleash seal pips.")]
    public ToggleNode ShowSeals { get; set; } = new(true);

    [Menu("Show snapshot bar", "Show a single bar for the freshest blade -- tracks when ALL stacks expire.")]
    public ToggleNode ShowSnapshotBar { get; set; } = new(true);

    [Menu("Show full cluster panel", "Show all clusters with per-blade bars. If off, only seals + snapshot bar are drawn.")]
    public ToggleNode ShowFullPanel { get; set; } = new(false);

    [Menu("Max Unleash seals", "Your max seals (Greater Unleash = 4, Caster Mastery +1, etc).")]
    public RangeNode<int> MaxSeals { get; set; } = new(4, 1, 10);

    [Menu("Cluster window (ms)", "Blades spawned within this window are grouped into the same cast cluster.")]
    public RangeNode<int> ClusterWindowMs { get; set; } = new(150, 30, 600);

    // Panel position
    [Menu("Panel X")]
    public RangeNode<int> PanelX { get; set; } = new(520, 0, 3840);

    [Menu("Panel Y")]
    public RangeNode<int> PanelY { get; set; } = new(200, 0, 2160);

    [Menu("Panel width")]
    public RangeNode<int> PanelWidth { get; set; } = new(400, 260, 700);

    // Alerts
    [Menu("Snapshot-lost: red flash", "Decaying red border flash when all blades expire.")]
    public ToggleNode AlertEdgeFlash { get; set; } = new(true);

    [Menu("Snapshot-lost: red header", "Flip the header to red SNAPSHOT LOST text.")]
    public ToggleNode AlertRedHeader { get; set; } = new(true);

    // Colors
    [Menu("Color: border")]
    public ColorNode BorderColor { get; set; } = new(new Color(90, 75, 26, 255));

    [Menu("Color: background")]
    public ColorNode BackgroundColor { get; set; } = new(new Color(10, 13, 20, 235));

    [Menu("Color: alive")]
    public ColorNode AliveColor { get; set; } = new(new Color(95, 224, 122, 255));

    [Menu("Color: warn")]
    public ColorNode WarnColor { get; set; } = new(new Color(224, 200, 58, 255));

    [Menu("Color: danger")]
    public ColorNode DangerColor { get; set; } = new(new Color(224, 64, 64, 255));

    [Menu("Color: muted text")]
    public ColorNode MutedColor { get; set; } = new(new Color(136, 146, 166, 255));

    [Menu("Color: text")]
    public ColorNode TextColor { get; set; } = new(new Color(232, 234, 240, 255));
}
