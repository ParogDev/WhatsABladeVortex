using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using ImGuiNET;

namespace WhatsABladeVortex;

/// <summary>
/// Settings panel: the generated SettingsMenu (the Overlay tab from the [Menu] headers and rows, colours included)
/// plus what it can't know about: a live status strip above the tabs, the sound test / folder buttons under the
/// Overlay settings and a live Debug tab (snapshot, blades, spectre, Minion Pact).
/// </summary>
public sealed class BladeVortexSettingsUi
{
    private readonly SettingsMenu _menu;
    private WhatsABladeVortexSettings? _settings;
    private BladeVortexRenderState? _state;

    // Blue-gold palette: blade/snapshot theme.
    private static readonly Vector4 Accent = new(0.42f, 0.66f, 1f, 1f);
    private static readonly Vector4 Gold = new(0.95f, 0.83f, 0.48f, 1f);
    private static readonly Vector4 LabelCol = new(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Vector4 Muted = new(0.52f, 0.55f, 0.60f, 1f);
    private static readonly Vector4 Good = new(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Vector4 Bad = new(0.9f, 0.25f, 0.25f, 1f);

    public BladeVortexSettingsUi()
    {
        _menu = new SettingsMenu { Accent = Accent, RootTabName = "Overlay" }
            .Banner(DrawStatusStrip)
            .Tab("Overlay", DrawSoundTools)
            .Tab("Debug", DrawDebugTab);
    }

    public void Draw(WhatsABladeVortexSettings settings, BladeVortexRenderState state)
    {
        _settings = settings;
        _state = state;
        _menu.Draw(settings);
    }

    private static Vector4 SnapshotColor(SnapshotState s) => s switch
    {
        SnapshotState.Active => Good,
        SnapshotState.Lost => Bad,
        _ => Gold
    };

    // ---- status strip above the tabs ----

    private void DrawStatusStrip()
    {
        var st = _state;
        if (st == null) { SettingsMenu.Dim("No live state yet"); return; }
        string snap = st.SnapshotState switch { SnapshotState.Active => "ACTIVE", SnapshotState.Lost => "LOST", _ => "READY" };
        _menu.Badge("Snapshot: " + snap, SnapshotColor(st.SnapshotState), "Minion Pact snapshot state");
        ImGui.SameLine();
        _menu.Badge($"{st.BladeCount} Blades", st.BladeCount > 0 ? Good : Muted, "new_new_blade_vortex buff entries on you");
        ImGui.SameLine();
        _menu.Badge($"{st.SealCount} Seals", st.SealCount > 0 ? Gold : Muted, "Greater Unleash seals (anticipation buff charges)");
        ImGui.SameLine();
        string spec = st.SpectreFound ? (st.SpectreAlive ? "Spectre Alive" : "Spectre Dead") : "No Spectre";
        _menu.Badge(spec, st.SpectreFound ? (st.SpectreAlive ? Good : Bad) : Muted, "Raise Spectre minion state");
        ImGui.Spacing();
    }

    // ---- under the Overlay settings: sound tools ----

    private void DrawSoundTools()
    {
        var st = _state;
        _menu.SectionHeader("Sound test", "Play the configured snapshot-lost sound, or open the folder it is read from");
        if (_menu.AccentButton("Test sound")) st?.PlayTestSound?.Invoke();
        if (ImGui.IsItemHovered()) SettingsMenu.Tooltip("Play the configured sound at the current volume");
        ImGui.SameLine();
        if (_menu.AccentButton("Open Sounds folder")) OpenSoundsFolder(st?.SoundsDirectory);
        if (ImGui.IsItemHovered()) SettingsMenu.Tooltip(string.IsNullOrEmpty(st?.SoundsDirectory) ? "Sounds folder not known yet" : st!.SoundsDirectory);
        ImGui.Spacing();
    }

    private static void OpenSoundsFolder(string? dir)
    {
        if (string.IsNullOrEmpty(dir)) return;
        try
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }
        catch (Exception)
        {
            // a locked or unreachable folder: nothing to open, the tooltip shows the path
        }
    }

    // ---- Debug tab ----

    private void DrawDebugTab()
    {
        var st = _state;
        var s = _settings;
        if (st == null || s == null) { SettingsMenu.Dim("No live state yet"); return; }

        _menu.SectionHeader("Live State");
        Pair("Snapshot State", st.SnapshotState.ToString().ToUpperInvariant(), SnapshotColor(st.SnapshotState));
        Pair("Blade Count", $"{st.BladeCount}/10", Accent);
        Pair("Cluster Count", st.Tracker?.ClusterCount.ToString() ?? "--", Accent);
        Pair("Seals", $"{st.SealCount}/{Math.Max(1, s.MaxSeals.Value)}", st.SealCount > 0 ? Gold : Muted);

        _menu.SectionHeader("Spectre");
        Pair("Found", st.SpectreFound ? "yes" : "no", st.SpectreFound ? Good : Bad);
        string alive = st.SpectreAlive
            ? (st.SpectreMaxHp > 1 ? $"yes ({st.SpectreHpPct * 100f:F0}%)" : "yes (HP loading...)")
            : "DEAD";
        Pair("Alive", alive, st.SpectreAlive ? (st.SpectreMaxHp > 1 ? Good : Gold) : Bad);
        Pair("Max HP", st.SpectreMaxHp > 1 ? $"{st.SpectreMaxHp:N0}" : "--", st.SpectreMaxHp > 1 ? LabelCol : Muted);

        _menu.SectionHeader("Minion Pact");
        Pair("Linked", st.HasMinionPact ? $"yes ({st.MinionPactPermillage * 0.1f:F1}%)" : "not linked", st.HasMinionPact ? Good : Muted);
        string added = st.CalculatedAddedPhys > 0 ? $"~{st.CalculatedAddedPhys:N0}" : st.SpectreMaxHp <= 1 ? "(need HP)" : "--";
        Pair("Added Phys", added, st.CalculatedAddedPhys > 0 ? Accent : Muted);
    }

    private static void Pair(string label, string value, Vector4 valueCol)
    {
        SettingsMenu.Text(LabelCol, label + ":");
        ImGui.SameLine(Math.Max(140f, ImGui.GetContentRegionAvail().X * 0.4f));
        SettingsMenu.Text(valueCol, value);
    }
}
