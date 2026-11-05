using System;
using System.Collections.Generic;

namespace Core.Map
{
    /// <summary>Undirected adjacency graph of provinces. Province ids are dense indices 0..Count-1.</summary>
    public class ProvinceGraph
    {
        private readonly List<Province> _provinces = new List<Province>();
        private readonly List<List<Province>> _neighbors = new List<List<Province>>();

        public IReadOnlyList<Province> Provinces => _provinces;
        public int Count => _provinces.Count;

        public Province Add(Province province)
        {
            if (province.Id != _provinces.Count)
                throw new ArgumentException($"Province ids must be added in order; expected {_provinces.Count}, got {province.Id}.");
            _provinces.Add(province);
            _neighbors.Add(new List<Province>(4));
            return province;
        }

        public Province Get(int id) => _provinces[id];

        public void Connect(int a, int b)
        {
            if (a == b) throw new ArgumentException("A province cannot border itself.");
            if (a < 0 || b < 0 || a >= Count || b >= Count)
                throw new ArgumentException($"Unknown province in edge {a}-{b}.");
            if (AreAdjacent(_provinces[a], _provinces[b])) return;
            _neighbors[a].Add(_provinces[b]);
            _neighbors[b].Add(_provinces[a]);
        }

        public bool AreAdjacent(Province a, Province b) => a != null && b != null && _neighbors[a.Id].Contains(b);

        public IReadOnlyList<Province> Neighbors(Province province) => _neighbors[province.Id];

        public int CountControlledBy(Faction faction)
        {
            int count = 0;
            for (int i = 0; i < _provinces.Count; i++)
            {
                if (_provinces[i].Owner == faction) count++;
            }

            return count;
        }

        /// <summary>True when every province can be reached from every other one.</summary>
        public bool IsConnected() => Count == 0 || CountReachable(_provinces[0], null, false) == Count;

        /// <summary>True when all provinces owned by the faction form one connected region.</summary>
        public bool IsTerritoryContiguous(Faction faction)
        {
            for (int i = 0; i < _provinces.Count; i++)
            {
                if (_provinces[i].Owner == faction)
                    return CountReachable(_provinces[i], faction, true) == CountControlledBy(faction);
            }

            return true;
        }

        /// <summary>
        /// Shortest path (BFS, inclusive of both ends) through provinces that pass <paramref name="canEnter"/>.
        /// Returns null if there is no path. Needed later for Guild trade routes.
        /// </summary>
        public List<Province> ShortestPath(Province from, Province to, Func<Province, bool> canEnter = null)
        {
            var previous = new int[Count];
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            previous[from.Id] = from.Id;

            var queue = new Queue<int>(Count);
            queue.Enqueue(from.Id);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == to.Id) break;

                var neighbors = _neighbors[current];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int next = neighbors[i].Id;
                    if (previous[next] != -1 || (canEnter != null && !canEnter(neighbors[i]))) continue;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (previous[to.Id] == -1) return null;

            var path = new List<Province>();
            for (int id = to.Id; ; id = previous[id])
            {
                path.Add(_provinces[id]);
                if (id == from.Id) break;
            }

            path.Reverse();
            return path;
        }

        private int CountReachable(Province start, Faction owner, bool filterByOwner)
        {
            var seen = new bool[Count];
            var stack = new Stack<int>(Count);
            seen[start.Id] = true;
            stack.Push(start.Id);
            int reached = 1;

            while (stack.Count > 0)
            {
                var neighbors = _neighbors[stack.Pop()];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    var next = neighbors[i];
                    if (seen[next.Id] || (filterByOwner && next.Owner != owner)) continue;
                    seen[next.Id] = true;
                    stack.Push(next.Id);
                    reached++;
                }
            }

            return reached;
        }
    }
}
