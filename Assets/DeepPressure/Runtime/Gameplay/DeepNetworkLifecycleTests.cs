using System;
using System.Reflection;
using UnityEngine;

namespace DeepPressure
{
    public static class DeepNetworkLifecycleTests
    {
        public static string RunAll()
        {
            RestoredBeforeStart(); InterleavedRoomAndPipeFrames();
            return "Gas lifecycle: 2 groups passed. Deferred Start preserves an explicitly restored tank/clock; 300 interleaved session/pipe frames preserve every species across real room intake and supply, including disabled/re-enabled devices.";
        }
        static void RestoredBeforeStart()
        {
            var root = new GameObject("Deferred network start test"); root.SetActive(false);
            try
            {
                var nodeObject = new GameObject("Restored tank"); nodeObject.transform.SetParent(root.transform); var node = nodeObject.AddComponent<GasNode>();
                node.initialPressureKPa = 200;
                var network = root.AddComponent<GasNetworkSimulator>(); network.EnsureInitialized();
                node.gas = new GasMixture { oxygen = 17,methane = 3,processVapor = 2 };
                network.RestoreSavedClock(3,30,.05);
                typeof(GasNetworkSimulator).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(network,null);
                Assert(node.gas.oxygen == 17 && node.gas.methane == 3 && node.gas.processVapor == 2,"The first native Start must not refill restored nodes.");
                Assert(network.ElapsedSeconds == 3 && network.StepCount == 30 && Math.Abs(network.SaveRemainder-.05) < 1e-10,"The first native Start preserves the restored clock.");
                Assert(network.VerifyConservation(out string reason),reason);
                network.EnsureInitialized(); Assert(node.gas.oxygen == 17,"Repeated initialization is idempotent.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void InterleavedRoomAndPipeFrames()
        {
            var root = new GameObject("Live frame gas ledger test");
            var intakeDef = ScriptableObject.CreateInstance<DeepBuildingDefinition>();
            var supplyDef = ScriptableObject.CreateInstance<DeepBuildingDefinition>();
            try
            {
                var world = root.AddComponent<DeepPressureWorld>(); world.width = 10; world.height = 6; world.terrainKinds = new TerrainKind[60];
                world.defaultPressureKPa = 100; world.defaultComposition = new Vector4(.15f,.8f,.05f,0);
                for (int x = 0; x < world.width; x++) world.SetTerrain(x,0,TerrainKind.Basalt); world.RebuildRooms();
                intakeDef.id = "ledger_intake"; intakeDef.role = DeepBuildingRole.GasPump; intakeDef.blocksMovement = false; intakeDef.exchangesRoomGas = true; intakeDef.gasMode = DeepGasFacilityMode.Collect; intakeDef.gasTransferMolPerSecond = 4;
                supplyDef.id = "ledger_supply"; supplyDef.role = DeepBuildingRole.Vent; supplyDef.blocksMovement = false; supplyDef.exchangesRoomGas = true; supplyDef.gasMode = DeepGasFacilityMode.Supply; supplyDef.gasTransferMolPerSecond = 3;
                var intake = Facility(root,intakeDef,new Vector2Int(2,1),0,new Vector4(.21f,.79f,0,0));
                var supply = Facility(root,supplyDef,new Vector2Int(7,1),200,new Vector4(1,0,0,0));
                var storageObject = new GameObject("Downstream buffer"); storageObject.transform.SetParent(root.transform); var storage = storageObject.AddComponent<GasNode>(); storage.initialPressureKPa = 0;
                var linkObject = new GameObject("Intake-to-buffer pipe"); linkObject.transform.SetParent(root.transform); var link = linkObject.AddComponent<GasLink>(); link.from = intake; link.to = storage;
                var network = root.AddComponent<GasNetworkSimulator>(); network.EnsureInitialized();
                var session = root.AddComponent<DeepGameSession>(); session.world = world; session.network = network; session.lifeSupportEnabled = true; session.InitializeSession();
                GasMixture initial = network.TotalInventory()+session.Atmosphere.TotalInventory();
                float accumulated = 0;
                for (int frame = 0; frame < 300; frame++)
                {
                    if (frame == 80 || frame == 150) session.ToggleBuilding(intake.GetComponent<DeepBuildingInstance>());
                    float dt = frame%3 == 0 ? .016f : frame%3 == 1 ? .033f : .025f;
                    session.Tick(dt); accumulated += dt;
                    while (accumulated >= .1f) { network.Step(.1f); accumulated -= .1f; }
                    Assert(network.VerifyConservation(out string reason),"Frame "+frame+": "+reason);
                    var actual = network.TotalInventory()+session.Atmosphere.TotalInventory();
                    for (int s = 0; s < GasMixture.SpeciesCount; s++) Assert(Math.Abs(actual[s]-initial[s]) < 1e-6,"Room/pipe exchange loses no species at frame "+frame+" species "+s);
                }
                Assert(storage.gas.Total > 0 && supply.gas.oxygen < GasMixture.FromPressure(200,supply.volumeM3,supply.temperatureC,new Vector4(1,0,0,0)).oxygen,"The smoke test actually transferred gas through both room and pipe paths.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(intakeDef); UnityEngine.Object.DestroyImmediate(supplyDef); }
        }
        static GasNode Facility(GameObject root,DeepBuildingDefinition definition,Vector2Int cell,float pressure,Vector4 composition)
        {
            var go = new GameObject(definition.id); go.transform.SetParent(root.transform); var node = go.AddComponent<GasNode>(); node.initialPressureKPa = pressure; node.initialComposition = composition;
            var building = go.AddComponent<DeepBuildingInstance>(); building.definition = definition; building.origin = cell; return node;
        }
        static void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException("Gas lifecycle test: "+message); }
    }
}
