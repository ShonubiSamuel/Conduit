using System;
using System.Collections.Generic;
using Conduit.Engine.Core;

namespace Conduit.Engine.Spatial
{
    /// <summary>
    /// Uniform-grid accelerator for neighbour queries (D11). An OPTIMISATION ONLY: every query
    /// returns EntityId-sorted candidates and must match a brute-force scan (asserted in tests).
    /// Rebuilt deterministically each tick from the EntityId-sorted field.
    ///
    /// Generic over the caller's element type; the engine passes (EntityId, position) pairs.
    /// </summary>
    public sealed class SpatialGrid
    {
        private readonly int _cell;            // cell size in mu (>= max query radius)
        private readonly int _minX, _minY, _cols, _rows;
        private readonly List<int>[] _cells;   // each holds indices into the registered list
        private readonly List<(EntityId Id, FxVec2 Pos)> _items = new List<(EntityId, FxVec2)>();

        public SpatialGrid(int minX, int minY, int width, int height, int cellSize)
        {
            _cell = Math.Max(1, cellSize);
            _minX = minX; _minY = minY;
            _cols = Math.Max(1, (width + _cell - 1) / _cell) + 1;
            _rows = Math.Max(1, (height + _cell - 1) / _cell) + 1;
            _cells = new List<int>[_cols * _rows];
            for (int i = 0; i < _cells.Length; i++) _cells[i] = new List<int>();
        }

        public void Clear()
        {
            _items.Clear();
            for (int i = 0; i < _cells.Length; i++) _cells[i].Clear();
        }

        /// <summary>Add items in EntityId order (caller passes the sorted field) so cell lists stay ordered.</summary>
        public void Add(EntityId id, FxVec2 pos)
        {
            int idx = _items.Count;
            _items.Add((id, pos));
            _cells[CellIndex(pos)].Add(idx);
        }

        private int CellIndex(FxVec2 p)
        {
            int cx = Clamp((p.X - _minX) / _cell, 0, _cols - 1);
            int cy = Clamp((p.Y - _minY) / _cell, 0, _rows - 1);
            return cy * _cols + cx;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>Ids within <paramref name="radius"/> of centre, EntityId-sorted, optionally filtered.
        /// Scans the centre cell + neighbours covering the radius.</summary>
        public List<EntityId> WithinRadius(FxVec2 center, int radius, Predicate<EntityId>? filter = null)
        {
            var result = new List<EntityId>();
            long rSq = (long)radius * radius;
            int reach = radius / _cell + 1;
            int ccx = Clamp((center.X - _minX) / _cell, 0, _cols - 1);
            int ccy = Clamp((center.Y - _minY) / _cell, 0, _rows - 1);
            for (int gy = Math.Max(0, ccy - reach); gy <= Math.Min(_rows - 1, ccy + reach); gy++)
                for (int gx = Math.Max(0, ccx - reach); gx <= Math.Min(_cols - 1, ccx + reach); gx++)
                    foreach (int idx in _cells[gy * _cols + gx])
                    {
                        var (id, pos) = _items[idx];
                        if (pos.DistSq(center) > rSq) continue;
                        if (filter != null && !filter(id)) continue;
                        result.Add(id);
                    }
            result.Sort((a, b) => a.Value.CompareTo(b.Value));   // EntityId order (D11)
            return result;
        }

        /// <summary>Nearest matching id to centre (EntityId breaks distance ties). None if empty.</summary>
        public EntityId Nearest(FxVec2 center, Predicate<EntityId>? filter = null)
        {
            // simple: widen search rings until found; correctness over cleverness for v0
            EntityId best = EntityId.None;
            long bestSq = long.MaxValue;
            foreach (var (id, pos) in _items)
            {
                if (filter != null && !filter(id)) continue;
                long d = pos.DistSq(center);
                if (d < bestSq || (d == bestSq && (best.IsNone || id.Value < best.Value)))
                { bestSq = d; best = id; }
            }
            return best;
        }
    }
}
