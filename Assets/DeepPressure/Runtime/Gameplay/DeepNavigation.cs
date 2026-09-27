using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public static class DeepNavigation
    {
        // Feet are the navigation coordinate. Every transition checks the complete two-cell body.
        static readonly Vector2Int[] Steps = {
            Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down,
            new Vector2Int(-1,1),new Vector2Int(1,1),new Vector2Int(-1,-1),new Vector2Int(1,-1),
            new Vector2Int(-2,0),new Vector2Int(2,0)
        };
        public static bool TryFindPath(DeepGameSession session,Vector2Int start,ICollection<Vector2Int> goals,out List<Vector2Int> path)
        {
            path = null;
            if (session == null || session.world == null || !session.world.IsInside(start) || goals == null || goals.Count == 0) return false;
            session.RefreshLadderSupport();
            var goalSet = new HashSet<Vector2Int>(goals);
            var open = new List<Vector2Int> { start };
            var previous = new Dictionary<Vector2Int,Vector2Int> { [start] = start };
            var cost = new Dictionary<Vector2Int,int> { [start] = 0 };
            var closed = new HashSet<Vector2Int>();
            while (open.Count > 0)
            {
                int best=0;
                for(int i=1;i<open.Count;i++) if(cost[open[i]]<cost[open[best]]) best=i;
                Vector2Int cell=open[best];open.RemoveAt(best);
                if(!closed.Add(cell))continue;
                if (goalSet.Contains(cell) && session.IsStandable(cell))
                {
                    path = new List<Vector2Int>();
                    for (Vector2Int p = cell; p != start; p = previous[p]) path.Add(p);
                    path.Reverse(); return true;
                }
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next=cell+step;
                    if(closed.Contains(next)||!CanTraverse(session,cell,next))continue;
                    int edge=StepCost(step);
                    int candidate=cost[cell]+edge;
                    if(cost.TryGetValue(next,out int old)&&candidate>=old)continue;
                    previous[next]=cell;cost[next]=candidate;
                    if(!open.Contains(next))open.Add(next);
                }
            }
            return false;
        }

        public static bool CanTraverse(DeepGameSession session,Vector2Int from,Vector2Int to)
        {
            if(session==null||!session.IsStandable(from)||!session.IsStandable(to))return false;
            Vector2Int delta=to-from;
            int dx=Mathf.Abs(delta.x),dy=Mathf.Abs(delta.y);
            if(dx==1&&dy==0)return true;
            if(dx==0&&dy==1)return session.IsLadder(from)||session.IsLadder(to);
            if(dx==1&&dy==1)
            {
                // Step up before moving across a ledge; step across before lowering onto a rung.
                Vector2Int corner=delta.y>0?from+Vector2Int.up:new Vector2Int(to.x,from.y);
                return session.IsPassable(corner);
            }
            if(dx==2&&dy==0)
            {
                // Hop one missing floor tile, with a full body-height clearance throughout the arc.
                var middle=new Vector2Int((from.x+to.x)/2,from.y);
                return !session.IsStandable(middle)&&session.IsPassable(middle)&&
                    session.IsPassable(from+Vector2Int.up)&&session.IsPassable(middle+Vector2Int.up)&&session.IsPassable(to+Vector2Int.up);
            }
            return false;
        }

        public static int TravelCost(IList<Vector2Int> path,Vector2Int start)
        {
            int cost=0;var previous=start;
            foreach(var cell in path){cost+=StepCost(cell-previous);previous=cell;}
            return cost;
        }
        static int StepCost(Vector2Int delta)
        {
            if(delta==Vector2Int.zero)return 0;
            // A gap crosses two cells plus the 0.55-cell rise and descent used by DeepWorker.
            // These weights also select work positions and compare competing work orders.
            return delta.y==0?(Mathf.Abs(delta.x)==2?31:10):delta.x==0?13:20;
        }
    }
}
