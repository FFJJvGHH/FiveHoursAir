using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace DeepPressure
{
    public sealed partial class DeepGameSession
    {
        DeepSaveData initialSaveState;
        readonly Dictionary<string,DeepTerrainTile> savedTilePalette = new Dictionary<string,DeepTerrainTile>(StringComparer.Ordinal);
        public static string SaveDirectory => Path.Combine(Application.persistentDataPath,"DeepPressure","Saves","v1");
        public bool HasPlayableSession { get; private set; }
        public string LastSaveMessage { get; private set; }
        public float LastSavedAtRealtime { get; private set; } = -1;
        public void CaptureInitialState()
        {
            if (initialSaveState != null) return;
            InitializeSession(); if (!initialized) return;
            initialSaveState = CaptureSaveState();
            if (Application.isPlaying && GetComponent<DeepAutosave>() == null) gameObject.AddComponent<DeepAutosave>().session = this;
        }
        public DeepSaveSlotInfo[] GetSaveSlots() => DeepSaveStore.List(SaveDirectory);
        public bool SaveGame(string slot,out string message)
        {
            try
            {
                CaptureInitialState(); var data = CaptureSaveState(); data.title = DeepSaveStore.SlotTitle(slot);
                if (!ValidateSaveState(data,out message)) { LastSaveMessage = message; return false; }
                bool saved = DeepSaveStore.Write(SaveDirectory,slot,data,out message);
                LastSaveMessage = message; if (saved) LastSavedAtRealtime = Time.realtimeSinceStartup;
                return saved;
            }
            catch (Exception exception) { message = LastSaveMessage = "保存失败："+exception.Message; return false; }
        }
        public bool LoadGame(string slot,out string message)
        {
            CaptureInitialState();
            if (!DeepSaveStore.Read(SaveDirectory,slot,out var data,out message,out bool recovered)) return false;
            if (!TryRestoreSaveState(data,out message)) return false;
            HasPlayableSession = true;
            message = recovered ? "已恢复上一份完整备份" : "已读取 "+DeepSaveStore.SlotTitle(slot);
            ResetAutosaveTimer(); return true;
        }
        public bool ContinueGame(out string message)
        {
            var slots = new List<DeepSaveSlotInfo>(GetSaveSlots());
            slots.Sort((a,b) => string.CompareOrdinal(b.savedUtc,a.savedUtc));
            message = "尚无可继续的存档";
            foreach (var slot in slots) if (slot.isValid && LoadGame(slot.slotId,out message)) return true;
            return false;
        }
        public bool NewGame(out string message)
        {
            CaptureInitialState();
            if (initialSaveState == null) { message = "关卡尚未初始化"; return false; }
            if (!TryRestoreSaveState(initialSaveState,out message)) return false;
            HasPlayableSession = true; paused = false; speed = 1;
            ResetAutosaveTimer(); message = "新基地已就绪"; return true;
        }
        void ResetAutosaveTimer() { var autosave = GetComponent<DeepAutosave>(); if (autosave != null) autosave.ResetTimer(); }
        public DeepSaveData CaptureSaveState()
        {
            InitializeSession(); if (!initialized || !world.HasValidTerrainData) throw new InvalidOperationException("关卡尚未初始化");
            var hud = world.GetComponent<DeepPressureHUD>(); var camera = hud != null && hud.viewCamera != null ? hud.viewCamera : Camera.main;
            var data = new DeepSaveData
            {
                sceneName = world.gameObject.scene.name,savedUtc = DateTime.UtcNow.ToString("O"),title = "深压基地",
                width = world.width,height = world.height,terrain = (TerrainKind[])world.terrainKinds.Clone(),terrainTiles = new string[world.width*world.height],
                simulationTime = SimulationTime,speed = speed,paused = paused,nextOrderId = nextOrderId,defaultOrderPriority = defaultOrderPriority,
                technologies = new List<string>(technologies).ToArray(),inventory = SaveItems(inventory.Items),displacedGas = world.displacedGas,
                exploration = exploration == null ? null : exploration.CaptureDiscovery(),
                hasCamera = camera != null,cameraPosition = camera == null ? Vector3.zero : camera.transform.position,cameraSize = camera == null ? 12 : camera.orthographicSize,
                overlay = hud == null ? 0 : (int)hud.overlay,
                systemsRevision=1,wiredPower=useWiredPower,lifeSupport=lifeSupportEnabled,wires=completedWireCells.ToArray(),
                stableAirSeconds=StableAirSeconds,production=productionTargets.ConvertAll(p=>new DeepProductionTarget{recipeId=p.recipeId,targetAmount=p.targetAmount,enabled=p.enabled}).ToArray()
            };
            if (constructedFloorTile != null) savedTilePalette[constructedFloorTile.name] = constructedFloorTile;
            for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
            {
                var tile = world.MaterialAt(new Vector2Int(x,y)); if (tile == null) continue;
                savedTilePalette[tile.name] = tile; data.terrainTiles[y*world.width+x] = tile.name;
            }
            var buildingData = new List<DeepSavedBuilding>();
            foreach (var building in Buildings)
            {
                if (building == null) continue;
                buildingData.Add(new DeepSavedBuilding { id = ObjectId(building,"b"),definitionId = building.definition == null ? null : building.definition.id,
                    name = building.name,origin = building.origin,position = building.transform.position,rotation = building.transform.rotation,scale = building.transform.localScale,
                    isOn = building.isOn,isConstructed = building.isConstructed,active = building.gameObject.activeSelf,fuelRemainder = building.fuelRemainder,
                    batteryEnergy=building.batteryEnergy,fuelSecondsRemaining=building.fuelSecondsRemaining });
            }
            data.buildings = buildingData.ToArray();
            var nodeData = new List<DeepSavedNode>();
            foreach (var node in world.GetComponentsInChildren<GasNode>(true))
            {
                var building = node.GetComponentInParent<DeepBuildingInstance>();
                nodeData.Add(new DeepSavedNode { id = ObjectId(node,"n"),buildingId = building == null ? null : ObjectId(building,"b"),
                    path = building == null ? null : ChildPath(building.transform,node.transform),position = node.transform.position,name = node.displayName,
                    kind = node.kind,gas = node.gas,volume = node.volumeM3,temperature = node.temperatureC,maxPressure = node.maxPressureKPa,targetPressure = node.targetPressureKPa,
                    throughput = node.throughputMolPerSecond,enabled = node.enabled });
            }
            data.nodes = nodeData.ToArray();
            var linkData = new List<DeepSavedLink>();
            foreach (var link in world.GetComponentsInChildren<GasLink>(true))
                if (link.from != null && link.to != null) linkData.Add(new DeepSavedLink { id = ObjectId(link,"p"),fromId = ObjectId(link.from,"n"),toId = ObjectId(link.to,"n"),port = link.fromPort,
                    isOpen = link.isOpen,allowReverse = link.allowReverse,enabled = link.enabled,valve = link.valve,conductance = link.conductanceMolPerSecondPerKPa,maxFlow = link.maxFlowMolPerSecond });
            data.links = linkData.ToArray();
            var workerData = new List<DeepSavedWorker>();
            foreach (var worker in Workers)
            {
                if (worker == null) continue;
                workerData.Add(new DeepSavedWorker { id = ObjectId(worker,"w"),name = worker.displayName,position = worker.transform.position,path = worker.CapturePath(),currentOrderId = worker.currentOrder == null ? -1 : worker.currentOrder.id,
                    moveSpeed = worker.moveCellsPerSecond,workSpeed = worker.workSpeed,digPreference = worker.digPreference,buildPreference = worker.buildPreference,researchPreference = worker.researchPreference,
                    craftPreference = worker.craftPreference,pipePreference = worker.pipePreference,automationPaused = worker.automationPaused,airReserveSeconds=worker.airReserveSeconds });
            }
            data.workers = workerData.ToArray();
            var orderData = new List<DeepSavedOrder>();
            foreach (var order in Orders)
                orderData.Add(new DeepSavedOrder { id = order.id,kind = order.kind,state = order.state,label = order.label,statusReason = order.statusReason,priority = order.priority,
                    targetCell = order.targetCell,workCell = order.workCell,pickupCell = order.pickupCell,completedSeconds = order.completedSeconds,totalSeconds = order.totalSeconds,nextRetryTime = order.nextRetryTime,
                    workerId = ObjectId(order.worker,"w"),requestedWorkerId = ObjectId(order.requestedWorker,"w"),targetBuildingId = ObjectId(order.targetBuilding,"b"),
                    buildingDefinitionId = order.buildingDefinition == null ? null : order.buildingDefinition.id,technologyId = order.technology == null ? null : order.technology.id,recipeId = order.recipe == null ? null : order.recipe.id,
                    fromNodeId = ObjectId(order.fromNode,"n"),toNodeId = ObjectId(order.toNode,"n"),fromPort = order.fromPort,batches = order.batches,
                    fetchingMaterials = order.fetchingMaterials,materialsCollected = order.materialsCollected,reservation = order.reservation == null ? null : SaveItems(order.reservation.items),reservationSettled = order.reservation != null && order.reservation.IsSettled });
            data.orders = orderData.ToArray();
            var roomData = new List<DeepSavedRoom>();
            foreach (var room in world.Rooms) roomData.Add(new DeepSavedRoom { anchor = room.cells[0],cells = room.cellCount,gas = room.gas,temperature = room.temperatureC });
            data.rooms = roomData.ToArray();
            if (network != null) { data.networkElapsed = network.ElapsedSeconds; data.networkSteps = network.StepCount; data.networkRemainder = network.SaveRemainder; data.networkStepSeconds = network.fixedStepSeconds; }
            return data;
        }
        public bool TryRestoreSaveState(DeepSaveData data,out string message)
        {
            InitializeSession();
            if (!ValidateSaveState(data,out message)) return false;
            DeepSaveData rollback = CaptureSaveState(); bool previousMenu = menuOpen;
            try { ApplySaveState(data); message = "存档已恢复"; return true; }
            catch (Exception exception)
            {
                try { ApplySaveState(rollback); } catch (Exception recovery) { Debug.LogError("恢复载入前状态失败："+recovery,this); message = "载入与恢复均失败："+exception.Message; return false; }
                message = "载入失败，已保留原基地："+exception.Message; return false;
            }
            finally
            {
                menuOpen = previousMenu; if (network != null) network.paused = IsSimulationPaused;
                if (Application.isPlaying) Time.timeScale = IsSimulationPaused ? 0 : speed;
            }
        }
        static DeepSavedItem[] SaveItems(IEnumerable<KeyValuePair<string,int>> items)
        {
            var result = new List<DeepSavedItem>(); foreach (var pair in items) result.Add(new DeepSavedItem { id = pair.Key,amount = pair.Value });
            result.Sort((a,b) => string.CompareOrdinal(a.id,b.id)); return result.ToArray();
        }
        string ObjectId(Component component,string prefix)
        {
            if (component == null) return null;
            var identity = component.GetComponent<DeepSaveIdentity>(); if (identity == null) identity = component.gameObject.AddComponent<DeepSaveIdentity>();
            if (string.IsNullOrEmpty(identity.id))
            {
                var parts = new List<string>(); Transform cursor = component.transform;
                while (cursor != null && cursor != world.transform) { parts.Add(cursor.name+"["+cursor.GetSiblingIndex()+"]"); cursor = cursor.parent; }
                parts.Reverse(); identity.id = string.Join("/",parts);
                if (string.IsNullOrEmpty(identity.id)) identity.id = "world";
            }
            return prefix+":"+identity.id;
        }
        static void SetObjectId(Component component,string id)
        {
            var identity = component.GetComponent<DeepSaveIdentity>(); if (identity == null) identity = component.gameObject.AddComponent<DeepSaveIdentity>();
            identity.id = id.Substring(id.IndexOf(':')+1);
        }
        static string ChildPath(Transform root,Transform child)
        {
            var parts = new List<string>(); while (child != root) { parts.Add(child.GetSiblingIndex().ToString()); child = child.parent; }
            parts.Reverse(); return string.Join("/",parts);
        }
        static Transform FindChild(Transform root,string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            foreach (string part in path.Split('/'))
            { if (!int.TryParse(part,out int index) || index < 0 || index >= root.childCount) return null; root = root.GetChild(index); }
            return root;
        }
        static T FindSaved<T>(Dictionary<string,T> objects,string id) where T : class
        { return !string.IsNullOrEmpty(id) && objects.TryGetValue(id,out var result) ? result : null; }
        void RemoveSavedObject(GameObject target)
        {
            target.SetActive(false); target.transform.SetParent(null,false);
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
        void ApplySaveState(DeepSaveData data)
        {
            bool menuWasOpen = menuOpen; menuOpen = true; DeltaTime = 0;
            if (network != null) network.paused = true;
            var buildings = new Dictionary<string,DeepBuildingInstance>(StringComparer.Ordinal);
            foreach (var building in Buildings) if (building != null) buildings[ObjectId(building,"b")] = building;
            var desiredBuildings = new HashSet<string>(); foreach (var entry in data.buildings) desiredBuildings.Add(entry.id);
            foreach (var pair in buildings) if (!desiredBuildings.Contains(pair.Key)) RemoveSavedObject(pair.Value.gameObject);
            Buildings.Clear();
            foreach (var entry in data.buildings)
            {
                var building = FindSaved(buildings,entry.id); var definition = catalog.FindBuilding(entry.definitionId);
                if (building == null)
                {
                    var go = Instantiate(definition.prefab,entry.position,entry.rotation,world.transform);
                    building = go.GetComponent<DeepBuildingInstance>(); if (building == null) building = go.AddComponent<DeepBuildingInstance>();
                    SetObjectId(building,entry.id); buildings[entry.id] = building;
                }
                building.name = entry.name; building.definition = definition; building.origin = entry.origin;
                building.transform.SetPositionAndRotation(entry.position,entry.rotation); building.transform.localScale = entry.scale;
                building.isOn = entry.isOn; building.isConstructed = entry.isConstructed; building.fuelRemainder = entry.fuelRemainder; building.session = this;
                building.batteryEnergy=entry.batteryEnergy;building.fuelSecondsRemaining=entry.fuelSecondsRemaining;
                building.gameObject.SetActive(entry.active); Buildings.Add(building);
            }
            var workers = new Dictionary<string,DeepWorker>(StringComparer.Ordinal);
            foreach (var worker in Workers) if (worker != null) workers[ObjectId(worker,"w")] = worker;
            foreach (var entry in data.workers)
            {
                var worker = workers[entry.id]; worker.currentOrder = null; worker.displayName = entry.name; worker.transform.position = entry.position;
                worker.moveCellsPerSecond = entry.moveSpeed; worker.workSpeed = entry.workSpeed; worker.RestorePath(entry.path);
                worker.digPreference = entry.digPreference; worker.buildPreference = entry.buildPreference; worker.researchPreference = entry.researchPreference;
                worker.craftPreference = entry.craftPreference; worker.pipePreference = entry.pipePreference; worker.automationPaused = entry.automationPaused;
                worker.airReserveSeconds=data.systemsRevision>0?entry.airReserveSeconds:90;worker.environmentEfficiency=1;
            }
            world.terrainKinds = (TerrainKind[])data.terrain.Clone();
            if (world.terrain != null)
            {
                var positions = new Vector3Int[data.terrain.Length]; var tiles = new TileBase[data.terrain.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = new Vector3Int(i%world.width,i/world.width,0);
                    if (data.terrain[i] == TerrainKind.Empty) continue;
                    DeepTerrainTile tile = null;
                    if (!string.IsNullOrEmpty(data.terrainTiles[i])) savedTilePalette.TryGetValue(data.terrainTiles[i],out tile);
                    if (tile == null) foreach (var candidate in savedTilePalette.Values) if (candidate != null && candidate.kind == data.terrain[i]) { tile = candidate; break; }
                    tiles[i] = tile;
                }
                world.terrain.ClearAllTiles(); world.terrain.SetTiles(positions,tiles); world.terrain.RefreshAllTiles();
            }
            world.RebuildRooms(); world.displacedGas = data.displacedGas;
            foreach (var room in world.Rooms) room.gas = default;
            foreach (var entry in data.rooms)
            {
                var room = world.RoomAt(entry.anchor);
                if (room == null || room.cellCount != entry.cells) throw new InvalidOperationException("气室拓扑与存档不一致");
                room.gas = entry.gas; room.temperatureC = entry.temperature;
            }
            if (exploration != null) exploration.RestoreDiscovery(data.exploration);
            var nodes = new Dictionary<string,GasNode>(StringComparer.Ordinal);
            foreach (var node in world.GetComponentsInChildren<GasNode>(true)) nodes[ObjectId(node,"n")] = node;
            foreach (var entry in data.nodes)
            {
                var node = FindSaved(nodes,entry.id);
                if (node == null && !string.IsNullOrEmpty(entry.buildingId))
                {
                    var target = FindChild(buildings[entry.buildingId].transform,entry.path);
                    if (target != null) node = target.GetComponent<GasNode>();
                }
                if (node == null) throw new InvalidOperationException("找不到气体设备："+entry.name);
                SetObjectId(node,entry.id); nodes[entry.id] = node;
                node.displayName = entry.name; node.kind = entry.kind; node.transform.position = entry.position;
                node.volumeM3 = entry.volume; node.temperatureC = entry.temperature; node.maxPressureKPa = entry.maxPressure; node.targetPressureKPa = entry.targetPressure;
                node.throughputMolPerSecond = entry.throughput; node.gas = entry.gas; node.enabled = entry.enabled;
                node.lastInflowMolPerSecond = node.lastOutflowMolPerSecond = 0; node.status = "已恢复";
            }
            var links = new Dictionary<string,GasLink>(StringComparer.Ordinal);
            foreach (var link in world.GetComponentsInChildren<GasLink>(true)) links[ObjectId(link,"p")] = link;
            var desiredLinks = new HashSet<string>(); foreach (var entry in data.links) desiredLinks.Add(entry.id);
            foreach (var pair in links) if (!desiredLinks.Contains(pair.Key)) RemoveSavedObject(pair.Value.gameObject);
            foreach (var entry in data.links)
            {
                var link = FindSaved(links,entry.id);
                if (link == null) { link = DeepPlayerPipeFactory.Create(this,nodes[entry.fromId],nodes[entry.toId],entry.port); SetObjectId(link,entry.id); }
                link.from = nodes[entry.fromId]; link.to = nodes[entry.toId]; link.fromPort = entry.port;
                link.isOpen = entry.isOpen; link.allowReverse = entry.allowReverse; link.valve = entry.valve;
                link.conductanceMolPerSecondPerKPa = entry.conductance; link.maxFlowMolPerSecond = entry.maxFlow; link.enabled = entry.enabled; link.lastFlowMolPerSecond = 0;
            }
            technologies.Clear(); foreach (string id in data.technologies) technologies.Add(id);
            Orders.Clear(); placementClaims.Clear(); var orders = new Dictionary<int,DeepWorkOrder>(); int held = 0;
            foreach (var entry in data.orders)
            {
                var order = new DeepWorkOrder { id = entry.id,kind = entry.kind,state = entry.state,label = entry.label,statusReason = entry.statusReason,priority = entry.priority,
                    targetCell = entry.targetCell,workCell = entry.workCell,pickupCell = entry.pickupCell,completedSeconds = entry.completedSeconds,totalSeconds = entry.totalSeconds,nextRetryTime = entry.nextRetryTime,
                    worker = FindSaved(workers,entry.workerId),requestedWorker = FindSaved(workers,entry.requestedWorkerId),targetBuilding = FindSaved(buildings,entry.targetBuildingId),
                    buildingDefinition = catalog.FindBuilding(entry.buildingDefinitionId),technology = catalog.FindTech(entry.technologyId),recipe = catalog.FindRecipe(entry.recipeId),
                    fromNode = FindSaved(nodes,entry.fromNodeId),toNode = FindSaved(nodes,entry.toNodeId),fromPort = entry.fromPort,batches = entry.batches,fetchingMaterials = entry.fetchingMaterials,materialsCollected = entry.materialsCollected };
                if (entry.reservation != null)
                {
                    var reserved = new Dictionary<string,int>(StringComparer.Ordinal); foreach (var item in entry.reservation) reserved.Add(item.id,item.amount);
                    order.reservation = new DeepReservation(reserved) { settled = entry.reservationSettled };
                    if (!entry.reservationSettled) held += order.reservation.Units;
                }
                if (!order.IsTerminal && order.kind == DeepWorkKind.Build)
                    foreach (Vector2Int cell in new RectInt(order.targetCell,order.buildingDefinition.footprint).allPositionsWithin) placementClaims[cell] = order;
                Orders.Add(order); orders.Add(order.id,order);
            }
            foreach (var entry in data.workers) workers[entry.id].currentOrder = entry.currentOrderId < 0 ? null : orders[entry.currentOrderId];
            inventory.RestoreSavedInventory(data.inventory,held);
            SimulationTime = data.simulationTime; nextOrderId = data.nextOrderId; speed = data.speed; paused = data.paused; defaultOrderPriority = data.defaultOrderPriority;
            if(data.systemsRevision>0)
            {
                useWiredPower=data.wiredPower;lifeSupportEnabled=data.lifeSupport;completedWireCells=new List<Vector2Int>(data.wires??Array.Empty<Vector2Int>());
                StableAirSeconds=data.stableAirSeconds;productionTargets=new List<DeepProductionTarget>();
                foreach(var p in data.production??Array.Empty<DeepProductionTarget>())productionTargets.Add(new DeepProductionTarget{recipeId=p.recipeId,targetAmount=p.targetAmount,enabled=p.enabled});
            }
            nextProductionCheck=0;InvalidatePowerTopology();
            RebuildOccupancy(); RefreshStorageCapacity(); UpdatePower(0);
            if (network != null)
            {
                // Assign topology directly: commissioning would refill reconstructed tanks.
                network.nodes = world.GetComponentsInChildren<GasNode>(true); network.links = world.GetComponentsInChildren<GasLink>(true);
                network.fixedStepSeconds = data.networkStepSeconds; network.RestoreSavedClock(data.networkElapsed,data.networkSteps,data.networkRemainder);
                network.simulationSpeed = speed;
            }
            var hud = world.GetComponent<DeepPressureHUD>(); var camera = hud != null && hud.viewCamera != null ? hud.viewCamera : Camera.main;
            if (data.hasCamera && camera != null) { camera.transform.position = data.cameraPosition; camera.orthographicSize = data.cameraSize; }
            if (hud != null) hud.overlay = (DeepPressureHUD.OverlayMode)data.overlay;
            menuOpen = menuWasOpen; if (network != null) network.paused = IsSimulationPaused;
            RefreshExplorationVisibility();
            if (Application.isPlaying) Time.timeScale = IsSimulationPaused ? 0 : speed;
        }
    }
}
