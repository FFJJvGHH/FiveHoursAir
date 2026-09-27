using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public static class DeepGameplayTests
    {
        public static string RunGameplayTests()
        {
            ReservationAndBuild(); PauseAndNavigation(); ResearchProgress(); CraftStorage(); Excavation(); PipeConstruction(); PriorityAndDirectControl(); WorkerPreferences(); BlockedPlansAndPower();
            return "Deep Pressure gameplay: 9 groups passed. Reservations/refunds/claims/warehouse pickup; worker arrival/build; pause/headroom/ladders; prerequisites/research; finite storage/crafting; excavation yield; paid pipe construction/conservation; priority/stop/resume/direct-move; enabled worker specialties; blocked plans and workstation power recovery.";
        }
        public static string RunAll() => RunGameplayTests();
        static void ReservationAndBuild()
        {
            using (var f = new Fixture())
            {
                var lamp = f.Definition("lamp",DeepBuildingRole.Light,new Vector2Int(1,2)); lamp.cost = new[] { new DeepItemAmount(f.ore,4) };
                int initialBuildings = f.session.Buildings.Count;
                Assert(f.session.RequestBuild(lamp,new Vector2Int(5,1),out string reason),reason);
                Assert(f.session.inventory.GetAmount(f.ore) == 16 && f.session.inventory.UsedCapacity == 20,"Build reserves materials without removing their warehouse space.");
                Assert(f.session.Buildings.Count == initialBuildings,"A queued order must not instantiate a building.");
                Assert(!f.session.RequestBuild(lamp,new Vector2Int(5,1),out _),"Pending footprints prevent duplicate placement.");
                var order = f.session.Orders[0]; Assert(f.session.CancelOrder(order,out _),"Pending construction can be cancelled.");
                Assert(f.session.inventory.GetAmount(f.ore) == 20,"Cancellation refunds all reserved material.");
                Assert(!f.session.CancelOrder(order,out _) && f.session.inventory.GetAmount(f.ore) == 20,"Repeated cancellation cannot mint resources.");
                Assert(f.session.RequestBuild(lamp,new Vector2Int(5,1),out reason),reason);
                Vector3 start = f.worker.transform.position; f.session.Tick(.05f);
                Assert(f.session.Buildings.Count == initialBuildings && f.worker.transform.position != start && f.worker.currentOrder.fetchingMaterials,"Worker must walk to the warehouse before construction.");
                f.Run(4);
                Assert(f.session.Buildings.Count == initialBuildings+1 && f.session.Orders[1].state == DeepWorkState.Completed,"Construction completes only after warehouse pickup, arrival and work.");
                Assert(f.session.inventory.GetAmount(f.ore) == 16 && f.session.inventory.UsedCapacity == 16,"Finished building consumes its reserved materials once.");
            }
        }
        static void PauseAndNavigation()
        {
            using (var f = new Fixture())
            {
                Assert(f.session.RequestMove(f.worker,new Vector2Int(10,1),out string reason),reason);
                Vector3 start = f.worker.transform.position;
                f.session.menuOpen = true; f.session.Tick(5);
                Assert(f.worker.transform.position == start && f.session.DeltaTime == 0 && f.network.paused,"Menu freezes workers and gas clock.");
                f.session.menuOpen = false; f.session.paused = true; f.session.Tick(5);
                Assert(f.worker.transform.position == start,"Pause keeps orders physically frozen.");
                f.session.paused = false; f.session.Tick(.1f);
                Assert(f.worker.transform.position != start,"Unpausing resumes real movement.");
                f.session.CancelOrder(f.worker.currentOrder,out _); f.worker.TeleportToCell(new Vector2Int(1,1));
                f.world.SetTerrain(4,2,TerrainKind.Basalt);
                Assert(!f.session.IsPassable(new Vector2Int(4,1)),"A 1-cell-high tunnel cannot admit a 2-cell worker.");
                Assert(!f.session.RequestMove(f.worker,new Vector2Int(7,1),out _),"Workers cannot tunnel through a low ceiling or fly over it.");
                var ladder = f.Definition("ladder",DeepBuildingRole.Ladder,new Vector2Int(1,3)); ladder.requiresFloor = false;
                f.Station(ladder,new Vector2Int(3,1));
                Assert(f.session.RequestMove(f.worker,new Vector2Int(3,3),out reason),"Ladder permits vertical route: "+reason);
                f.Run(4); Assert(f.worker.Cell == new Vector2Int(3,3),"Worker actually climbs to the requested ladder cell.");
            }
        }
        static void ResearchProgress()
        {
            using (var f = new Fixture())
            {
                var desk = f.Definition("desk",DeepBuildingRole.Research,new Vector2Int(2,2)); desk.powerRequired = 5; f.Station(desk,new Vector2Int(6,1));
                var generator = f.Definition("generator",DeepBuildingRole.Generator,Vector2Int.one); generator.powerGenerated = 20; f.Station(generator,new Vector2Int(11,1)); f.session.Tick(.01f);
                var basic = f.Asset<DeepTechDefinition>(); basic.id = "basic"; basic.displayName = "基础"; basic.workSeconds = .3f; basic.cost = new[] { new DeepItemAmount(f.ore,2) };
                var advanced = f.Asset<DeepTechDefinition>(); advanced.id = "advanced"; advanced.prerequisiteIds = new[] { "basic" }; advanced.cost = new[] { new DeepItemAmount(f.ore,1) };
                Assert(!f.session.RequestResearch(advanced,out _) && f.session.inventory.GetAmount(f.ore) == 20,"Missing prerequisites reject research without charging.");
                Assert(f.session.RequestResearch(basic,out string reason),reason);
                Assert(!f.session.IsTechUnlocked("basic") && f.session.inventory.GetAmount(f.ore) == 18,"Research starts with reserved inputs, not an instant unlock.");
                f.Run(4); Assert(f.session.IsTechUnlocked("basic"),"Worker at a powered research desk unlocks the completed technology.");
                Assert(f.session.RequestResearch(advanced,out reason),reason);
                var order = f.session.Orders[f.session.Orders.Count-1]; f.session.CancelOrder(order,out _);
                Assert(!f.session.IsTechUnlocked("advanced") && f.session.inventory.GetAmount(f.ore) == 18,"Cancelling research refunds cost but cannot unlock the technology.");
            }
        }
        static void CraftStorage()
        {
            using (var f = new Fixture())
            {
                var factory = f.Definition("factory",DeepBuildingRole.Fabricator,new Vector2Int(3,2)); f.Station(factory,new Vector2Int(6,1));
                var recipe = f.Asset<DeepRecipeDefinition>(); recipe.id = "craft"; recipe.displayName = "合金"; recipe.requiredBuildingId = "factory"; recipe.workSeconds = .2f;
                recipe.inputs = new[] { new DeepItemAmount(f.ore,2) }; recipe.outputs = new[] { new DeepItemAmount(f.alloy,5) };
                Assert(f.session.inventory.TryAdd(f.ore,18),"Fixture fills warehouse close to capacity.");
                Assert(f.session.RequestCraft(recipe,1,out string reason),reason); f.Run(4);
                var order = f.session.Orders[0];
                Assert(order.state == DeepWorkState.Blocked && order.Progress == 1 && f.session.inventory.GetAmount(f.alloy) == 0,"Full warehouse blocks finished outputs without deleting or granting them.");
                f.session.CancelOrder(order,out _);
                Assert(f.session.inventory.GetAmount(f.ore) == 38 && f.session.inventory.UsedCapacity == 38,"Blocked crafting can refund inputs without storage loss.");
                Assert(f.session.RequestCraft(recipe,1,out reason),reason); f.Run(1);
                f.session.inventory.SetCapacity(45); f.Run(2);
                Assert(f.session.inventory.GetAmount(f.ore) == 36 && f.session.inventory.GetAmount(f.alloy) == 5,"Increasing capacity releases exactly one paid-for output batch.");
                f.Run(2); Assert(f.session.inventory.GetAmount(f.alloy) == 5,"Completed orders cannot duplicate their outputs.");
            }
        }
        static void Excavation()
        {
            using (var f = new Fixture())
            {
                f.session.excavationSeconds = .2f;
                Assert(f.session.RequestDig(new Vector2Int(4,0),out string reason),reason);
                Assert(f.world.GetTerrain(4,0) == TerrainKind.Basalt,"Terrain remains until workers finish excavation.");
                f.Run(4);
                Assert(f.world.GetTerrain(4,0) == TerrainKind.Empty && f.session.inventory.GetAmount(f.ore) == 23,"Completed excavation removes terrain and deposits finite mineral yield.");
            }
        }
        static void PipeConstruction()
        {
            using (var f = new Fixture())
            {
                var catalog = f.Asset<DeepGameplayCatalog>(); catalog.items = new[] { f.ore,f.alloy }; f.session.catalog = catalog;
                GasNode[] nodes = new GasNode[2];
                for (int i = 0; i < 2; i++)
                {
                    var go = new GameObject("PipeTestNode"); go.transform.SetParent(f.root.transform); go.transform.position = new Vector3(i == 0 ? 2.5f : 10.5f,1,0);
                    nodes[i] = go.AddComponent<GasNode>(); nodes[i].volumeM3 = 1; nodes[i].initialPressureKPa = i == 0 ? 100 : 0; nodes[i].kind = GasNodeKind.Storage;
                }
                f.network.RefreshTopologyPreservingGas(); var before = f.network.TotalInventory();
                Assert(f.session.inventory.TryAdd(f.alloy,8),"Fixture has finite pipe metal.");
                int cost = f.session.PipeCost(nodes[0],nodes[1]);
                Assert(f.session.RequestPipe(nodes[0],nodes[1],GasOutputPort.Mixed,out string reason),reason);
                Assert(f.network.links.Length == 0 && f.session.inventory.GetAmount(f.alloy) == 8-cost,"Pipe reserves metal without creating an instant connection.");
                Assert(!f.session.RequestPipe(nodes[0],nodes[1],GasOutputPort.Mixed,out _),"Pending pipe duplicates are rejected.");
                Assert(!f.session.RequestPipe(nodes[0],nodes[1],GasOutputPort.OxygenProduct,out _),"A plain tank cannot acquire a separator output.");
                f.session.CancelOrder(f.session.Orders[0],out _);
                Assert(f.session.inventory.GetAmount(f.alloy) == 8,"Cancelling pipe work refunds metal.");
                Assert(f.session.RequestPipe(nodes[0],nodes[1],GasOutputPort.Mixed,out reason),reason); f.Run(8);
                Assert(f.network.links.Length == 1 && f.network.links[0].from == nodes[0] && f.network.links[0].to == nodes[1],"Worker completion creates exactly the explicit endpoint connection.");
                Assert(f.session.inventory.GetAmount(f.alloy) == 8-cost,"Completed pipe spends its reserved metal once.");
                Assert(Math.Abs(f.network.TotalInventory().Total-before.Total) < 1e-7,"Adding a pipe preserves existing tank gas.");
                f.network.Step(.1f); Assert(f.network.VerifyConservation(out reason),reason);
            }
        }
        static void PriorityAndDirectControl()
        {
            using (var f = new Fixture())
            {
                Assert(f.session.RequestDig(new Vector2Int(3,0),out string reason),reason);
                var near = f.session.Orders[0]; f.session.SetOrderPriority(near,1);
                Assert(f.session.RequestDig(new Vector2Int(12,0),out reason),reason);
                var urgent = f.session.Orders[1]; f.session.SetOrderPriority(urgent,9);
                f.session.Tick(.05f);
                Assert(f.worker.currentOrder == urgent,"A distant priority-9 job wins over the nearer priority-1 job.");
                Assert(f.session.RequestStop(f.worker,out reason),reason);
                f.Run(1);
                Assert(f.worker.currentOrder == null && f.worker.automationPaused && !urgent.IsTerminal,"Stop holds the worker and keeps unfinished colony work available.");
                f.session.ResumeWorker(f.worker); f.session.Tick(.05f);
                Assert(f.worker.currentOrder == urgent,"Resuming restores automatic work selection.");
                Assert(f.session.RequestMove(f.worker,new Vector2Int(2,1),out reason),reason);
                Assert(!urgent.IsTerminal && urgent.worker == null && f.worker.currentOrder.kind == DeepWorkKind.Move,"Direct movement returns the existing job to the colony queue.");
                var firstMove = f.worker.currentOrder;
                Assert(f.session.RequestMove(f.worker,new Vector2Int(6,1),out reason),reason);
                Assert(firstMove.state == DeepWorkState.Cancelled && f.worker.currentOrder != firstMove,"Replacing a move cancels the old private command.");
            }
        }
        static void WorkerPreferences()
        {
            using (var f = new Fixture())
            {
                var lamp = f.Definition("test_lamp",DeepBuildingRole.Light,Vector2Int.one);
                Assert(f.session.RequestBuild(lamp,new Vector2Int(6,1),out string reason),reason);
                var construction = f.session.Orders[0]; f.session.SetOrderPriority(construction,9);
                Assert(f.session.RequestDig(new Vector2Int(10,0),out reason),reason);
                var digging = f.session.Orders[1]; f.session.SetOrderPriority(digging,1);
                f.session.SetWorkerPreference(f.worker,DeepWorkKind.Build,1); f.session.SetWorkerPreference(f.worker,DeepWorkKind.Dig,3);
                f.session.Tick(.05f);
                Assert(f.worker.currentOrder == digging,"Worker specialties are considered before colony task priority.");
                f.session.SetWorkerPreference(f.worker,DeepWorkKind.Dig,0); f.session.Tick(.05f);
                Assert(f.worker.currentOrder == construction && !digging.IsTerminal,"Disabling a specialty releases its job and permits another enabled kind.");
            }
            using (var f = new Fixture())
            {
                var desk = f.Definition("shared_research",DeepBuildingRole.Research,new Vector2Int(2,2));
                f.Station(desk,new Vector2Int(2,1)); var reachable = f.Station(desk,new Vector2Int(10,1));
                f.world.SetTerrain(7,1,TerrainKind.Basalt); f.world.SetTerrain(7,2,TerrainKind.Basalt); f.worker.TeleportToCell(new Vector2Int(12,1));
                var helperObject = new GameObject("Disabled researcher"); helperObject.transform.SetParent(f.root.transform);
                var helper = helperObject.AddComponent<DeepWorker>(); helper.session = f.session; helper.researchPreference = 0; helper.TeleportToCell(new Vector2Int(1,1)); f.session.Workers.Add(helper);
                var tech = f.Asset<DeepTechDefinition>(); tech.id = "station_selection";
                Assert(f.session.RequestResearch(tech,out string reason),reason); f.session.Tick(.1f);
                Assert(f.worker.currentOrder != null && f.worker.currentOrder.targetBuilding == reachable,"An enabled researcher chooses a reachable station even when another station was initially assigned across a wall.");
            }
        }
        static void BlockedPlansAndPower()
        {
            using (var f = new Fixture())
            {
                var floor = f.Definition("planned_floor",DeepBuildingRole.Floor,Vector2Int.one); floor.blocksMovement = true;
                var machine = f.Definition("supported_machine",DeepBuildingRole.Light,Vector2Int.one);
                Assert(f.session.RequestBuild(floor,new Vector2Int(6,1),out string reason),reason);
                Assert(f.session.IsPassable(new Vector2Int(6,1)),"An unfinished construction blueprint does not physically block passage.");
                Assert(f.session.RequestBuild(machine,new Vector2Int(6,2),out reason),"Equipment can be planned on queued support: "+reason);
                var equipment = f.session.Orders[1]; f.session.SetOrderPriority(equipment,9); f.session.Tick(.1f);
                Assert(equipment.state == DeepWorkState.Blocked && equipment.statusReason.Contains("地板"),"A dependent machine waits for its real support even with higher priority.");
                f.Run(5); Assert(equipment.state == DeepWorkState.Completed,"Support completion automatically releases dependent construction.");
            }
            using (var f = new Fixture())
            {
                f.world.SetTerrain(9,1,TerrainKind.Basalt); f.world.SetTerrain(9,2,TerrainKind.Basalt);
                Assert(f.session.RequestDig(new Vector2Int(12,0),out string reason),"Unreachable excavation remains plannable: "+reason);
                var order = f.session.Orders[0]; f.session.Tick(.1f);
                Assert(order.state == DeepWorkState.Blocked && order.statusReason.Contains("不可达"),"Blocked plans explain the missing route.");
                f.world.SetTerrain(9,1,TerrainKind.Empty); f.world.SetTerrain(9,2,TerrainKind.Empty); f.Run(10);
                Assert(order.state == DeepWorkState.Completed,"Opening a route automatically resumes a blocked plan.");
            }
            using (var f = new Fixture())
            {
                var desk = f.Definition("desk",DeepBuildingRole.Research,new Vector2Int(2,2)); desk.powerRequired = 5;
                var station = f.Station(desk,new Vector2Int(6,1));
                var generator = f.Definition("generator",DeepBuildingRole.Generator,Vector2Int.one); generator.powerGenerated = 20; f.Station(generator,new Vector2Int(11,1)); f.session.Tick(.01f);
                var tech = f.Asset<DeepTechDefinition>(); tech.id = "power_test"; tech.workSeconds = 20;
                Assert(f.session.RequestResearch(tech,out string reason),reason); var order = f.session.Orders[0];
                for (int step = 0; step < 100 && order.state != DeepWorkState.Working; step++) f.session.Tick(.1f);
                Assert(order.state == DeepWorkState.Working,"Fixture reaches a powered research station.");
                station.isOn = false; f.session.Tick(.1f);
                Assert(order.state == DeepWorkState.Blocked && f.worker.currentOrder == null,"Power interruption releases workers instead of trapping them at a disabled station.");
                float progress = order.completedSeconds; station.isOn = true; f.Run(4);
                Assert(order.completedSeconds > progress,"Workstation recovery resumes retained research progress.");
            }
        }
        sealed class Fixture : IDisposable
        {
            public readonly GameObject root;
            public readonly DeepPressureWorld world;
            public readonly GasNetworkSimulator network;
            public readonly DeepGameSession session;
            public readonly DeepWorker worker;
            public readonly DeepItemDefinition ore,alloy;
            readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
            public Fixture()
            {
                root = new GameObject("DeepGameplayTest");
                world = root.AddComponent<DeepPressureWorld>(); world.width = 15; world.height = 7; world.terrainKinds = new TerrainKind[105];
                for (int x = 0; x < 15; x++) world.SetTerrain(x,0,TerrainKind.Basalt); world.RebuildRooms();
                network = root.AddComponent<GasNetworkSimulator>(); network.ResetSimulation();
                ore = Asset<DeepItemDefinition>(); ore.id = "ore"; alloy = Asset<DeepItemDefinition>(); alloy.id = "alloy";
                session = root.AddComponent<DeepGameSession>(); session.world = world; session.network = network; session.baseStorageCapacity = 40; session.excavationItem = ore;
                session.useWiredPower = false; // These worker/order fixtures isolate legacy electricity; DeepPowerTests covers real circuits.
                session.startingInventory = new[] { new DeepItemAmount(ore,20) };
                var person = new GameObject("TestWorker"); person.transform.SetParent(root.transform); person.transform.position = new Vector3(1.5f,1,0);
                worker = person.AddComponent<DeepWorker>(); session.InitializeSession();
                Station(Definition("warehouse",DeepBuildingRole.Storage,new Vector2Int(1,2)),new Vector2Int(4,1));
            }
            public T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); temporary.Add(asset); return asset; }
            public DeepBuildingDefinition Definition(string id,DeepBuildingRole role,Vector2Int size)
            {
                var definition = Asset<DeepBuildingDefinition>(); definition.id = definition.displayName = id; definition.role = role; definition.footprint = size; definition.blocksMovement = false; definition.workSeconds = .2f;
                var prefab = new GameObject("TestPrefab_"+id); prefab.SetActive(false); prefab.AddComponent<DeepBuildingInstance>(); temporary.Add(prefab); definition.prefab = prefab; return definition;
            }
            public DeepBuildingInstance Station(DeepBuildingDefinition definition,Vector2Int origin)
            {
                var go = new GameObject("TestStation_"+definition.id); go.transform.SetParent(root.transform); go.transform.position = session.BuildingPosition(origin);
                var building = go.AddComponent<DeepBuildingInstance>(); building.definition = definition; building.origin = origin; building.session = session;
                session.Buildings.Add(building); session.RebuildOccupancy(); session.RefreshStorageCapacity(); return building;
            }
            public void Run(float seconds) { for (int i = 0; i < Mathf.CeilToInt(seconds/.1f); i++) session.Tick(.1f); }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); foreach (var item in temporary) if (item != null) UnityEngine.Object.DestroyImmediate(item); }
        }
        static void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException("Gameplay self-test: "+message); }
    }
}
