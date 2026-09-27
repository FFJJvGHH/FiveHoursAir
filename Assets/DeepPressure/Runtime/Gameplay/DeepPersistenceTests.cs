using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DeepPressure
{
    /// <summary>Called explicitly by the editor validation runner; never writes into player save slots.</summary>
    public static class DeepPersistenceTests
    {
        public static string RunAll()
        {
            PendingWorkRoundTrip(); CompletedWorldAndNewGame(); DiscoveryRoundTrip(); CorruptionAndAtomicRecovery(); RejectInvalidBeforeMutation();
            return "Deep Pressure persistence: 5 groups passed. In-flight worker paths/reservations/priorities; completed buildings/terrain/room and network gas/new game; discovery fog; JSON/checksum/backup recovery; incompatible-save rejection before mutation.";
        }
        static void PendingWorkRoundTrip()
        {
            using (var f = new Fixture())
            {
                f.worker.researchPreference = 3; f.worker.pipePreference = 0;
                f.session.defaultOrderPriority = 8;
                Assert(f.session.RequestBuild(f.lamp,new Vector2Int(8,1),out string reason),reason);
                f.session.Tick(.1f);
                var order = f.worker.currentOrder;
                Assert(order != null && order.state == DeepWorkState.Moving && order.fetchingMaterials,"Fixture must save during physical material pickup.");
                var saved = Clone(f.session.CaptureSaveState()); Vector3 position = f.worker.transform.position;
                int heldCapacity = f.session.inventory.UsedCapacity, pathLength = f.worker.CapturePath().Length;
                f.session.CancelOrder(order); f.worker.TeleportToCell(new Vector2Int(12,1)); f.worker.researchPreference = 0;
                Assert(f.session.TryRestoreSaveState(saved,out reason),reason);
                Assert(Vector3.Distance(position,f.worker.transform.position) < .0001f && f.worker.CapturePath().Length == pathLength,"Worker resumes the saved point and remaining path.");
                Assert(f.worker.currentOrder != null && f.worker.currentOrder.fetchingMaterials && f.worker.currentOrder.priority == 8,"Claimed pickup state and order priority survive load.");
                Assert(f.worker.researchPreference == 3 && f.worker.pipePreference == 0,"Worker job preferences survive load.");
                Assert(f.session.inventory.GetAmount(f.ore) == 26 && f.session.inventory.UsedCapacity == heldCapacity,"Reserved materials retain both their cost and occupied capacity.");
                Assert(f.session.CancelOrder(f.worker.currentOrder,out reason),reason);
                Assert(f.session.inventory.GetAmount(f.ore) == 30 && f.session.inventory.UsedCapacity == 30,"A restored reservation refunds once, without minting extra material.");
            }
        }
        static void CompletedWorldAndNewGame()
        {
            using (var f = new Fixture())
            {
                f.session.CaptureInitialState();
                Assert(f.session.RequestBuild(f.lamp,new Vector2Int(8,1),out string reason),reason);
                f.Run(8);
                Assert(f.session.BuildingAt(new Vector2Int(8,1)) != null,"Fixture must construct a runtime building.");
                f.world.SetTerrain(6,0,TerrainKind.Empty); f.world.RebuildRoomsPreservingGas();
                f.world.Rooms[0].gas = new GasMixture { oxygen = 43,nitrogen = 19 };
                f.a.gas = new GasMixture { oxygen = 17,nitrogen = 11 }; f.b.gas = new GasMixture { waterVapour = 5 };
                var link = DeepPlayerPipeFactory.Create(f.session,f.a,f.b,GasOutputPort.Mixed); link.valve = .35f; link.isOpen = false;
                f.network.nodes = new[] { f.a,f.b }; f.network.links = new[] { link }; f.network.RestoreSavedClock(12.4,124,.04);
                f.session.paused = true; f.session.speed = 2;
                var saved = Clone(f.session.CaptureSaveState());
                Assert(f.session.NewGame(out reason),reason);
                Assert(f.session.BuildingAt(new Vector2Int(8,1)) == null && f.world.GetTerrain(6,0) == TerrainKind.Basalt,"New game restores the initial terrain and removes runtime construction.");
                Assert(f.session.Orders.Count == 0 && f.session.inventory.GetAmount(f.ore) == 30 && f.network.links.Length == 0,"New game resets jobs, warehouse and runtime pipes.");
                Assert(f.session.TryRestoreSaveState(saved,out reason),reason);
                Assert(f.session.BuildingAt(new Vector2Int(8,1)) != null && f.world.GetTerrain(6,0) == TerrainKind.Empty,"Loading reconstructs missing buildings and edited terrain.");
                Assert(Math.Abs(f.world.Rooms[0].gas.oxygen-43) < 1e-9 && Math.Abs(f.a.gas.oxygen-17) < 1e-9,"Room and tank inventories survive without reset or refill.");
                Assert(f.network.links.Length == 1 && !f.network.links[0].isOpen && Mathf.Abs(f.network.links[0].valve-.35f) < .0001f,"Pipes and valve settings are reconstructed.");
                Assert(Math.Abs(f.network.ElapsedSeconds-12.4) < 1e-9 && f.network.StepCount == 124 && f.session.paused && f.session.speed == 2,"Simulation clocks and pause/speed settings restore.");
                Assert(f.network.VerifyConservation(out reason),reason);
                f.session.paused = false; f.network.Step(.1f); Assert(f.network.VerifyConservation(out reason),reason);
                Assert(f.session.inventory.GetAmount(f.ore) == 26,"Completed construction remains paid after load.");
            }
        }
        static void DiscoveryRoundTrip()
        {
            using (var f = new Fixture(true))
            {
                var exploration = f.world.GetComponent<DeepExploration>();
                Assert(exploration.TrySample(new Vector2Int(11,2),out string reason),reason);
                exploration.RevealAroundWork(new Vector2Int(10,4)); exploration.hasIsolationEquipment = true;
                var saved = Clone(f.session.CaptureSaveState());
                exploration.RevealAroundWork(new Vector2Int(14,5)); exploration.hasIsolationEquipment = false;
                Assert(f.session.TryRestoreSaveState(saved,out reason),reason);
                Assert(exploration.IsVisible(new Vector2Int(10,4)) && !exploration.IsVisible(new Vector2Int(14,5)),"Load restores fog rather than retaining later discoveries.");
                var region = f.world.GetComponentInChildren<DeepPressureRegion>();
                Assert(exploration.GetState(region) == DeepExplorationState.Sampled && exploration.TryGetSample(region,out _) && exploration.hasIsolationEquipment,"Region samples and isolation equipment survive save/load.");
            }
        }
        static void CorruptionAndAtomicRecovery()
        {
            using (var f = new Fixture())
            {
                string directory = Path.Combine(Path.GetTempPath(),"DeepPressure-save-test-"+Guid.NewGuid().ToString("N"));
                try
                {
                    var first = f.session.CaptureSaveState(); first.simulationTime = 10;
                    Assert(DeepSaveStore.Write(directory,"manual1",first,out string reason),reason);
                    var second = Clone(first); second.simulationTime = 20;
                    Assert(DeepSaveStore.Write(directory,"manual1",second,out reason),reason);
                    Assert(DeepSaveStore.Read(directory,"manual1",out var loaded,out reason,out bool recovered) && !recovered && loaded.simulationTime == 20,"Latest atomic save is readable.");
                    File.WriteAllText(Path.Combine(directory,"manual1.json"),"{truncated");
                    Assert(DeepSaveStore.Read(directory,"manual1",out loaded,out reason,out recovered) && recovered && loaded.simulationTime == 10,"Corrupt primary falls back to the previous complete generation.");
                    Assert(DeepSaveStore.Write(directory,"manual1",second,out reason),reason);
                    File.WriteAllText(Path.Combine(directory,"manual1.json"),"{truncated-again");
                    Assert(DeepSaveStore.Read(directory,"manual1",out loaded,out reason,out recovered) && recovered && loaded.simulationTime == 10,"Writing over corruption preserves the healthy backup.");
                    string tampered = DeepSaveStore.Encode(first).Replace("DeepPressure.Save","Foreign.Save");
                    Assert(!DeepSaveStore.Decode(tampered,out _,out _),"Unknown save format is rejected.");
                    string checksum = DeepSaveStore.Encode(first).Replace("\"checksum\": \"","\"checksum\": \"BAD");
                    Assert(!DeepSaveStore.Decode(checksum,out _,out _),"Checksum mismatch is rejected.");
                    Assert(!DeepSaveStore.Write(directory,"../escape",first,out _),"Slot IDs cannot escape the save directory.");
                }
                finally
                {
                    // Delete only explicitly named files in this unique test-owned directory.
                    foreach (string name in new[] { "manual1.json","manual1.json.bak","manual1.json.tmp" })
                    { string path = Path.Combine(directory,name); if (File.Exists(path)) File.Delete(path); }
                    if (Directory.Exists(directory)) Directory.Delete(directory,false);
                }
            }
        }
        static void RejectInvalidBeforeMutation()
        {
            using (var f = new Fixture())
            {
                var snapshot = Clone(f.session.CaptureSaveState()); snapshot.width++;
                int ore = f.session.inventory.GetAmount(f.ore); Vector3 position = f.worker.transform.position;
                Assert(!f.session.TryRestoreSaveState(snapshot,out _),"A different map size must be rejected.");
                snapshot = Clone(f.session.CaptureSaveState()); snapshot.nodes[0].gas.oxygen = double.NaN;
                Assert(!f.session.TryRestoreSaveState(snapshot,out _),"Non-finite gas must be rejected.");
                snapshot = Clone(f.session.CaptureSaveState()); snapshot.buildings[0].definitionId = "removed-content";
                Assert(!f.session.TryRestoreSaveState(snapshot,out _),"Missing content must be rejected before mutation.");
                Assert(f.session.inventory.GetAmount(f.ore) == ore && f.worker.transform.position == position && f.session.Buildings.Count == 1,"Rejected saves leave the running colony intact.");
            }
        }
        static DeepSaveData Clone(DeepSaveData original)
        { Assert(DeepSaveStore.Decode(DeepSaveStore.Encode(original),out var copy,out string reason),reason); return copy; }
        sealed class Fixture : IDisposable
        {
            public readonly GameObject root;
            public readonly DeepPressureWorld world;
            public readonly DeepGameSession session;
            public readonly GasNetworkSimulator network;
            public readonly DeepWorker worker;
            public readonly GasNode a,b;
            public readonly DeepItemDefinition ore;
            public readonly DeepBuildingDefinition lamp;
            readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
            public Fixture(bool discovery = false)
            {
                root = new GameObject("DeepPersistenceTest"); root.SetActive(false);
                world = root.AddComponent<DeepPressureWorld>(); world.width = 16; world.height = 8; world.terrainKinds = new TerrainKind[128];
                for (int x = 0; x < world.width; x++) world.SetTerrain(x,0,TerrainKind.Basalt);
                if (discovery)
                {
                    var regionObject = new GameObject("TestRegion"); regionObject.transform.SetParent(root.transform);
                    var region = regionObject.AddComponent<DeepPressureRegion>(); region.stableId = "test-region"; region.bounds = new RectInt(10,1,3,3);
                    var exploration = root.AddComponent<DeepExploration>(); exploration.world = world; exploration.initialExploredAreas = new[] { new RectInt(0,0,8,8) };
                }
                world.RebuildRooms(); network = root.AddComponent<GasNetworkSimulator>();
                ore = Asset<DeepItemDefinition>(); ore.id = "ore";
                var catalog = Asset<DeepGameplayCatalog>(); catalog.items = new[] { ore };
                var storage = Definition("storage",DeepBuildingRole.Storage); lamp = Definition("lamp",DeepBuildingRole.Light); lamp.cost = new[] { new DeepItemAmount(ore,4) };
                catalog.buildings = new[] { storage,lamp };
                var warehouse = UnityEngine.Object.Instantiate(storage.prefab,root.transform); warehouse.name = "InitialWarehouse";
                var instance = warehouse.GetComponent<DeepBuildingInstance>(); instance.definition = storage; instance.origin = new Vector2Int(4,1); warehouse.transform.position = new Vector3(4,1,0); warehouse.SetActive(true);
                var person = new GameObject("Worker"); person.transform.SetParent(root.transform); person.transform.position = new Vector3(1.5f,1,0); worker = person.AddComponent<DeepWorker>();
                a = Node("A",new Vector3(2,2,0)); b = Node("B",new Vector3(12,2,0)); network.ResetSimulation();
                session = root.AddComponent<DeepGameSession>(); session.world = world; session.network = network; session.catalog = catalog; session.baseStorageCapacity = 100;
                session.startingInventory = new[] { new DeepItemAmount(ore,30) }; root.SetActive(true); session.InitializeSession();
            }
            T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); temporary.Add(asset); return asset; }
            DeepBuildingDefinition Definition(string id,DeepBuildingRole role)
            {
                var definition = Asset<DeepBuildingDefinition>(); definition.id = definition.displayName = id; definition.role = role;
                definition.footprint = new Vector2Int(1,2); definition.blocksMovement = false; definition.workSeconds = .5f;
                var prefab = new GameObject("Prefab_"+id); prefab.SetActive(false); prefab.AddComponent<DeepBuildingInstance>(); temporary.Add(prefab); definition.prefab = prefab; return definition;
            }
            GasNode Node(string name,Vector3 position)
            { var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = position; return go.AddComponent<GasNode>(); }
            public void Run(float seconds) { for (int i = 0; i < Mathf.CeilToInt(seconds/.1f); i++) session.Tick(.1f); }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); foreach (var obj in temporary) if (obj != null) UnityEngine.Object.DestroyImmediate(obj); }
        }
        static void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException("Persistence self-test: "+message); }
    }
}
