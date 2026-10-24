using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Data;
using Godot;

namespace ContingencyZero.Game.View.Meshes;

/// <summary>
/// Procedural low-poly models for every unit, structure and map feature. Nothing is loaded from
/// disk: each mesh is composed from primitives (<see cref="MeshKit"/>) the first time it is asked
/// for, then cached.
///
/// The model is chosen from the unit's (faction, role). Conventions:
/// <list type="bullet">
/// <item>+Y up, ground at y = 0, models face -Z; factory bay doors open towards +Z.</item>
/// <item>Mobile units are about 2.4 × radius long; ships sit with the waterline at y = 0; aircraft are modelled around y = 0.</item>
/// <item>Structures fill footprint × 8 units square, centred, with a 0.5-unit margin.</item>
/// <item>Vertex alpha encodes the material: 1 plain, 0 team colour (RGB brightness modulates the player colour), 0.5 glow.</item>
/// </list>
/// Meshes are flat shaded (per-face normals), one surface, no material: the renderer applies its own shader.
/// </summary>
public static class UnitModels
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, UnitModel> ModelCache = new();
    private static readonly Dictionary<int, ArrayMesh> WreckCache = new();
    private static readonly Dictionary<string, ArrayMesh> FeatureCache = new();

    /// <summary>Builds (and caches by unit id) the model for a unit type.</summary>
    public static UnitModel For(UnitDef def)
    {
        lock (Gate)
        {
            if (ModelCache.TryGetValue(def.Id, out UnitModel? cached)) return cached;
            UnitModel model = Build(def);
            ModelCache[def.Id] = model;
            return model;
        }
    }

    /// <summary>A generic debris pile for wrecks, roughly <paramref name="size"/> world units across. Cached by rounded size.</summary>
    public static ArrayMesh Wreck(float size)
    {
        int key = Math.Max(1, (int)MathF.Round(size));
        lock (Gate)
        {
            if (WreckCache.TryGetValue(key, out ArrayMesh? mesh)) return mesh;
            mesh = Features.Wreck(key);
            WreckCache[key] = mesh;
            return mesh;
        }
    }

    /// <summary>The ruined foundation a structure leaves, <paramref name="width"/> world units square; variant 0..2. Cached.</summary>
    public static ArrayMesh Foundation(int width, int variant) =>
        Feature("foundation", width.ToString(), variant, (w, v) => Features.Foundation(int.Parse(w), v));

    /// <summary>A small heap of structure rubble; variant 0..2. Cached.</summary>
    public static ArrayMesh Rubble(int variant) => Feature("rubble", "", variant, (_, v) => Features.Rubble(v));

    /// <summary>A low-poly tree for the map theme ("temperate", "desert", "lava", "arctic"); variant 0..2.</summary>
    public static ArrayMesh Tree(string theme, int variant) => Feature("tree", theme, variant, Features.Tree);

    /// <summary>A low-poly rock for the map theme; variant 0..2.</summary>
    public static ArrayMesh Rock(string theme, int variant) => Feature("rock", theme, variant, Features.Rock);

    private static ArrayMesh Feature(string kind, string theme, int variant, Func<string, int, ArrayMesh> make)
    {
        variant = ((variant % 3) + 3) % 3;
        string key = $"{kind}:{theme}:{variant}";
        lock (Gate)
        {
            if (FeatureCache.TryGetValue(key, out ArrayMesh? mesh)) return mesh;
            mesh = make(theme, variant);
            FeatureCache[key] = mesh;
            return mesh;
        }
    }

    // ---- Dispatch ----------------------------------------------------------------------------

    private delegate void RoleBuilder(ModelRig rig, Style style, UnitDef def);

    private static readonly Dictionary<string, RoleBuilder> Roles = new()
    {
        // Bots
        ["commander"] = Models.Commander,
        ["builder_bot"] = Models.BuilderBot,
        ["scout_bot"] = Models.ScoutBot,
        ["assault_bot"] = Models.AssaultBot,
        ["aa_bot"] = Models.AaBot,
        ["mortar_bot"] = Models.MortarBot,
        ["amphibious"] = Models.Amphibious,
        // Vehicles
        ["builder_vehicle"] = Models.BuilderVehicle,
        ["tank"] = Models.Tank,
        ["flak_vehicle"] = Models.FlakVehicle,
        ["artillery_vehicle"] = Models.ArtilleryVehicle,
        ["salvager"] = Models.Salvager,
        ["builder_adv"] = Models.BuilderAdv,
        ["heavy_assault"] = Models.HeavyAssault,
        ["long_artillery"] = Models.LongArtillery,
        // Aircraft
        ["builder_air"] = Models.BuilderAir,
        ["fighter"] = Models.Fighter,
        ["bomber"] = Models.Bomber,
        ["gunship"] = Models.Gunship,
        // Ships
        ["builder_sea"] = Models.BuilderSea,
        ["patrol_boat"] = Models.PatrolBoat,
        ["destroyer"] = Models.Destroyer,
        ["battleship"] = Models.Battleship,
        // Experimentals
        ["experimental"] = Models.Experimental,
        // Economy
        ["metal_extractor"] = Models.MetalExtractor,
        ["deep_extractor"] = Models.DeepExtractor,
        ["solar_collector"] = Models.SolarCollector,
        ["wind_turbine"] = Models.WindTurbine,
        ["metal_storage"] = Models.MetalStorage,
        ["energy_storage"] = Models.EnergyStorage,
        ["metal_maker"] = Models.MetalMaker,
        ["fusion_reactor"] = Models.FusionReactor,
        // Defence and intel
        ["light_turret"] = Models.LightTurret,
        ["aa_turret"] = Models.AaTurret,
        ["heavy_turret"] = Models.HeavyTurret,
        ["plasma_battery"] = Models.PlasmaBattery,
        ["radar_tower"] = Models.RadarTower,
        // Factories
        ["factory_bot"] = Models.FactoryBot,
        ["factory_vehicle"] = Models.FactoryVehicle,
        ["factory_air"] = Models.FactoryAir,
        ["factory_ship"] = Models.FactoryShip,
        ["factory_adv"] = Models.FactoryAdv,
        ["factory_gantry"] = Models.FactoryGantry,
    };

    /// <summary>
    /// Light bots are narrow compared with vehicles of the same collision radius, so they are drawn
    /// a little larger than their radius to keep their silhouettes readable from the RTS camera.
    /// </summary>
    private static float VisualScale(string role) => role switch
    {
        "scout_bot" => 1.25f,
        "assault_bot" or "aa_bot" => 1.15f,
        "builder_bot" or "mortar_bot" => 1.1f,
        _ => 1f,
    };

    /// <summary>True if (role) has a dedicated model; unknown roles fall back to a generic block.</summary>
    internal static bool HasRole(string role) => Roles.ContainsKey(role);

    private static UnitModel Build(UnitDef def)
    {
        if (!def.IsStructure && CustomModels.TryLoad(def, Math.Max(0.5f, def.Radius.Raw / 65536f) * VisualScale(def.Role)) is { } custom)
            return custom;
        Style style = Style.For(def.Faction);
        ModelRig rig = def.IsStructure
            ? ModelRig.ForStructure(def.Footprint)
            : ModelRig.ForMobile(Math.Max(0.5f, def.Radius.Raw / 65536f) * VisualScale(def.Role));
        if (Roles.TryGetValue(def.Role, out RoleBuilder? build)) build(rig, style, def);
        else Models.Generic(rig, style, def);
        return rig.Finish();
    }
}

/// <summary>
/// Collects the parts of one model while it is being composed. Mobile units are authored at
/// radius 1 (about 2.4 long, nose at -1.2) and scaled to their real radius; structures are
/// authored directly in world units.
/// </summary>
internal sealed class ModelRig
{
    /// <summary>World units per authoring unit.</summary>
    public float Scale { get; }

    /// <summary>Structures: half the usable edge (footprint × 4 − 0.5), in world units. Mobiles: 1.2.</summary>
    public float Half { get; }

    public bool IsStructure { get; }

    public MeshKit Body { get; }
    public MeshKit? Turret { get; private set; }
    public MeshKit? Rotor { get; private set; }

    private Vector3 _turretPivot, _rotorPivot, _rotorAxis = Vector3.Up;

    private ModelRig(float scale, float half, bool structure)
    {
        Scale = scale;
        Half = half;
        IsStructure = structure;
        Body = new MeshKit(Root);
    }

    private Transform3D Root => new(Basis.Identity.Scaled(new Vector3(Scale, Scale, Scale)), Vector3.Zero);

    public static ModelRig ForMobile(float radius) => new(radius, 1.2f, false);

    public static ModelRig ForStructure(int footprint) => new(1f, Math.Max(1, footprint) * 4f - 0.5f, true);

    /// <summary>Starts the turret part; <paramref name="pivot"/> is in authoring space. Author the turret around its own origin, barrels towards -Z.</summary>
    public MeshKit AddTurret(Vector3 pivot)
    {
        _turretPivot = pivot;
        return Turret = new MeshKit(Root);
    }

    /// <summary>Starts the spinning part, authored around its own origin, spinning about <paramref name="axis"/>.</summary>
    public MeshKit AddRotor(Vector3 pivot, Vector3 axis)
    {
        _rotorPivot = pivot;
        _rotorAxis = axis.Normalized();
        return Rotor = new MeshKit(Root);
    }

    public UnitModel Finish()
    {
        float height = Body.Max.Y;
        Vector3 turretPivot = _turretPivot * Scale, rotorPivot = _rotorPivot * Scale;
        if (Turret is { IsEmpty: false }) height = Math.Max(height, turretPivot.Y + Turret.Max.Y);
        if (Rotor is { IsEmpty: false })
        {
            float top = Mathf.Abs(_rotorAxis.Y) > 0.99f ? Rotor.Max.Y : Rotor.MaxDistanceFrom(Vector3.Zero);
            height = Math.Max(height, rotorPivot.Y + top);
        }

        float radius;
        if (IsStructure)
        {
            radius = (Half + 0.5f) * 1.12f;
        }
        else
        {
            // Horizontal reach of the body, a little generous, but never under the sim radius.
            float reach = Math.Max(Math.Max(-Body.Min.X, Body.Max.X), Math.Max(-Body.Min.Z, Body.Max.Z));
            radius = Math.Max(Scale, reach * 0.9f);
        }

        return new UnitModel
        {
            Body = Body.ToMesh(),
            Turret = Turret is { IsEmpty: false } ? Turret.ToMesh() : null,
            TurretPivot = turretPivot,
            Rotor = Rotor is { IsEmpty: false } ? Rotor.ToMesh() : null,
            RotorPivot = rotorPivot,
            RotorAxis = _rotorAxis,
            Height = Math.Max(0.5f, height),
            Radius = radius,
        };
    }
}
