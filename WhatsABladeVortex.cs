using System;
using System.Collections.Generic;
using System.Numerics;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using SharpDX;
using Color = SharpDX.Color;
using Vec2 = System.Numerics.Vector2;

namespace WhatsABladeVortex;

public sealed class WhatsABladeVortex : BaseSettingsPlugin<WhatsABladeVortexSettings>
{
    private readonly BladeTracker _tracker = new();
    private readonly SnapshotPanelRenderer _renderer = new();
    private readonly List<(float timer, float maxTime)> _rawBlades = new();

    private int _sealCount;
    private float _freshestTimer;
    private float _freshestMaxTime;
    private bool _snapshotAlive;
    private bool _prevSnapshotAlive;
    private bool _spectreDead;
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
        _snapshotAlive = false;
        _prevSnapshotAlive = false;
        _spectreDead = false;
        _snapshotLostAt = DateTime.MinValue;
        _freshestTimer = 0f;
        _freshestMaxTime = 0f;
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

        int seals = 0;
        bool spectreDead = false;
        float freshestTimer = 0f;
        float freshestMaxTime = 0f;
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
                    spectreDead = true;
                    break;
            }
        }

        _sealCount = seals;
        _spectreDead = spectreDead;
        _freshestTimer = freshestTimer;
        _freshestMaxTime = freshestMaxTime;

        // Build clusters if full panel is on.
        if (Settings.ShowFullPanel.Value)
        {
            float clusterWindow = Settings.ClusterWindowMs.Value / 1000f;
            _tracker.Rebuild(_rawBlades, clusterWindow);
        }

        _snapshotAlive = _rawBlades.Count > 0;

        if (_prevSnapshotAlive && !_snapshotAlive)
            _snapshotLostAt = DateTime.UtcNow;

        _prevSnapshotAlive = _snapshotAlive;
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

        // Edge flash (independent toggle).
        if (Settings.AlertEdgeFlash.Value && _snapshotLostAt != DateTime.MinValue)
        {
            var elapsed = (float)(DateTime.UtcNow - _snapshotLostAt).TotalSeconds;
            const float dur = 0.6f;
            if (elapsed < dur)
                DrawEdgeFlash(1f - elapsed / dur);
        }

        if (Settings.ShowFullPanel.Value)
        {
            _renderer.DrawFullPanel(Graphics, Settings, _tracker, _sealCount, _snapshotAlive, _spectreDead);
            return;
        }

        // Compact mode: seals + snapshot bar, whichever are enabled.
        _renderer.DrawCompact(Graphics, Settings, _sealCount, _snapshotAlive,
            _freshestTimer, _freshestMaxTime, _rawBlades.Count);
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
