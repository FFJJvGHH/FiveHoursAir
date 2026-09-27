using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public static class DeepNavigation
    {
        static readonly Vector2Int[] Directions = { Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down };
        public static bool TryFindPath(DeepGameSession session,Vector2Int start,ICollection<Vector2Int> goals,out List<Vector2Int> path)
        {
            path = null;
            if (session == null || session.world == null || !session.world.IsInside(start) || goals == null || goals.Count == 0) return false;
            var goalSet = new HashSet<Vector2Int>(goals);
            var queue = new Queue<Vector2Int>(); var previous = new Dictionary<Vector2Int,Vector2Int>();
            queue.Enqueue(start); previous[start] = start;
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                if (goalSet.Contains(cell) && session.IsStandable(cell))
                {
                    path = new List<Vector2Int>();
                    for (Vector2Int p = cell; p != start; p = previous[p]) path.Add(p);
                    path.Reverse(); return true;
                }
                foreach (Vector2Int d in Directions)
                {
                    Vector2Int next = cell+d;
                    if (previous.ContainsKey(next) || !session.IsStandable(next)) continue;
                    if (d.y != 0 && !(session.IsLadder(cell) || session.IsLadder(next))) continue;
                    previous[next] = cell; queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}
