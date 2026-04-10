using System;
using System.Collections.Generic;

namespace WhatsABladeVortex;

public sealed class BladeInfo
{
    public float Timer;
    public float MaxTime;
    public int ClusterId;

    public float Fraction => MaxTime > 0f ? Math.Clamp(Timer / MaxTime, 0f, 1f) : 0f;

    public void Reset()
    {
        Timer = 0f;
        MaxTime = 0f;
        ClusterId = 0;
    }
}

public sealed class BladeCluster
{
    public int Id;
    public int BladeCount;
    private readonly List<BladeInfo> _blades = new();

    public IReadOnlyList<BladeInfo> Blades => _blades;

    public void Reset()
    {
        Id = 0;
        BladeCount = 0;
        _blades.Clear();
    }

    public void AddBlade(BladeInfo blade)
    {
        _blades.Add(blade);
        BladeCount = _blades.Count;
    }

    public float EarliestTimer()
    {
        float min = float.MaxValue;
        foreach (var b in _blades)
            if (b.Timer < min) min = b.Timer;
        return min == float.MaxValue ? 0f : min;
    }

    public float CastAgoSeconds()
    {
        float maxTimer = 0f;
        float maxTime = 0f;
        foreach (var b in _blades)
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
/// Uses object pooling to avoid per-frame allocations.
/// No persistent state -- rebuilt from scratch each frame from buff reads.
/// </summary>
public sealed class BladeTracker
{
    private const int MaxBlades = 10;
    private const int MaxClusters = 10;

    private readonly BladeInfo[] _bladePool = new BladeInfo[MaxBlades];
    private readonly BladeCluster[] _clusterPool = new BladeCluster[MaxClusters];
    private int _bladeCount;
    private int _clusterCount;

    public int BladeCount => _bladeCount;
    public int ClusterCount => _clusterCount;

    public BladeTracker()
    {
        for (int i = 0; i < MaxBlades; i++)
            _bladePool[i] = new BladeInfo();
        for (int i = 0; i < MaxClusters; i++)
            _clusterPool[i] = new BladeCluster();
    }

    /// <summary>Active clusters (valid indices 0.._clusterCount-1).</summary>
    public BladeCluster GetCluster(int index) => _clusterPool[index];

    /// <summary>
    /// Rebuild from a list of (timer, maxTime) tuples extracted from all
    /// "new_new_blade_vortex" buff entries on the player.
    /// </summary>
    public void Rebuild(List<(float timer, float maxTime)> rawBlades, float clusterWindowSeconds)
    {
        // Reset pools
        for (int i = 0; i < _bladeCount; i++)
            _bladePool[i].Reset();
        for (int i = 0; i < _clusterCount; i++)
            _clusterPool[i].Reset();
        _bladeCount = 0;
        _clusterCount = 0;

        if (rawBlades.Count == 0) return;

        // Sort by timer descending (highest = most recently cast).
        rawBlades.Sort((a, b) => b.timer.CompareTo(a.timer));

        int clusterId = 1;
        BladeCluster? current = null;

        foreach (var (timer, maxTime) in rawBlades)
        {
            if (_bladeCount >= MaxBlades) break;

            var blade = _bladePool[_bladeCount++];
            blade.Timer = timer;
            blade.MaxTime = maxTime;

            // Start a new cluster if this blade's timer is more than the window
            // below the previous blade's timer (i.e. it was cast noticeably earlier).
            bool newCluster = current == null ||
                (current.Blades[current.BladeCount - 1].Timer - timer) > clusterWindowSeconds;

            if (newCluster)
            {
                if (_clusterCount >= MaxClusters) break;
                current = _clusterPool[_clusterCount++];
                current.Id = clusterId++;
            }

            blade.ClusterId = current!.Id;
            current.AddBlade(blade);
        }
    }

    public float EarliestTimer()
    {
        float min = float.MaxValue;
        for (int i = 0; i < _bladeCount; i++)
            if (_bladePool[i].Timer < min) min = _bladePool[i].Timer;
        return min == float.MaxValue ? 0f : min;
    }

    public void FindNextLoss(out int clusterBladeCount, out float expiresIn)
    {
        clusterBladeCount = 0;
        expiresIn = 0f;
        float minEarliest = float.MaxValue;
        BladeCluster? warn = null;
        for (int i = 0; i < _clusterCount; i++)
        {
            var c = _clusterPool[i];
            var e = c.EarliestTimer();
            if (e < minEarliest) { minEarliest = e; warn = c; }
        }
        if (warn != null)
        {
            clusterBladeCount = warn.BladeCount;
            expiresIn = warn.EarliestTimer();
        }
    }

    /// <summary>
    /// Returns the two most recent clusters' earliest-blade timers for the two-bar UI.
    /// Clusters are sorted newest-first (highest timer), so index 0 = latest cast.
    /// </summary>
    public void FindClusterBars(
        out float latestTimer, out float latestMaxTime, out int latestBladeCount,
        out float secondTimer, out float secondMaxTime, out int secondBladeCount)
    {
        latestTimer = 0f; latestMaxTime = 0f; latestBladeCount = 0;
        secondTimer = 0f; secondMaxTime = 0f; secondBladeCount = 0;

        if (_clusterCount == 0) return;

        var c0 = _clusterPool[0];
        latestTimer = c0.EarliestTimer();
        latestMaxTime = c0.Blades[0].MaxTime;
        latestBladeCount = c0.BladeCount;

        if (_clusterCount >= 2)
        {
            var c1 = _clusterPool[1];
            secondTimer = c1.EarliestTimer();
            secondMaxTime = c1.Blades[0].MaxTime;
            secondBladeCount = c1.BladeCount;
        }
    }
}
