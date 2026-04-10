using System;
using System.Collections.Generic;

namespace WhatsABladeVortex;

public sealed class BladeInfo
{
    public float Timer;
    public float MaxTime;
    public int ClusterId;

    public float Fraction => MaxTime > 0f ? Math.Clamp(Timer / MaxTime, 0f, 1f) : 0f;
}

public sealed class BladeCluster
{
    public int Id;
    public readonly List<BladeInfo> Blades = new();

    public float EarliestTimer()
    {
        float min = float.MaxValue;
        foreach (var b in Blades)
            if (b.Timer < min) min = b.Timer;
        return min == float.MaxValue ? 0f : min;
    }

    public float CastAgoSeconds()
    {
        // The blade with the highest timer was cast most recently in this cluster.
        // "Cast ago" = maxTime - highestTimer
        float maxTimer = 0f;
        float maxTime = 0f;
        foreach (var b in Blades)
        {
            if (b.Timer > maxTimer)
            {
                maxTimer = b.Timer;
                maxTime = b.MaxTime;
            }
        }
        return maxTime > 0f ? maxTime - maxTimer : 0f;
    }
}

/// <summary>
/// Builds blade clusters from the player's buff list every tick.
/// No persistent state -- rebuilt from scratch each frame from buff reads.
/// </summary>
public sealed class BladeTracker
{
    private readonly List<BladeInfo> _blades = new();
    private readonly List<BladeCluster> _clusters = new();

    public IReadOnlyList<BladeCluster> Clusters => _clusters;
    public int BladeCount => _blades.Count;

    /// <summary>
    /// Rebuild from a list of (timer, maxTime) tuples extracted from all
    /// "new_new_blade_vortex" buff entries on the player.
    /// </summary>
    public void Rebuild(List<(float timer, float maxTime)> rawBlades, float clusterWindowSeconds)
    {
        _blades.Clear();
        _clusters.Clear();

        if (rawBlades.Count == 0) return;

        // Sort by timer descending (highest = most recently cast).
        rawBlades.Sort((a, b) => b.timer.CompareTo(a.timer));

        int clusterId = 1;
        BladeCluster? current = null;

        foreach (var (timer, maxTime) in rawBlades)
        {
            var blade = new BladeInfo { Timer = timer, MaxTime = maxTime };
            _blades.Add(blade);

            // Start a new cluster if this blade's timer is more than the window
            // below the previous blade's timer (i.e. it was cast noticeably earlier).
            if (current == null || (current.Blades[^1].Timer - timer) > clusterWindowSeconds)
            {
                current = new BladeCluster { Id = clusterId++ };
                _clusters.Add(current);
            }

            blade.ClusterId = current.Id;
            current.Blades.Add(blade);
        }
    }

    public float EarliestTimer()
    {
        float min = float.MaxValue;
        foreach (var b in _blades)
            if (b.Timer < min) min = b.Timer;
        return min == float.MaxValue ? 0f : min;
    }

    public void FindNextLoss(out int clusterBladeCount, out float expiresIn)
    {
        clusterBladeCount = 0;
        expiresIn = 0f;
        float minEarliest = float.MaxValue;
        BladeCluster? warn = null;
        foreach (var c in _clusters)
        {
            var e = c.EarliestTimer();
            if (e < minEarliest) { minEarliest = e; warn = c; }
        }
        if (warn != null)
        {
            clusterBladeCount = warn.Blades.Count;
            expiresIn = warn.EarliestTimer();
        }
    }

    /// <summary>
    /// Returns the two most recent clusters' earliest-blade timers for the two-bar UI.
    /// Clusters are sorted newest-first (highest timer), so index 0 = latest cast.
    /// "Fall off" = earliest blade in each cluster (when the cluster starts losing blades).
    /// </summary>
    public void FindClusterBars(
        out float latestTimer, out float latestMaxTime, out int latestBladeCount,
        out float secondTimer, out float secondMaxTime, out int secondBladeCount)
    {
        latestTimer = 0f; latestMaxTime = 0f; latestBladeCount = 0;
        secondTimer = 0f; secondMaxTime = 0f; secondBladeCount = 0;

        if (_clusters.Count == 0) return;

        // Clusters[0] is the newest (sorted by timer descending in Rebuild)
        var c0 = _clusters[0];
        latestTimer = c0.EarliestTimer();
        latestMaxTime = c0.Blades[0].MaxTime;
        latestBladeCount = c0.Blades.Count;

        if (_clusters.Count >= 2)
        {
            var c1 = _clusters[1];
            secondTimer = c1.EarliestTimer();
            secondMaxTime = c1.Blades[0].MaxTime;
            secondBladeCount = c1.Blades.Count;
        }
    }
}
