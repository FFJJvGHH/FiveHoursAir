using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public sealed partial class DeepGameSession
    {
        static readonly Vector2Int[] AnchorDirections={Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
        readonly HashSet<Vector2Int> supportedLadderCells=new HashSet<Vector2Int>();
        internal int NavigationRevision {get;private set;}

        void NotifyNavigationChanged()
        {
            NavigationRevision++;
            RefreshLadderSupport();
            // ApplySaveState rebuilds occupancy with the menu gate held; retain its saved timers.
            if(menuOpen)return;
            foreach(var worker in Workers)if(worker!=null)worker.nextWorkSearchTime=0;
            foreach(var order in Orders)if(!order.IsTerminal&&order.state==DeepWorkState.Blocked)order.nextRetryTime=0;
        }

        bool TryMaterialRoute(DeepWorker worker,List<Vector2Int> workPositions,out List<Vector2Int> pickupPath,out Vector2Int pickupCell,out Vector2Int workCell,out int cost)
        {
            pickupPath=null;pickupCell=workCell=default;cost=int.MaxValue;
            foreach(var building in Buildings)
            {
                if(building==null||!building.isConstructed||building.definition==null||building.definition.role!=DeepBuildingRole.Storage)continue;
                if(!DeepNavigation.TryFindPath(this,worker.Cell,WorkPositions(building.Bounds),out var approach))continue;
                var pickup=approach.Count==0?worker.Cell:approach[approach.Count-1];
                if(!DeepNavigation.TryFindPath(this,pickup,workPositions,out var delivery))continue;
                int routeCost=DeepNavigation.TravelCost(approach,worker.Cell)+DeepNavigation.TravelCost(delivery,pickup);
                if(routeCost>=cost)continue;
                pickupPath=approach;pickupCell=pickup;workCell=delivery.Count==0?pickup:delivery[delivery.Count-1];cost=routeCost;
            }
            return pickupPath!=null;
        }

        bool CloserIdleWorkerCanClaim(DeepWorkOrder order,DeepWorker candidate,List<Vector2Int> workPositions,int candidateTravel,bool fetching)
        {
            if(order.requestedWorker!=null||order.kind!=DeepWorkKind.Dig&&order.kind!=DeepWorkKind.Build)return false;
            foreach(var other in Workers)
            {
                if(other==null||other==candidate||!other.IsAlive||!other.isActiveAndEnabled||other.automationPaused||other.currentOrder!=null||
                    SimulationTime<other.nextWorkSearchTime||other.GetPreference(order.kind)!=candidate.GetPreference(order.kind))continue;
                int travel;
                if(fetching)
                {if(!TryMaterialRoute(other,workPositions,out _,out _,out _,out travel))continue;}
                else
                {if(!DeepNavigation.TryFindPath(this,other.Cell,workPositions,out var route))continue;travel=DeepNavigation.TravelCost(route,other.Cell);}
                if(travel<candidateTravel)return true;
            }
            return false;
        }

        internal void RepathWorker(DeepWorker worker,string reason)
        {
            var order=worker==null?null:worker.currentOrder;
            if(order==null||order.IsTerminal)return;
            var positions=PositionsFor(order,worker);
            List<Vector2Int> route=null;
            bool found;
            if(order.fetchingMaterials)
                found=TryMaterialRoute(worker,positions,out route,out order.pickupCell,out order.workCell,out _);
            else
            {
                found=DeepNavigation.TryFindPath(this,worker.Cell,positions,out route);
                if(found)order.workCell=route.Count==0?worker.Cell:route[route.Count-1];
            }
            if(found)
            {
                worker.SetPath(route);order.state=DeepWorkState.Moving;order.statusReason=reason;
                return;
            }
            // Keep the original command and reserved stock when a floor/wall temporarily blocks it.
            worker.SetPath(null);worker.currentOrder=null;order.worker=null;
            order.fetchingMaterials=false;order.materialsCollected=false;
            order.state=DeepWorkState.Blocked;order.statusReason="原目标暂不可达，等待通路恢复";order.nextRetryTime=SimulationTime+.25f;
        }

        internal void RefreshLadderSupport()
        {
            supportedLadderCells.Clear();
            var visited=new HashSet<Vector2Int>();
            foreach(var seed in ladderCells)
            {
                if(visited.Contains(seed))continue;
                var component=new List<Vector2Int>();var queue=new Queue<Vector2Int>();queue.Enqueue(seed);visited.Add(seed);
                bool anchored=false;
                while(queue.Count>0)
                {
                    var cell=queue.Dequeue();component.Add(cell);
                    foreach(var direction in AnchorDirections)
                    {
                        var next=cell+direction;
                        if(ladderCells.Contains(next)){if(visited.Add(next))queue.Enqueue(next);}
                        else if(world.IsInside(next)&&IsSupport(next))anchored=true;
                    }
                }
                if(anchored)foreach(var cell in component)supportedLadderCells.Add(cell);
            }
        }

        bool LadderHasAnchor(RectInt candidate,bool includePlans,Vector2Int? removedSupport=null)
        {
            var ladderSet=new HashSet<Vector2Int>(ladderCells);
            foreach(var cell in candidate.allPositionsWithin)ladderSet.Add(cell);
            if(includePlans)
                foreach(var order in Orders)
                    if(!order.IsTerminal&&order.kind==DeepWorkKind.Build&&order.buildingDefinition!=null&&order.buildingDefinition.role==DeepBuildingRole.Ladder)
                        foreach(var cell in new RectInt(order.targetCell,order.buildingDefinition.footprint).allPositionsWithin)ladderSet.Add(cell);
            var visited=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();
            queue.Enqueue(candidate.position);visited.Add(candidate.position);
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();
                foreach(var direction in AnchorDirections)
                {
                    var next=cell+direction;
                    if(ladderSet.Contains(next)){if(visited.Add(next))queue.Enqueue(next);continue;}
                    if(!world.IsInside(next)||removedSupport.HasValue&&next==removedSupport.Value)continue;
                    if(IsSupport(next)||includePlans&&IsPlannedSupport(next))return true;
                }
            }
            return false;
        }

        bool RemovingSupportDetachesLadder(Vector2Int support)
        {
            foreach(var direction in AnchorDirections)
            {
                var cell=support+direction;
                if(ladderCells.Contains(cell)&&LadderHasAnchor(new RectInt(cell,Vector2Int.one),false)&&
                    !LadderHasAnchor(new RectInt(cell,Vector2Int.one),false,support))return true;
            }
            return false;
        }

    }
}
