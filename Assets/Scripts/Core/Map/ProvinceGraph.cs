using System;
using System.Collections.Generic;
using System.Linq;

namespace Projection.Core.Map
{
    /// <summary>Undirected adjacency graph of provinces.</summary>
    public class ProvinceGraph
    {
        private readonly Dictionary<int, Province> _provinces = new Dictionary<int, Province>();
        private readonly Dictionary<int, HashSet<int>> _edges = new Dictionary<int, HashSet<int>>();

        public IEnumerable<Province> Provinces => _provinces.Values;
        public int Count => _provinces.Count;

        public Province Add(Province province)
        {
            if (_provinces.ContainsKey(province.Id))
                throw new ArgumentException($"Province id {province.Id} already exists.");
            _provinces.Add(province.Id, province);
            _edges.Add(province.Id, new HashSet<int>());
            return province;
        }

        public Province Get(int id) => _provinces[id];

        public void Connect(int a, int b)
        {
            if (a == b) throw new ArgumentException("A province cannot border itself.");
            if (!_provinces.ContainsKey(a) || !_provinces.ContainsKey(b))
                throw new ArgumentException($"Unknown province in edge {a}-{b}.");
            _edges[a].Add(b);
            _edges[b].Add(a);
        }

        public bool AreAdjacent(Province a, Province b) =>
            a != null && b != null && _edges.TryGetValue(a.Id, out var n) && n.Contains(b.Id);

        public IEnumerable<Province> Neighbors(Province province) => _edges[province.Id].Select(id => _provinces[id]);

        public IEnumerable<Province> ControlledBy(Faction faction) => _provinces.Values.Where(p => p.Owner == faction);

        public int CountControlledBy(Faction faction) => _provinces.Values.Count(p => p.Owner == faction);

        /// <summary>True when every province can be reached from every other one.</summary>
        public bool IsConnected()
        {
            if (_provinces.Count == 0) return true;
            return Reachable(_provinces.Values.First(), _ => true).Count == _provinces.Count;
        }

        /// <summary>True when all provinces owned by the faction form one connected region.</summary>
        public bool IsTerritoryContiguous(Faction faction)
        {
            var owned = ControlledBy(faction).ToList();
            if (owned.Count == 0) return true;
            return Reachable(owned[0], p => p.Owner == faction).Count == owned.Count;
        }

        /// <summary>
        /// Shortest path (BFS, inclusive of both ends) through provinces that pass <paramref name="canEnter"/>.
        /// Returns null if there is no path. Needed later for Guild trade routes.
        /// </summary>
        public List<Province> ShortestPath(Province from, Province to, Func<Province, bool> canEnter = null)
        {
            canEnter = canEnter ?? (_ => true);
            var previous = new Dictionary<int, int> { [from.Id] = from.Id };
            var queue = new Queue<int>();
            queue.Enqueue(from.Id);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == to.Id) break;
                foreach (int next in _edges[current])
                {
                    if (previous.ContainsKey(next) || !canEnter(_provinces[next])) continue;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!previous.ContainsKey(to.Id)) return null;

            var path = new List<Province>();
            for (int id = to.Id; ; id = previous[id])
            {
                path.Add(_provinces[id]);
                if (id == from.Id) break;
            }
            path.Reverse();
            return path;
        }

        private HashSet<int> Reachable(Province start, Func<Province, bool> filter)
        {
            var seen = new HashSet<int> { start.Id };
            var stack = new Stack<int>();
            stack.Push(start.Id);
            while (stack.Count > 0)
            {
                foreach (int next in _edges[stack.Pop()])
                {
                    if (seen.Contains(next) || !filter(_provinces[next])) continue;
                    seen.Add(next);
                    stack.Push(next);
                }
            }
            return seen;
        }
    }
}
