using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public static class DeepGameplayTests
    {
        public static string RunGameplayTests()
        {
            ReservationAndBuild(); PauseAndNavigation(); ResearchProgress(); CraftStorage(); Excavation(); PipeConstruction(); PriorityAndDirectControl(); WorkerPreferences(); BlockedPlansAndPower(); LadderDescentAndAnchors(); StepsAndGaps(); HazardousManualMovement(); RouteChangesKeepIntent(); NearestCrewAndWorkPositions(); ConsistentRouteCosts();
            return "Deep Pressure gameplay: 15 groups passed. Reservations/refunds/claims/warehouse pickup; worker arrival/build; pause/headroom/ladders; prerequisites/research; finite storage/crafting; excavation yield; paid pipe construction/conservation; priority/stop/resume/direct-move; enabled worker specialties; blocked plans and workstation power recovery; supported ladder chains and downward construction; body-safe steps and one-cell gap crossings; manual hazardous movement without automatic retreat; topology-triggered detours keep manual targets and carried stock; nearest eligible idle crew and reachable work-side selection; consistent uncapped movement costs.";
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
        static void LadderDescentAndAnchors()
        {
            using(var f=new Fixture())
            {
                // An open two-cell shaft below a platform. The second rung is out of arm's reach
                // until the worker actually descends onto the first completed rung.
                for(int x=0;x<=5;x++)for(int y=1;y<=3;y++)f.world.SetTerrain(x,y,TerrainKind.Basalt);
                f.worker.TeleportToCell(new Vector2Int(4,4));
                var ladder=f.Definition("shaft_ladder",DeepBuildingRole.Ladder,Vector2Int.one);ladder.requiresFloor=false;
                Assert(!f.session.RequestBuild(ladder,new Vector2Int(11,4),out string reason),"A completely floating ladder must be rejected.");
                Assert(f.session.RequestBuild(ladder,new Vector2Int(6,3),out reason),reason);
                Assert(f.session.RequestBuild(ladder,new Vector2Int(6,2),out reason),reason);
                var lower=f.session.Orders[f.session.Orders.Count-1];
                bool stoodOnFirst=false;
                for(int i=0;i<150&&!lower.IsTerminal;i++)
                {
                    f.session.Tick(.05f);
                    if(f.worker.Cell==new Vector2Int(6,3)&&f.session.IsLadder(f.worker.Cell))stoodOnFirst=true;
                }
                Assert(lower.state==DeepWorkState.Completed&&stoodOnFirst,"Worker climbs onto the first rung to construct the next rung downward.");
                Assert(f.session.RequestMove(f.worker,new Vector2Int(6,2),out reason),reason);f.Run(2);
                Assert(f.worker.Cell==new Vector2Int(6,2),"Descending built ladder chain remains navigable.");
            }
            using(var f=new Fixture())
            {
                var ladder=f.Definition("anchored_chain",DeepBuildingRole.Ladder,Vector2Int.one);ladder.requiresFloor=false;
                f.world.SetTerrain(8,3,TerrainKind.Basalt);
                f.Station(ladder,new Vector2Int(9,3));f.Station(ladder,new Vector2Int(9,2));
                Assert(f.session.IsLadder(new Vector2Int(9,2)),"A middle or upper wall anchor supports the entire connected ladder chain.");
                Assert(!f.session.RequestDig(new Vector2Int(8,3),out string reason)&&reason.Contains("承重点"),"Removing the chain's final anchor is prevented.");
                f.world.SetTerrain(10,2,TerrainKind.Basalt);
                Assert(f.session.RequestDig(new Vector2Int(8,3),out reason),"A second anchor permits removing the old anchor: "+reason);
                f.world.SetTerrain(8,3,TerrainKind.Empty);f.world.SetTerrain(10,2,TerrainKind.Empty);f.session.RebuildOccupancy();
                Assert(!f.session.IsLadder(new Vector2Int(9,3))&&!f.session.IsStandable(new Vector2Int(9,2)),"An unsupported legacy ladder is not a floating navigation foothold.");
            }
        }
        static void StepsAndGaps()
        {
            using(var f=new Fixture())
            {
                f.world.SetTerrain(3,1,TerrainKind.Basalt);
                Assert(f.session.RequestMove(f.worker,new Vector2Int(3,2),out string reason),"One-cell step is climbable: "+reason);
                f.Run(3);Assert(f.worker.Cell==new Vector2Int(3,2),"Worker steps up with body clearance.");
                Assert(f.session.RequestMove(f.worker,new Vector2Int(5,1),out reason),"Worker can step back down: "+reason);
                f.Run(3);Assert(f.worker.Cell==new Vector2Int(5,1),"Step-down arrives at the floor.");
                for(int x=0;x<f.world.width;x++)for(int y=0;y<=2;y++)f.world.SetTerrain(x,y,x==6?TerrainKind.Empty:TerrainKind.Basalt);
                f.worker.TeleportToCell(new Vector2Int(5,3));
                Assert(f.session.RequestMove(f.worker,new Vector2Int(7,3),out reason),"One missing floor cell is crossable: "+reason);
                f.Run(3);Assert(f.worker.Cell==new Vector2Int(7,3),"Worker crosses the gap without gravity cancelling the traversal.");
                f.worker.TeleportToCell(new Vector2Int(5,3));f.world.SetTerrain(6,5,TerrainKind.Basalt);
                Assert(!f.session.RequestMove(f.worker,new Vector2Int(7,3),out _),"A low ceiling over the gap blocks a jump that cannot fit the full body.");
                f.world.SetTerrain(6,5,TerrainKind.Empty);for(int y=0;y<=2;y++)f.world.SetTerrain(7,y,TerrainKind.Empty);
                Assert(!f.session.RequestMove(f.worker,new Vector2Int(8,3),out _),"Two missing floor cells require a constructed route.");
            }
        }
        static void HazardousManualMovement()
        {
            using(var f=new Fixture())
            {
                f.world.defaultPressureKPa=100;f.world.defaultComposition=new Vector4(.21f,.79f,0,0);f.world.RebuildRooms();
                f.session.lifeSupportEnabled=true;f.session.hazardsEnabled=false;
                var field=f.session.Atmosphere;
                for(int x=8;x<f.world.width;x++)for(int y=1;y<f.world.height;y++)
                {
                    var cell=new Vector2Int(x,y);field.Take(cell,field.Sample(cell).Total);
                    field.Add(cell,GasMixture.FromPressure(100,field.CellVolumeM3,18,new Vector4(0,1,0,0)));
                }
                Assert(!f.session.IsBreathableAt(new Vector2Int(10,1)),"Fixture's remote work zone is not breathable.");
                Assert(f.session.RequestMove(f.worker,new Vector2Int(10,1),out string reason),"A manual order may deliberately enter hazardous air: "+reason);
                var movement=f.worker.currentOrder;
                f.WaitFor(()=>movement.state==DeepWorkState.Completed,15,"Manual move must physically finish, including the low-oxygen movement penalty");
                Assert(f.worker.Cell==new Vector2Int(10,1)&&Vector3.Distance(f.worker.transform.position,f.session.FootPosition(new Vector2Int(10,1)))<.001f,"Adequate reserve permits arriving at the exact hazardous-zone destination.");
                f.session.excavationSeconds=20;
                Assert(f.session.RequestDig(new Vector2Int(11,0),out reason),reason);
                var work=f.session.Orders[f.session.Orders.Count-1];
                f.WaitFor(()=>f.worker.currentOrder==work&&work.state==DeepWorkState.Working,8,"Worker must claim and physically start the requested hazardous-zone work");
                float progress=work.completedSeconds;f.worker.airReserveSeconds=12;Vector2Int workCell=f.worker.Cell;int orders=f.session.Orders.Count;
                f.Run(4);
                Assert(f.worker.IsAlive&&f.worker.breathingUnsafe&&f.worker.airReserveSeconds<12,"The low-oxygen environment still consumes emergency air.");
                Assert(f.worker.currentOrder==work&&f.worker.Cell==workCell&&work.completedSeconds>progress,"Low air does not replace the player's work target with automatic retreat.");
                Assert(f.session.Orders.Count==orders,"Hazardous air never creates an unsolicited movement order.");
            }
        }
        static void RouteChangesKeepIntent()
        {
            using(var f=new Fixture())
            {
                Assert(f.session.RequestMove(f.worker,new Vector2Int(13,1),out string reason),reason);
                var move=f.worker.currentOrder;f.session.Tick(.1f);
                f.world.SetTerrain(6,1,TerrainKind.Basalt);f.session.RebuildOccupancy();
                f.WaitFor(()=>System.Array.IndexOf(f.worker.CapturePath(),new Vector2Int(6,2))>=0,2,"A new one-cell obstacle must cause an early step-over detour");
                Assert(f.worker.currentOrder==move&&f.worker.Cell.x<5,"Replanning keeps the player's original move and happens before reaching the obstruction.");
                f.WaitFor(()=>move.state==DeepWorkState.Completed,12,"Detour must reach the original destination");
                Assert(f.session.RequestMove(f.worker,new Vector2Int(1,1),out reason),reason);move=f.worker.currentOrder;
                f.world.SetTerrain(6,2,TerrainKind.Basalt);f.session.RebuildOccupancy();
                f.WaitFor(()=>move.state==DeepWorkState.Blocked,2,"A fully closed route should wait without cancelling the command");
                Assert(!move.IsTerminal&&move.requestedWorker==f.worker,"Temporarily unreachable manual movement stays attached to its intended worker.");
                f.world.SetTerrain(6,1,TerrainKind.Empty);f.world.SetTerrain(6,2,TerrainKind.Empty);f.session.RebuildOccupancy();
                f.WaitFor(()=>move.state==DeepWorkState.Completed,12,"Opening the route should resume the same command");
                Assert(f.worker.Cell==new Vector2Int(1,1)&&f.session.Orders.Count==2,"Route recovery neither changes the destination nor creates replacement orders.");
                Assert(f.session.RequestMove(f.worker,new Vector2Int(5,1),out reason),reason);move=f.worker.currentOrder;
                f.world.SetTerrain(1,0,TerrainKind.Empty);f.session.RebuildOccupancy();f.session.Tick(.1f);
                Assert(!move.IsTerminal,"Losing the supporting floor does not erase the player's original movement command.");
                f.WaitFor(()=>move.state==DeepWorkState.Completed,8,"After falling one level the worker should step out and continue the same command");
            }
            using(var f=new Fixture())
            {
                var lamp=f.Definition("detour_lamp",DeepBuildingRole.Light,new Vector2Int(1,2));lamp.cost=new[]{new DeepItemAmount(f.ore,4)};
                Assert(f.session.RequestBuild(lamp,new Vector2Int(12,1),out string reason),reason);
                var build=f.session.Orders[0];
                f.WaitFor(()=>build.materialsCollected&&build.state==DeepWorkState.Moving,5,"Fixture must pick up the paid construction stock");
                f.world.SetTerrain(6,1,TerrainKind.Basalt);f.session.RebuildOccupancy();
                f.WaitFor(()=>System.Array.IndexOf(f.worker.CapturePath(),new Vector2Int(6,2))>=0,2,"A carrying worker should take the new detour");
                Assert(build.worker==f.worker&&build.materialsCollected&&!build.fetchingMaterials,"A viable detour does not discard carried materials or restart a warehouse pickup.");
                f.WaitFor(()=>build.state==DeepWorkState.Completed,12,"Detoured construction must finish");
                Assert(f.session.inventory.GetAmount(f.ore)==16&&f.session.inventory.UsedCapacity==16,"A rerouted build consumes exactly the original reservation once.");
            }
        }
        static void NearestCrewAndWorkPositions()
        {
            using(var f=new Fixture())
            {
                var go=new GameObject("Nearby builder");go.transform.SetParent(f.root.transform);var nearby=go.AddComponent<DeepWorker>();nearby.session=f.session;nearby.TeleportToCell(new Vector2Int(10,1));f.session.Workers.Add(nearby);
                f.session.excavationSeconds=20;
                Assert(f.session.RequestDig(new Vector2Int(12,0),out string reason),reason);var dig=f.session.Orders[0];f.session.Tick(.05f);
                Assert(dig.worker==nearby&&f.worker.currentOrder==null,"For equal specialties an idle nearby worker claims the job before an earlier-listed distant worker.");
                f.worker.TeleportToCell(new Vector2Int(12,1));f.session.Tick(.5f);
                Assert(dig.worker==nearby&&nearby.currentOrder==dig,"Another worker becoming closer does not steal an already assigned order.");
            }
            using(var f=new Fixture())
            {
                f.worker.TeleportToCell(new Vector2Int(5,1));f.world.SetTerrain(6,1,TerrainKind.Basalt);f.world.SetTerrain(6,2,TerrainKind.Basalt);
                var ladder=f.Definition("route_compare_ladder",DeepBuildingRole.Ladder,new Vector2Int(1,3));ladder.requiresFloor=false;
                f.Station(ladder,new Vector2Int(5,1));f.Station(ladder,new Vector2Int(7,1));
                var go=new GameObject("Clear-route worker");go.transform.SetParent(f.root.transform);var direct=go.AddComponent<DeepWorker>();direct.session=f.session;direct.TeleportToCell(new Vector2Int(12,1));f.session.Workers.Add(direct);
                Assert(f.session.RequestDig(new Vector2Int(7,0),out string reason),reason);var dig=f.session.Orders[0];f.session.Tick(.05f);
                Assert(dig.worker==direct,"A worker five columns away with a clear route beats a worker two columns away who must climb around a two-cell wall.");
            }
            using(var f=new Fixture())
            {
                f.worker.TeleportToCell(new Vector2Int(10,1));
                var wide=f.Definition("wide_machine",DeepBuildingRole.Light,new Vector2Int(4,2));
                var small=f.Definition("small_machine",DeepBuildingRole.Light,new Vector2Int(1,2));
                Assert(f.session.RequestBuild(wide,new Vector2Int(5,1),out string reason),reason);var nearSide=f.session.Orders[0];
                Assert(f.session.RequestBuild(small,new Vector2Int(14,1),out reason),reason);f.session.Tick(.05f);
                Assert(f.worker.currentOrder==nearSide&&nearSide.workCell==new Vector2Int(9,1),"Task distance is measured to the nearest reachable work side, not the building's far-away origin.");
            }
        }
        static void ConsistentRouteCosts()
        {
            using(var f=new Fixture())
            {
                var ladder=f.Definition("cost_ladder",DeepBuildingRole.Ladder,new Vector2Int(1,4));ladder.requiresFloor=false;f.Station(ladder,new Vector2Int(0,1));
                f.world.SetTerrain(2,0,TerrainKind.Empty);
                Assert(DeepNavigation.TryFindPath(f.session,new Vector2Int(0,1),new[]{new Vector2Int(3,1),new Vector2Int(0,4)},out var path),"Both route choices have a physical path.");
                Assert(path[path.Count-1]==new Vector2Int(0,4)&&DeepNavigation.TravelCost(path,new Vector2Int(0,1))==39,"Ladder and gap traversal costs remain consistent between route finding and task scoring.");
            }
            using(var f=new Fixture())
            {
                f.world.width=130;f.world.terrainKinds=new TerrainKind[f.world.width*f.world.height];
                for(int x=0;x<f.world.width;x++)f.world.SetTerrain(x,0,TerrainKind.Basalt);f.world.RebuildRooms();
                Assert(f.session.RequestDig(new Vector2Int(126,0),out string reason),reason);
                Assert(f.session.RequestDig(new Vector2Int(110,0),out reason),reason);var nearer=f.session.Orders[1];f.session.Tick(.05f);
                Assert(f.worker.currentOrder==nearer,"Different routes longer than 99 cells must not collapse into equal distance scores.");
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
            public void WaitFor(Func<bool> condition,float maximumSeconds,string context)
            {
                for(int i=0;i<Mathf.CeilToInt(maximumSeconds/.05f)&&!condition()&&worker.IsAlive;i++)session.Tick(.05f);
                var order=worker.currentOrder;
                Assert(condition(),context+". Cell="+worker.Cell+", feet="+worker.transform.position+", health="+worker.health+", air="+worker.airReserveSeconds+", activity="+worker.Status+", order="+(order==null?"none":order.kind+"/"+order.state+"/"+order.statusReason));
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); foreach (var item in temporary) if (item != null) UnityEngine.Object.DestroyImmediate(item); }
        }
        static void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException("Gameplay self-test: "+message); }
    }
}
