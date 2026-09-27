using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace DeepPressure
{
    /// <summary>Called explicitly by the editor validation runner; never writes into player save slots.</summary>
    public static class DeepPersistenceTests
    {
        public static string RunAll()
        {
            PendingWorkRoundTrip(); CompletedWorldAndNewGame(); DiscoveryRoundTrip(); CorruptionAndAtomicRecovery(); RejectInvalidBeforeMutation();
            UpgradedSystemsRoundTrip(); PendingInfrastructureWorkRoundTrip(); LegacyAndInvalidSystems(); ScriptReloadRecovery();
            return "Deep Pressure persistence: 9 groups passed. In-flight jobs/material reservations; buildings/terrain/pipes/new game; fog/samples; checksums/backup recovery; preflight rejection; six-species cell gases/heat/wires/batteries/prepaid fuel/production/hazard clocks; pending wire/sample/survey work; legacy migration and invalid upgraded saves; editor-reload cache and initial-baseline recovery.";
        }
        /// <summary>Run explicitly in the authored Play session, after starting its colony.</summary>
        public static string RunActiveSceneRoundTrip()
        {
            Assert(Application.isPlaying,"Actual-scene round-trip requires Play mode.");
            var session=UnityEngine.Object.FindObjectOfType<DeepGameSession>();
            Assert(session!=null&&session.HasPlayableSession,"Start the authored colony before its end-to-end round-trip.");
            session.InitializeSession();var before=Clone(session.CaptureSaveState());bool restored=false;
            try
            {
                Assert(session.ValidateSaveState(before,out string reason),reason);
                Assert(session.NewGame(out reason),"Actual scene NewGame: "+reason);
                Assert(session.world.Rooms.Count>0&&session.Orders.Count==0,"Authored NewGame must rebuild a playable initial map.");
                var fresh=session.CaptureSaveState();Assert(session.ValidateSaveState(fresh,out reason),reason);
                Assert(session.TryRestoreSaveState(before,out reason),reason);restored=true;
                var after=session.CaptureSaveState();
                Assert(after.buildings.Length==before.buildings.Length&&after.workers.Length==before.workers.Length&&after.orders.Length==before.orders.Length,"Actual-scene object and work counts must survive NewGame/restore.");
                Assert(Mathf.Abs(after.simulationTime-before.simulationTime)<.0001f,"Actual-scene clock must return to its saved value.");
                for(int i=0;i<before.terrain.Length;i++)Assert(after.terrain[i]==before.terrain[i],"Actual-scene terrain must round-trip.");
                if(before.lifeSupport)for(int i=0;i<before.atmosphereCells.Length;i++)
                {
                    AssertGas(after.atmosphereCells[i],before.atmosphereCells[i],"Actual-scene per-cell inventories must round-trip.");
                    Assert(Math.Abs(after.atmosphereTemperatures[i]-before.atmosphereTemperatures[i])<1e-7,"Actual-scene cell temperatures must round-trip.");
                }
                return "Authored Play scene: NewGame + validated full-state restore passed ("+after.buildings.Length+" buildings, "+after.workers.Length+" workers, "+after.terrain.Length+" terrain cells).";
            }
            finally{if(!restored)session.TryRestoreSaveState(before,out _);}
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
        static void UpgradedSystemsRoundTrip()
        {
            using (var f = new Fixture())
            {
                f.session.useWiredPower=true;f.session.lifeSupportEnabled=true;f.session.hazardsEnabled=false;
                var generator=f.Build("save-generator",DeepBuildingRole.Generator,new Vector2Int(2,1));
                generator.definition.powerGenerated=30;generator.definition.fuelItem=f.fuel;generator.definition.fuelUnitsPerSecond=1;
                var battery=f.Build("save-battery",DeepBuildingRole.Battery,new Vector2Int(5,1));battery.definition.batteryCapacity=100;battery.definition.batteryTransferRate=20;
                var consumer=f.Build("save-consumer",DeepBuildingRole.Light,new Vector2Int(6,1));consumer.definition.powerRequired=4;
                for(int x=2;x<=6;x++)f.session.completedWireCells.Add(new Vector2Int(x,1));f.session.InvalidatePowerTopology();
                Assert(f.session.inventory.TryAdd(f.fuel,5),"Fixture receives finite generator fuel.");
                f.session.Tick(.25f);
                Assert(generator.fuelSecondsRemaining>0&&battery.batteryEnergy>0,"Fixture must have partially burnt prepaid fuel and stored battery energy.");
                var field=f.session.Atmosphere;field.diffusionPerSecond=.65f;field.thermalConductionPerSecond=.12f;
                var changedCell=new Vector2Int(3,2);field.Take(changedCell,double.MaxValue);
                field.Add(changedCell,new GasMixture{oxygen=7,nitrogen=11,carbonDioxide=2,waterVapour=3,methane=4,processVapor=5},73);
                field.SetTemperature(new Vector2Int(4,2),6);
                var recipe=f.Recipe("save-production");f.session.SetProductionTarget(recipe,23,true);
                f.worker.airReserveSeconds=17.25f;f.worker.environmentUnsafe=true;f.worker.environmentEfficiency=.5f;
                f.worker.nextWorkSearchTime=f.session.SimulationTime+.3f;
                var saved=Clone(f.session.CaptureSaveState());
                saved.stableAirSeconds=.2f;saved.nextProductionCheck=saved.simulationTime+.75f;
                saved.hazardEventCount=1;saved.lastHazardMessage="saved ignition";
                saved.hazards=new[]{new DeepSavedHazard{kind=DeepHazardKind.Combustion,cell=changedCell,direction=Vector2.up,strength=1,time=saved.simulationTime,message="saved ignition"}};
                saved.ignitionCooldowns=new[]{new DeepSavedIgnition{cell=changedCell,time=saved.simulationTime}};
                float energy=battery.batteryEnergy,burn=generator.fuelSecondsRemaining;int fuel=f.session.inventory.GetAmount(f.fuel);
                field.Take(changedCell,double.MaxValue);field.SetTemperature(changedCell,-10);field.SyncRooms();
                battery.batteryEnergy=0;generator.fuelSecondsRemaining=0;f.session.completedWireCells.Clear();
                f.session.productionTargets.Clear();f.worker.nextWorkSearchTime=1000;f.worker.airReserveSeconds=90;
                Assert(f.session.TryRestoreSaveState(saved,out string reason),reason);
                AssertGas(field.Sample(changedCell),saved.atmosphereCells[changedCell.y*f.world.width+changedCell.x],"Cell gas must restore all six species exactly.");
                Assert(Math.Abs(field.TemperatureC(changedCell)-73)<1e-8&&Math.Abs(field.TemperatureC(new Vector2Int(4,2))-6)<1e-8,"Different cell temperatures remain different after load.");
                Assert(Mathf.Abs(field.diffusionPerSecond-.65f)<1e-6&&Mathf.Abs(field.thermalConductionPerSecond-.12f)<1e-6,"Atmosphere pacing settings restore.");
                Assert(battery.batteryEnergy==energy&&generator.fuelSecondsRemaining==burn&&f.session.inventory.GetAmount(f.fuel)==fuel,"Zero-time load neither burns fuel nor charges a battery.");
                Assert(f.session.HasWire(new Vector2Int(4,1))&&consumer.IsOperational,"Completed wire topology immediately restores local power.");
                Assert(f.session.productionTargets.Count==1&&f.session.productionTargets[0].targetAmount==23&&f.session.productionTargets[0].enabled,"Automatic production rule restores.");
                Assert(f.worker.airReserveSeconds==17.25f&&f.worker.environmentUnsafe&&f.worker.environmentEfficiency==.5f&&Mathf.Abs(f.worker.nextWorkSearchTime-saved.workers[0].nextWorkSearchTime)<1e-6,"Worker air state and job-search timer rewind together.");
                var recaptured=f.session.CaptureSaveState();
                Assert(recaptured.nextProductionCheck==saved.nextProductionCheck&&recaptured.stableAirSeconds==saved.stableAirSeconds&&recaptured.hazardEventCount==1&&recaptured.ignitionCooldowns.Length==1,"Production cadence, objective progress and ignition cooldowns round-trip.");
                Assert(recaptured.hazards[0].time==saved.hazards[0].time&&recaptured.lastHazardMessage==saved.lastHazardMessage&&!recaptured.hazardsEnabled,"Hazard history/settings rewind with simulation time.");
                AssertGas(field.TotalInventory(),SumRooms(f.world),"Room summaries must equal authoritative cells after load.");
                f.session.Tick(.1f);
                Assert(f.session.inventory.GetAmount(f.fuel)==fuel&&Mathf.Abs(generator.fuelSecondsRemaining-(burn-.1f))<1e-5,"Resuming uses already-paid fuel without purchasing it again.");
                Assert(f.session.productionTargets[0].status==null,"A restored future production check must not run early.");
            }
        }
        static void PendingInfrastructureWorkRoundTrip()
        {
            using(var f=new Fixture(true))
            {
                Assert(f.session.inventory.TryAdd(f.alloy,5),"Fixture receives wire material.");
                f.session.samplingSeconds=2;f.session.defaultOrderPriority=1;
                Assert(f.session.RequestWire(new Vector2Int(6,1),out string reason),reason);
                f.session.defaultOrderPriority=9;
                Assert(f.session.RequestSample(new Vector2Int(11,2),out reason),reason);
                f.session.Tick(.1f);f.session.Tick(.2f);
                Assert(f.worker.currentOrder!=null&&f.worker.currentOrder.kind==DeepWorkKind.Sample&&f.worker.currentOrder.completedSeconds>0,"Fixture saves an on-site sample in progress alongside a reserved wire.");
                var saved=Clone(f.session.CaptureSaveState());float progress=f.worker.currentOrder.completedSeconds;
                foreach(var order in f.session.Orders)if(!order.IsTerminal)f.session.CancelOrder(order);
                f.worker.nextWorkSearchTime=1000;
                Assert(f.session.TryRestoreSaveState(saved,out reason),reason);
                Assert(f.worker.currentOrder.kind==DeepWorkKind.Sample&&f.worker.currentOrder.completedSeconds==progress,"Sample progress and assigned worker restore.");
                Assert(f.session.inventory.GetAmount(f.alloy)==4&&!f.session.HasWire(new Vector2Int(6,1)),"A pending wire remains paid but nonconducting.");
                f.Run(12);
                Assert(f.session.HasWire(new Vector2Int(6,1))&&f.session.inventory.GetAmount(f.alloy)==4,"Loaded wire completes and consumes exactly its existing reservation.");
                Assert(f.session.inventory.GetAmount(f.researchData)==4,"Loaded first sampling grants research data once.");
                Assert(f.session.TryRestoreSaveState(saved,out reason),reason);f.Run(12);
                Assert(f.session.inventory.GetAmount(f.researchData)==4,"Rewinding a completed sample restores its inventory and cannot duplicate its reward.");
                var exploration=f.world.GetComponent<DeepExploration>();
                exploration.RevealLocalSight(new Vector2Int(10,2),4);
                Assert(f.session.RequestSurvey(new Vector2Int(12,2),out reason),reason);f.session.Tick(.1f);
                Assert(f.worker.currentOrder!=null&&f.worker.currentOrder.kind==DeepWorkKind.Survey,"Survey is an actual travelling worker order.");
                var survey=Clone(f.session.CaptureSaveState());int surveyId=f.worker.currentOrder.id;
                f.session.CancelOrder(f.worker.currentOrder);Assert(f.session.TryRestoreSaveState(survey,out reason),reason);f.Run(10);
                Assert(f.session.Orders.Find(o=>o.id==surveyId).state==DeepWorkState.Completed,"A saved survey route resumes and completes.");
            }
        }
        static void LegacyAndInvalidSystems()
        {
            using(var f=new Fixture())
            {
                var legacy=Clone(f.session.CaptureSaveState());legacy.systemsRevision=0;
                legacy.wires=null;legacy.production=null;legacy.atmosphereCells=null;legacy.atmosphereTemperatures=null;
                legacy.hazards=null;legacy.ignitionCooldowns=null;
                f.session.lifeSupportEnabled=true;f.session.useWiredPower=true;
                var field=f.session.Atmosphere;var cell=new Vector2Int(3,2);field.Add(cell,new GasMixture{methane=99});field.SyncRooms();
                f.session.completedWireCells.Add(new Vector2Int(3,1));f.session.SetProductionTarget(f.Recipe("legacy-production"),20,true);
                f.worker.nextWorkSearchTime=1000;
                Assert(f.session.TryRestoreSaveState(legacy,out string reason),reason);
                Assert(!f.session.lifeSupportEnabled&&!f.session.useWiredPower&&f.session.completedWireCells.Count==0&&f.session.productionTargets.Count==0&&f.worker.nextWorkSearchTime==0,"Legacy saves reset newer systems and stale future job-search clocks.");
                AssertGas(field.TotalInventory(),SumRooms(f.world),"Existing field is reset from legacy room gas, even with life support disabled.");
                Assert(field.TotalInventory().methane==0,"A legacy load must not retain methane from the previous colony.");
                f.session.lifeSupportEnabled=true;var good=Clone(f.session.CaptureSaveState());
                var invalid=Clone(good);invalid.atmosphereCells[0]=new GasMixture{oxygen=1};
                Assert(!f.session.TryRestoreSaveState(invalid,out _),"Gas embedded in a solid cell is rejected before reset.");
                invalid=Clone(good);invalid.atmosphereTemperatures[0]=-273;
                Assert(!f.session.TryRestoreSaveState(invalid,out _),"Temperature rejected by the field is rejected during preflight too.");
                invalid=Clone(good);invalid.wires=new[]{new Vector2Int(3,1),new Vector2Int(3,1)};
                Assert(!f.session.TryRestoreSaveState(invalid,out _),"Duplicate wire cells are rejected.");
                invalid=Clone(good);invalid.systemsRevision=99;
                Assert(!f.session.TryRestoreSaveState(invalid,out _),"Future system revisions are not silently interpreted as current saves.");
                invalid=Clone(good);invalid.workers[0].nextWorkSearchTime=float.NaN;
                Assert(!f.session.TryRestoreSaveState(invalid,out _),"Invalid job-search clocks are rejected.");
                invalid=Clone(good);invalid.rooms[0].gas.oxygen+=1;
                Assert(!f.session.TryRestoreSaveState(invalid,out _),"New saves cannot disagree about cell and room gas inventories.");
                AssertGas(field.TotalInventory(),SumRooms(f.world),"Rejected upgraded saves leave both atmosphere views unchanged.");
            }
        }
        static void ScriptReloadRecovery()
        {
            using(var f=new Fixture())
            {
                f.session.CaptureInitialState();Assert(f.session.NewGame(out string reason),reason);
                Assert(f.session.RequestBuild(f.lamp,new Vector2Int(8,1),out reason),reason);f.session.Tick(.1f);
                f.a.gas=new GasMixture{oxygen=17,methane=3};f.network.RestoreSavedClock(.5,5,0);
                var before=Clone(f.session.CaptureSaveState());f.session.PreserveSessionForReload();
                // Unity preserves serialized fields and transforms but reconstructs these runtime caches.
                var roomList=(System.Collections.IList)typeof(DeepPressureWorld).GetField("rooms",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.world);roomList.Clear();
                SetPrivate(f.world,"roomMap",null);SetPrivate(f.session,"initialized",false);SetPrivate(f.session,"initialSaveState",null);
                SetPrivate(f.session,"<SimulationTime>k__BackingField",0f);SetPrivate(f.session,"nextOrderId",1);
                SetPrivate(f.session,"<HasPlayableSession>k__BackingField",false);
                f.session.inventory=new DeepInventory();f.session.Orders.Clear();f.worker.currentOrder=null;f.worker.SetPath(null);f.a.gas=f.b.gas=default;
                f.session.InitializeSession();
                var restored=f.session.CaptureSaveState();Assert(f.session.ValidateSaveState(restored,out reason),reason);
                Assert(f.world.Rooms.Count>0&&f.session.HasPlayableSession,"Reload recovers topology and the active session flag.");
                Assert(f.worker.currentOrder!=null&&f.worker.currentOrder.fetchingMaterials&&f.worker.CapturePath().Length==before.workers[0].path.Length,"Reload restores a live material pickup and route.");
                Assert(f.session.inventory.GetAmount(f.ore)==26&&f.session.inventory.UsedCapacity==30,"Reload retains reservations without reapplying starting inventory.");
                AssertGas(f.a.gas,before.nodes[0].gas,"Reload restores nonserialized gas tanks.");
                Assert(f.session.NewGame(out reason),reason);
                Assert(f.session.Orders.Count==0&&f.session.SimulationTime==0&&f.session.inventory.GetAmount(f.ore)==30,"NewGame uses the preserved initial baseline, not the in-progress reload snapshot.");
                // Even without a session restart, capture repairs a missing world topology cache.
                roomList.Clear();SetPrivate(f.world,"roomMap",null);
                Assert(f.session.ValidateSaveState(f.session.CaptureSaveState(),out reason),"Capture after world cache loss: "+reason);
            }
        }
        static void SetPrivate(object target,string name,object value)
        {var field=target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);Assert(field!=null,"Reload fixture field exists: "+name);field.SetValue(target,value);}
        static GasMixture SumRooms(DeepPressureWorld world) { GasMixture total=default;foreach(var room in world.Rooms)total+=room.gas;return total; }
        static void AssertGas(GasMixture actual,GasMixture expected,string message)
        {for(int species=0;species<GasMixture.SpeciesCount;species++)Assert(Math.Abs(actual[species]-expected[species])<Math.Max(1e-7,Math.Abs(expected[species])*1e-9),message);}
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
            public readonly DeepItemDefinition ore,alloy,fuel,researchData;
            public readonly DeepBuildingDefinition lamp;
            readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
            public Fixture(bool discovery = false)
            {
                root = new GameObject("DeepPersistenceTest"); root.SetActive(false);
                world = root.AddComponent<DeepPressureWorld>(); world.width = 16; world.height = 8; world.terrainKinds = new TerrainKind[128];
                for (int x = 0; x < world.width; x++) world.SetTerrain(x,0,TerrainKind.Basalt);
                if (discovery)
                {
                    // Survey permission still requires a genuinely breathable atmosphere.
                    world.defaultPressureKPa=100;world.defaultTemperatureC=22;world.defaultComposition=new Vector4(.21f,.79f,0,0);
                    var regionObject = new GameObject("TestRegion"); regionObject.transform.SetParent(root.transform);
                    var region = regionObject.AddComponent<DeepPressureRegion>(); region.stableId = "test-region"; region.bounds = new RectInt(10,1,3,3);region.initialPressureKPa=100;region.composition=new Vector4(.21f,.79f,0,0);
                    var exploration = root.AddComponent<DeepExploration>(); exploration.world = world; exploration.initialExploredAreas = new[] { new RectInt(0,0,8,8) };
                }
                world.RebuildRooms(); network = root.AddComponent<GasNetworkSimulator>();
                ore = Asset<DeepItemDefinition>(); ore.id = "ore";
                alloy=Asset<DeepItemDefinition>();alloy.id="alloy";fuel=Asset<DeepItemDefinition>();fuel.id="fuel";researchData=Asset<DeepItemDefinition>();researchData.id="research_data";
                var catalog = Asset<DeepGameplayCatalog>(); catalog.items = new[] { ore,alloy,fuel,researchData };
                var storage = Definition("storage",DeepBuildingRole.Storage); lamp = Definition("lamp",DeepBuildingRole.Light); lamp.cost = new[] { new DeepItemAmount(ore,4) };
                catalog.buildings = new[] { storage,lamp };
                var warehouse = UnityEngine.Object.Instantiate(storage.prefab,root.transform); warehouse.name = "InitialWarehouse";
                var instance = warehouse.GetComponent<DeepBuildingInstance>(); instance.definition = storage; instance.origin = new Vector2Int(4,1); warehouse.transform.position = new Vector3(4,1,0); warehouse.SetActive(true);
                var person = new GameObject("Worker"); person.transform.SetParent(root.transform); person.transform.position = new Vector3(1.5f,1,0); worker = person.AddComponent<DeepWorker>();
                a = Node("A",new Vector3(2,2,0)); b = Node("B",new Vector3(12,2,0)); network.ResetSimulation();
                session = root.AddComponent<DeepGameSession>(); session.world = world; session.network = network; session.catalog = catalog; session.baseStorageCapacity = 100;session.useWiredPower=false;
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
            public DeepBuildingInstance Build(string id,DeepBuildingRole role,Vector2Int origin)
            {
                var definition=Definition(id,role);var definitions=new List<DeepBuildingDefinition>(session.catalog.buildings){definition};session.catalog.buildings=definitions.ToArray();
                var go=UnityEngine.Object.Instantiate(definition.prefab,root.transform);go.SetActive(true);
                var building=go.GetComponent<DeepBuildingInstance>();building.definition=definition;building.origin=origin;building.session=session;go.transform.position=session.BuildingPosition(origin);
                session.Buildings.Add(building);session.RebuildOccupancy();return building;
            }
            public DeepRecipeDefinition Recipe(string id)
            {
                var recipe=Asset<DeepRecipeDefinition>();recipe.id=recipe.displayName=id;recipe.inputs=new[]{new DeepItemAmount(ore,1)};recipe.outputs=new[]{new DeepItemAmount(alloy,1)};
                var recipes=new List<DeepRecipeDefinition>(session.catalog.recipes){recipe};session.catalog.recipes=recipes.ToArray();return recipe;
            }
            public void Run(float seconds) { for (int i = 0; i < Mathf.CeilToInt(seconds/.1f); i++) session.Tick(.1f); }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); foreach (var obj in temporary) if (obj != null) UnityEngine.Object.DestroyImmediate(obj); }
        }
        static void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException("Persistence self-test: "+message); }
    }
}
