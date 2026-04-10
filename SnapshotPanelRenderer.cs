using System;
using System.Numerics;
using ExileCore;
using SharpDX;
using Color = SharpDX.Color;
using Graphics = ExileCore.Graphics;
using Vec2 = System.Numerics.Vector2;

namespace WhatsABladeVortex;

public sealed class SnapshotPanelRenderer
{
    private static readonly Color[] ClusterPalette =
    {
        new Color(106, 169, 255, 255),
        new Color(255, 154, 90, 255),
        new Color(169, 120, 255, 255),
        new Color(90, 255, 200, 255),
        new Color(255, 120, 170, 255),
    };

    private static readonly Color GoldBright = new(242, 211, 122, 255);
    private static readonly Color GoldGlow = new(255, 243, 196, 180);

    // ----------------------------------------------------------------
    //  Compact mode: seals + two-bar snapshot (main + next expiry)
    // ----------------------------------------------------------------

    public void DrawCompact(Graphics g, WhatsABladeVortexSettings s, BladeVortexRenderState state)
    {
        bool showSeals = s.ShowSeals.Value;
        bool showBar = s.ShowSnapshotBar.Value;
        if (!showSeals && !showBar) return;

        float padX = 10f;
        float padY = 6f;
        float x = s.PanelX.Value;
        float y = s.PanelY.Value;

        int maxSeals = Math.Max(1, s.MaxSeals.Value);
        float pipSize = 12f;
        float pipGap = 5f;

        float sealRowW = 0f;
        if (showSeals)
        {
            float labelW = g.MeasureText("UNLEASH").X;
            float pipsW = maxSeals * (pipSize + pipGap) - pipGap;
            float countW = g.MeasureText($"{state.SealCount}/{maxSeals}").X;
            sealRowW = labelW + 10f + pipsW + 8f + countW;
        }

        float barW = 120f;
        float barRowW = 0f;
        if (showBar)
        {
            float labelW = g.MeasureText("SNAPSHOT").X;
            barRowW = labelW + 10f + barW + 8f + g.MeasureText("6.4s 10").X;
        }

        float contentW = Math.Max(sealRowW, barRowW);
        float totalW = padX + contentW + padX;

        float mainBarH = 14f;
        float smallBarH = 6f;
        float smallBarGap = 2f;
        float rowH = Math.Max(pipSize, 14f);
        bool showSmallBar = showBar && state.SecondClusterBladeCount > 0;

        int rows = (showSeals ? 1 : 0) + (showBar ? 1 : 0);
        float totalH = padY + rows * (rowH + 4f) - 4f + padY;
        if (showSmallBar)
            totalH += smallBarGap + smallBarH;

        var tl = new Vec2(x, y);
        var br = new Vec2(x + totalW, y + totalH);
        g.DrawBox(tl, br, s.BackgroundColor.Value);
        g.DrawFrame(tl, br, s.BorderColor.Value, 1);
        DrawCornerTick(g, tl, true, s.BorderColor.Value);
        DrawCornerTick(g, br, false, s.BorderColor.Value);

        float cy = y + padY;

        // Seal pips row
        if (showSeals)
        {
            float cx = x + padX;
            g.DrawText("UNLEASH", new Vec2(cx, cy), s.MutedColor.Value);
            cx += g.MeasureText("UNLEASH").X + 10f;

            for (int i = 0; i < maxSeals; i++)
            {
                var p1 = new Vec2(cx + i * (pipSize + pipGap), cy + 1f);
                var p2 = new Vec2(p1.X + pipSize, p1.Y + pipSize);
                bool filled = i < state.SealCount;
                g.DrawBox(p1, p2, filled ? GoldBright : new Color(0, 0, 0, 180));
                g.DrawFrame(p1, p2, s.BorderColor.Value, 1);
                if (filled)
                    g.DrawBox(new Vec2(p1.X + 2f, p1.Y + 2f), new Vec2(p2.X - 2f, p2.Y - 2f), GoldGlow);
            }

            float pipsEnd = cx + maxSeals * (pipSize + pipGap) - pipGap + 8f;
            g.DrawText($"{state.SealCount}/{maxSeals}", new Vec2(pipsEnd, cy), s.TextColor.Value);

            cy += rowH + 4f;
        }

        // Main snapshot bar
        if (showBar)
        {
            float cx = x + padX;
            g.DrawText("SNAPSHOT", new Vec2(cx, cy), s.MutedColor.Value);
            cx += g.MeasureText("SNAPSHOT").X + 10f;

            var barTL = new Vec2(cx, cy + 1f);
            var barBR = new Vec2(cx + barW, cy + 1f + mainBarH);

            g.DrawBox(barTL, barBR, new Color(5, 7, 9, 220));
            g.DrawFrame(barTL, barBR, new Color(35, 40, 53, 160), 1);

            bool hasBlades = state.LatestClusterMaxTime > 0f && state.LatestClusterBladeCount > 0;

            if (hasBlades)
            {
                float frac = Math.Clamp(state.LatestClusterTimer / state.LatestClusterMaxTime, 0f, 1f);
                var fillBR = new Vec2(barTL.X + barW * frac, barBR.Y);
                g.DrawBox(barTL, fillBR, BarColor(frac, s));
                var lbl = $"{state.LatestClusterTimer:F1}s {state.LatestClusterBladeCount}";
                g.DrawText(lbl, new Vec2(barBR.X + 6f, cy), s.TextColor.Value);
            }
            else
            {
                string chipText;
                Color chipColor;
                switch (state.SnapshotState)
                {
                    case SnapshotState.Lost:
                        chipText = "LOST";
                        chipColor = s.DangerColor.Value;
                        break;
                    case SnapshotState.Ready:
                        chipText = "READY";
                        chipColor = s.SnapshotReadyColor.Value;
                        break;
                    default:
                        chipText = "--";
                        chipColor = s.MutedColor.Value;
                        break;
                }
                g.DrawText(chipText, new Vec2(barBR.X + 6f, cy), chipColor);
            }

            // Small bar: next cluster to expire (underneath main bar)
            if (showSmallBar)
            {
                float smallY = barBR.Y + smallBarGap;
                var sTL = new Vec2(barTL.X, smallY);
                var sBR = new Vec2(barTL.X + barW, smallY + smallBarH);

                g.DrawBox(sTL, sBR, new Color(5, 7, 9, 180));
                g.DrawFrame(sTL, sBR, new Color(35, 40, 53, 100), 1);

                if (state.SecondClusterMaxTime > 0f)
                {
                    float nFrac = Math.Clamp(state.SecondClusterTimer / state.SecondClusterMaxTime, 0f, 1f);
                    var nFillBR = new Vec2(sTL.X + barW * nFrac, sBR.Y);
                    var dimColor = BarColor(nFrac, s);
                    dimColor = new Color(dimColor.R, dimColor.G, dimColor.B, (byte)(dimColor.A * 0.6f));
                    g.DrawBox(sTL, nFillBR, dimColor);
                }
            }
        }
    }

    // ----------------------------------------------------------------
    //  Full panel: header, seals, minion pact info, clusters, footer
    // ----------------------------------------------------------------

    public void DrawFullPanel(Graphics g, WhatsABladeVortexSettings s, BladeVortexRenderState state)
    {
        var tracker = state.Tracker;
        float panelW = s.PanelWidth.Value;
        float ox = s.PanelX.Value;
        float oy = s.PanelY.Value;
        float pad = 10f;

        float headerH = 34f;
        float sealRowH = s.ShowSeals.Value ? 22f : 0f;
        float minionPactRowH = 20f;
        bool hasBlades = state.BladeCount > 0;
        float clusterH = Math.Max(60f, EstimateClustersHeight(tracker, hasBlades));
        float footerH = 22f;
        float totalH = pad + headerH
            + (s.ShowSeals.Value ? sealRowH + 6f : 0f)
            + minionPactRowH + 6f
            + 8f + clusterH + 8f + footerH + pad;

        var tl = new Vec2(ox, oy);
        var br = new Vec2(ox + panelW, oy + totalH);
        g.DrawBox(tl, br, s.BackgroundColor.Value);
        g.DrawFrame(tl, br, s.BorderColor.Value, 1);
        DrawCornerTick(g, tl, true, s.BorderColor.Value);
        DrawCornerTick(g, br, false, s.BorderColor.Value);

        float y = oy + pad;

        // Header
        g.DrawText("BLADE VORTEX SNAPSHOT", new Vec2(ox + pad, y), s.BorderColor.Value);
        g.DrawText("Minion Pact tracker", new Vec2(ox + pad, y + 14f), s.MutedColor.Value);

        string chipText;
        Color chipColor;
        switch (state.SnapshotState)
        {
            case SnapshotState.Active:
                chipText = "SNAPSHOT ACTIVE";
                chipColor = s.AliveColor.Value;
                break;
            case SnapshotState.Lost:
                chipText = "SNAPSHOT LOST";
                chipColor = s.DangerColor.Value;
                break;
            default:
                chipText = "READY";
                chipColor = s.SnapshotReadyColor.Value;
                break;
        }

        var chipSz = g.MeasureText(chipText);
        float chipW = chipSz.X + 12f;
        var cTL = new Vec2(ox + panelW - pad - chipW, y + 4f);
        var cBR = new Vec2(cTL.X + chipW, cTL.Y + 18f);
        g.DrawBox(cTL, cBR, new Color(0, 0, 0, 140));
        g.DrawFrame(cTL, cBR, chipColor, 1);
        g.DrawText(chipText, new Vec2(cTL.X + 6f, cTL.Y + 2f), chipColor);

        y += headerH;
        DrawSep(g, ox + pad, ox + panelW - pad, y, s.BorderColor.Value);
        y += 6f;

        // Seal meter
        if (s.ShowSeals.Value)
        {
            int maxSeals = Math.Max(1, s.MaxSeals.Value);
            g.DrawText("UNLEASH SEALS", new Vec2(ox + pad, y + 4f), s.MutedColor.Value);
            float pipSz = 10f, pipGap = 4f, pipsX = ox + pad + 100f;
            for (int i = 0; i < maxSeals; i++)
            {
                var p1 = new Vec2(pipsX + i * (pipSz + pipGap), y + 4f);
                var p2 = new Vec2(p1.X + pipSz, p1.Y + pipSz);
                g.DrawBox(p1, p2, i < state.SealCount ? GoldBright : new Color(0, 0, 0, 180));
                g.DrawFrame(p1, p2, s.BorderColor.Value, 1);
            }
            var sealStr = $"{state.SealCount}/{maxSeals}";
            g.DrawText(sealStr, new Vec2(ox + panelW - pad - g.MeasureText(sealStr).X, y + 4f), s.TextColor.Value);
            y += sealRowH;
            DrawSep(g, ox + pad, ox + panelW - pad, y, s.BorderColor.Value);
            y += 6f;
        }

        // Minion Pact info row
        if (state.HasMinionPact)
        {
            var mpLabel = $"MINION PACT  +{state.CalculatedAddedPhys:N0} phys";
            g.DrawText(mpLabel, new Vec2(ox + pad, y + 2f), s.SnapshotActiveColor.Value);
            var pctStr = $"{state.MinionPactPermillage * 0.1f:F1}%";
            g.DrawText(pctStr, new Vec2(ox + panelW - pad - g.MeasureText(pctStr).X, y + 2f), s.MutedColor.Value);
        }
        else
        {
            g.DrawText("MINION PACT  not linked", new Vec2(ox + pad, y + 2f), s.MutedColor.Value);
        }
        y += minionPactRowH;
        DrawSep(g, ox + pad, ox + panelW - pad, y, s.BorderColor.Value);
        y += 6f;

        // Blade summary
        int total = state.BladeCount;
        g.DrawText($"BLADES  {total}/10", new Vec2(ox + pad, y + 2f), s.TextColor.Value);
        if (total > 0)
        {
            float e = tracker.EarliestTimer();
            var eStr = $"earliest {e:F1}s";
            g.DrawText(eStr, new Vec2(ox + panelW - pad - g.MeasureText(eStr).X, y + 2f),
                e < 1.5f ? s.WarnColor.Value : s.MutedColor.Value);
        }
        y += 18f;

        // Clusters or state message
        if (total == 0)
        {
            string msg;
            string hint;
            if (state.SnapshotState == SnapshotState.Lost)
            {
                msg = "ALL BLADES EXPIRED";
                hint = state.SpectreAlive
                    ? "Ready to snapshot. Cast BV to capture Minion Pact damage."
                    : "Spectre is dead. Zone to reset and re-snapshot.";
            }
            else
            {
                msg = state.SnapshotState == SnapshotState.Ready ? "WAITING FOR SNAPSHOT" : "NO BLADES";
                hint = state.SpectreAlive
                    ? "Cast BV to sacrifice spectre and snapshot damage."
                    : "Spectre is dead. Waiting for resurrection.";
            }
            g.DrawText(msg, new Vec2(ox + (panelW - g.MeasureText(msg).X) * 0.5f, y + 10f),
                state.SnapshotState == SnapshotState.Lost ? s.DangerColor.Value : s.SnapshotReadyColor.Value);
            g.DrawText(hint, new Vec2(ox + (panelW - g.MeasureText(hint).X) * 0.5f, y + 28f), s.MutedColor.Value);
        }
        else
        {
            int warnId = FindWarnClusterId(tracker);
            for (int i = 0; i < tracker.ClusterCount; i++)
            {
                var c = tracker.GetCluster(i);
                y = DrawCluster(g, s, c, warnId, ox + pad, ox + panelW - pad, y);
                y += 4f;
            }
        }

        // Footer
        float footerTop = br.Y - footerH - pad + 4f;
        DrawSep(g, ox + pad, ox + panelW - pad, footerTop, s.BorderColor.Value);
        g.DrawText("NEXT LOSS", new Vec2(ox + pad, footerTop + 6f), s.MutedColor.Value);
        string fv; Color fc;
        if (total > 0)
        {
            tracker.FindNextLoss(out int n, out float w);
            fv = $"{n} blades in {w:F1}s";
            fc = w < 1.5f ? s.WarnColor.Value : s.TextColor.Value;
        }
        else { fv = state.SnapshotState == SnapshotState.Lost ? "LOST" : "--"; fc = s.DangerColor.Value; }
        g.DrawText(fv, new Vec2(ox + panelW - pad - g.MeasureText(fv).X, footerTop + 6f), fc);
    }

    // ----------------------------------------------------------------
    //  Debug window
    // ----------------------------------------------------------------

    public void DrawDebugWindow(Graphics g, WhatsABladeVortexSettings s, BladeVortexRenderState state)
    {
        float padX = 10f;
        float padY = 8f;
        float lineH = 16f;
        int lines = 13;
        float panelW = 320f;
        float panelH = padY + lines * lineH + padY;

        float ox = s.PanelX.Value + s.PanelWidth.Value + 20f;
        float oy = s.PanelY.Value;

        var tl = new Vec2(ox, oy);
        var br = new Vec2(ox + panelW, oy + panelH);
        g.DrawBox(tl, br, s.BackgroundColor.Value);
        g.DrawFrame(tl, br, s.BorderColor.Value, 1);
        DrawCornerTick(g, tl, true, s.BorderColor.Value);
        DrawCornerTick(g, br, false, s.BorderColor.Value);

        float y = oy + padY;
        float lx = ox + padX;
        float vx = ox + 150f;

        g.DrawText("DEBUG", new Vec2(lx, y), s.BorderColor.Value);
        y += lineH;

        DrawSep(g, lx, ox + panelW - padX, y, s.BorderColor.Value);
        y += 4f;

        // Spectre section
        DrawDebugRow(g, s, lx, vx, ref y, lineH, "Spectre found:", state.SpectreFound ? "yes" : "no",
            state.SpectreFound ? s.AliveColor.Value : s.DangerColor.Value);

        string spectreHpStr = state.SpectreAlive
            ? $"yes  ({state.SpectreHpPct * 100f:F0}% of {state.SpectreMaxHp:N0})"
            : "DEAD";
        Color spectreHpColor = state.SpectreAlive ? s.AliveColor.Value : s.DangerColor.Value;
        if (state.SpectreAlive && state.SpectreMaxHp <= 1)
            spectreHpColor = s.WarnColor.Value;
        DrawDebugRow(g, s, lx, vx, ref y, lineH, "Spectre alive:",
            state.SpectreAlive && state.SpectreMaxHp <= 1 ? "yes  (HP loading...)" : spectreHpStr, spectreHpColor);

        DrawDebugRow(g, s, lx, vx, ref y, lineH, "Minion Pact:",
            state.HasMinionPact ? $"yes  ({state.MinionPactPermillage} permil = {state.MinionPactPermillage * 0.1f:F1}%)" : "not linked",
            state.HasMinionPact ? s.SnapshotActiveColor.Value : s.MutedColor.Value);

        string addedStr = state.SpectreMaxHp > 1 && state.CalculatedAddedPhys > 0
            ? $"~{state.CalculatedAddedPhys:N0}"
            : state.SpectreMaxHp <= 1 ? "(need HP)" : "--";
        DrawDebugRow(g, s, lx, vx, ref y, lineH, "Added phys:",
            addedStr, state.CalculatedAddedPhys > 0 ? s.TextColor.Value : s.MutedColor.Value);

        DrawSep(g, lx, ox + panelW - padX, y, s.BorderColor.Value);
        y += 4f;

        // State section
        Color stateColor = state.SnapshotState switch
        {
            SnapshotState.Active => s.AliveColor.Value,
            SnapshotState.Lost => s.DangerColor.Value,
            _ => s.SnapshotReadyColor.Value
        };
        DrawDebugRow(g, s, lx, vx, ref y, lineH, "Snapshot state:", state.SnapshotState.ToString().ToUpper(), stateColor);

        DrawDebugRow(g, s, lx, vx, ref y, lineH, "Blade count:", state.BladeCount.ToString(), s.TextColor.Value);
        DrawDebugRow(g, s, lx, vx, ref y, lineH, "Cluster count:", state.Tracker.ClusterCount.ToString(), s.TextColor.Value);

        DrawDebugRow(g, s, lx, vx, ref y, lineH, "Latest cluster:",
            state.LatestClusterTimer > 0f ? $"{state.LatestClusterTimer:F1}s ({state.LatestClusterBladeCount} blades)" : "--",
            s.TextColor.Value);
        DrawDebugRow(g, s, lx, vx, ref y, lineH, "2nd cluster:",
            state.SecondClusterTimer > 0f ? $"{state.SecondClusterTimer:F1}s ({state.SecondClusterBladeCount} blades)" : "--",
            state.SecondClusterTimer > 0f ? s.WarnColor.Value : s.MutedColor.Value);

        int maxSeals = Math.Max(1, s.MaxSeals.Value);
        DrawDebugRow(g, s, lx, vx, ref y, lineH, "Seal count:", $"{state.SealCount}/{maxSeals}", s.TextColor.Value);
    }

    private static void DrawDebugRow(Graphics g, WhatsABladeVortexSettings s,
        float lx, float vx, ref float y, float lineH, string label, string value, Color valueColor)
    {
        g.DrawText(label, new Vec2(lx, y), s.MutedColor.Value);
        g.DrawText(value, new Vec2(vx, y), valueColor);
        y += lineH;
    }

    // ----------------------------------------------------------------
    //  Shared helpers
    // ----------------------------------------------------------------

    private static float EstimateClustersHeight(BladeTracker t, bool alive)
    {
        if (!alive || t.BladeCount == 0) return 60f;
        float h = 0f;
        for (int i = 0; i < t.ClusterCount; i++)
        {
            var c = t.GetCluster(i);
            h += 22f + c.BladeCount * 14f + 6f;
        }
        return h;
    }

    private float DrawCluster(Graphics g, WhatsABladeVortexSettings s, BladeCluster cluster,
        int warnId, float x0, float x1, float y)
    {
        bool isWarn = cluster.Id == warnId;
        var color = ClusterPalette[(cluster.Id - 1) % ClusterPalette.Length];
        float age = cluster.CastAgoSeconds();
        var hdr = $"CLUSTER {cluster.Id}  //  cast {age:F1}s ago  //  {cluster.BladeCount} blades";

        g.DrawBox(new Vec2(x0 + 2f, y + 3f), new Vec2(x0 + 10f, y + 11f), color);
        g.DrawText(hdr, new Vec2(x0 + 14f, y), s.MutedColor.Value);

        if (isWarn)
        {
            var chip = "! NEXT LOSS";
            var sz = g.MeasureText(chip);
            var ct = new Vec2(x1 - sz.X - 8f, y);
            var cb = new Vec2(x1, y + 14f);
            g.DrawBox(ct, cb, new Color(224, 200, 58, 40));
            g.DrawFrame(ct, cb, s.WarnColor.Value, 1);
            g.DrawText(chip, new Vec2(ct.X + 3f, ct.Y), s.WarnColor.Value);
        }
        y += 18f;

        float barH = 11f, barGap = 3f, bx0 = x0 + 4f, bx1 = x1 - 4f;
        foreach (var b in cluster.Blades)
        {
            var barTL = new Vec2(bx0, y);
            var barBR = new Vec2(bx1, y + barH);
            g.DrawBox(barTL, barBR, new Color(5, 7, 9, 220));
            g.DrawFrame(barTL, barBR, new Color(35, 40, 53, 160), 1);
            g.DrawBox(barTL, new Vec2(bx0 + (bx1 - bx0) * b.Fraction, barBR.Y), BarColor(b.Fraction, s));
            var lbl = $"{b.Timer:F1}s";
            g.DrawText(lbl, new Vec2(bx1 - g.MeasureText(lbl).X - 4f, y - 2f), s.TextColor.Value);
            y += barH + barGap;
        }
        return y + 2f;
    }

    private static Color BarColor(float frac, WhatsABladeVortexSettings s)
    {
        if (frac > 0.5f) return Lerp(s.WarnColor.Value, s.AliveColor.Value, (frac - 0.5f) * 2f);
        return Lerp(s.DangerColor.Value, s.WarnColor.Value, frac * 2f);
    }

    private static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t), (byte)(a.A + (b.A - a.A) * t));
    }

    private static void DrawSep(Graphics g, float x0, float x1, float y, Color c)
        => g.DrawBox(new Vec2(x0, y), new Vec2(x1, y + 1f), new Color(c.R, c.G, c.B, (byte)(c.A / 2)));

    private static void DrawCornerTick(Graphics g, Vec2 corner, bool isTopLeft, Color c)
    {
        const float len = 10f;
        if (isTopLeft)
        {
            g.DrawBox(corner, new Vec2(corner.X + len, corner.Y + 1f), c);
            g.DrawBox(corner, new Vec2(corner.X + 1f, corner.Y + len), c);
        }
        else
        {
            g.DrawBox(new Vec2(corner.X - len, corner.Y - 1f), corner, c);
            g.DrawBox(new Vec2(corner.X - 1f, corner.Y - len), corner, c);
        }
    }

    private static int FindWarnClusterId(BladeTracker t)
    {
        int id = 0; float min = float.MaxValue;
        for (int i = 0; i < t.ClusterCount; i++)
        {
            var c = t.GetCluster(i);
            var e = c.EarliestTimer();
            if (e < min) { min = e; id = c.Id; }
        }
        return id;
    }
}
