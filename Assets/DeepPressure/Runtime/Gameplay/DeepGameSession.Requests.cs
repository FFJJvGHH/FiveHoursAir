using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public sealed partial class DeepGameSession
    {
        [Range(1,9)] public int defaultOrderPriority = 5;
        public bool SetOrderPriority(DeepWorkOrder order,int priority)
        {
            if (order == null || order.IsTerminal || !Orders.Contains(order)) return false;
            order.priority = Mathf.Clamp(priority,1,9); order.nextRetryTime = 0; return true;
        }
        public void SetWorkerPreference(DeepWorker worker,DeepWorkKind kind,int preference)
        {
            if (worker == null || !worker.IsAlive || !Workers.Contains(worker)) return;
            worker.nextWorkSearchTime = 0;
            int value = Mathf.Clamp(preference,0,3);
            switch (kind)
            {
                case DeepWorkKind.Dig: worker.digPreference = value; break;
                case DeepWorkKind.Build: worker.buildPreference = value; break;
                case DeepWorkKind.Research: worker.researchPreference = value; break;
                case DeepWorkKind.Craft: worker.craftPreference = value; break;
                case DeepWorkKind.Pipe: worker.pipePreference = value; break;
            }
            if (value == 0 && worker.currentOrder != null && worker.currentOrder.kind == kind) RequeueWorker(worker,"工种已禁用，等待其他工人");
        }
        public bool RequestStop(DeepWorker worker,out string reason)
        {
            if (worker == null || !worker.IsAlive || !Workers.Contains(worker)) { reason = "先选择一名工人"; return false; }
            RequeueWorker(worker,"工人停止，工单保留并等待重新分配"); worker.automationPaused = true;
            reason = "已停止；未完成工单保留，点击继续自动工作"; return true;
        }
        public void ResumeWorker(DeepWorker worker)
        {
            if (worker != null && worker.IsAlive && Workers.Contains(worker)) { worker.automationPaused = false; worker.nextWorkSearchTime = 0; }
        }
        public bool CanBuild(DeepBuildingDefinition definition,Vector2Int origin,out string reason)
        {
            InitializeSession(); reason = string.Empty;
            if (!initialized || definition == null || definition.prefab == null || definition.footprint.x < 1 || definition.footprint.y < 1) { reason = "建筑尚未配置"; return false; }
            if (definition.id == "printing_pod") { reason = "打印舱为初始设备"; return false; }
            if (!IsTechUnlocked(definition.requiredTechId)) { reason = "需要科技："+TechnologyLabel(definition.requiredTechId); return false; }
            RectInt area = new RectInt(origin,definition.footprint);
            foreach (Vector2Int cell in area.allPositionsWithin)
            {
                if (!world.IsInside(cell) || !IsKnown(cell)) { reason = "只能在已探明区域施工"; return false; }
                if (world.GetTerrain(cell.x,cell.y) != TerrainKind.Empty || BuildingAt(cell) != null || placementClaims.ContainsKey(cell)) { reason = "位置已被占用"; return false; }
                if (definition.blocksMovement) foreach (var worker in Workers) if (worker != null && worker.IsAlive && (worker.Cell == cell || worker.Cell+Vector2Int.up == cell)) { reason = "位置有人占用"; return false; }
            }
            if (definition.requiresFloor)
                for (int x = area.xMin; x < area.xMax; x++) if (!IsSupport(new Vector2Int(x,area.yMin-1)) && !IsPlannedSupport(new Vector2Int(x,area.yMin-1))) { reason = "建筑底部需要地面或已规划的承重地板"; return false; }
            if (!inventory.CanAfford(definition.cost,out reason)) return false;
            return true;
        }
        public bool RequestBuild(DeepBuildingDefinition definition,Vector2Int origin,out string reason)
        {
            if (!CanBuild(definition,origin,out reason)) return false;
            if (!inventory.TryReserve(definition.cost,out var reservation,out reason)) return false;
            var order = NewOrder(DeepWorkKind.Build,"建造 "+definition.displayName,origin,definition.workSeconds);
            order.buildingDefinition = definition; order.reservation = reservation;
            foreach (Vector2Int cell in new RectInt(origin,definition.footprint).allPositionsWithin) placementClaims[cell] = order;
            reason = "已排入施工队列"; return true;
        }
        public bool CanDig(Vector2Int cell,out string reason)
        {
            InitializeSession(); reason = string.Empty;
            if (!initialized || !world.IsInside(cell) || !IsKnown(cell)) { reason = "只能挖掘已探明地形"; return false; }
            if (world.GetTerrain(cell.x,cell.y) == TerrainKind.Empty) { reason = "这里没有可挖掘的地形"; return false; }
            if (BuildingAt(cell) != null || placementClaims.ContainsKey(cell)) { reason = "位置已有建筑或工单"; return false; }
            foreach (var order in Orders) if (!order.IsTerminal && order.kind == DeepWorkKind.Dig && order.targetCell == cell) { reason = "该位置已有挖掘工单"; return false; }
            return true;
        }
        public bool RequestDig(Vector2Int cell,out string reason)
        {
            if (!CanDig(cell,out reason)) return false;
            DeepTerrainTile tile = world.MaterialAt(cell);
            float resistance = tile == null ? 1 : Mathf.Max(.25f,tile.excavationResistance);
            NewOrder(DeepWorkKind.Dig,"挖掘地形",cell,excavationSeconds*resistance);
            reason = "已排入挖掘队列"; return true;
        }
        public bool RequestMove(DeepWorker worker,Vector2Int cell,out string reason)
        {
            InitializeSession(); reason = string.Empty;
            if (worker == null || !worker.IsAlive || !Workers.Contains(worker) || !IsKnown(cell) || !IsStandable(cell)) { reason = "此处没有可站立的位置"; return false; }
            if (!DeepNavigation.TryFindPath(this,worker.Cell,new[] { cell },out var path)) { reason = "目的地不可达，需要地面或梯子"; return false; }
            RequeueWorker(worker,"等待重新认领");
            worker.automationPaused = false;
            var order = NewOrder(DeepWorkKind.Move,"前往指定位置",cell,.01f);
            order.requestedWorker = worker; order.worker = worker; order.workCell = cell; order.state = DeepWorkState.Moving;
            worker.currentOrder = order; worker.SetPath(path);
            DeepParticleFeedback.Emit(DeepFeedbackKind.Move,FootPosition(cell));
            reason = "已下达移动指令"; return true;
        }
        public bool CanResearch(DeepTechDefinition technology,out string reason)
        {
            InitializeSession(); reason = string.Empty;
            if (!initialized || technology == null || string.IsNullOrWhiteSpace(technology.id)) { reason = "未选择科技"; return false; }
            if (IsTechUnlocked(technology.id)) { reason = "科技已经解锁"; return false; }
            foreach (string prerequisite in technology.prerequisiteIds ?? System.Array.Empty<string>()) if (!IsTechUnlocked(prerequisite)) { reason = "前置科技："+TechnologyLabel(prerequisite); return false; }
            foreach (var order in Orders) if (!order.IsTerminal && order.kind == DeepWorkKind.Research && order.technology == technology) { reason = "该科技正在研究队列中"; return false; }
            if (!inventory.CanAfford(technology.cost,out reason)) return false;
            if (!FindReachableStation(DeepBuildingRole.Research,null,false,out _,HasCost(technology.cost))) { reason = "需要可达仓库及研究台"; return false; }
            return true;
        }
        public bool RequestResearch(DeepTechDefinition technology,out string reason)
        {
            if (!CanResearch(technology,out reason)) return false;
            if (!inventory.TryReserve(technology.cost,out var reservation,out reason)) return false;
            FindReachableStation(DeepBuildingRole.Research,null,false,out var station);
            var order = NewOrder(DeepWorkKind.Research,"研究 "+technology.displayName,station.origin,technology.workSeconds);
            order.technology = technology; order.targetBuilding = station; order.reservation = reservation;
            reason = "已排入研究队列"; return true;
        }
        public bool CanCraft(DeepRecipeDefinition recipe,int batches,out string reason)
        {
            InitializeSession(); reason = string.Empty;
            if (!initialized || recipe == null || batches < 1 || batches > 99) { reason = "配方或数量无效"; return false; }
            if (!IsTechUnlocked(recipe.requiredTechId)) { reason = "需要科技："+TechnologyLabel(recipe.requiredTechId); return false; }
            if (recipe.outputs == null || recipe.outputs.Length == 0) { reason = "配方没有产出"; return false; }
            foreach (var output in recipe.outputs) if (output.item == null || output.amount <= 0) { reason = "配方产出尚未配置"; return false; }
            if (!inventory.CanAfford(recipe.inputs,out reason,batches)) return false;
            if (!FindReachableStation(DeepBuildingRole.Fabricator,recipe.requiredBuildingId,false,out _,HasCost(recipe.inputs))) { reason = "需要可达仓库及制造台"; return false; }
            return true;
        }
        public bool RequestCraft(DeepRecipeDefinition recipe,int batches,out string reason)
        {
            if (!CanCraft(recipe,batches,out reason)) return false;
            if (!inventory.TryReserve(recipe.inputs,out var reservation,out reason,batches)) return false;
            FindReachableStation(DeepBuildingRole.Fabricator,recipe.requiredBuildingId,false,out var station);
            var order = NewOrder(DeepWorkKind.Craft,"制造 "+recipe.displayName,station.origin,recipe.workSeconds*batches);
            order.recipe = recipe; order.batches = batches; order.targetBuilding = station; order.reservation = reservation;
            reason = "已排入制造队列"; return true;
        }
        public bool CanPipe(GasNode from,GasNode to,GasOutputPort port,out string reason)
        {
            InitializeSession(); reason = string.Empty;
            if (from == null || to == null || from == to || network == null) { reason = "请选择两个不同的气体接口"; return false; }
            if (System.Array.IndexOf(network.nodes,from) < 0 || System.Array.IndexOf(network.nodes,to) < 0) { reason = "设备尚未接入基地"; return false; }
            if (!IsKnown(world.WorldToCell(from.transform.position)) || !IsKnown(world.WorldToCell(to.transform.position))) { reason = "远端设备尚未探明"; return false; }
            if ((port != GasOutputPort.Mixed && from.kind != GasNodeKind.Separator) || (from.kind == GasNodeKind.Separator && port == GasOutputPort.Mixed)) { reason = "分离器需要指定氧气或尾气出口"; return false; }
            foreach (var link in network.links) if (link != null && link.from == from && link.to == to && link.fromPort == port) { reason = "接口之间已有管道"; return false; }
            foreach (var order in Orders) if (!order.IsTerminal && order.kind == DeepWorkKind.Pipe && order.fromNode == from && order.toNode == to && order.fromPort == port) { reason = "该管道已在施工队列中"; return false; }
            var alloy = catalog == null ? null : catalog.FindItem("alloy");
            if (alloy == null) { reason = "尚未配置管道材料"; return false; }
            if (!inventory.CanAfford(new[] { new DeepItemAmount(alloy,PipeCost(from,to)) },out reason)) return false;
            if (!AnyWorkerCanReach(PipeWorkPositions(from,to),out _,true)) { reason = "需要可达仓库与管道端点"; return false; }
            return true;
        }
        public int PipeCost(GasNode from,GasNode to)
        {
            if (from == null || to == null) return 0;
            Vector2Int a = world.WorldToCell(from.transform.position), b = world.WorldToCell(to.transform.position);
            return Mathf.Max(1,Mathf.CeilToInt((Mathf.Abs(a.x-b.x)+Mathf.Abs(a.y-b.y))/3f));
        }
        public bool RequestPipe(GasNode from,GasNode to,GasOutputPort port,out string reason)
        {
            if (!CanPipe(from,to,port,out reason)) return false;
            int cost = PipeCost(from,to);
            if (!inventory.TryReserve(new[] { new DeepItemAmount(catalog.FindItem("alloy"),cost) },out var reservation,out reason)) return false;
            var order = NewOrder(DeepWorkKind.Pipe,"铺设气体管道",world.WorldToCell(from.transform.position),2+cost*.5f);
            order.fromNode = from; order.toNode = to; order.fromPort = port; order.reservation = reservation;
            reason = "已排入管道施工队列"; return true;
        }
        List<Vector2Int> PipeWorkPositions(GasNode from,GasNode to)
        {
            var positions = new List<Vector2Int>();
            foreach (var node in new[] { from,to })
            {
                if (node == null) continue;
                var building = node.GetComponentInParent<DeepBuildingInstance>();
                positions.AddRange(WorkPositions(building == null ? new RectInt(world.WorldToCell(node.transform.position),Vector2Int.one) : building.Bounds));
            }
            return positions;
        }
        public bool CancelOrder(DeepWorkOrder order,out string reason)
        {
            if (order == null || !Orders.Contains(order) || order.IsTerminal) { reason = "工单已经结束"; return false; }
            inventory.Refund(order.reservation); ReleaseClaims(order);
            if (order.worker != null) { order.worker.currentOrder = null; order.worker.SetPath(null); }
            order.worker = null; order.state = DeepWorkState.Cancelled; order.statusReason = "已取消，物料已退回";
            reason = order.statusReason; return true;
        }
        public void CancelOrder(DeepWorkOrder order) => CancelOrder(order,out _);
        DeepWorkOrder NewOrder(DeepWorkKind kind,string label,Vector2Int cell,float seconds)
        {
            var order = new DeepWorkOrder { id = nextOrderId++,priority = Mathf.Clamp(defaultOrderPriority,1,9),kind = kind,label = label,targetCell = cell,totalSeconds = Mathf.Max(.01f,seconds),state = DeepWorkState.Queued,statusReason = "等待工人" };
            Orders.Add(order);
            foreach (var worker in Workers) if (worker != null) worker.nextWorkSearchTime = 0;
            return order;
        }
        List<Vector2Int> WorkPositions(RectInt area)
        {
            var cells = new List<Vector2Int>();
            for (int y = area.yMin-2; y <= area.yMax; y++) for (int x = area.xMin-1; x <= area.xMax; x++)
            {
                var cell = new Vector2Int(x,y); if (!area.Contains(cell) && IsStandable(cell)) cells.Add(cell);
            }
            return cells;
        }
        static bool HasCost(DeepItemAmount[] cost) { if (cost == null) return false; foreach (var item in cost) if (item.amount > 0) return true; return false; }
        bool TryWarehousePath(DeepWorker worker,out List<Vector2Int> path,out Vector2Int pickupCell)
        {
            path = null; pickupCell = default;
            foreach (var building in Buildings)
            {
                if (building == null || !building.isConstructed || building.definition == null || building.definition.role != DeepBuildingRole.Storage) continue;
                if (DeepNavigation.TryFindPath(this,worker.Cell,WorkPositions(building.Bounds),out var candidate) && (path == null || candidate.Count < path.Count))
                { path = candidate; pickupCell = path.Count == 0 ? worker.Cell : path[path.Count-1]; }
            }
            return path != null;
        }
        public string TechnologyLabel(string id)
        {
            var technology = catalog == null ? null : catalog.FindTech(id);
            return technology == null ? id : technology.displayName;
        }
        bool IsPlannedSupport(Vector2Int cell)
        {
            return placementClaims.TryGetValue(cell,out var claim) && !claim.IsTerminal && claim.buildingDefinition != null && claim.buildingDefinition.blocksMovement;
        }
        bool AnyWorkerCanReach(List<Vector2Int> goals,out DeepWorker reachable,bool requireMaterials = false)
        {
            reachable = null;
            foreach (var worker in Workers)
                if (worker != null && worker.IsAlive && worker.isActiveAndEnabled && DeepNavigation.TryFindPath(this,worker.Cell,goals,out _) && (!requireMaterials || TryWarehousePath(worker,out _,out _))) { reachable = worker; return true; }
            return false;
        }
        bool FindReachableStation(DeepBuildingRole role,string requiredId,bool operational,out DeepBuildingInstance result,bool requireMaterials = false)
        {
            result = null;
            foreach (var building in Buildings)
            {
                if (building == null || !building.isConstructed || building.definition == null || building.definition.role != role || (!string.IsNullOrWhiteSpace(requiredId) && building.definition.id != requiredId) || (operational && !building.IsOperational)) continue;
                if (AnyWorkerCanReach(WorkPositions(building.Bounds),out _,requireMaterials)) { result = building; return true; }
            }
            return false;
        }
        void ReleaseClaims(DeepWorkOrder order)
        {
            if (order.kind != DeepWorkKind.Build || order.buildingDefinition == null) return;
            foreach (Vector2Int cell in new RectInt(order.targetCell,order.buildingDefinition.footprint).allPositionsWithin)
                if (placementClaims.TryGetValue(cell,out var owner) && owner == order) placementClaims.Remove(cell);
        }
    }
}
