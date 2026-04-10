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

    /// <summary>
    /// Compact mode: seals pips + optional single "freshest blade" bar.
    /// Small, unobtrusive. Each section independently toggleable.
    /// </summary>
    public void DrawCompact(
        Graphics g, WhatsABladeVortexSettings s,
        int sealCount, bool snapshotAlive,
        float freshestTimer, float freshestMaxTime, int bladeCount)
    {
        bool showSeals = s.ShowSeals.Value;
        bool showBar = s.ShowSnapshotBar.Value;
        if (!showSeals && !showBar) return;

        float padX = 10f;
        float padY = 6f;
        float x = s.PanelX.Value;
        float y = s.PanelY.Value;

        // Measure width from the widest row.
        int maxSeals = Math.Max(1, s.MaxSeals.Value);
        float pipSize = 12f;
        float pipGap = 5f;

        float sealRowW = 0f;
        if (showSeals)
        {
            float labelW = g.MeasureText("UNLEASH").X;
            float pipsW = maxSeals * (pipSize + pipGap) - pipGap;
            float countW = g.MeasureText($"{sealCount}/{maxSeals}").X;
            sealRowW = labelW + 10f + pipsW + 8f + countW;
        }

        float barRowW = 0f;
        if (showBar)
        {
            float labelW = g.MeasureText("SNAPSHOT").X;
            barRowW = labelW + 10f + 120f + 8f + g.MeasureText("6.4s 10").X;
        }

        float contentW = Math.Max(sealRowW, barRowW);
        float totalW = padX + contentW + padX;
        float rowH = Math.Max(pipSize, 14f);
        int rows = (showSeals ? 1 : 0) + (showBar ? 1 : 0);
        float totalH = padY + rows * (rowH + 4f) - 4f + padY;

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
                bool filled = i < sealCount;
                g.DrawBox(p1, p2, filled ? GoldBright : new Color(0, 0, 0, 180));
                g.DrawFrame(p1, p2, s.BorderColor.Value, 1);
                if (filled)
                    g.DrawBox(new Vec2(p1.X + 2f, p1.Y + 2f), new Vec2(p2.X - 2f, p2.Y - 2f), GoldGlow);
            }

            float pipsEnd = cx + maxSeals * (pipSize + pipGap) - pipGap + 8f;
            g.DrawText($"{sealCount}/{maxSeals}", new Vec2(pipsEnd, cy), s.TextColor.Value);

            cy += rowH + 4f;
        }

        // Snapshot bar row (freshest blade = when ALL stacks will have expired)
        if (showBar)
        {
            float cx = x + padX;
            g.DrawText("SNAPSHOT", new Vec2(cx, cy), s.MutedColor.Value);
            cx += g.MeasureText("SNAPSHOT").X + 10f;

            float barW = 120f;
            float barH = 12f;
            var barTL = new Vec2(cx, cy + 1f);
            var barBR = new Vec2(cx + barW, cy + 1f + barH);

            g.DrawBox(barTL, barBR, new Color(5, 7, 9, 220));
            g.DrawFrame(barTL, barBR, new Color(35, 40, 53, 160), 1);

            if (snapshotAlive && freshestMaxTime > 0f)
            {
                float frac = Math.Clamp(freshestTimer / freshestMaxTime, 0f, 1f);
                var fillBR = new Vec2(barTL.X + barW * frac, barBR.Y);
                g.DrawBox(barTL, fillBR, BarColor(frac, s));
                var lbl = $"{freshestTimer:F1}s {bladeCount}";
                g.DrawText(lbl, new Vec2(barBR.X + 6f, cy), s.TextColor.Value);
            }
            else
            {
                var lostColor = s.AlertRedHeader.Value ? s.DangerColor.Value : s.MutedColor.Value;
                g.DrawText("LOST", new Vec2(barBR.X + 6f, cy), lostColor);
            }
        }
    }

    /// <summary>
    /// Full panel: header, seals, blade summary, all clusters with bars, footer.
    /// </summary>
    public void DrawFullPanel(
        Graphics g, WhatsABladeVortexSettings s,
        BladeTracker tracker, int sealCount,
        bool snapshotAlive, bool spectreDead)
    {
        float panelW = s.PanelWidth.Value;
        float ox = s.PanelX.Value;
        float oy = s.PanelY.Value;
        float pad = 10f;

        float headerH = 34f;
        float sealRowH = s.ShowSeals.Value ? 22f : 0f;
        float clusterH = Math.Max(60f, EstimateClustersHeight(tracker, snapshotAlive));
        float footerH = 22f;
        float totalH = pad + headerH + (s.ShowSeals.Value ? sealRowH + 6f : 0f) + 8f + clusterH + 8f + footerH + pad;

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

        var chipText = snapshotAlive ? "SNAPSHOT ALIVE" : (s.AlertRedHeader.Value ? "SNAPSHOT LOST" : "INACTIVE");
        var chipColor = snapshotAlive ? s.AliveColor.Value : s.DangerColor.Value;
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
                g.DrawBox(p1, p2, i < sealCount ? GoldBright : new Color(0, 0, 0, 180));
                g.DrawFrame(p1, p2, s.BorderColor.Value, 1);
            }
            var sealStr = $"{sealCount}/{maxSeals}";
            g.DrawText(sealStr, new Vec2(ox + panelW - pad - g.MeasureText(sealStr).X, y + 4f), s.TextColor.Value);
            y += sealRowH;
            DrawSep(g, ox + pad, ox + panelW - pad, y, s.BorderColor.Value);
            y += 6f;
        }

        // Blade summary
        int total = tracker.BladeCount;
        g.DrawText($"BLADES  {total}/10", new Vec2(ox + pad, y + 2f), s.TextColor.Value);
        if (snapshotAlive && total > 0)
        {
            float e = tracker.EarliestTimer();
            var eStr = $"earliest {e:F1}s";
            g.DrawText(eStr, new Vec2(ox + panelW - pad - g.MeasureText(eStr).X, y + 2f),
                e < 1.5f ? s.WarnColor.Value : s.MutedColor.Value);
        }
        y += 18f;

        // Clusters or lost state
        if (!snapshotAlive || total == 0)
        {
            var msg = "ALL BLADES EXPIRED";
            g.DrawText(msg, new Vec2(ox + (panelW - g.MeasureText(msg).X) * 0.5f, y + 10f), s.DangerColor.Value);
            var hint = spectreDead ? "Zone to reset specter and re-snapshot." : "Cast BV to create a new snapshot.";
            g.DrawText(hint, new Vec2(ox + (panelW - g.MeasureText(hint).X) * 0.5f, y + 28f), s.MutedColor.Value);
        }
        else
        {
            int warnId = FindWarnClusterId(tracker);
            foreach (var c in tracker.Clusters)
            {
                y = DrawCluster(g, s, c, warnId, ox + pad, ox + panelW - pad, y);
                y += 4f;
            }
        }

        // Footer
        float footerTop = br.Y - footerH - pad + 4f;
        DrawSep(g, ox + pad, ox + panelW - pad, footerTop, s.BorderColor.Value);
        g.DrawText("NEXT LOSS", new Vec2(ox + pad, footerTop + 6f), s.MutedColor.Value);
        string fv; Color fc;
        if (snapshotAlive && total > 0)
        {
            tracker.FindNextLoss(out int n, out float w);
            fv = $"{n} blades in {w:F1}s";
            fc = w < 1.5f ? s.WarnColor.Value : s.TextColor.Value;
        }
        else { fv = "LOST"; fc = s.DangerColor.Value; }
        g.DrawText(fv, new Vec2(ox + panelW - pad - g.MeasureText(fv).X, footerTop + 6f), fc);
    }

    // ── Shared helpers ──

    private static float EstimateClustersHeight(BladeTracker t, bool alive)
    {
        if (!alive || t.BladeCount == 0) return 60f;
        float h = 0f;
        foreach (var c in t.Clusters) h += 22f + c.Blades.Count * 14f + 6f;
        return h;
    }

    private float DrawCluster(Graphics g, WhatsABladeVortexSettings s, BladeCluster cluster,
        int warnId, float x0, float x1, float y)
    {
        bool isWarn = cluster.Id == warnId;
        var color = ClusterPalette[(cluster.Id - 1) % ClusterPalette.Length];
        float age = cluster.CastAgoSeconds();
        var hdr = $"CLUSTER {cluster.Id}  //  cast {age:F1}s ago  //  {cluster.Blades.Count} blades";

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
        foreach (var c in t.Clusters) { var e = c.EarliestTimer(); if (e < min) { min = e; id = c.Id; } }
        return id;
    }
}
