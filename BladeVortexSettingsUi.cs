using System;
using System.Collections.Generic;
using System.Numerics;
using ExileCore;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Graphics = ExileCore.Graphics;

namespace WhatsABladeVortex;

/// <summary>
/// Custom ImGui settings panel for WhatsABladeVortex.
/// Blue-gold accent -- blade/snapshot theme.
/// Tabbed layout with live state display, following CC settings UI pattern.
/// </summary>
public sealed class BladeVortexSettingsUi
{
    private int _activeTab;
    private readonly Dictionary<string, float> _anims = new();

    private static readonly string[] Tabs = { "Overlay", "Colors", "Debug" };

    // -- Palette --
    private static uint Accent => Col(0.42f, 0.66f, 1f);      // blue
    private static uint AccentDim => Col(0.28f, 0.44f, 0.65f);
    private static uint Gold => Col(0.95f, 0.83f, 0.48f);      // gold
    private static uint Label => Col(0.88f, 0.88f, 0.88f);
    private static uint Desc => Col(0.38f, 0.40f, 0.44f);
    private static uint TabOff => Col(0.48f, 0.48f, 0.52f);
    private static uint CardBg => Col(0.05f, 0.05f, 0.07f, 1f);
    private static uint StatusGreen => Col(0.2f, 0.85f, 0.3f);
    private static uint StatusRed => Col(0.9f, 0.25f, 0.25f);
    private static uint StatusDim => Col(0.3f, 0.3f, 0.35f);
    private const float Row = 34f;

    // -- Entry point --

    public void Draw(WhatsABladeVortexSettings s, Graphics gfx, BladeVortexRenderState state)
    {
        var contentMin = ImGui.GetCursorScreenPos();
        float contentW = ImGui.GetContentRegionAvail().X;
        var dl = ImGui.GetWindowDrawList();

        // Tab bar
        float tabH = 26f;
        float tabW = contentW / Tabs.Length;

        for (int i = 0; i < Tabs.Length; i++)
        {
            var tMin = new Vector2(contentMin.X + i * tabW, contentMin.Y);
            var tMax = new Vector2(contentMin.X + (i + 1) * tabW, contentMin.Y + tabH);
            bool active = i == _activeTab;

            if (active)
                dl.AddRectFilled(tMin, tMax, WithAlpha(Accent, 0.12f));

            ImGui.SetCursorScreenPos(tMin);
            ImGui.InvisibleButton($"##bv_tab_{i}", tMax - tMin);
            bool hov = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked()) _activeTab = i;

            if (hov && !active)
                dl.AddRectFilled(tMin, tMax, WithAlpha(Accent, 0.06f));
            if (active)
                dl.AddLine(tMin with { Y = tMax.Y - 1 } + new Vector2(3, 0),
                    tMax - new Vector2(3, 1), Accent, 2f);

            uint tc = active ? Accent : (hov ? AccentDim : TabOff);
            CenterText(dl, Tabs[i], (tMin + tMax) * 0.5f, tc);
        }

        float sepY = contentMin.Y + tabH + 1;
        dl.AddLine(new Vector2(contentMin.X, sepY),
            new Vector2(contentMin.X + contentW, sepY), WithAlpha(Accent, 0.10f), 1f);

        // Status bar
        float statusY = sepY + 3;
        DrawStatusBar(dl, contentMin.X, contentW, statusY, state);
        float statusH = 20f;

        // Content area
        ImGui.SetCursorScreenPos(new Vector2(contentMin.X, sepY + statusH + 6));
        var avail = new Vector2(contentW, ImGui.GetContentRegionAvail().Y);
        ImGui.BeginChild("##bv_content", avail, ImGuiChildFlags.None, ImGuiWindowFlags.None);

        var cdl = ImGui.GetWindowDrawList();
        var cMin = ImGui.GetWindowPos();
        var cSz = ImGui.GetWindowSize();

        cdl.AddRectFilled(cMin, cMin + cSz, CardBg, 3f);
        float pulse = (float)(0.4 + 0.3 * Math.Sin(ImGui.GetTime() * 1.8));
        cdl.AddRect(cMin, cMin + cSz, WithAlpha(Accent, pulse * 0.18f), 3f, ImDrawFlags.None, 1f);

        float y = cMin.Y + 10;
        float x = cMin.X + 12;
        float cx = cMin.X + cSz.X * 0.50f;
        float sw = cSz.X * 0.40f;

        switch (_activeTab)
        {
            case 0: TabOverlay(cdl, s, x, cx, ref y, sw); break;
            case 1: TabColors(cdl, s, x, cx, ref y); break;
            case 2: TabDebug(cdl, s, x, cx, ref y, state); break;
        }

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.Dummy(new Vector2(1, 4));
        ImGui.EndChild();
    }

    // -- Status Bar --

    private static void DrawStatusBar(ImDrawListPtr dl, float startX, float width, float y, BladeVortexRenderState state)
    {
        float x = startX + 12;

        // Snapshot state
        string stateText = state.SnapshotState switch
        {
            SnapshotState.Active => "ACTIVE",
            SnapshotState.Lost => "LOST",
            _ => "READY"
        };
        uint stateDot = state.SnapshotState switch
        {
            SnapshotState.Active => StatusGreen,
            SnapshotState.Lost => StatusRed,
            _ => Gold
        };
        dl.AddCircleFilled(new Vector2(x + 4, y + 8), 4f, stateDot);
        string t1 = $"Snapshot: {stateText}";
        dl.AddText(new Vector2(x + 12, y + 1), stateDot, t1);
        x += ImGui.CalcTextSize(t1).X + 26;

        // Blade count
        uint bladeDot = state.BladeCount > 0 ? StatusGreen : StatusDim;
        dl.AddCircleFilled(new Vector2(x + 4, y + 8), 4f, bladeDot);
        string t2 = $"{state.BladeCount} Blades";
        dl.AddText(new Vector2(x + 12, y + 1), state.BladeCount > 0 ? Label : StatusDim, t2);
        x += ImGui.CalcTextSize(t2).X + 26;

        // Seals
        uint sealDot = state.SealCount > 0 ? Gold : StatusDim;
        dl.AddCircleFilled(new Vector2(x + 4, y + 8), 4f, sealDot);
        string t3 = $"{state.SealCount} Seals";
        dl.AddText(new Vector2(x + 12, y + 1), state.SealCount > 0 ? Label : StatusDim, t3);
        x += ImGui.CalcTextSize(t3).X + 26;

        // Spectre
        uint specDot = state.SpectreAlive ? StatusGreen : StatusRed;
        dl.AddCircleFilled(new Vector2(x + 4, y + 8), 4f, state.SpectreFound ? specDot : StatusDim);
        string t4 = state.SpectreFound ? (state.SpectreAlive ? "Spectre Alive" : "Spectre Dead") : "No Spectre";
        dl.AddText(new Vector2(x + 12, y + 1), state.SpectreFound ? Label : StatusDim, t4);
    }

    // -- Tabs --

    private void TabOverlay(ImDrawListPtr dl, WhatsABladeVortexSettings s,
        float x, float cx, ref float y, float sw)
    {
        SectionHeader(dl, x, ref y, "Display Mode");
        Toggle("bv_seals", s.ShowSeals, dl, x, cx, ref y,
            "Show Unleash Seals", "Show the Greater Unleash seal pips");
        Toggle("bv_bar", s.ShowSnapshotBar, dl, x, cx, ref y,
            "Show Snapshot Bar", "Show a bar tracking when stacks expire");
        Toggle("bv_full", s.ShowFullPanel, dl, x, cx, ref y,
            "Full Cluster Panel", "Show all clusters with per-blade bars");

        SectionHeader(dl, x, ref y, "Unleash");
        IntSlider("bv_maxseals", s.MaxSeals, dl, x, cx, ref y, sw,
            "Max Seals", "Your max seals (Greater Unleash = 4, Caster Mastery +1, etc)");
        IntSlider("bv_clusterms", s.ClusterWindowMs, dl, x, cx, ref y, sw,
            "Cluster Window (ms)", "Blades within this window are grouped into the same cluster");

        SectionHeader(dl, x, ref y, "Position");
        IntSlider("bv_px", s.PanelX, dl, x, cx, ref y, sw,
            "Panel X", "Horizontal screen position in pixels");
        IntSlider("bv_py", s.PanelY, dl, x, cx, ref y, sw,
            "Panel Y", "Vertical screen position in pixels");
        IntSlider("bv_pw", s.PanelWidth, dl, x, cx, ref y, sw,
            "Panel Width", "Width of the full cluster panel");

        SectionHeader(dl, x, ref y, "Alerts");
        Toggle("bv_flash", s.AlertEdgeFlash, dl, x, cx, ref y,
            "Edge Flash", "Decaying red border flash when all blades expire");
        Toggle("bv_redhdr", s.AlertRedHeader, dl, x, cx, ref y,
            "Red Header", "Flip the header to red SNAPSHOT LOST text");
    }

    private void TabColors(ImDrawListPtr dl, WhatsABladeVortexSettings s,
        float x, float cx, ref float y)
    {
        SectionHeader(dl, x, ref y, "Panel Colors");
        ColorPicker("bv_cborder", s.BorderColor, dl, x, cx, ref y,
            "Border", "Panel border color");
        ColorPicker("bv_cbg", s.BackgroundColor, dl, x, cx, ref y,
            "Background", "Panel background color");
        ColorPicker("bv_ctext", s.TextColor, dl, x, cx, ref y,
            "Text", "Primary text color");
        ColorPicker("bv_cmuted", s.MutedColor, dl, x, cx, ref y,
            "Muted", "Secondary/label text color");

        SectionHeader(dl, x, ref y, "State Colors");
        ColorPicker("bv_calive", s.AliveColor, dl, x, cx, ref y,
            "Alive", "Bar color when blade has time remaining");
        ColorPicker("bv_cwarn", s.WarnColor, dl, x, cx, ref y,
            "Warn", "Bar color when blade is expiring");
        ColorPicker("bv_cdanger", s.DangerColor, dl, x, cx, ref y,
            "Danger", "Bar color when blade is nearly expired");

        SectionHeader(dl, x, ref y, "Snapshot Colors");
        ColorPicker("bv_cactive", s.SnapshotActiveColor, dl, x, cx, ref y,
            "Snapshot Active", "Color for active snapshot state");
        ColorPicker("bv_cready", s.SnapshotReadyColor, dl, x, cx, ref y,
            "Snapshot Ready", "Color for ready-to-snapshot state");
    }

    private void TabDebug(ImDrawListPtr dl, WhatsABladeVortexSettings s,
        float x, float cx, ref float y, BladeVortexRenderState state)
    {
        SectionHeader(dl, x, ref y, "Diagnostics");
        Toggle("bv_dbg", s.ShowDebugWindow, dl, x, cx, ref y,
            "Show Debug Window", "Show spectre, Minion Pact, and snapshot state diagnostics");

        y += 8f;

        SectionHeader(dl, x, ref y, "Live State");

        // Snapshot
        string stateStr = state.SnapshotState.ToString().ToUpper();
        uint stateCol = state.SnapshotState switch
        {
            SnapshotState.Active => StatusGreen,
            SnapshotState.Lost => StatusRed,
            _ => Gold
        };
        dl.AddText(new Vector2(x + 6, y), Label, "Snapshot State:");
        dl.AddText(new Vector2(cx, y), stateCol, stateStr);
        y += 16f;

        // Blades
        dl.AddText(new Vector2(x + 6, y), Label, "Blade Count:");
        dl.AddText(new Vector2(cx, y), Accent, $"{state.BladeCount}/10");
        y += 16f;

        // Clusters
        dl.AddText(new Vector2(x + 6, y), Label, "Cluster Count:");
        dl.AddText(new Vector2(cx, y), Accent, state.Tracker.ClusterCount.ToString());
        y += 16f;

        // Seals
        dl.AddText(new Vector2(x + 6, y), Label, "Seals:");
        dl.AddText(new Vector2(cx, y), state.SealCount > 0 ? Gold : StatusDim,
            $"{state.SealCount}/{Math.Max(1, s.MaxSeals.Value)}");
        y += 20f;

        SectionHeader(dl, x, ref y, "Spectre");

        dl.AddText(new Vector2(x + 6, y), Label, "Found:");
        dl.AddText(new Vector2(cx, y), state.SpectreFound ? StatusGreen : StatusRed,
            state.SpectreFound ? "yes" : "no");
        y += 16f;

        dl.AddText(new Vector2(x + 6, y), Label, "Alive:");
        uint aliveCol = state.SpectreAlive ? StatusGreen : StatusRed;
        string aliveStr = state.SpectreAlive
            ? (state.SpectreMaxHp > 1 ? $"yes ({state.SpectreHpPct * 100f:F0}%)" : "yes (HP loading...)")
            : "DEAD";
        dl.AddText(new Vector2(cx, y), state.SpectreAlive && state.SpectreMaxHp <= 1 ? Gold : aliveCol, aliveStr);
        y += 16f;

        dl.AddText(new Vector2(x + 6, y), Label, "Max HP:");
        dl.AddText(new Vector2(cx, y), state.SpectreMaxHp > 1 ? Label : StatusDim,
            state.SpectreMaxHp > 1 ? $"{state.SpectreMaxHp:N0}" : "--");
        y += 20f;

        SectionHeader(dl, x, ref y, "Minion Pact");

        dl.AddText(new Vector2(x + 6, y), Label, "Linked:");
        dl.AddText(new Vector2(cx, y), state.HasMinionPact ? StatusGreen : StatusDim,
            state.HasMinionPact ? $"yes ({state.MinionPactPermillage * 0.1f:F1}%)" : "not linked");
        y += 16f;

        dl.AddText(new Vector2(x + 6, y), Label, "Added Phys:");
        string addedStr = state.CalculatedAddedPhys > 0
            ? $"~{state.CalculatedAddedPhys:N0}"
            : state.SpectreMaxHp <= 1 ? "(need HP)" : "--";
        dl.AddText(new Vector2(cx, y), state.CalculatedAddedPhys > 0 ? Accent : StatusDim, addedStr);
        y += 16f;
    }

    // -- Widget primitives --

    private void SectionHeader(ImDrawListPtr dl, float x, ref float y, string title)
    {
        y += 4f;
        dl.AddText(new Vector2(x, y), Accent, title);
        y += 18f;
        dl.AddLine(new Vector2(x, y - 4), new Vector2(x + ImGui.CalcTextSize(title).X + 40, y - 4),
            WithAlpha(Accent, 0.25f), 1f);
    }

    private void Toggle(string key, ToggleNode node, ImDrawListPtr dl,
        float x, float cx, ref float y, string label, string desc)
    {
        dl.AddText(new Vector2(x + 6, y + 1), Label, label);
        dl.AddText(new Vector2(x + 6, y + 16), Desc, desc);
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 5));
        var v = node.Value;
        _anims.TryGetValue(key, out float a);
        if (PillToggle($"##{key}", ref v, ref a))
            node.Value = v;
        _anims[key] = a;
        y += Row;
    }

    private void IntSlider(string key, RangeNode<int> node, ImDrawListPtr dl,
        float x, float cx, ref float y, float sw, string label, string desc)
    {
        dl.AddText(new Vector2(x + 6, y + 1), Label, label);
        dl.AddText(new Vector2(x + 6, y + 16), Desc, desc);
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 7));
        var v = node.Value;
        if (SliderInt($"##{key}", ref v, node.Min, node.Max, sw))
            node.Value = v;
        dl.AddText(new Vector2(cx + sw + 6, y + 7), Accent, v.ToString());
        y += Row;
    }

    private void ColorPicker(string key, ColorNode node, ImDrawListPtr dl,
        float x, float cx, ref float y, string label, string desc)
    {
        dl.AddText(new Vector2(x + 6, y + 1), Label, label);
        dl.AddText(new Vector2(x + 6, y + 16), Desc, desc);

        var c = node.Value;
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 3));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.07f, 0.07f, 0.09f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.28f, 0.44f, 0.65f, 0.6f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        var colorVec = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        ImGui.SetNextItemWidth(120);
        if (ImGui.ColorEdit4($"##{key}", ref colorVec,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
        {
            node.Value = new SharpDX.Color(
                (int)(colorVec.X * 255), (int)(colorVec.Y * 255),
                (int)(colorVec.Z * 255), (int)(colorVec.W * 255));
        }
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);

        y += Row;
    }

    // -- Self-contained drawing helpers --

    private static bool PillToggle(string id, ref bool value, ref float animState)
    {
        var dl = ImGui.GetWindowDrawList();
        var cur = ImGui.GetCursorScreenPos();
        const float w = 40f, h = 20f, r = h * 0.5f;

        ImGui.InvisibleButton(id, new Vector2(w, h));
        bool changed = ImGui.IsItemClicked();
        if (changed) value = !value;

        float dt = ImGui.GetIO().DeltaTime;
        float target = value ? 1f : 0f;
        animState = Math.Clamp(animState + (target - animState) * Math.Min(dt * 10f, 1f), 0f, 1f);

        uint trackOff = ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 1f));
        uint trackColor = LerpCol(trackOff, Accent, animState);
        dl.AddRectFilled(cur, cur + new Vector2(w, h), trackColor, r);

        float knobX = cur.X + r + animState * (w - h);
        uint knobCol = ImGui.GetColorU32(new Vector4(
            0.5f + 0.5f * animState, 0.5f + 0.5f * animState,
            0.5f + 0.5f * animState, 1f));
        dl.AddCircleFilled(new Vector2(knobX, cur.Y + r), r - 2f, knobCol);

        return changed;
    }

    private static bool SliderInt(string id, ref int value, int min, int max, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var cur = ImGui.GetCursorScreenPos();
        const float h = 16f, th = 4f, tr = 7f;

        ImGui.InvisibleButton(id, new Vector2(width, h));
        bool changed = false;
        if (ImGui.IsItemActive())
        {
            float frac = Math.Clamp((ImGui.GetMousePos().X - cur.X) / width, 0f, 1f);
            int nv = min + (int)(frac * (max - min));
            if (nv != value) { value = nv; changed = true; }
        }

        float trackY = cur.Y + (h - th) * 0.5f;
        dl.AddRectFilled(cur with { Y = trackY }, new Vector2(cur.X + width, trackY + th),
            ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)), 2f);

        float fv = max > min ? (float)(value - min) / (max - min) : 0f;
        float thumbX = cur.X + fv * width;
        dl.AddRectFilled(cur with { Y = trackY }, new Vector2(thumbX, trackY + th), Accent, 2f);
        dl.AddCircleFilled(new Vector2(thumbX, cur.Y + h * 0.5f), tr, Accent);
        dl.AddCircleFilled(new Vector2(thumbX, cur.Y + h * 0.5f), tr - 2f,
            ImGui.GetColorU32(new Vector4(0.95f, 0.95f, 0.95f, 1f)));

        return changed;
    }

    // -- Color utilities --

    private static uint Col(float r, float g, float b, float a = 1f)
        => ImGui.GetColorU32(new Vector4(r, g, b, a));

    private static uint WithAlpha(uint col, float a)
    {
        var v = ImGui.ColorConvertU32ToFloat4(col);
        v.W = a;
        return ImGui.GetColorU32(v);
    }

    private static uint LerpCol(uint a, uint b, float t)
    {
        var va = ImGui.ColorConvertU32ToFloat4(a);
        var vb = ImGui.ColorConvertU32ToFloat4(b);
        return ImGui.GetColorU32(new Vector4(
            va.X + (vb.X - va.X) * t, va.Y + (vb.Y - va.Y) * t,
            va.Z + (vb.Z - va.Z) * t, va.W + (vb.W - va.W) * t));
    }

    private static void CenterText(ImDrawListPtr dl, string text, Vector2 center, uint col)
    {
        var sz = ImGui.CalcTextSize(text);
        dl.AddText(center - sz * 0.5f, col, text);
    }
}
