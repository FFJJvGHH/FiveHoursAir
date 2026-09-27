using System;
using UnityEngine;

namespace DeepPressure
{
    public static class DeepHazardTests
    {
        public static string RunAll()
        {
            SixSpeciesPipeTransport(); ConditionalReaction(); OperationalIgnition(); PressureBreach();
            return "Deep Pressure hazards: 4 groups passed. Six-species finite pipe conservation and external ledger; bounded conditional reaction conserves atoms; an actual wired energized machine ignites the authoritative cell gas and shutdown stops it; unannounced high-pressure breach kills an adjacent unprotected worker without deleting gas.";
        }
        static void SixSpeciesPipeTransport()
        {
            var root = new GameObject("Six-species gas test");
            try
            {
                var network = root.AddComponent<GasNetworkSimulator>();
                var aObject = new GameObject("Source"); aObject.transform.SetParent(root.transform);
                var bObject = new GameObject("Receiver"); bObject.transform.SetParent(root.transform);
                var a = aObject.AddComponent<GasNode>(); var b = bObject.AddComponent<GasNode>();
                a.initialPressureKPa = 200; a.initialComposition = new Vector4(.15f,.5f,.03f,.02f); a.reactiveFractions = new Vector2(.1f,.2f); b.initialPressureKPa = 0;
                var linkObject = new GameObject("Pipe"); linkObject.transform.SetParent(root.transform); var link = linkObject.AddComponent<GasLink>(); link.from = a; link.to = b;
                network.ResetSimulation(); var before = network.TotalInventory();
                for (int i = 0; i < 20; i++) network.Step(.1f);
                Assert(b.gas.methane > 0 && b.gas.processVapor > 0,"New gases travel through actual pipes.");
                Assert(network.VerifyConservation(out string reason),reason);
                for (int species = 0; species < GasMixture.SpeciesCount; species++) Assert(Math.Abs(before[species]-network.TotalInventory()[species]) < 1e-7,"Every species is preserved by network transport.");
                var external = new GasMixture { methane=2,processVapor=3 }; b.gas += external; network.RegisterExternalExchange(external);
                Assert(network.VerifyConservation(out reason),"Room exchange must be explicitly accounted: "+reason);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void ConditionalReaction()
        {
            var gas = new GasMixture { methane = 10,oxygen = 25,nitrogen = 60,processVapor = 5 };
            var original = gas;
            Assert(!DeepGasChemistry.TryIgnite(ref gas,.1,false,out _),"Gas mixing without an energized source does not explode.");
            Assert(gas.methane == original.methane && gas.oxygen == original.oxygen,"A non-igniting mixture is unchanged.");
            Assert(DeepGasChemistry.TryIgnite(ref gas,.1,true,out double burned) && burned > 0,"The same mixture reacts at an active hot machine.");
            Assert(Math.Abs(gas.Total-original.Total) < 1e-8 && gas.IsFiniteAndNonnegative,"Reaction inventories remain finite and nonnegative.");
            Assert(Math.Abs(gas.methane+gas.carbonDioxide-original.methane-original.carbonDioxide) < 1e-8,"Carbon atoms are conserved.");
            Assert(Math.Abs(4*gas.methane+2*gas.waterVapour-4*original.methane-2*original.waterVapour) < 1e-8,"Hydrogen atoms are conserved.");
            Assert(Math.Abs(2*gas.oxygen+2*gas.carbonDioxide+gas.waterVapour-2*original.oxygen) < 1e-8,"Oxygen atoms are conserved.");
            Assert(gas.processVapor == original.processVapor,"Industrial process vapor is not arbitrarily treated as explosive fuel.");
            var lean = new GasMixture { methane = 1,oxygen = 21,nitrogen = 78 };
            Assert(!DeepGasChemistry.TryIgnite(ref lean,.1,true,out _),"An insufficient fuel fraction does not ignite.");
            var inert = new GasMixture { methane = 10,oxygen = 1,nitrogen = 89 };
            Assert(!DeepGasChemistry.TryIgnite(ref inert,.1,true,out _),"A fuel cloud without oxygen does not ignite.");
        }
        static void PressureBreach()
        {
            var root = new GameObject("Pressure breach test"); root.SetActive(false);
            try
            {
                var world = root.AddComponent<DeepPressureWorld>(); world.width = 9; world.height = 5; world.terrainKinds = new TerrainKind[45];
                for (int x=0;x<9;x++) world.SetTerrain(x,0,TerrainKind.Basalt);
                for (int y=0;y<5;y++) world.SetTerrain(4,y,TerrainKind.Basalt);
                Region(root,new RectInt(0,0,4,5),220,"high"); Region(root,new RectInt(5,0,4,5),60,"low"); world.RebuildRooms();
                var session = root.AddComponent<DeepGameSession>(); session.world = world; session.lifeSupportEnabled = true; session.InitializeSession();
                var workerObject = new GameObject("At breach"); workerObject.transform.SetParent(root.transform); var worker = workerObject.AddComponent<DeepWorker>(); worker.session=session; worker.TeleportToCell(new Vector2Int(5,1)); session.Workers.Add(worker);
                var cell = new Vector2Int(4,1); Assert(string.IsNullOrEmpty(session.ExcavationRisk(cell)),"Hidden high pressure gives no pre-excavation warning.");
                GasMixture before = default; foreach(var room in world.Rooms) before += room.gas;
                session.PrepareExcavationHazard(cell); world.SetTerrain(cell.x,cell.y,TerrainKind.Empty); world.RebuildRoomsPreservingGas(); session.ResolveExcavationHazard(cell);
                Assert(session.HazardEventCount == 1 && !worker.IsAlive && worker.deathCause=="气压冲击","An unprotected worker beside a 160 kPa breach dies from the pressure shock.");
                GasMixture after = default; foreach(var room in world.Rooms) after += room.gas;
                for(int i=0;i<GasMixture.SpeciesCount;i++) Assert(Math.Abs(before[i]-after[i])<1e-7,"Pressure shock never deletes gas.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void OperationalIgnition()
        {
            var root = new GameObject("Operational ignition test"); root.SetActive(false);
            var generatorDef = ScriptableObject.CreateInstance<DeepBuildingDefinition>();
            var lampDef = ScriptableObject.CreateInstance<DeepBuildingDefinition>();
            try
            {
                var world = root.AddComponent<DeepPressureWorld>(); world.width=8;world.height=5;world.terrainKinds=new TerrainKind[40];
                for(int x=0;x<8;x++)world.SetTerrain(x,0,TerrainKind.Basalt);
                var regionObject=new GameObject("Fuel-air chamber");regionObject.transform.SetParent(root.transform);var region=regionObject.AddComponent<DeepPressureRegion>();
                region.bounds=new RectInt(0,0,8,5);region.initialPressureKPa=100;region.composition=new Vector4(.25f,.65f,0,0);region.reactiveFractions=new Vector2(.1f,0);world.RebuildRooms();
                var field=root.AddComponent<DeepAtmosphereField>();field.Initialize(world);
                var session=root.AddComponent<DeepGameSession>();session.world=world;session.lifeSupportEnabled=true;session.useWiredPower=true;
                generatorDef.id="ignition_test_generator";generatorDef.role=DeepBuildingRole.Generator;generatorDef.powerGenerated=20;generatorDef.blocksMovement=false;
                lampDef.id="ignition_test_load";lampDef.role=DeepBuildingRole.Light;lampDef.powerRequired=5;lampDef.blocksMovement=false;
                var generatorObject=new GameObject("Generator");generatorObject.transform.SetParent(root.transform);var generator=generatorObject.AddComponent<DeepBuildingInstance>();generator.definition=generatorDef;generator.origin=new Vector2Int(2,1);
                var lampObject=new GameObject("Load");lampObject.transform.SetParent(root.transform);var lamp=lampObject.AddComponent<DeepBuildingInstance>();lamp.definition=lampDef;lamp.origin=new Vector2Int(4,1);
                for(int x=2;x<=4;x++)session.completedWireCells.Add(new Vector2Int(x,1));session.InitializeSession();
                double methane=world.Rooms[0].gas.methane;session.Tick(.1f);
                Assert(session.HazardEventCount>0&&world.Rooms[0].gas.methane<methane,"A live wired ignition source consumes the local atmospheric methane.");
                session.ToggleBuilding(generator);methane=world.Rooms[0].gas.methane;session.Tick(.1f);
                Assert(Math.Abs(world.Rooms[0].gas.methane-methane)<1e-6,"De-energizing every hot source prevents further combustion while gas still diffuses.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(generatorDef);UnityEngine.Object.DestroyImmediate(lampDef);}
        }
        static void Region(GameObject root,RectInt bounds,float pressure,string id)
        {
            var go = new GameObject(id); go.transform.SetParent(root.transform); var region = go.AddComponent<DeepPressureRegion>(); region.bounds=bounds; region.initialPressureKPa=pressure; region.stableId=id;
        }
        static void Assert(bool condition,string message) { if(!condition)throw new InvalidOperationException("Hazard test: "+message); }
    }
}
