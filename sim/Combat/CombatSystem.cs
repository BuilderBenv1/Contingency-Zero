using System.Collections.Generic;
using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.State;

namespace ContingencyZero.Sim.Combat;

/// <summary>
/// Target acquisition and firing for every weapon. Lasers and flak hit instantly; cannons,
/// artillery, missiles and bombs launch projectiles that <see cref="ProjectileSystem"/> flies.
/// Units only shoot at what their team can see (artillery also fires at radar contacts).
/// </summary>
public static class CombatSystem
{

    public static void Tick(World world)
    {
        UnitStore units = world.Units;
        UnitCatalog catalog = world.Catalog;
        int count = units.Count;
        // Alternate the order units act in, tick by tick, so neither low nor high unit ids (and so
        // neither player slot) systematically shoots first.
        bool reverse = (world.Tick & 1) == 1;

        for (int k = 0; k < count; k++)
        {
            int id = reverse ? count - 1 - k : k;
            if (!units.Alive[id] || !units.Complete[id]) continue;
            UnitDef def = catalog[units.TypeIndex[id]];
            if (!def.IsArmed) continue;

            for (int slot = 0; slot < def.Weapons.Count && slot < UnitStore.MaxWeapons; slot++)
            {
                WeaponDef w = def.Weapons[slot];
                int reload = ReloadOf(units, id, slot);
                if (reload > 0) SetReload(units, id, slot, reload - 1);
                if (w.Kind == WeaponKind.DGun) continue; // fired only by order

                int target = PickTarget(world, id, def, w, slot, out FixVec2 groundAim, out bool ground);
                SetTarget(units, id, slot, target);
                if ((target < 0 && !ground) || ReloadOf(units, id, slot) > 0) continue;

                SetReload(units, id, slot, w.ReloadTicks);
                FixVec2 aim = target >= 0 ? units.Position[target] : groundAim;
                Fire(world, id, slot, w, target, aim);
            }
        }
    }

    public static int ReloadOf(UnitStore u, int id, int slot) => slot == 0 ? u.Reload0[id] : u.Reload1[id];
    private static void SetReload(UnitStore u, int id, int slot, int v) { if (slot == 0) u.Reload0[id] = v; else u.Reload1[id] = v; }
    private static int TargetOf(UnitStore u, int id, int slot) => slot == 0 ? u.Target0[id] : u.Target1[id];
    private static void SetTarget(UnitStore u, int id, int slot, int v) { if (slot == 0) u.Target0[id] = v; else u.Target1[id] = v; }

    public static int DGunSlot(UnitDef def)
    {
        for (int i = 0; i < def.Weapons.Count; i++) if (def.Weapons[i].Kind == WeaponKind.DGun) return i;
        return -1;
    }

    public static Fix64 MaxRange(UnitDef def)
    {
        Fix64 r = Fix64.Zero;
        foreach (WeaponDef w in def.Weapons) if (w.Kind != WeaponKind.DGun && w.Range > r) r = w.Range;
        return r;
    }

    public static Fix64 BestGroundRange(UnitDef def)
    {
        Fix64 r = Fix64.Zero;
        foreach (WeaponDef w in def.Weapons) if (w.TargetsGround && w.Kind != WeaponKind.DGun && w.Range > r) r = w.Range;
        return r;
    }

    /// <summary>Longest range of any weapon that can hit this target (by air or ground).</summary>
    public static Fix64 BestRangeAgainst(World world, UnitDef def, int target)
    {
        bool air = world.Catalog[world.Units.TypeIndex[target]].IsAir;
        Fix64 r = Fix64.Zero;
        foreach (WeaponDef w in def.Weapons)
        {
            if (w.Kind == WeaponKind.DGun) continue;
            if (air ? w.TargetsAir : w.TargetsGround) r = Fix64.Max(r, w.Range);
        }
        return r;
    }

    public static bool CanEverHit(World world, UnitDef def, int target)
    {
        bool air = world.Catalog[world.Units.TypeIndex[target]].IsAir;
        foreach (WeaponDef w in def.Weapons)
            if (w.Kind != WeaponKind.DGun && (air ? w.TargetsAir : w.TargetsGround)) return true;
        return false;
    }

    private static bool WeaponCanHit(World world, WeaponDef w, int target)
    {
        bool air = world.Catalog[world.Units.TypeIndex[target]].IsAir;
        return air ? w.TargetsAir : w.TargetsGround;
    }

    private static bool InRange(FixVec2 from, FixVec2 to, WeaponDef w)
    {
        Fix64 d = FixVec2.DistanceSquared(from, to);
        return d <= w.Range * w.Range && d >= w.MinRange * w.MinRange;
    }

    private static int PickTarget(World world, int id, UnitDef def, WeaponDef w, int slot, out FixVec2 groundAim, out bool ground)
    {
        UnitStore units = world.Units;
        FixVec2 pos = units.Position[id];
        int team = world.TeamOf(id);
        bool ballistic = w.Kind == WeaponKind.Artillery;
        groundAim = default;
        ground = false;

        // Orders come first.
        Order order = units.CurrentOrder(id);
        if (order.Type == OrderType.Attack && units.IsValid(order.Target)
            && WeaponCanHit(world, w, order.Target) && InRange(pos, units.Position[order.Target], w)
            && world.CanSee(team, order.Target, radarCounts: true))
            return order.Target;
        if (order.Type == OrderType.AttackGround && w.TargetsGround && InRange(pos, order.Position, w))
        {
            groundAim = order.Position;
            ground = true;
            return -1;
        }

        FireState fire = units.Fire[id];
        if (fire == FireState.HoldFire) return -1;

        bool Valid(int t) =>
            units.IsValid(t)
            && world.Players[units.Owner[t]].Team != team
            && WeaponCanHit(world, w, t)
            && InRange(pos, units.Position[t], w)
            && world.CanSee(team, t, radarCounts: ballistic);

        if (fire == FireState.ReturnFire)
        {
            int attacker = units.LastAttacker[id];
            return Valid(attacker) ? attacker : -1;
        }

        int current = TargetOf(units, id, slot);
        if (Valid(current)) return current;

        return world.Grid.FindNearest(units, pos, w.Range, Valid, w.MinRange);
    }

    private static void Fire(World world, int id, int slot, WeaponDef w, int target, FixVec2 aim)
    {
        UnitStore units = world.Units;
        FixVec2 from = units.Position[id];
        int owner = units.Owner[id];
        world.Events.Add(new GameEvent(GameEventType.WeaponFired, id, target, slot, (byte)w.Kind, from, aim));

        Fix64 accuracy = Fix64.Min(Fix64.One, w.Accuracy + Fix64.FromRatio(VeteranRank(units.Kills[id]), 20));
        bool hit = world.Rng.Chance(accuracy);

        switch (w.Kind)
        {
            case WeaponKind.Laser:
            case WeaponKind.Flak:
                if (hit && target >= 0) DealDamage(world, target, w.Damage, id);
                break;

            case WeaponKind.Cannon:
            case WeaponKind.Artillery:
            case WeaponKind.Bomb:
            {
                FixVec2 landing = aim;
                if (!hit)
                {
                    // Scatter grows with distance.
                    Fix64 dist = FixVec2.Distance(from, aim);
                    Fix64 spread = Fix64.FromInt(2) + dist / 8;
                    landing = aim + new FixVec2(
                        (world.Rng.NextFix() * 2 - Fix64.One) * spread,
                        (world.Rng.NextFix() * 2 - Fix64.One) * spread);
                }
                Launch(world, id, owner, w, landing, hit ? target : -1, homing: false);
                break;
            }

            case WeaponKind.Missile:
                Launch(world, id, owner, w, aim, target, homing: true);
                break;
        }
    }

    public static int VeteranRank(int kills) => kills >= 15 ? 3 : kills >= 7 ? 2 : kills >= 3 ? 1 : 0;

    private static void Launch(World world, int id, int owner, WeaponDef w, FixVec2 aim, int target, bool homing)
    {
        FixVec2 from = world.Units.Position[id];
        Fix64 speed = w.ProjectileSpeedPerTick > Fix64.Zero ? w.ProjectileSpeedPerTick : Fix64.FromInt(4);
        int flight;
        if (w.Kind == WeaponKind.Bomb)
        {
            flight = 12;
            speed = FixVec2.Distance(from, aim) / flight;
            if (speed < Fix64.FromRatio(1, 10)) speed = Fix64.FromRatio(1, 10);
        }
        else
        {
            Fix64 dist = FixVec2.Distance(from, aim);
            flight = (dist / speed).ToIntFloor() + 1;
        }

        world.Projectiles.Add(new Projectile
        {
            Id = world.NextProjectileId(),
            Kind = w.Kind,
            Owner = owner,
            SourceUnit = id,
            Team = world.Players[owner].Team,
            Start = from,
            Position = from,
            Aim = aim,
            TargetUnit = homing || w.Kind == WeaponKind.Cannon ? target : -1,
            SpeedPerTick = speed,
            Damage = w.Damage,
            Splash = w.Splash,
            HitsAir = w.TargetsAir,
            FlightTicks = flight,
        });
    }

    /// <summary>The Breach Cannon: a slow bolt in a straight line that destroys everything it touches.</summary>
    public static void FireDGun(World world, int id, int slot, FixVec2 aim)
    {
        UnitDef def = world.Catalog[world.Units.TypeIndex[id]];
        WeaponDef w = def.Weapons[slot];
        if (slot == 0) world.Units.Reload0[id] = w.ReloadTicks; else world.Units.Reload1[id] = w.ReloadTicks;
        FixVec2 from = world.Units.Position[id];
        FixVec2 dir = aim - from;
        Fix64 len = dir.Length();
        FixVec2 end = len > Fix64.Zero ? from + dir * (w.Range / len) : aim;
        int owner = world.Units.Owner[id];
        world.Events.Add(new GameEvent(GameEventType.WeaponFired, id, -1, slot, (byte)WeaponKind.DGun, from, end));
        world.Projectiles.Add(new Projectile
        {
            Id = world.NextProjectileId(),
            Kind = WeaponKind.DGun,
            Owner = owner,
            SourceUnit = id,
            Team = world.Players[owner].Team,
            Start = from,
            Position = from,
            Aim = end,
            SpeedPerTick = w.ProjectileSpeedPerTick,
            Damage = w.Damage,
            Splash = w.Splash,
            FlightTicks = (w.Range / w.ProjectileSpeedPerTick).ToIntFloor() + 1,
        });
    }

    public static void DealDamage(World world, int target, Fix64 damage, int attacker)
    {
        UnitStore units = world.Units;
        if (!units.Alive[target] || units.Health[target] <= Fix64.Zero) return;
        units.Health[target] -= damage;
        if (attacker >= 0)
        {
            units.LastAttacker[target] = attacker;
            if (units.Health[target] <= Fix64.Zero && units.Alive[attacker]
                && world.AreEnemies(units.Owner[attacker], units.Owner[target]))
            {
                units.Kills[attacker]++;
                world.Players[units.Owner[attacker]].Kills++;
            }
        }
    }

    /// <summary>Area damage: full at the centre, falling to a quarter at the edge. Hits friend and foe.</summary>
    public static void Splash(World world, FixVec2 centre, Fix64 damage, Fix64 radius, int source, bool hitsAir)
    {
        if (radius <= Fix64.Zero) return;
        UnitStore units = world.Units;
        var hits = world.ScratchHits;
        hits.Clear();
        world.Grid.Query(units, centre, radius, hits, Fix64.FromInt(8));
        foreach (int id in hits)
        {
            if (!units.Alive[id]) continue;
            UnitDef def = world.Catalog[units.TypeIndex[id]];
            if (def.IsAir && !hitsAir) continue;
            Fix64 size = def.IsStructure ? Fix64.FromInt(def.Footprint * 3) : def.Radius;
            Fix64 d = FixVec2.Distance(centre, units.Position[id]) - size;
            if (d < Fix64.Zero) d = Fix64.Zero;
            if (d > radius) continue;
            Fix64 falloff = Fix64.One - (d / radius) * Fix64.Parse("0.75");
            DealDamage(world, id, damage * falloff, source);
        }
    }
}
