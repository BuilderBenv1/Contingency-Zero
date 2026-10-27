using ContingencyZero.Sim.Core;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Pathing;

namespace ContingencyZero.Sim;

public sealed partial class World
{
    /// <summary>
    /// Claims a free destination tile for a unit, spiralling out from the requested point when it
    /// is occupied, reserved, or unsuitable for the mover. The returned position is also the order's
    /// new goal, so path completion and reservation ownership refer to the same place.
    /// </summary>
    internal FixVec2 ReserveDestination(int id, MoveClass moveClass, FixVec2 requested)
    {
        int maxRadius = Math2.Max(Terrain.TilesX, Terrain.TilesY);
        int sx = MapData.TileOf(Units.Position[id].X), sy = MapData.TileOf(Units.Position[id].Y);
        int startZone = Terrain.ZoneOf(moveClass, sx, sy);
        FixVec2 goal = Pathfinder.NearestPassable(moveClass, requested, maxRadius, tile =>
        {
            int owner = Units.GoalOwner(tile);
            if (owner >= 0 && owner != id) return false;
            // Snapping to another island would claim the wrong bank and leave A* to undo the
            // reservation later. Keep the destination in the mover's current connected region.
            return startZone < 0 || Terrain.ZoneOf(moveClass, tile % Terrain.TilesX, tile / Terrain.TilesX) == startZone;
        });

        int tx = MapData.TileOf(goal.X), ty = MapData.TileOf(goal.Y);
        if (!Terrain.Passable(moveClass, tx, ty))
        {
            Units.ReleaseGoal(id);
            return requested;
        }
        Units.ReserveGoal(id, Terrain.Index(tx, ty));
        return goal;
    }
}
