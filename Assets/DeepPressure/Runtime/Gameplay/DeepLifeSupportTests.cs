using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public static class DeepLifeSupportTests
    {
        public static string RunAll()
        {
            CollectionAndPower(); TraceCollection(); SupplyAndRespiration(); ReclaimerBackpressure(); CleanOxygenAcceptance(); ProductionTargets();
            return "Life support: 6 integration groups passed. Wired collector and finite ledger; trace extraction conservation; local supply and respiratory conversion; exact 5-mol industrial recovery/storage backpressure; clean-product contamination rejection; production-target reservation/completion limits.";
        }
        static void CollectionAndPower()
        {
            using (var f = new Fixture(100))
            {
                var pump = f.Facility("collector",DeepGasFacilityMode.Collect,new Vector2Int(3,1),6);
                var generator = f.Station("generator",DeepBuildingRole.Generator,new Vector2Int(1,1)); generator.definition.powerGenerated = 20;
                var total = f.Total(); f.session.Tick(.5f);
                Assert(pump.gas.Total == 0,"An unwired powered collector cannot withdraw room gas.");
                for (int x = 1; x <= 3; x++) f.session.completedWireCells.Add(new Vector2Int(x,1)); f.session.InvalidatePowerTopology();
                f.session.Tick(.5f);
                Assert(pump.gas.Total > 0,"A completed live wire allows collection into its finite node.");
                Near(f.Total(),total,"Collection transfers each species from room to node without creation/loss.");
                Assert(f.network.VerifyConservation(out var reason),reason);
                double held = pump.gas.Total; Assert(f.session.RemoveWire(new Vector2Int(2,1),out reason),reason); f.session.Tick(.5f);
                Assert(Math.Abs(pump.gas.Total-held) < 1e-9,"Breaking the circuit stops the collector.");
            }
        }
        static void TraceCollection()
        {
            using (var f = new Fixture(0))
            {
                var cell = new Vector2Int(3,1); var pump = f.Facility("trace",DeepGasFacilityMode.Collect,cell);
                f.session.Atmosphere.Add(cell,new GasMixture { methane = 5e-7 });
                var total = f.Total(); f.session.Tick(.1f);
                Assert(pump.gas.methane > 0,"A finite trace packet must reach the collector, never disappear at the empty-status cutoff.");
                Near(f.Total(),total,"Trace collection conserves inventory.",1e-12);
                Assert(f.network.VerifyConservation(out var reason),reason);
            }
        }
        static void SupplyAndRespiration()
        {
            using (var f = new Fixture(0))
            {
                var vent = f.Facility("supply",DeepGasFacilityMode.Supply,new Vector2Int(3,1));
                f.SetGas(vent,new GasMixture { oxygen = 20 }); var total = f.Total();
                f.session.Tick(.5f);
                Assert(vent.gas.oxygen < 20 && f.session.Atmosphere.TotalInventory().oxygen > 0,"Supply transfers finite oxygen from the pipe node into the local atmosphere.");
                Near(f.Total(),total,"Supply preserves total field plus network inventory.");
                Assert(f.network.VerifyConservation(out var reason),reason);
            }
            using (var f = new Fixture(100))
            {
                var worker = f.Worker(new Vector2Int(2,1)); var before = f.Total();
                f.session.Tick(.4f); var after = f.Total();
                double used = f.session.breathingMolPerSecond*.4;
                Assert(Math.Abs(before.oxygen-after.oxygen-used) < 1e-7 && Math.Abs(after.carbonDioxide-before.carbonDioxide-used) < 1e-7,"Respiration converts finite local O₂ to CO₂ at the configured rate.");
                Assert(Math.Abs(before.Total-after.Total) < 1e-7 && !worker.environmentUnsafe,"Breathing preserves total mol and reads the local breathable atmosphere.");
            }
        }
        static void ReclaimerBackpressure()
        {
            using (var f = new Fixture(0))
            {
                var cell = new Vector2Int(3,1); var recovery = f.Facility("vapor_reclaimer",DeepGasFacilityMode.Recover,cell);
                // A near-full tank cannot round a fractional amount up into a whole reagent.
                f.SetGas(recovery,new GasMixture { processVapor = 4.9999995 }); f.session.Tick(.1f);
                Assert(f.session.inventory.GetAmount(f.reagent) == 0,"Recovery requires all five mol; the completion epsilon must not create material.");
                f.session.Atmosphere.Add(cell,new GasMixture { processVapor = .0000005 }); f.session.Tick(.1f);
                Assert(f.session.inventory.GetAmount(f.reagent) == 1 && recovery.gas.processVapor < 1e-8,"Exactly five mol become one sealed reagent.");
                Assert(f.network.VerifyConservation(out var reason),reason);
                f.session.inventory.SetCapacity(1);
                f.session.Atmosphere.Add(cell,new GasMixture { processVapor = 5 }); var before = f.Total(); f.session.Tick(.1f);
                Assert(f.session.inventory.GetAmount(f.reagent) == 1 && recovery.gas.processVapor == 0,"A full warehouse prevents further recovery instead of deleting captured material.");
                Near(f.Total(),before,"Warehouse backpressure preserves process gas for later extraction.");
            }
        }
        static void CleanOxygenAcceptance()
        {
            using (var f = new Fixture(0))
            {
                var destination = f.Facility("product",DeepGasFacilityMode.Storage,new Vector2Int(6,1));
                destination.GetComponent<DeepBuildingInstance>().definition.gasAcceptance = DeepGasAcceptance.Oxygen;
                Assert(DeepGasFacility.Accepts(destination,new GasMixture { oxygen = 8,nitrogen = 2 }),"A clean oxygen-rich product passes the configured purity gate.");
                Assert(!DeepGasFacility.Accepts(destination,new GasMixture { oxygen = 9,methane = 1 }),"High O₂ percentage cannot disguise fuel contamination.");
                Assert(!DeepGasFacility.Accepts(destination,new GasMixture { oxygen = 9.99,processVapor = .01 }),"Process vapour at the configured product limit is rejected.");
                Assert(!DeepGasFacility.Accepts(destination,new GasMixture { oxygen = 9.8,carbonDioxide = .2 }),"Excess CO₂ is rejected even when O₂ concentration is high.");
                var source = f.Facility("source",DeepGasFacilityMode.Storage,new Vector2Int(3,1));
                f.SetGas(source,new GasMixture { oxygen = 90,methane = 10 });
                var wire = new GameObject("Contamination test gas pipe"); wire.transform.SetParent(f.world.transform);
                var link = wire.AddComponent<GasLink>(); link.from = source; link.to = destination; link.maxFlowMolPerSecond = 20; link.conductanceMolPerSecondPerKPa = 1;
                f.network.RefreshTopologyPreservingGas(); f.network.Step(.5f);
                Assert(destination.gas.Total == 0 && link.lastFlowMolPerSecond == 0,"The real network refuses contaminated product transfers.");
                Assert(f.network.VerifyConservation(out var reason),reason);
            }
        }
        static void ProductionTargets()
        {
            using (var f = new Fixture(100))
            {
                f.Worker(new Vector2Int(1,1)); f.Station("warehouse",DeepBuildingRole.Storage,new Vector2Int(3,1));
                var factory = f.Station("factory",DeepBuildingRole.Fabricator,new Vector2Int(5,1));
                var recipe = f.Asset<DeepRecipeDefinition>(); recipe.id = "test_reagent_recipe"; recipe.requiredBuildingId = factory.definition.id; recipe.workSeconds = .2f;
                recipe.inputs = new[] { new DeepItemAmount(f.ore,2) }; recipe.outputs = new[] { new DeepItemAmount(f.reagent,1) };
                f.session.catalog.recipes = new[] { recipe }; Assert(f.session.inventory.TryAdd(f.ore,8),"Fixture inputs fit.");
                f.session.SetProductionTarget(recipe,2,true); f.session.Tick(.1f); f.session.Tick(.1f);
                Assert(f.session.Orders.Count == 1 && f.session.inventory.GetAmount(f.ore) == 6,"An active batch reserves once; repeated target checks do not duplicate orders.");
                for (int i = 0; i < 180; i++) f.session.Tick(.1f);
                Assert(f.session.inventory.GetAmount(f.reagent) == 2 && f.session.inventory.GetAmount(f.ore) == 4,"The target creates exactly the requested batches using finite inputs.");
                foreach (var order in f.session.Orders) Assert(order.IsTerminal,"Reaching the inventory target leaves no extra production order queued.");
            }
        }
        sealed class Fixture : IDisposable
        {
            readonly GameObject root;
            readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            public readonly DeepPressureWorld world;
            public readonly DeepGameSession session;
            public readonly GasNetworkSimulator network;
            public readonly DeepItemDefinition ore,reagent;
            public Fixture(float pressure)
            {
                root = new GameObject("Life support integration"); root.SetActive(false);
                world = root.AddComponent<DeepPressureWorld>(); world.width = 18; world.height = 5; world.terrainKinds = new TerrainKind[90];
                for (int x = 0; x < 18; x++) world.SetTerrain(x,0,TerrainKind.Basalt);
                world.defaultPressureKPa = pressure; world.defaultTemperatureC = 22; world.defaultComposition = new Vector4(.21f,.79f,0,0); world.RebuildRooms();
                network = root.AddComponent<GasNetworkSimulator>(); network.ResetSimulation();
                session = root.AddComponent<DeepGameSession>(); session.world = world; session.network = network; session.lifeSupportEnabled = true; session.hazardsEnabled = false;
                session.catalog = Asset<DeepGameplayCatalog>(); ore = Asset<DeepItemDefinition>(); ore.id = "ore"; reagent = Asset<DeepItemDefinition>(); reagent.id = "process_reagent";
                session.catalog.items = new[] { ore,reagent };
                root.SetActive(true); session.InitializeSession();
            }
            public T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
            public DeepBuildingInstance Station(string id,DeepBuildingRole role,Vector2Int origin)
            {
                var definition = Asset<DeepBuildingDefinition>(); definition.id = definition.displayName = id; definition.role = role; definition.blocksMovement = false; definition.footprint = new Vector2Int(1,2);
                var go = new GameObject(id); go.transform.SetParent(root.transform); go.transform.position = session.BuildingPosition(origin);
                var instance = go.AddComponent<DeepBuildingInstance>(); instance.definition = definition; instance.origin = origin; instance.session = session;
                session.Buildings.Add(instance); session.RebuildOccupancy(); return instance;
            }
            public GasNode Facility(string id,DeepGasFacilityMode mode,Vector2Int origin,float power = 0)
            {
                var instance = Station(id,DeepBuildingRole.GasPump,origin); instance.definition.gasMode = mode;
                instance.definition.exchangesRoomGas = mode != DeepGasFacilityMode.Storage; instance.definition.powerRequired = power;
                var node = instance.gameObject.AddComponent<GasNode>(); node.kind = GasNodeKind.Storage; node.initialPressureKPa = 0; node.volumeM3 = 8; node.maxPressureKPa = 600;
                instance.RefreshOwnedComponents(); network.RefreshTopologyPreservingGas(); return node;
            }
            public DeepWorker Worker(Vector2Int cell)
            {
                var go = new GameObject("Life support worker"); go.transform.SetParent(root.transform); go.transform.position = session.FootPosition(cell);
                var worker = go.AddComponent<DeepWorker>(); worker.session = session; session.Workers.Add(worker); return worker;
            }
            public void SetGas(GasNode node,GasMixture gas) { network.RegisterExternalExchange(gas-node.gas); node.gas = gas; }
            public GasMixture Total() => session.Atmosphere.TotalInventory()+network.TotalInventory();
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset); }
        }
        static void Near(GasMixture a,GasMixture b,string message,double tolerance = 1e-7)
        { for (int s = 0; s < GasMixture.SpeciesCount; s++) Assert(Math.Abs(a[s]-b[s]) < tolerance,message+" Species "+s); }
        static void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException("Life support integration: "+message); }
    }
}
