using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using SharpDX;
using Color = SharpDX.Color;
using Vec2 = System.Numerics.Vector2;

namespace WhatsABladeVortex;

public enum SnapshotState { Ready, Active, Lost }

public sealed class WhatsABladeVortex : BaseSettingsPlugin<WhatsABladeVortexSettings>
{
    private readonly BladeTracker _tracker = new();
    private readonly SnapshotPanelRenderer _renderer = new();
    private readonly List<(float timer, float maxTime)> _rawBlades = new();

    // Unleash seals
    private int _sealCount;

    // Freshest blade (for debug display)
    private float _freshestTimer;
    private float _freshestMaxTime;

    // Latest cluster bar (main bar - newest cast cluster's fall-off)
    private float _latestClusterTimer;
    private float _latestClusterMaxTime;
    private int _latestClusterBladeCount;

    // 2nd latest cluster bar (small bar - previous cast cluster's fall-off)
    private float _secondClusterTimer;
    private float _secondClusterMaxTime;
    private int _secondClusterBladeCount;

    // Spectre tracking (cached in Tick)
    private SnapshotState _snapshotState = SnapshotState.Ready;
    private bool _spectreAlive;
    private bool _prevSpectreAlive;
    private bool _spectreFound;
    private int _spectreMaxHp;
    private float _spectreHpPct;

    // Minion Pact detection (cached in Tick)
    private bool _hasMinionPact;
    private int _minionPactPermillage;
    private int _calculatedAddedPhys;

    // Alerts
    private DateTime _snapshotLostAt = DateTime.MinValue;
    private bool _canRender;
    private bool _inGracePeriod;

    public override bool Initialise()
    {
        Name = "Whats A Blade Vortex";
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _snapshotState = SnapshotState.Ready;
        _prevSpectreAlive = false;
        _spectreAlive = false;
        _spectreFound = false;
        _spectreMaxHp = 0;
        _spectreHpPct = 0f;
        _hasMinionPact = false;
        _minionPactPermillage = 0;
        _calculatedAddedPhys = 0;
        _snapshotLostAt = DateTime.MinValue;
        _freshestTimer = 0f;
        _freshestMaxTime = 0f;
        _latestClusterTimer = 0f;
        _latestClusterMaxTime = 0f;
        _latestClusterBladeCount = 0;
        _secondClusterTimer = 0f;
        _secondClusterMaxTime = 0f;
        _secondClusterBladeCount = 0;
        _sealCount = 0;
        _rawBlades.Clear();
        _tracker.Rebuild(_rawBlades, 0.15f);
    }

    public override Job? Tick()
    {
        _canRender = false;
        if (!Settings.Enable.Value) return null;
        if (!GameController.InGame) return null;

        var player = GameController.Player;
        if (player == null || !player.IsAlive) return null;

        _inGracePeriod = false;
        var buffs = player.GetComponent<Buffs>()?.BuffsList;
        if (buffs == null) return null;

        for (int i = 0; i < buffs.Count; i++)
        {
            if (buffs[i]?.Name == "grace_period")
            {
                _inGracePeriod = true;
                return null;
            }
        }

        // -- Read buffs --
        int seals = 0;
        float freshestTimer = 0f;
        float freshestMaxTime = 0f;
        bool hasDeadSpectreBuff = false;
        _rawBlades.Clear();

        for (int i = 0; i < buffs.Count; i++)
        {
            var b = buffs[i];
            if (b == null) continue;

            switch (b.Name)
            {
                case "anticipation":
                    seals = b.BuffCharges;
                    break;

                case "new_new_blade_vortex":
                    _rawBlades.Add((b.Timer, b.MaxTime));
                    if (b.Timer > freshestTimer)
                    {
                        freshestTimer = b.Timer;
                        freshestMaxTime = b.MaxTime;
                    }
                    break;

                case "number_of_orb_spectres":
                    hasDeadSpectreBuff = true;
                    break;
            }
        }

        _sealCount = seals;
        _freshestTimer = freshestTimer;
        _freshestMaxTime = freshestMaxTime;

        // -- Always rebuild clusters (compact mode needs cluster-bar data) --
        float clusterWindow = Settings.ClusterWindowMs.Value / 1000f;
        _tracker.Rebuild(_rawBlades, clusterWindow);
        _tracker.FindClusterBars(
            out _latestClusterTimer, out _latestClusterMaxTime, out _latestClusterBladeCount,
            out _secondClusterTimer, out _secondClusterMaxTime, out _secondClusterBladeCount);

        // -- Spectre + Minion Pact detection via Actor --
        var actor = player.GetComponent<Actor>();
        if (actor != null)
        {
            // Minion Pact stat on BV skill
            var bvSkill = actor.ActorSkills?.FirstOrDefault(s => s.InternalName == "blade_vortex");
            _minionPactPermillage = bvSkill?.GetStat(
                GameStat.SupportMinionPactSacrificeNearbyDamageableMinionToGainPermillageOfMaxLifeAsPhysicalDamage) ?? 0;
            _hasMinionPact = _minionPactPermillage > 0;

            // Spectre alive = no "Dead Spectre" buff present.
            // The buff is authoritative; DeployedObjects.Entity.IsAlive is unreliable
            // (wisp entity still reports alive).
            _spectreAlive = !hasDeadSpectreBuff;

            // Read spectre HP from DeployedObjects when alive (for damage calc)
            var spectreSkill = actor.ActorSkills?.FirstOrDefault(s => s.InternalName == "raise_spectre");
            var deployed = spectreSkill?.DeployedObjects;
            _spectreFound = deployed != null && deployed.Count > 0;
            float bestHpPct = 0f;

            if (deployed != null && _spectreAlive)
            {
                foreach (var d in deployed)
                {
                    var entity = d.Entity;
                    if (entity == null || !entity.IsValid) continue;
                    var life = entity.GetComponent<Life>();
                    if (life != null)
                    {
                        int hp = life.MaxHP;
                        // Life returns 1 when component address is null;
                        // only update if we get a real value (> 1)
                        if (hp > 1)
                            _spectreMaxHp = hp;
                        bestHpPct = life.HPPercentage;
                    }
                }
            }

            _spectreHpPct = bestHpPct;
        }

        // Damage calculation (use last known max HP even if spectre is dead)
        _calculatedAddedPhys = _hasMinionPact && _spectreMaxHp > 0
            ? _spectreMaxHp * _minionPactPermillage / 1000
            : 0;

        // -- Snapshot state machine --
        bool hasBlades = _rawBlades.Count > 0;

        switch (_snapshotState)
        {
            case SnapshotState.Ready:
                // Waiting for spectre sacrifice: was alive last tick, now dead, and blades exist
                if (_prevSpectreAlive && !_spectreAlive && hasBlades)
                    _snapshotState = SnapshotState.Active;
                break;

            case SnapshotState.Active:
                // Blades carry snapshotted damage; lost when all expire
                if (!hasBlades)
                {
                    _snapshotState = SnapshotState.Lost;
                    _snapshotLostAt = DateTime.UtcNow;
                }
                break;

            case SnapshotState.Lost:
                // Spectre resurrects -> ready to sacrifice again
                if (_spectreAlive)
                    _snapshotState = SnapshotState.Ready;
                break;
        }

        _prevSpectreAlive = _spectreAlive;
        _canRender = true;
        return null;
    }

    public override void Render()
    {
        if (!_canRender || _inGracePeriod) return;
        if (!GameController.InGame) return;

        var ui = GameController.Game.IngameState.IngameUi;
        if (ui.FullscreenPanels != null)
        {
            foreach (var p in ui.FullscreenPanels)
                if (p?.IsVisible == true) return;
        }

        // Edge flash on snapshot lost
        if (Settings.AlertEdgeFlash.Value && _snapshotLostAt != DateTime.MinValue)
        {
            var elapsed = (float)(DateTime.UtcNow - _snapshotLostAt).TotalSeconds;
            const float dur = 0.6f;
            if (elapsed < dur)
                DrawEdgeFlash(1f - elapsed / dur);
        }

        if (Settings.ShowFullPanel.Value)
        {
            _renderer.DrawFullPanel(Graphics, Settings, _tracker, _sealCount,
                _snapshotState, _spectreAlive, _hasMinionPact, _calculatedAddedPhys,
                _minionPactPermillage);
        }
        else
        {
            _renderer.DrawCompact(Graphics, Settings, _sealCount, _snapshotState,
                _latestClusterTimer, _latestClusterMaxTime, _latestClusterBladeCount,
                _secondClusterTimer, _secondClusterMaxTime, _secondClusterBladeCount,
                _hasMinionPact, _spectreAlive);
        }

        // Debug window (independent of panel mode)
        if (Settings.ShowDebugWindow.Value)
        {
            _renderer.DrawDebugWindow(Graphics, Settings,
                _spectreFound, _spectreAlive, _spectreMaxHp, _spectreHpPct,
                _hasMinionPact, _minionPactPermillage, _calculatedAddedPhys,
                _snapshotState,
                _rawBlades.Count, _tracker.Clusters.Count,
                _latestClusterTimer, _latestClusterBladeCount,
                _secondClusterTimer, _secondClusterBladeCount,
                _sealCount);
        }
    }

    private void DrawEdgeFlash(float intensity)
    {
        var rect = GameController.Window.GetWindowRectangleTimeCache;
        var w = rect.Width;
        var h = rect.Height;
        const float t = 8f;
        var alpha = (byte)(intensity * 200f);
        var red = new Color((byte)224, (byte)64, (byte)64, alpha);

        Graphics.DrawBox(new Vec2(0, 0), new Vec2(w, t), red);
        Graphics.DrawBox(new Vec2(0, h - t), new Vec2(w, h), red);
        Graphics.DrawBox(new Vec2(0, 0), new Vec2(t, h), red);
        Graphics.DrawBox(new Vec2(w - t, 0), new Vec2(w, h), red);
    }
}
