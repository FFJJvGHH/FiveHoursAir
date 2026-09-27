using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public sealed partial class DeepGameSession
    {
        public bool ValidateSaveState(DeepSaveData data,out string reason)
        {
            reason = string.Empty;
            try
            {
                RequireSave(initialized && world != null && catalog != null,"关卡或内容目录未就绪");
                RequireSave(data != null && data.version == 1,"不支持的世界数据版本");
                RequireSave(data.sceneName == world.gameObject.scene.name,"存档属于另一个关卡");
                RequireSave(data.width == world.width && data.height == world.height,"存档地图尺寸不匹配");
                RequireSave(data.terrain != null && data.terrain.Length == world.width*world.height && data.terrainTiles != null && data.terrainTiles.Length == data.terrain.Length,"地形数据不完整");
                foreach (var kind in data.terrain) RequireSave(Enum.IsDefined(typeof(TerrainKind),kind),"地形类型无效");
                RequireSave(data.inventory != null && data.technologies != null && data.buildings != null && data.nodes != null && data.links != null && data.workers != null && data.orders != null && data.rooms != null,"存档缺少必要数据");
                RequireSave(SaveFinite(data.simulationTime) && data.simulationTime >= 0 && SaveFinite(data.speed) && data.speed >= .25f && data.speed <= 4,"模拟时钟数据无效");
                RequireSave(SaveFinite(data.networkElapsed) && data.networkElapsed >= 0 && data.networkSteps >= 0 && SaveFinite(data.networkRemainder) && data.networkRemainder >= 0,"气体时钟数据无效");
                RequireSave(network == null || SaveFinite(data.networkStepSeconds) && data.networkStepSeconds >= .01f && data.networkStepSeconds <= .5f,"气体步长无效");
                RequireSave(data.displacedGas.IsFiniteAndNonnegative,"排挤气体数据无效");
                RequireSave(data.defaultOrderPriority >= 1 && data.defaultOrderPriority <= 9 && data.overlay >= 0 && data.overlay <= 4,"操作设置无效");
                if(data.systemsRevision>0)
                {
                    RequireSave(data.wires!=null&&data.production!=null&&SaveFinite(data.stableAirSeconds)&&data.stableAirSeconds>=0,"电网或生产数据缺失");
                    var cells=new HashSet<Vector2Int>();foreach(var cell in data.wires)RequireSave(world.IsInside(cell)&&cells.Add(cell),"电线格重复或越界");
                    var recipes=new HashSet<string>();foreach(var p in data.production)RequireSave(p!=null&&catalog.FindRecipe(p.recipeId)!=null&&recipes.Add(p.recipeId)&&p.targetAmount>0&&p.targetAmount<=999,"生产目标无效");
                    if(data.lifeSupport)
                    {
                        RequireSave(data.atmosphereCells!=null&&data.atmosphereTemperatures!=null&&data.atmosphereCells.Length==data.terrain.Length&&data.atmosphereTemperatures.Length==data.terrain.Length,"逐格气氛数据不完整");
                        for(int i=0;i<data.atmosphereCells.Length;i++)RequireSave(data.atmosphereCells[i].IsFiniteAndNonnegative&&SaveFinite(data.atmosphereTemperatures[i])&&data.atmosphereTemperatures[i]>-273.15,"逐格气氛数据无效");
                    }
                }
                RequireSave(!data.hasCamera || SaveFinite(data.cameraPosition) && SaveFinite(data.cameraSize) && data.cameraSize > 0 && data.cameraSize <= 200,"镜头数据无效");
                ValidateSavedItems(data.inventory);
                var unlocked = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in data.technologies) RequireSave(!string.IsNullOrEmpty(id) && unlocked.Add(id) && (catalog.FindTech(id) != null || Array.IndexOf(initialUnlockedTechIds,id) >= 0),"存档引用了缺失或重复的科技："+id);
                var buildingIds = new HashSet<string>(StringComparer.Ordinal); var savedBuildings = new Dictionary<string,DeepSavedBuilding>();
                foreach (var entry in data.buildings)
                {
                    RequireSave(entry != null && ValidSaveId(entry.id,"b") && buildingIds.Add(entry.id),"建筑 ID 重复或无效");
                    var definition = catalog.FindBuilding(entry.definitionId);
                    RequireSave(definition != null && definition.prefab != null,"缺少建筑资源："+entry.definitionId);
                    RequireSave(world.IsInside(entry.origin) && world.IsInside(entry.origin+definition.footprint-Vector2Int.one),"建筑位置超出地图");
                    RequireSave(SaveFinite(entry.position) && SaveFinite(entry.scale) && SaveFinite(entry.rotation.x) && SaveFinite(entry.rotation.y) && SaveFinite(entry.rotation.z) && SaveFinite(entry.rotation.w) && SaveFinite(entry.fuelRemainder) && entry.fuelRemainder >= 0,"建筑状态无效");
                    savedBuildings.Add(entry.id,entry);
                    RequireSave(SaveFinite(entry.batteryEnergy)&&entry.batteryEnergy>=0&&entry.batteryEnergy<=definition.batteryCapacity+.01f&&SaveFinite(entry.fuelSecondsRemaining)&&entry.fuelSecondsRemaining>=0,"电池或燃料状态无效");
                }
                var currentNodes = new Dictionary<string,GasNode>(); foreach (var node in world.GetComponentsInChildren<GasNode>(true)) currentNodes[ObjectId(node,"n")] = node;
                var nodeIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in data.nodes)
                {
                    RequireSave(entry != null && ValidSaveId(entry.id,"n") && nodeIds.Add(entry.id),"气体接口 ID 重复或无效");
                    RequireSave(entry.gas.IsFiniteAndNonnegative && SaveFinite(entry.position) && SaveFinite(entry.volume) && entry.volume > 0 && SaveFinite(entry.temperature) && entry.temperature > -273.15f && SaveFinite(entry.maxPressure) && entry.maxPressure > 0 && SaveFinite(entry.targetPressure) && entry.targetPressure >= 0 && SaveFinite(entry.throughput) && entry.throughput >= 0,"气体设备数据无效");
                    RequireSave(Enum.IsDefined(typeof(GasNodeKind),entry.kind),"气体设备类型无效");
                    if (string.IsNullOrEmpty(entry.buildingId)) RequireSave(currentNodes.ContainsKey(entry.id),"关卡缺少气体设备："+entry.name);
                    else
                    {
                        RequireSave(buildingIds.Contains(entry.buildingId),"气体设备所属建筑缺失");
                        if (!currentNodes.ContainsKey(entry.id))
                        {
                            var prefab = catalog.FindBuilding(savedBuildings[entry.buildingId].definitionId).prefab;
                            var target = FindChild(prefab.transform,entry.path);
                            RequireSave(target != null && target.GetComponent<GasNode>() != null,"建筑预制体缺少存档中的气体接口");
                        }
                    }
                }
                var linkIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in data.links)
                {
                    RequireSave(entry != null && ValidSaveId(entry.id,"p") && linkIds.Add(entry.id) && nodeIds.Contains(entry.fromId) && nodeIds.Contains(entry.toId) && entry.fromId != entry.toId,"管道端点或 ID 无效");
                    RequireSave(Enum.IsDefined(typeof(GasOutputPort),entry.port) && SaveFinite(entry.valve) && entry.valve >= 0 && entry.valve <= 1 && SaveFinite(entry.conductance) && entry.conductance >= 0 && SaveFinite(entry.maxFlow) && entry.maxFlow >= 0,"管道设置无效");
                }
                var existingWorkers = new HashSet<string>(); foreach (var worker in Workers) if (worker != null) existingWorkers.Add(ObjectId(worker,"w"));
                var workerIds = new HashSet<string>(StringComparer.Ordinal); var savedWorkers = new Dictionary<string,DeepSavedWorker>();
                RequireSave(existingWorkers.Count == data.workers.Length,"工人数量与当前关卡不匹配");
                foreach (var entry in data.workers)
                {
                    RequireSave(entry != null && ValidSaveId(entry.id,"w") && workerIds.Add(entry.id) && existingWorkers.Contains(entry.id),"工人 ID 不匹配");
                    RequireSave(SaveFinite(entry.position) && SaveFinite(entry.moveSpeed) && entry.moveSpeed > 0 && SaveFinite(entry.workSpeed) && entry.workSpeed > 0,"工人状态无效");
                    foreach (int preference in new[] { entry.digPreference,entry.buildPreference,entry.researchPreference,entry.craftPreference,entry.pipePreference }) RequireSave(preference >= 0 && preference <= 3,"工人优先级无效");
                    RequireSave(entry.path != null,"工人路线缺失"); foreach (var cell in entry.path) RequireSave(world.IsInside(cell),"工人路线超出地图");
                    savedWorkers.Add(entry.id,entry);
                    RequireSave(SaveFinite(entry.airReserveSeconds)&&entry.airReserveSeconds>=0&&entry.airReserveSeconds<=90,"工人气氛缓冲状态无效");
                }
                var orderIds = new HashSet<int>(); var savedOrders = new Dictionary<int,DeepSavedOrder>(); long held = 0;
                foreach (var entry in data.orders)
                {
                    RequireSave(entry != null && entry.id > 0 && orderIds.Add(entry.id) && data.nextOrderId > entry.id,"工单编号无效");
                    RequireSave(Enum.IsDefined(typeof(DeepWorkKind),entry.kind) && Enum.IsDefined(typeof(DeepWorkState),entry.state) && entry.priority >= 1 && entry.priority <= 9 && entry.batches >= 1 && entry.batches <= 999,"工单类型或优先级无效");
                    RequireSave(SaveFinite(entry.totalSeconds) && entry.totalSeconds > 0 && SaveFinite(entry.completedSeconds) && entry.completedSeconds >= 0 && entry.completedSeconds <= entry.totalSeconds+.001f && SaveFinite(entry.nextRetryTime),"工单进度无效");
                    bool active = entry.state != DeepWorkState.Completed && entry.state != DeepWorkState.Cancelled;
                    RequireSave(string.IsNullOrEmpty(entry.buildingDefinitionId) || catalog.FindBuilding(entry.buildingDefinitionId) != null,"工单建筑资源缺失");
                    RequireSave(string.IsNullOrEmpty(entry.technologyId) || catalog.FindTech(entry.technologyId) != null,"工单科技资源缺失");
                    RequireSave(string.IsNullOrEmpty(entry.recipeId) || catalog.FindRecipe(entry.recipeId) != null,"工单配方资源缺失");
                    RequireSave(!active || entry.kind != DeepWorkKind.Build || catalog.FindBuilding(entry.buildingDefinitionId) != null,"建造工单缺少建筑定义");
                    RequireSave(!active || entry.kind != DeepWorkKind.Research || catalog.FindTech(entry.technologyId) != null,"研究工单缺少科技定义");
                    RequireSave(!active || entry.kind != DeepWorkKind.Craft || catalog.FindRecipe(entry.recipeId) != null,"制造工单缺少配方定义");
                    RequireSave(!active || entry.kind != DeepWorkKind.Pipe || nodeIds.Contains(entry.fromNodeId) && nodeIds.Contains(entry.toNodeId),"管道工单缺少端点");
                    RequireSave(string.IsNullOrEmpty(entry.workerId) || workerIds.Contains(entry.workerId),"工单执行者缺失");
                    RequireSave(string.IsNullOrEmpty(entry.requestedWorkerId) || workerIds.Contains(entry.requestedWorkerId),"工单指定工人缺失");
                    RequireSave(string.IsNullOrEmpty(entry.targetBuildingId) || buildingIds.Contains(entry.targetBuildingId),"工单工作台缺失");
                    if (entry.reservation != null) { ValidateSavedItems(entry.reservation); if (!entry.reservationSettled) foreach (var item in entry.reservation) held += item.amount; }
                    RequireSave(!active || !entry.reservationSettled,"未完成工单的材料已经结算");
                    RequireSave(active || string.IsNullOrEmpty(entry.workerId),"已结束工单仍占用工人");
                    if (!string.IsNullOrEmpty(entry.workerId)) RequireSave(savedWorkers[entry.workerId].currentOrderId == entry.id,"工人与工单关联不一致");
                    savedOrders.Add(entry.id,entry);
                }
                foreach (var entry in data.workers)
                    if (entry.currentOrderId >= 0) RequireSave(savedOrders.TryGetValue(entry.currentOrderId,out var order) && order.workerId == entry.id,"工人引用无效工单");
                long stored = held; foreach (var item in data.inventory) stored += item.amount;
                RequireSave(held <= int.MaxValue && stored <= int.MaxValue && data.nextOrderId > 0,"仓库数量或工单编号溢出");
                if (exploration != null)
                {
                    RequireSave(data.exploration != null && data.exploration.visible != null && data.exploration.visible.Length == data.terrain.Length && data.exploration.regions != null,"探索数据不完整");
                    var regionIds = new HashSet<string>(); foreach (var region in world.GetComponentsInChildren<DeepPressureRegion>(true)) regionIds.Add(region.stableId);
                    var savedRegionIds = new HashSet<string>();
                    foreach (var region in data.exploration.regions) RequireSave(region != null && regionIds.Contains(region.id) && savedRegionIds.Add(region.id) && Enum.IsDefined(typeof(DeepExplorationState),region.state),"探索区域缺失或重复");
                    RequireSave(regionIds.Count == savedRegionIds.Count,"探索区域数量不匹配");
                }
                ValidateRoomTopology(data);
                // All material references are checked before replacing any tile or object.
                if (world.terrain != null)
                    for (int i = 0; i < data.terrain.Length; i++)
                    {
                        if (data.terrain[i] == TerrainKind.Empty) continue;
                        bool found = !string.IsNullOrEmpty(data.terrainTiles[i]) && savedTilePalette.ContainsKey(data.terrainTiles[i]);
                        if (!found) foreach (var tile in savedTilePalette.Values) if (tile != null && tile.kind == data.terrain[i]) { found = true; break; }
                        RequireSave(found,"地形材质资源缺失："+data.terrain[i]);
                    }
                return true;
            }
            catch (Exception exception) { reason = "无法恢复存档："+exception.Message; return false; }
        }
        void ValidateSavedItems(DeepSavedItem[] entries)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal); long total = 0;
            foreach (var item in entries)
            { RequireSave(!string.IsNullOrWhiteSpace(item.id) && ids.Add(item.id) && catalog.FindItem(item.id) != null && item.amount >= 0,"物料 ID、数量或资源无效"); total += item.amount; }
            RequireSave(total <= int.MaxValue,"物料数量溢出");
        }
        void ValidateRoomTopology(DeepSaveData data)
        {
            var claimed = new bool[data.terrain.Length]; var queue = new Queue<Vector2Int>(); int total = 0;
            foreach (var room in data.rooms)
            {
                RequireSave(room != null && world.IsInside(room.anchor) && room.cells > 0 && room.gas.IsFiniteAndNonnegative && SaveFinite(room.temperature) && room.temperature > -273.15,"气室数据无效");
                int start = room.anchor.y*world.width+room.anchor.x;
                RequireSave(data.terrain[start] == TerrainKind.Empty && !claimed[start],"气室锚点无效或重复");
                queue.Enqueue(room.anchor); claimed[start] = true; int count = 0;
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue(); count++;
                    foreach (var direction in new[] { Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down })
                    {
                        var next = cell+direction; if (!world.IsInside(next)) continue; int index = next.y*world.width+next.x;
                        if (claimed[index] || data.terrain[index] != TerrainKind.Empty) continue; claimed[index] = true; queue.Enqueue(next);
                    }
                }
                RequireSave(count == room.cells,"气室连通区域与保存的气体库存不匹配"); total += count;
            }
            int expected = 0; foreach (var kind in data.terrain) if (kind == TerrainKind.Empty) expected++;
            RequireSave(total == expected,"存档缺少气室库存");
        }
        static bool ValidSaveId(string id,string prefix) => !string.IsNullOrEmpty(id) && id.StartsWith(prefix+":",StringComparison.Ordinal) && id.Length > 2;
        static void RequireSave(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
        static bool SaveFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool SaveFinite(Vector3 value) => SaveFinite(value.x) && SaveFinite(value.y) && SaveFinite(value.z);
    }
}
