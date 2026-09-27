using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public static class DeepExplorationTests
    {
        public static string RunAll()
        {
            SightAndOcclusion(); SampleRequiresWork(); SurveyCannotTeleport();
            return "Exploration: 3 groups passed. Local workers/known facilities; occluded walls and permanent memory; physical sampling, single research reward and cancellation; sealed-room survey rejection.";
        }
        static void SightAndOcclusion()
        {
            using (var f = new Fixture())
            {
                f.exploration.RefreshProximity(f.session,true);
                Assert(f.exploration.IsVisible(new Vector2Int(7,2)),"A worker reveals nearby floor without clicking a survey button.");
                Assert(!f.exploration.IsVisible(new Vector2Int(11,2)),"A rock wall blocks worker and machine sight.");
                Assert(!f.exploration.IsVisible(new Vector2Int(19,2)),"An undiscovered authored machine does not reveal itself.");
                f.worker.TeleportToCell(new Vector2Int(7,1));
                f.world.SetTerrain(9,1,TerrainKind.Empty); f.world.SetTerrain(9,2,TerrainKind.Empty);
                f.world.RebuildRooms(); f.exploration.RefreshAfterTerrainChange(); f.exploration.RefreshProximity(f.session,true);
                Assert(f.exploration.IsVisible(new Vector2Int(11,2)),"Opening a wall reveals the exposed chamber from the worker's current position.");
                f.worker.TeleportToCell(new Vector2Int(1,1)); f.exploration.RefreshProximity(f.session,true);
                Assert(f.exploration.IsVisible(new Vector2Int(11,2)),"Previously seen terrain stays in map memory after leaving.");
                f.world.SetTerrain(5,2,TerrainKind.Basalt); f.world.SetTerrain(4,3,TerrainKind.Basalt);
                Assert(!f.exploration.HasSightLine(new Vector2Int(4,2),new Vector2Int(5,3)),"Two touching wall corners block diagonal sight.");
            }
        }
        static void SampleRequiresWork()
        {
            using (var f = new Fixture())
            {
                f.exploration.RefreshProximity(f.session,true);
                Assert(f.session.RequestSample(new Vector2Int(13,2),out var reason),reason);
                Assert(!f.exploration.TryGetSample(f.region,out _) && f.session.inventory.GetAmount(f.data) == 0,"Scheduling a sample grants neither readings nor research data.");
                Assert(!f.session.RequestSample(new Vector2Int(14,2),out _),"A region cannot have duplicate sample work orders.");
                f.session.Tick(.1f);
                Assert(!f.exploration.TryGetSample(f.region,out _),"Starting travel cannot complete measurement.");
                f.Run(8);
                Assert(f.exploration.TryGetSample(f.region,out _) && f.session.inventory.GetAmount(f.data) == 4,"A completed on-site sample grants measured atmosphere and four research data.");
                Assert(!f.exploration.IsVisible(new Vector2Int(13,2)),"Sampling through a sealed wall does not reveal its terrain.");
                Assert(f.session.RequestSample(new Vector2Int(13,2),out reason),reason); f.Run(8);
                Assert(f.session.inventory.GetAmount(f.data) == 4,"Repeat measurements never farm new-region research rewards.");
                Assert(f.session.RequestSample(new Vector2Int(13,2),out reason),reason);
                var pending = f.session.Orders[f.session.Orders.Count-1]; f.session.CancelOrder(pending);
                f.Run(2); Assert(pending.state == DeepWorkState.Cancelled && f.session.inventory.GetAmount(f.data) == 4,"Cancellation does not complete or reward a sample.");
            }
        }
        static void SurveyCannotTeleport()
        {
            using (var f = new Fixture())
            {
                Assert(f.exploration.TrySample(new Vector2Int(13,2),out var reason),reason);
                Assert(!f.session.RequestSurvey(new Vector2Int(13,2),out _),"A safe sampled sealed room still needs a physically reachable known entrance.");
                f.exploration.hasIsolationEquipment = true;
                Assert(!f.exploration.IsVisible(new Vector2Int(13,2)),"Equipment toggles cannot alter discovery.");
            }
        }
        sealed class Fixture : IDisposable
        {
            readonly GameObject root;
            readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            public readonly DeepPressureWorld world;
            public readonly DeepGameSession session;
            public readonly DeepExploration exploration;
            public readonly DeepWorker worker;
            public readonly DeepPressureRegion region;
            public readonly DeepItemDefinition data;
            public Fixture()
            {
                root = new GameObject("Exploration regression"); root.SetActive(false);
                world = root.AddComponent<DeepPressureWorld>(); world.width = 24; world.height = 7; world.terrainKinds = new TerrainKind[168];
                world.defaultPressureKPa = 100; world.defaultComposition = new Vector4(.21f,.79f,0,0);
                for (int x = 0; x < 24; x++) world.SetTerrain(x,0,TerrainKind.Basalt);
                for (int y = 1; y < 7; y++) world.SetTerrain(9,y,TerrainKind.Basalt);
                var chamber = new GameObject("Measured chamber"); chamber.transform.SetParent(root.transform);
                region = chamber.AddComponent<DeepPressureRegion>(); region.stableId = "sample-test"; region.bounds = new RectInt(12,1,5,4); region.initialPressureKPa = 100; region.composition = new Vector4(.21f,.79f,0,0);
                world.RebuildRooms();
                exploration = root.AddComponent<DeepExploration>(); exploration.world = world; exploration.initialExploredAreas = new[] { new RectInt(1,1,3,3) };
                session = root.AddComponent<DeepGameSession>(); session.world = world;
                var person = new GameObject("Surveyor"); person.transform.SetParent(root.transform); person.transform.position = new Vector3(2.5f,1,0);
                worker = person.AddComponent<DeepWorker>();
                var catalog = ScriptableObject.CreateInstance<DeepGameplayCatalog>(); assets.Add(catalog);
                data = ScriptableObject.CreateInstance<DeepItemDefinition>(); assets.Add(data); data.id = "research_data";
                catalog.items = new[] { data }; session.catalog = catalog;
                Facility(new Vector2Int(6,1)); Facility(new Vector2Int(19,1));
                root.SetActive(true); session.InitializeSession();
            }
            void Facility(Vector2Int origin)
            {
                var definition = ScriptableObject.CreateInstance<DeepBuildingDefinition>(); assets.Add(definition); definition.role = DeepBuildingRole.Research; definition.footprint = new Vector2Int(2,2); definition.blocksMovement = false;
                var go = new GameObject("Survey test facility"); go.transform.SetParent(root.transform); go.transform.position = world.CellToWorld(origin);
                var instance = go.AddComponent<DeepBuildingInstance>(); instance.definition = definition; instance.origin = origin;
            }
            public void Run(float seconds) { for (int i = 0; i < seconds*10; i++) session.Tick(.1f); }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset); }
        }
        static void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException("Exploration regression: "+message); }
    }
}
