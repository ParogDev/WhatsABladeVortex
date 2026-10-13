using System;
using System.Collections.Generic;
using System.IO;
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

/// <summary>
/// All data needed by the renderer, built once per Tick.
/// Passed to both the overlay renderer and the settings UI.
/// </summary>
public sealed class BladeVortexRenderState
{
    // Seals
    public int SealCount;

    // Snapshot
    public SnapshotState SnapshotState;
    public DateTime SnapshotLostAt;

    // Cluster bar data (compact mode)
    public float LatestClusterTimer;
    public float LatestClusterMaxTime;
    public int LatestClusterBladeCount;
    public float SecondClusterTimer;
    public float SecondClusterMaxTime;
    public int SecondClusterBladeCount;

    // Spectre
    public bool SpectreFound;
    public bool SpectreAlive;
    public int SpectreMaxHp;
    public float SpectreHpPct;

    // Minion Pact
    public bool HasMinionPact;
    public int MinionPactPermillage;
    public int CalculatedAddedPhys;

    // Blade totals
    public int BladeCount;

    // Skill icon
    public string? SkillIconPath;

    // Sounds directory (for settings UI)
    public string SoundsDirectory = "";

    // Callback for test-sound button in settings UI
    public Action? PlayTestSound;

    // Tracker reference (for full panel cluster iteration)
    public BladeTracker Tracker = null!;
}

public sealed class WhatsABladeVortex : BaseSettingsPlugin<WhatsABladeVortexSettings>
{
    private readonly BladeTracker _tracker = new();
    private readonly SnapshotPanelRenderer _renderer = new();
    private readonly BladeVortexRenderState _renderState = new();
    private readonly List<(float timer, float maxTime)> _rawBlades = new();

    // Snapshot state machine
    private SnapshotState _snapshotState = SnapshotState.Ready;
    private bool _prevSpectreAlive;
    private int _spectreMaxHp;
    private DateTime _snapshotLostAt = DateTime.MinValue;

    // Icon
    private const string BvIconFile = "bv_icon.png";
    private bool _iconLoaded;

    // Render guards
    private bool _canRender;
    private bool _inGracePeriod;

    // Custom settings UI
    private BladeVortexSettingsUi? _settingsUi;

    public override bool Initialise()
    {
        Name = "Whats A Blade Vortex";

        var soundsDir = Path.GetFullPath(Path.Combine(DirectoryFullName, "..", "..", "..", "Sounds"));
        if (!Directory.Exists(soundsDir))
            Directory.CreateDirectory(soundsDir);
        _renderState.SoundsDirectory = soundsDir;
        _renderState.PlayTestSound = TryPlaySnapshotLostSound;

        // Pre-load BV skill icon (shipped as bv_icon.png in plugin dir)
        var iconPath = Path.Combine(DirectoryFullName, BvIconFile);
        if (File.Exists(iconPath))
        {
            Graphics.InitImage(BvIconFile, iconPath);
            _iconLoaded = true;
            _renderState.SkillIconPath = BvIconFile;
        }

        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _snapshotState = SnapshotState.Ready;
        _prevSpectreAlive = false;
        _spectreMaxHp = 0;
        _snapshotLostAt = DateTime.MinValue;
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
                    break;

                case "number_of_orb_spectres":
                    hasDeadSpectreBuff = true;
                    break;
            }
        }

        // -- Always rebuild clusters (compact mode needs cluster-bar data) --
        float clusterWindow = Settings.ClusterWindowMs.Value / 1000f;
        _tracker.Rebuild(_rawBlades, clusterWindow);
        _tracker.FindClusterBars(
            out var latestTimer, out var latestMaxTime, out var latestBladeCount,
            out var secondTimer, out var secondMaxTime, out var secondBladeCount);

        // -- Spectre + Minion Pact detection via Actor --
        bool spectreAlive = false;
        bool spectreFound = false;
        float spectreHpPct = 0f;
        bool hasMinionPact = false;
        int minionPactPermillage = 0;

        var actor = player.GetComponent<Actor>();
        if (actor != null)
        {
            var bvSkill = actor.ActorSkills?.FirstOrDefault(s => s.InternalName == "blade_vortex");
            minionPactPermillage = bvSkill?.GetStat(
                GameStat.SupportMinionPactSacrificeNearbyDamageableMinionToGainPermillageOfMaxLifeAsPhysicalDamage) ?? 0;
            hasMinionPact = minionPactPermillage > 0;

            spectreAlive = !hasDeadSpectreBuff;

            var spectreSkill = actor.ActorSkills?.FirstOrDefault(s => s.InternalName == "raise_spectre");
            var deployed = spectreSkill?.DeployedObjects;
            spectreFound = deployed != null && deployed.Count > 0;

            if (deployed != null && spectreAlive)
            {
                foreach (var d in deployed)
                {
                    var entity = d.Entity;
                    if (entity == null || !entity.IsValid) continue;
                    var life = entity.GetComponent<Life>();
                    if (life != null)
                    {
                        int hp = life.MaxHP;
                        if (hp > 1)
                            _spectreMaxHp = hp;
                        spectreHpPct = life.HPPercentage;
                    }
                }
            }
        }

        int calculatedAddedPhys = hasMinionPact && _spectreMaxHp > 0
            ? _spectreMaxHp * minionPactPermillage / 1000
            : 0;

        // -- Snapshot state machine --
        bool hasBlades = _rawBlades.Count > 0;

        switch (_snapshotState)
        {
            case SnapshotState.Ready:
                if (_prevSpectreAlive && !spectreAlive && hasBlades)
                    _snapshotState = SnapshotState.Active;
                break;

            case SnapshotState.Active:
                if (!hasBlades)
                {
                    _snapshotState = SnapshotState.Lost;
                    _snapshotLostAt = DateTime.UtcNow;
                    TryPlaySnapshotLostSound();
                }
                break;

            case SnapshotState.Lost:
                if (spectreAlive)
                    _snapshotState = SnapshotState.Ready;
                break;
        }

        _prevSpectreAlive = spectreAlive;

        // -- Build render state DTO --
        _renderState.SealCount = seals;
        _renderState.SnapshotState = _snapshotState;
        _renderState.SnapshotLostAt = _snapshotLostAt;
        _renderState.LatestClusterTimer = latestTimer;
        _renderState.LatestClusterMaxTime = latestMaxTime;
        _renderState.LatestClusterBladeCount = latestBladeCount;
        _renderState.SecondClusterTimer = secondTimer;
        _renderState.SecondClusterMaxTime = secondMaxTime;
        _renderState.SecondClusterBladeCount = secondBladeCount;
        _renderState.SpectreFound = spectreFound;
        _renderState.SpectreAlive = spectreAlive;
        _renderState.SpectreMaxHp = _spectreMaxHp;
        _renderState.SpectreHpPct = spectreHpPct;
        _renderState.HasMinionPact = hasMinionPact;
        _renderState.MinionPactPermillage = minionPactPermillage;
        _renderState.CalculatedAddedPhys = calculatedAddedPhys;
        _renderState.BladeCount = _rawBlades.Count;
        _renderState.Tracker = _tracker;

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
        if (Settings.AlertEdgeFlash.Value && _renderState.SnapshotLostAt != DateTime.MinValue)
        {
            var elapsed = (float)(DateTime.UtcNow - _renderState.SnapshotLostAt).TotalSeconds;
            const float dur = 0.6f;
            if (elapsed < dur)
                DrawEdgeFlash(1f - elapsed / dur);
        }

        if (Settings.ShowFullPanel.Value)
            _renderer.DrawFullPanel(Graphics, Settings, _renderState);
        else
            _renderer.DrawCompact(Graphics, Settings, _renderState);

        if (Settings.ShowDebugWindow.Value)
            _renderer.DrawDebugWindow(Graphics, Settings, _renderState);
    }

    public override void DrawSettings()
    {
        _settingsUi ??= new BladeVortexSettingsUi();
        _settingsUi.Draw(Settings, _renderState);
    }

    private void TryPlaySnapshotLostSound()
    {
        if (!Settings.AlertSound.Value)
            return;

        var file = Settings.SoundFile.Value?.Trim();
        if (string.IsNullOrWhiteSpace(file))
            return;

        var name = Path.GetFileNameWithoutExtension(file);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var soundPath = Path.Combine("..", "Sounds", name).Replace('\\', '/');
        float volume = Settings.SoundVolume.Value / 100f;
        GameController.SoundController.PlaySound(soundPath, volume);
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
