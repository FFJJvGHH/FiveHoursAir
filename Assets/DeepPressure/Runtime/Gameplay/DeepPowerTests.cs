using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public static class DeepPowerTests
    {
        public static string RunAll()
        {
            CircuitIsolationAndBreaks(); FiniteFuel(); BatteryAccounting(); WireConstruction(); MineralEconomy();
            return "Deep Pressure power: 5 groups passed. Completed-wire circuit isolation/disconnection; prepaid finite fuel; battery charge/discharge and isolated storage; paid worker construction/refunds; shale fuel and salvage yields.";
        }
        static void CircuitIsolationAndBreaks()
        {
            using (var f = new Fixture())
            {
                var generator = f.Build("generator",DeepBuildingRole.Generator,new Vector2Int(1,1)); generator.definition.powerGenerated = 20; generator.definition.fuelItem = f.fuel; generator.definition.fuelUnitsPerSecond = 1;
                var desk = f.Build("desk",DeepBuildingRole.Research,new Vector2Int(4,1)); desk.definition.powerRequired = 5;
                var remote = f.Build("remote",DeepBuildingRole.Research,new Vector2Int(9,1)); remote.definition.powerRequired = 5;
                f.Connect(1,4); f.session.completedWireCells.Add(new Vector2Int(9,1)); f.session.InvalidatePowerTopology();
                int fuel = f.session.inventory.GetAmount(f.fuel); f.session.ToggleBuilding(desk); f.session.ToggleBuilding(desk);
                Assert(f.session.inventory.GetAmount(f.fuel) == fuel,"Zero-time topology refresh cannot consume fuel.");
                f.session.Tick(.1f);
                Assert(desk.IsOperational && !remote.IsOperational,"Only physically connected devices receive generator power.");
                Assert(f.session.PowerCircuitCount == 2,"A separate isolated terminal is a separate circuit.");
                float burnTime = generator.fuelSecondsRemaining;
                Assert(f.session.RemoveWire(new Vector2Int(3,1),out _),"A completed wire can be cut."); f.session.Tick(.1f);
                Assert(!desk.IsOperational && generator.fuelSecondsRemaining == burnTime,"Disconnecting the only load stops fuel burn immediately.");
                f.session.completedWireCells.Add(new Vector2Int(3,1)); f.session.InvalidatePowerTopology(); f.session.Tick(.1f);
                Assert(desk.IsOperational && !remote.IsOperational,"Repairing a branch powers only that branch.");
            }
        }
        static void FiniteFuel()
        {
            using (var f = new Fixture())
            {
                var generator = f.Build("generator",DeepBuildingRole.Generator,new Vector2Int(1,1)); generator.definition.powerGenerated = 20; generator.definition.fuelItem = f.fuel; generator.definition.fuelUnitsPerSecond = 1;
                var desk = f.Build("desk",DeepBuildingRole.Research,new Vector2Int(4,1)); desk.definition.powerRequired = 5; f.Connect(1,4);
                f.session.Tick(.1f); Assert(f.session.inventory.GetAmount(f.fuel) == 1,"Fuel is paid before producing electricity.");
                for (int i = 0; i < 24; i++) f.session.Tick(.1f);
                Assert(!generator.powered && !desk.IsOperational && f.session.inventory.GetAmount(f.fuel) == 0,"Exhausted reserves shut down the generator and its consumers.");
                f.session.inventory.TryAdd(f.fuel,1); f.session.Tick(.1f);
                Assert(desk.IsOperational,"Acquiring finite new fuel restarts the wired machine.");
            }
        }
        static void BatteryAccounting()
        {
            using (var f = new Fixture())
            {
                var battery = f.Build("battery",DeepBuildingRole.Battery,new Vector2Int(1,1)); battery.definition.batteryCapacity = 100; battery.definition.batteryTransferRate = 20; battery.batteryEnergy = 10;
                var desk = f.Build("desk",DeepBuildingRole.Research,new Vector2Int(4,1)); desk.definition.powerRequired = 4; f.Connect(1,4);
                f.session.Tick(.5f); f.session.Tick(.5f);
                Assert(desk.IsOperational && Mathf.Abs(battery.batteryEnergy-6) < .001f,"A battery supplies exact energy = power × elapsed time.");
                f.session.RemoveWire(new Vector2Int(3,1),out _); f.session.Tick(1);
                Assert(Mathf.Abs(battery.batteryEnergy-6) < .001f && !desk.IsOperational,"Disconnected storage retains energy and cannot feed remote demand.");
                var generator = f.Build("generator",DeepBuildingRole.Generator,new Vector2Int(2,1)); generator.definition.powerGenerated = 20; generator.definition.fuelItem = f.fuel; generator.definition.fuelUnitsPerSecond = 1;
                f.session.Tick(1);
                Assert(Mathf.Abs(battery.batteryEnergy-26) < .001f,"Connected spare generation charges the battery and respects its rate.");
                battery.batteryEnergy = 100; float reserve = generator.fuelSecondsRemaining; f.session.Tick(.1f);
                Assert(Mathf.Abs(battery.batteryEnergy-100) < .001f && generator.fuelSecondsRemaining == reserve,"A full isolated battery cannot absorb more energy or trigger fuel burn.");
            }
        }
        static void WireConstruction()
        {
            using (var f = new Fixture())
            {
                f.Build("warehouse",DeepBuildingRole.Storage,new Vector2Int(4,1));
                f.root.SetActive(true);
                var workerObject = new GameObject("Wire worker"); workerObject.transform.SetParent(f.root.transform);
                var worker = workerObject.AddComponent<DeepWorker>(); worker.session = f.session; worker.TeleportToCell(new Vector2Int(2,1)); f.session.Workers.Add(worker);
                var cell = new Vector2Int(6,1); int before = f.session.inventory.GetAmount(f.alloy);
                Assert(f.session.RequestWire(cell,out string reason),reason);
                Assert(!f.session.HasWire(cell) && f.session.inventory.GetAmount(f.alloy) == before-1,"A wire blueprint reserves material but conducts nothing.");
                Assert(!f.session.RequestWire(cell,out _),"Wire plans cannot duplicate costs at the same grid cell.");
                f.session.CancelOrder(f.session.Orders[0],out _);
                Assert(f.session.inventory.GetAmount(f.alloy) == before,"Cancelling unbuilt wire returns all material.");
                Assert(f.session.RequestWire(cell,out reason),reason);
                for (int i = 0; i < 80; i++) f.session.Tick(.1f);
                Assert(f.session.HasWire(cell) && f.session.Orders[1].state == DeepWorkState.Completed,"A worker must fetch materials, arrive and finish before the wire exists.");
                Assert(f.session.inventory.GetAmount(f.alloy) == before-1,"Wire completion charges its material exactly once.");
            }
        }
        static void MineralEconomy()
        {
            using (var f = new Fixture())
            {
                f.world.SetTerrain(3,0,TerrainKind.Shale);
                var outputs = f.session.ExcavationOutputs(new Vector2Int(3,0));
                Assert(outputs.Length == 2 && outputs[0].item == f.fuel && outputs[0].amount == 2,"Shale supplies a bootstrap fuel source independent of powered fabrication.");
                f.world.SetTerrain(3,0,TerrainKind.Metal); outputs = f.session.ExcavationOutputs(new Vector2Int(3,0));
                Assert(outputs.Length == 1 && outputs[0].item == f.alloy,"Metal structure salvage returns alloy rather than generic ore.");
            }
        }
        sealed class Fixture : IDisposable
        {
            public readonly GameObject root;
            public readonly DeepPressureWorld world;
            public readonly DeepGameSession session;
            public readonly DeepItemDefinition fuel,alloy,ore;
            readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            public Fixture()
            {
                root = new GameObject("Power fixture"); root.SetActive(false);
                world = root.AddComponent<DeepPressureWorld>(); world.width = 14; world.height = 7; world.terrainKinds = new TerrainKind[98];
                for (int x = 0; x < world.width; x++) world.SetTerrain(x,0,TerrainKind.Basalt); world.RebuildRooms();
                fuel = Asset<DeepItemDefinition>(); fuel.id = "fuel"; alloy = Asset<DeepItemDefinition>(); alloy.id = "alloy"; ore = Asset<DeepItemDefinition>(); ore.id = "ore";
                var catalog = Asset<DeepGameplayCatalog>(); catalog.items = new[] { fuel,alloy,ore };
                session = root.AddComponent<DeepGameSession>(); session.world = world; session.catalog = catalog; session.excavationItem = ore; session.useWiredPower = true;
                session.startingInventory = new[] { new DeepItemAmount(fuel,2),new DeepItemAmount(alloy,10) }; session.InitializeSession();
            }
            T Asset<T>() where T : ScriptableObject { var result = ScriptableObject.CreateInstance<T>(); assets.Add(result); return result; }
            public DeepBuildingInstance Build(string id,DeepBuildingRole role,Vector2Int origin)
            {
                var definition = Asset<DeepBuildingDefinition>(); definition.id = id; definition.role = role; definition.footprint = Vector2Int.one; definition.blocksMovement = false;
                var go = new GameObject(id); go.transform.SetParent(root.transform); var result = go.AddComponent<DeepBuildingInstance>(); result.definition = definition; result.origin = origin; result.session = session;
                result.transform.position = session.BuildingPosition(origin); session.Buildings.Add(result); session.RebuildOccupancy(); session.InvalidatePowerTopology(); return result;
            }
            public void Connect(int from,int to) { for (int x = from; x <= to; x++) session.completedWireCells.Add(new Vector2Int(x,1)); session.InvalidatePowerTopology(); }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset); }
        }
        static void Assert(bool condition,string reason) { if (!condition) throw new InvalidOperationException("Power test: "+reason); }
    }
}
