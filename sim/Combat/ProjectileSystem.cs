using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Combat;

/// <summary>
/// Flies every shell, missile, bomb and Breach Cannon bolt one tick, and resolves the ones that
/// arrive. Shells and bombs land where they were aimed, so a target that moves away is missed.
/// </summary>
internal static class ProjectileSystem
{
    private static readonly Fix64 DGunRadius = Fix64.FromInt(3);
    private static readonly Fix64 MinSplash = Fix64.Parse("1.5");

    public static void Tick(World world)
    {
        UnitStore units = world.Units;
        foreach (Projectile p in world.Projectiles)
        {
            if (p.Done) continue;
            p.TicksAlive++;

            if (p.Kind == WeaponKind.DGun)
            {
                TickDGun(world, p);
                continue;
            }

            if (p.Kind == WeaponKind.Missile && units.IsValid(p.TargetUnit))
                p.Aim = units.Position[p.TargetUnit];

            p.Position = FixVec2.MoveTowards(p.Position, p.Aim, p.SpeedPerTick);
            if (p.Position != p.Aim && p.TicksAlive < 400) continue;

            p.Done = true;
            if (p.Kind == WeaponKind.Missile)
            {
                if (units.IsValid(p.TargetUnit)) CombatSystem.DealDamage(world, p.TargetUnit, p.Damage, p.SourceUnit);
            }
            else
            {
                CombatSystem.Splash(world, p.Position, p.Damage, Fix64.Max(p.Splash, MinSplash), p.SourceUnit, p.HitsAir);
            }
            world.Events.Add(new GameEvent(GameEventType.Impact, p.SourceUnit, p.TargetUnit, p.Splash.ToIntFloor(), (byte)p.Kind, p.Position));
        }

        world.Projectiles.RemoveAll(p => p.Done);
    }

    private static void TickDGun(World world, Projectile p)
    {
        UnitStore units = world.Units;
        p.Position = FixVec2.MoveTowards(p.Position, p.Aim, p.SpeedPerTick);

        var hits = world.ScratchHits;
        hits.Clear();
        world.Grid.Query(units, p.Position, DGunRadius, hits, Fix64.FromInt(10));
        foreach (int id in hits)
        {
            if (id == p.SourceUnit || !units.Alive[id]) continue;
            UnitDef def = world.Catalog[units.TypeIndex[id]];
            if (def.IsAir) continue;
            Fix64 reach = DGunRadius + (def.IsStructure ? Fix64.FromInt(def.Footprint * 4) : def.Radius);
            if (FixVec2.DistanceSquared(p.Position, units.Position[id]) > reach * reach) continue;
            CombatSystem.DealDamage(world, id, p.Damage, p.SourceUnit);
        }

        foreach (Feature f in world.Features)
        {
            if (f.Gone) continue;
            Fix64 reach = DGunRadius + f.Radius;
            if (FixVec2.DistanceSquared(p.Position, f.Position) <= reach * reach)
            {
                f.Metal = Fix64.Zero;
                f.Energy = Fix64.Zero;
            }
        }

        if (p.Position == p.Aim || p.TicksAlive >= p.FlightTicks)
        {
            p.Done = true;
            world.Events.Add(new GameEvent(GameEventType.Impact, p.SourceUnit, -1, p.Splash.ToIntFloor(), (byte)WeaponKind.DGun, p.Position));
        }
    }
}
