using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public sealed partial class DeepGameSession
    {
        void ClaimOrder(DeepWorker worker)
        {
            if (!worker.IsAlive || worker.automationPaused || SimulationTime < worker.nextWorkSearchTime) return;
            DeepWorkOrder selected = null;
            List<Vector2Int> selectedPath = null;
            Vector2Int selectedWork = default,selectedPickup = default;
            bool selectedFetching = false;
            int bestPreference=-1,bestPriority=-1,bestTravel=int.MaxValue;
            foreach (var order in Orders)
            {
                if (order.IsTerminal || order.worker != null || order.Progress >= 1 || SimulationTime < order.nextRetryTime || (order.requestedWorker != null && order.requestedWorker != worker)) continue;
                int preference = worker.GetPreference(order.kind);
                if (preference == 0)
                {
                    bool enabled = false;
                    foreach (var otherWorker in Workers) if (otherWorker != null && otherWorker.IsAlive && otherWorker.isActiveAndEnabled && !otherWorker.automationPaused && otherWorker.GetPreference(order.kind) > 0) { enabled = true; break; }
                    if (!enabled) { order.state = DeepWorkState.Blocked; order.statusReason = "没有启用此工种的工人"; }
                    continue;
                }
                order.statusReason = string.Empty;
                var positions = PositionsFor(order,worker);
                if (positions.Count == 0)
                { order.state = DeepWorkState.Blocked; if (string.IsNullOrEmpty(order.statusReason)) order.statusReason = "没有可站立的作业邻格，需要挖掘通路或建梯子"; continue; }
                if (!DeepNavigation.TryFindPath(this,worker.Cell,positions,out var path))
                { order.state = DeepWorkState.Blocked; order.statusReason = "工作位置不可达，需要地面或连续梯子"; continue; }
                Vector2Int workCell = path.Count == 0 ? worker.Cell : path[path.Count-1],pickupCell = default;
                bool fetching = order.reservation != null && order.reservation.Units > 0 && !order.materialsCollected;
                int travel = DeepNavigation.TravelCost(path,worker.Cell);
                if (fetching)
                {
                    if (!TryMaterialRoute(worker,positions,out var pickupPath,out pickupCell,out workCell,out travel))
                    { order.state = DeepWorkState.Blocked; order.statusReason = "仓库不可达，等待取料"; continue; }
                    path=pickupPath;
                }
                if(CloserIdleWorkerCanClaim(order,worker,positions,travel,fetching))
                {order.state=DeepWorkState.Queued;order.statusReason="等待附近同工种空闲人员";continue;}
                // A worker's enabled specialties come first, then the colony's 1–9 priority,
                // then travel distance. Stable iteration preserves older orders on exact ties.
                int priority=Mathf.Clamp(order.priority,1,9);
                order.state = DeepWorkState.Queued; order.statusReason = "等待空闲工人 · 优先级 "+order.priority;
                if(preference<bestPreference||preference==bestPreference&&(priority<bestPriority||priority==bestPriority&&travel>=bestTravel))continue;
                bestPreference=preference;bestPriority=priority;bestTravel=travel;
                selected = order; selectedPath = path; selectedWork = workCell; selectedPickup = pickupCell; selectedFetching = fetching;
            }
            if (selected == null) { worker.nextWorkSearchTime = SimulationTime+.35f; return; }
            worker.nextWorkSearchTime = 0;
            selected.workCell = selectedWork; selected.pickupCell = selectedPickup; selected.fetchingMaterials = selectedFetching;
            selected.worker = worker; worker.currentOrder = selected; worker.SetPath(selectedPath);
            selected.state = DeepWorkState.Moving; selected.statusReason = selected.fetchingMaterials ? "前往仓库" : "前往工作地点";
        }
        List<Vector2Int> PositionsFor(DeepWorkOrder order,DeepWorker candidateWorker = null)
        {
            if (order.kind == DeepWorkKind.Move) return IsStandable(order.targetCell) ? new List<Vector2Int> { order.targetCell } : new List<Vector2Int>();
            if (order.kind == DeepWorkKind.Build)
            {
                if (order.buildingDefinition == null) { order.statusReason = "建筑定义缺失"; return new List<Vector2Int>(); }
                var bounds = new RectInt(order.targetCell,order.buildingDefinition.footprint);
                if (order.buildingDefinition.requiresFloor)
                    for (int x = bounds.xMin; x < bounds.xMax; x++) if (!IsSupport(new Vector2Int(x,bounds.yMin-1))) { order.statusReason = "等待承重地板完工"; return new List<Vector2Int>(); }
                if(order.buildingDefinition.role==DeepBuildingRole.Ladder&&!LadderHasAnchor(bounds,false))
                {order.statusReason="等待相连梯子或实体承重点完工";return new List<Vector2Int>();}
                return WorkPositions(bounds);
            }
            if (order.kind == DeepWorkKind.Dig) return WorkPositions(new RectInt(order.targetCell,Vector2Int.one));
            if (order.kind == DeepWorkKind.Pipe) return PipeWorkPositions(order.fromNode,order.toNode);
            if (order.kind == DeepWorkKind.Wire) return WireWorkPositions(order.targetCell);
            if (order.kind == DeepWorkKind.Sample || order.kind == DeepWorkKind.Survey) return ExplorationWorkPositions(order);
            if(order.kind==DeepWorkKind.Craft)
            {
                // A crafting order belongs to the machine selected when it was queued.
                // Worker availability, queue contention and power loss never retarget it.
                if(!CraftStationReady(order,out var reason)){order.statusReason=reason;return new List<Vector2Int>();}
                order.targetCell=order.targetBuilding.origin;return WorkPositions(order.targetBuilding.Bounds);
            }
            if (candidateWorker != null || order.targetBuilding == null || !order.targetBuilding.isConstructed || !order.targetBuilding.IsOperational || StationBusy(order.targetBuilding,order))
            {
                var role = order.kind == DeepWorkKind.Research ? DeepBuildingRole.Research : DeepBuildingRole.Fabricator;
                string required = order.recipe == null ? null : order.recipe.requiredBuildingId;
                DeepBuildingInstance available = null;
                int shortest = int.MaxValue;
                foreach (var building in Buildings)
                {
                    if (building != null && building.isConstructed && building.definition != null && building.definition.role == role && (string.IsNullOrEmpty(required) || building.definition.id == required) && building.IsOperational && !StationBusy(building,order))
                    {
                        int distance = 0;
                        if (candidateWorker != null)
                        {
                            if (!DeepNavigation.TryFindPath(this,candidateWorker.Cell,WorkPositions(building.Bounds),out var stationPath)) continue;
                            distance = DeepNavigation.TravelCost(stationPath,candidateWorker.Cell);
                        }
                        if (distance < shortest) { available = building; shortest = distance; }
                    }
                }
                if (available != null) order.targetBuilding = available;
            }
            if (order.targetBuilding == null || !order.targetBuilding.isConstructed) { order.statusReason = "等待建造所需工作台"; return new List<Vector2Int>(); }
            if (!order.targetBuilding.IsOperational) { order.statusReason = "工作台已关闭或电力不足"; return new List<Vector2Int>(); }
            if (StationBusy(order.targetBuilding,order)) { order.statusReason = "工作台正忙，等待上一项工作完成"; return new List<Vector2Int>(); }
            order.targetCell = order.targetBuilding.origin;
            return WorkPositions(order.targetBuilding.Bounds);
        }
        bool StationBusy(DeepBuildingInstance station,DeepWorkOrder order)
        {
            foreach (var other in Orders) if (other != order && !other.IsTerminal && other.worker != null && other.targetBuilding == station) return true;
            return false;
        }
        bool CraftStationReady(DeepWorkOrder order,out string reason)
        {
            if(!StationCanMake(order.targetBuilding,order.recipe)){reason="指定生产设备已移除或不支持此配方";return false;}
            if(!order.targetBuilding.IsOperational){reason="指定生产设备已关闭或电力不足";return false;}
            foreach(var previous in Orders)
                if(previous!=order&&!previous.IsTerminal&&previous.kind==DeepWorkKind.Craft&&previous.targetBuilding==order.targetBuilding&&previous.id<order.id)
                {reason="等待本设备前面的生产任务";return false;}
            if(StationBusy(order.targetBuilding,order)){reason="指定设备正在工作";return false;}
            reason=string.Empty;return true;
        }
        void TickWorker(DeepWorker worker,float dt)
        {
            DeepWorkOrder order = worker.currentOrder;
            if (order == null || order.IsTerminal) { worker.currentOrder = null; return; }
            if (order.state == DeepWorkState.Moving)
            {
                if (!worker.Advance(dt) || worker.currentOrder != order) return;
                if (order.fetchingMaterials)
                {
                    order.fetchingMaterials = false; order.materialsCollected = true;
                    if (!DeepNavigation.TryFindPath(this,worker.Cell,PositionsFor(order,worker),out var deliveryPath)) { RequeueWorker(worker,"物料已退回，等待通路"); return; }
                    order.workCell = deliveryPath.Count == 0 ? worker.Cell : deliveryPath[deliveryPath.Count-1];
                    worker.SetPath(deliveryPath); order.statusReason = "搬运物料"; return;
                }
                if (order.kind == DeepWorkKind.Move) { FinishOrder(order); return; }
                order.state = DeepWorkState.Working; order.statusReason = "作业中";
                // Reaching a workstation consumes this step; work never occurs before physical arrival.
                return;
            }
            if (worker.Cell != order.workCell || !IsStandable(worker.Cell)) { RequeueWorker(worker,"工作站位已改变"); return; }
            if(order.kind==DeepWorkKind.Craft&&(!CraftStationReady(order,out var stationReason)||!WorkPositions(order.targetBuilding.Bounds).Contains(worker.Cell)))
            {RequeueWorker(worker,string.IsNullOrEmpty(stationReason)?"人员尚未到达指定设备":stationReason);order.state=DeepWorkState.Blocked;order.nextRetryTime=SimulationTime+1;return;}
            if ((order.kind == DeepWorkKind.Research || order.kind == DeepWorkKind.Craft) && (order.targetBuilding == null || !order.targetBuilding.IsOperational))
            { RequeueWorker(worker,"工作台停用或电力不足"); order.state = DeepWorkState.Blocked; order.nextRetryTime = SimulationTime+1; return; }
            order.state = DeepWorkState.Working; order.statusReason = "作业中";
            order.completedSeconds = Mathf.Min(order.totalSeconds,order.completedSeconds+dt*worker.workSpeed*Mathf.Max(.5f,worker.environmentEfficiency));
            if (order.Progress >= 1) TryComplete(order);
        }
        internal void RequeueWorker(DeepWorker worker,string reason)
        {
            if (worker == null || worker.currentOrder == null) return;
            var order = worker.currentOrder; worker.currentOrder = null; worker.SetPath(null);
            order.worker = null;
            order.fetchingMaterials = false; order.materialsCollected = false;
            if (order.kind == DeepWorkKind.Move) { order.state = DeepWorkState.Cancelled; order.statusReason = "移动已取消"; }
            else { order.state = DeepWorkState.Queued; order.statusReason = reason; order.nextRetryTime = SimulationTime+.25f; }
        }
        bool TryComplete(DeepWorkOrder order)
        {
            if (order == null || order.IsTerminal) return false;
            Vector3 effectPosition = world.CellToWorld(order.targetCell);
            switch (order.kind)
            {
                case DeepWorkKind.Build:
                {
                    var def = order.buildingDefinition;
                    RectInt area = new RectInt(order.targetCell,def.footprint);
                    if (def.requiresFloor)
                        for (int x = area.xMin; x < area.xMax; x++) if (!IsSupport(new Vector2Int(x,area.yMin-1))) return BlockFinished(order,"施工支撑已失效，等待地板修复");
                    if(def.role==DeepBuildingRole.Ladder&&!LadderHasAnchor(area,false))return BlockFinished(order,"梯子缺少实体承重点，等待支撑完工");
                    if (def.blocksMovement)
                        foreach (var worker in Workers) if (worker != null && worker.IsAlive && (area.Contains(worker.Cell) || area.Contains(worker.Cell+Vector2Int.up)))
                            return BlockFinished(order,"等待人员离开施工范围");
                    GameObject go = Instantiate(def.prefab,BuildingPosition(order.targetCell),world.transform.rotation,world.transform);
                    go.name = def.displayName; go.SetActive(true);
                    var instance = go.GetComponent<DeepBuildingInstance>(); if (instance == null) instance = go.AddComponent<DeepBuildingInstance>();
                    instance.definition = def; instance.origin = order.targetCell; instance.isConstructed = true; instance.session = this;
                    instance.lights = go.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
                    foreach (var node in go.GetComponentsInChildren<GasNode>(true)) { node.initialPressureKPa = 0; node.ResetInventory(); }
                    Buildings.Add(instance); ReleaseClaims(order); RebuildOccupancy();
                    if (def.role == DeepBuildingRole.Floor || def.role == DeepBuildingRole.Structure && def.blocksMovement)
                    {
                        foreach (Vector2Int cell in area.allPositionsWithin) PaintConstructionCell(cell);
                        RefreshWorldAfterTerrain(order.targetCell);
                    }
                    inventory.Commit(order.reservation); RefreshStorageCapacity(); UpdatePower(0);
                    if (network != null) network.RefreshTopologyPreservingGas();
                    DeepParticleFeedback.Emit(DeepFeedbackKind.Build,effectPosition); break;
                }
                case DeepWorkKind.Dig:
                {
                    if (world.GetTerrain(order.targetCell.x,order.targetCell.y) == TerrainKind.Empty) { FinishOrder(order); return true; }
                    if(RemovingSupportDetachesLadder(order.targetCell))return BlockFinished(order,"挖掘将移除梯子链最后承重点，请先补建支撑");
                    var output = ExcavationOutputs(order.targetCell);
                    if (!inventory.CanComplete(null,output,1,out string reason)) return BlockFinished(order,reason);
                    PrepareExcavationHazard(order.targetCell);
                    if (world.terrain != null) world.terrain.SetTile(new Vector3Int(order.targetCell.x,order.targetCell.y,0),null);
                    world.SetTerrain(order.targetCell.x,order.targetCell.y,TerrainKind.Empty);
                    RefreshWorldAfterTerrain(order.targetCell); inventory.Complete(null,output,1,out _);
                    ResolveExcavationHazard(order.targetCell);
                    DeepParticleFeedback.Emit(DeepFeedbackKind.Dig,effectPosition); break;
                }
                case DeepWorkKind.Research:
                    inventory.Commit(order.reservation); technologies.Add(order.technology.id);
                    DeepParticleFeedback.Emit(DeepFeedbackKind.Research,effectPosition); break;
                case DeepWorkKind.Craft:
                    if(!CraftStationReady(order,out string stationReason))return BlockFinished(order,stationReason);
                    if(order.worker!=null&&(!order.materialsCollected&&order.reservation!=null&&order.reservation.Units>0||!WorkPositions(order.targetBuilding.Bounds).Contains(order.worker.Cell)))
                        return BlockFinished(order,"人员与物料尚未到达指定生产设备");
                    if (!inventory.Complete(order.reservation,order.recipe.outputs,order.batches,out string craftReason)) return BlockFinished(order,craftReason);
                    DeepParticleFeedback.Emit(DeepFeedbackKind.Craft,effectPosition); break;
                case DeepWorkKind.Pipe:
                    if (order.fromNode == null || order.toNode == null) return BlockFinished(order,"管道端点已不存在");
                    DeepPlayerPipeFactory.Create(this,order.fromNode,order.toNode,order.fromPort);
                    inventory.Commit(order.reservation); network.RefreshTopologyPreservingGas();
                    DeepParticleFeedback.Emit(DeepFeedbackKind.Build,effectPosition); break;
                case DeepWorkKind.Wire:
                    if(!CompleteWireOrder(order,out string wireReason))return BlockFinished(order,wireReason);
                    break;
                case DeepWorkKind.Sample:
                case DeepWorkKind.Survey:
                    if(!CompleteExplorationWork(order,out string explorationReason))return BlockFinished(order,explorationReason);
                    break;
            }
            FinishOrder(order); return true;
        }
        bool BlockFinished(DeepWorkOrder order,string reason)
        {
            if (order.worker != null) { order.worker.currentOrder = null; order.worker.SetPath(null); }
            order.worker = null; order.state = DeepWorkState.Blocked; order.statusReason = reason; order.nextRetryTime = SimulationTime+1;
            return false;
        }
        void FinishOrder(DeepWorkOrder order)
        {
            if (order.worker != null) { order.worker.currentOrder = null; order.worker.SetPath(null); }
            order.worker = null; order.state = DeepWorkState.Completed; order.completedSeconds = order.totalSeconds; order.statusReason = "已完成";
        }
        void PaintConstructionCell(Vector2Int cell)
        {
            if (world.terrain != null)
            {
                if (constructedFloorTile == null)
                    for (int y = 0; y < world.height && constructedFloorTile == null; y++) for (int x = 0; x < world.width; x++)
                    { var tile = world.MaterialAt(new Vector2Int(x,y)); if (tile != null && tile.kind == TerrainKind.Metal) { constructedFloorTile = tile; break; } }
                if (constructedFloorTile != null) world.terrain.SetTile(new Vector3Int(cell.x,cell.y,0),constructedFloorTile);
            }
            world.SetTerrain(cell.x,cell.y,TerrainKind.Metal);
        }
        void RefreshWorldAfterTerrain(Vector2Int cell)
        {
            world.SyncTerrainFromTilemap();
            NotifyNavigationChanged();
            if(lifeSupportEnabled&&Atmosphere!=null&&Atmosphere.IsInitialized){world.RebuildRooms();Atmosphere.RebuildAfterTerrainChange();}
            else world.RebuildRoomsPreservingGas();
            if (exploration != null) { exploration.RefreshAfterTerrainChange(); exploration.RevealAroundWork(cell); }
        }
    }
}
