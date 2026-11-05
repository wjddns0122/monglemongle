using System.Collections.Generic;
using Mongle.Data;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// Pure grid rules for furniture placement (04 §3): footprint, rotation, bounds, overlap.
    /// Cell indices run from MinX/MinY (inclusive) to MaxX/MaxY (exclusive); cell (0,0) always starts at Origin, so a grid
    /// that grows around the old garden (D-006) keeps every saved placement where it was.
    /// </summary>
    public class VillageGrid
    {
        public readonly int MinX, MinY, MaxX, MaxY;
        public int Cols => MaxX - MinX;
        public int Rows => MaxY - MinY;
        public readonly float Cell;
        public readonly Vector3 Origin; // world position of cell (0,0) min corner
        /// <summary>Cells taken by fixed scenery (rides); nothing can be placed on them.</summary>
        public readonly HashSet<Vector2Int> Blocked = new HashSet<Vector2Int>();

        public VillageGrid(int cols, int rows, float cell, Vector3 origin) : this(0, 0, cols, rows, cell, origin) { }
        public VillageGrid(int minX, int minY, int maxX, int maxY, float cell, Vector3 origin) { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; Cell = cell; Origin = origin; }

        public Vector3 CellCenter(int x, int y) => Origin + new Vector3((x + 0.5f) * Cell, 0f, (y + 0.5f) * Cell);
        /// <summary>World-space middle of the whole grid.</summary>
        public Vector3 Middle => Origin + new Vector3((MinX + MaxX) * 0.5f * Cell, 0f, (MinY + MaxY) * 0.5f * Cell);

        /// <summary>Blocks every cell that a rectangle on the ground (world center, half size along its own axes, turned by yaw) touches.</summary>
        public void BlockBox(Vector3 center, Vector2 halfSize, float yawDegrees)
        {
            var rot = Quaternion.Euler(0f, -yawDegrees, 0f);
            for (int x = MinX; x < MaxX; x++)
                for (int y = MinY; y < MaxY; y++)
                {
                    var l = rot * (CellCenter(x, y) - center); // into the box's own axes
                    // a cell is a square of half size Cell/2: grow the box by it (conservative for turned boxes)
                    if (Mathf.Abs(l.x) < halfSize.x + Cell * 0.5f && Mathf.Abs(l.z) < halfSize.y + Cell * 0.5f) Blocked.Add(new Vector2Int(x, y));
                }
        }

        /// <summary>Blocks every cell that a disc on the ground (world center, radius) touches.</summary>
        public void BlockDisc(Vector3 center, float radius)
        {
            for (int x = MinX; x < MaxX; x++)
                for (int y = MinY; y < MaxY; y++)
                {
                    var c = Origin + new Vector3((x + 0.5f) * Cell, 0f, (y + 0.5f) * Cell);
                    // nearest point of the cell square to the disc center
                    float dx = Mathf.Max(Mathf.Abs(center.x - c.x) - Cell * 0.5f, 0f), dz = Mathf.Max(Mathf.Abs(center.z - c.z) - Cell * 0.5f, 0f);
                    if (dx * dx + dz * dz < radius * radius) Blocked.Add(new Vector2Int(x, y));
                }
        }

        public static Vector2Int Size(Vector2Int footprint, int rotation) => (rotation & 1) == 1 ? new Vector2Int(footprint.y, footprint.x) : footprint;

        public IEnumerable<Vector2Int> Cells(Vector2Int footprint, int cx, int cy, int rotation)
        {
            var s = Size(footprint, rotation);
            for (int x = 0; x < s.x; x++) for (int y = 0; y < s.y; y++) yield return new Vector2Int(cx + x, cy + y);
        }

        public bool InBounds(Vector2Int footprint, int cx, int cy, int rotation)
        {
            var s = Size(footprint, rotation);
            return cx >= MinX && cy >= MinY && cx + s.x <= MaxX && cy + s.y <= MaxY;
        }

        /// <param name="others">placements of the other pieces (the moving one excluded by the caller)</param>
        public bool IsValid(IEnumerable<(Vector2Int footprint, FurniturePlacement p)> others, Vector2Int footprint, int cx, int cy, int rotation)
        {
            if (!InBounds(footprint, cx, cy, rotation)) return false;
            var mine = new HashSet<Vector2Int>(Cells(footprint, cx, cy, rotation));
            if (mine.Overlaps(Blocked)) return false;
            foreach (var o in others)
                foreach (var c in Cells(o.footprint, o.p.cellX, o.p.cellY, o.p.rotation))
                    if (mine.Contains(c)) return false;
            return true;
        }

        public Vector3 CenterOf(Vector2Int footprint, int cx, int cy, int rotation)
        {
            var s = Size(footprint, rotation);
            return Origin + new Vector3((cx + s.x * 0.5f) * Cell, 0f, (cy + s.y * 0.5f) * Cell);
        }

        public Vector2Int CellAt(Vector3 world)
        {
            var l = world - Origin;
            return new Vector2Int(Mathf.FloorToInt(l.x / Cell), Mathf.FloorToInt(l.z / Cell));
        }

        /// <summary>Origin cell so that the footprint center is nearest to a world point, clamped inside the grid.</summary>
        public Vector2Int SnapOrigin(Vector2Int footprint, int rotation, Vector3 world)
        {
            var s = Size(footprint, rotation);
            int cx = Mathf.RoundToInt((world.x - Origin.x) / Cell - s.x * 0.5f);
            int cy = Mathf.RoundToInt((world.z - Origin.z) / Cell - s.y * 0.5f);
            return new Vector2Int(Mathf.Clamp(cx, MinX, Mathf.Max(MinX, MaxX - s.x)), Mathf.Clamp(cy, MinY, Mathf.Max(MinY, MaxY - s.y)));
        }
    }
}
