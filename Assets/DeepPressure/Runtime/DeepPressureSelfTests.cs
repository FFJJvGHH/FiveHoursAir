using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DeepPressure
{
    /// <summary>Run from the editor validation command or a batch entry point. Throws on failure.</summary>
    public static class DeepPressureSelfTests
    {
        public static string RunAll()
        {
            var passed = new List<string>();
            RoomTopology(); passed.Add("room topology / boundary / coordinates / region precedence");
            TilemapSource(); passed.Add("painted tilemap sync / erasure / background independence");
            ConservationAndCapacity(); passed.Add("four-species conservation / finite supply / pressure capacity");
            BranchOrder(); passed.Add("branch order invariance");
            ReverseFlow(); passed.Add("directed valve / explicit reverse flow");
            SeparatorInterlock(); passed.Add("separator purity / full-outlet interlock");
            ExplorationSequence(); passed.Add("sample-before-entry / measured fog memory / hazards / no remote equipment reveal / probe range");
            return "Deep Pressure: " + passed.Count + " self-test groups passed.\n" + string.Join("\n",passed);
        }
        static void ExplorationSequence()
        {
            var root = new GameObject("SelfTest_Exploration"); root.SetActive(false);
            try
            {
                var world = root.AddComponent<DeepPressureWorld>(); world.width = 32; world.height = 5;
                world.terrainKinds = new TerrainKind[world.width*world.height];
                for (int i = 0; i < world.terrainKinds.Length; i++) world.terrainKinds[i] = TerrainKind.Basalt;
                foreach (int x in new[] { 1,2,5,11,28 }) world.SetTerrain(x,1,TerrainKind.Empty);
                var safe = Region(root,"safe",new RectInt(5,1,1,1),0,100);
                Region(root,"hazard",new RectInt(11,1,1,1),0,600);
                Region(root,"distant",new RectInt(28,1,1,1),0,100);
                world.RebuildRooms();
                var exploration = root.AddComponent<DeepExploration>(); exploration.world = world;
                exploration.initialExploredAreas = new[] { new RectInt(1,1,2,2) };
                Assert(exploration.IsVisible(new Vector2Int(1,1)),"Initial safe area is visible.");
                var safeCell = new Vector2Int(5,1); var hazardCell = new Vector2Int(11,1);
                Assert(!exploration.IsVisible(safeCell),"Unexplored region starts hidden.");
                Assert(!exploration.TryExplore(safeCell,out _),"Exploration requires sampling first.");
                Assert(exploration.TrySample(safeCell,out string message),message);
                Assert(exploration.IsSampled(safe) && !exploration.IsVisible(safeCell),"Sampling records knowledge without revealing terrain.");
                Assert(exploration.TryGetSample(safe,out var sample) && Math.Abs(sample.pressureKPa-100) < 1e-3,"Sample records measured atmosphere.");
                Assert(exploration.TryExplore(safeCell,out message) && !exploration.IsVisible(safeCell),"Safe measurement permits entry but never remotely reveals terrain: "+message);
                Assert(exploration.TrySample(hazardCell,out message),message);
                Assert(!exploration.TryExplore(hazardCell,out _),"High-pressure region needs isolation equipment.");
                exploration.hasIsolationEquipment = true;
                Assert(!exploration.TryExplore(hazardCell,out _) && !exploration.IsVisible(hazardCell),"Legacy equipment switch cannot bypass hazardous atmosphere or reveal sealed terrain.");
                Assert(!exploration.TrySample(new Vector2Int(28,1),out _),"Remote sampling respects drill reach.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void TilemapSource()
        {
            var root = new GameObject("SelfTest_Tilemap");
            var tile = ScriptableObject.CreateInstance<DeepTerrainTile>(); tile.kind = TerrainKind.Basalt;
            try
            {
                var world = root.AddComponent<DeepPressureWorld>(); world.width = world.height = 2;
                var gridObject = new GameObject("Grid"); gridObject.transform.SetParent(root.transform); gridObject.AddComponent<Grid>();
                var foregroundObject = new GameObject("Foreground"); foregroundObject.transform.SetParent(gridObject.transform);
                var foreground = foregroundObject.AddComponent<Tilemap>(); world.terrain = foreground;
                var backgroundObject = new GameObject("Background"); backgroundObject.transform.SetParent(gridObject.transform);
                var background = backgroundObject.AddComponent<Tilemap>(); world.background = background;
                world.terrainKinds = new[] { TerrainKind.Metal,TerrainKind.Metal,TerrainKind.Metal,TerrainKind.Metal };
                foreground.SetTile(new Vector3Int(1,0,0),tile); background.SetTile(new Vector3Int(0,0,0),tile);
                world.SyncTerrainFromTilemap(); world.RebuildRooms();
                Assert(world.GetTerrain(1,0) == TerrainKind.Basalt,"Painted DeepTerrainTile must update serialized geology.");
                Assert(world.GetTerrain(0,0) == TerrainKind.Empty,"Background tiles never block foreground empty cells.");
                Assert(world.Rooms.Count == 1 && world.Rooms[0].cellCount == 3,"Room topology follows painted foreground.");
                foreground.SetTile(new Vector3Int(1,0,0),null); world.SyncTerrainFromTilemap(); world.RebuildRooms();
                Assert(world.GetTerrain(1,0) == TerrainKind.Empty && world.Rooms[0].cellCount == 4,"Erasing a tile clears stale terrain data.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(tile); }
        }
        static void RoomTopology()
        {
            GameObject root = new GameObject("SelfTest_Rooms");
            try
            {
                var world = root.AddComponent<DeepPressureWorld>();
                world.width = world.height = 5; world.cellSize = 2; root.transform.position = new Vector3(10,20,0);
                world.terrainKinds = new TerrainKind[25];
                for (int i = 0; i < 25; i++) world.terrainKinds[i] = TerrainKind.Basalt;
                world.SetTerrain(0,1,TerrainKind.Empty); world.SetTerrain(1,1,TerrainKind.Empty);
                world.SetTerrain(2,2,TerrainKind.Empty); world.SetTerrain(3,3,TerrainKind.Empty);
                var wide = Region(root,"wide",new RectInt(0,0,5,5),0,100);
                var smallZ = Region(root,"z",new RectInt(1,1,2,2),0,200);
                var smallA = Region(root,"a",new RectInt(1,1,2,2),0,300);
                world.RebuildRooms();
                Assert(world.Rooms.Count == 3,"Diagonal cells must not connect.");
                Assert(world.RoomAt(new Vector2Int(0,1)).isOpen,"A room touching a map edge is open.");
                Assert(!world.RoomAt(new Vector2Int(2,2)).isOpen,"A separated interior room is enclosed.");
                Assert(world.RoomAt(new Vector2Int(1,0)) == null,"Solid cells have no room.");
                Assert(world.RegionAt(new Vector2Int(1,1)) == smallA,"Equal-priority overlap picks smaller area then stable ID.");
                wide.priority = 1;
                Assert(world.RegionAt(new Vector2Int(1,1)) == wide,"Priority dominates area.");
                Assert(world.WorldToCell(world.CellToWorld(new Vector2Int(3,2))) == new Vector2Int(3,2),"Cell/world coordinates round trip.");
                Assert(world.WorldToCell(new Vector3(9.99f,20,0)).x == -1,"Negative local coordinates floor instead of truncate.");
                wide.priority = 0; world.RebuildRooms();
                var openRoom = world.RoomAt(new Vector2Int(0,1));
                Near(openRoom.PressureKPa,200,1e-4,"Room initialization sums mol at shared temperature (100+300)/2.");
                GC.KeepAlive(smallZ);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static DeepPressureRegion Region(GameObject root,string id,RectInt bounds,int priority,float pressure)
        {
            var go = new GameObject(id); go.transform.SetParent(root.transform);
            var region = go.AddComponent<DeepPressureRegion>(); region.stableId = id; region.bounds = bounds; region.priority = priority; region.initialPressureKPa = pressure; return region;
        }
        static void ConservationAndCapacity()
        {
            GameObject root = new GameObject("SelfTest_Conservation");
            try
            {
                GasNode source = Node(root,"source",GasNodeKind.Reservoir,600,800,1);
                GasNode a = Node(root,"a",GasNodeKind.Storage,0,60,.2f);
                GasNode b = Node(root,"b",GasNodeKind.Storage,0,110,.3f);
                Link(root,source,a); Link(root,source,b);
                var sim = root.AddComponent<GasNetworkSimulator>(); sim.ResetSimulation();
                double start = source.gas.Total;
                for (int i = 0; i < 500; i++) sim.Step(.1f);
                Assert(sim.VerifyConservation(out string message),message);
                Assert(source.gas.Total < start,"Finite source inventory decreases.");
                Assert(a.PressureKPa <= a.maxPressureKPa+1e-6 && b.PressureKPa <= b.maxPressureKPa+1e-6,"Receiver pressure never exceeds capacity.");
                sim.Step(1000); Assert(sim.VerifyConservation(out message),message);
                Assert(source.gas.IsFiniteAndNonnegative && a.gas.IsFiniteAndNonnegative && b.gas.IsFiniteAndNonnegative,"Long step cannot create negative gas.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void BranchOrder()
        {
            GameObject root = new GameObject("SelfTest_Branches");
            try
            {
                var source = Node(root,"source",GasNodeKind.Reservoir,300,500,1); source.throughputMolPerSecond = 2;
                var a = Node(root,"a",GasNodeKind.Storage,0,200,1);
                var b = Node(root,"b",GasNodeKind.Storage,0,200,1);
                var la = Link(root,source,a); var lb = Link(root,source,b);
                var sim = root.AddComponent<GasNetworkSimulator>(); sim.ResetSimulation();
                sim.links = new[] { la,lb };
                for (int i = 0; i < 150; i++) sim.Step(.1f);
                GasMixture finalSource = source.gas, finalA = a.gas, finalB = b.gas;
                sim.ResetSimulation(); sim.links = new[] { lb,la };
                for (int i = 0; i < 150; i++) sim.Step(.1f);
                MixtureNear(source.gas,finalSource,"Source must be independent of branch order.");
                MixtureNear(a.gas,finalA,"First branch must be independent of branch order.");
                MixtureNear(b.gas,finalB,"Second branch must be independent of branch order.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void ReverseFlow()
        {
            GameObject root = new GameObject("SelfTest_Reverse");
            try
            {
                var a = Node(root,"a",GasNodeKind.Storage,10,200,1);
                var b = Node(root,"b",GasNodeKind.Storage,100,200,1);
                var link = Link(root,a,b);
                var sim = root.AddComponent<GasNetworkSimulator>(); sim.ResetSimulation();
                GasMixture before = a.gas; sim.Step(.1f);
                MixtureNear(a.gas,before,"A directed link must not flow against its pressure gradient.");
                link.allowReverse = true; sim.Step(.1f);
                Assert(link.lastFlowMolPerSecond < 0 && a.gas.Total > before.Total,"Explicit reverse mode transfers high to low.");
                Assert(sim.VerifyConservation(out string message),message);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void SeparatorInterlock()
        {
            GameObject root = new GameObject("SelfTest_Separator");
            try
            {
                var source = Node(root,"source",GasNodeKind.Reservoir,300,500,1);
                var separator = Node(root,"separator",GasNodeKind.Separator,160,180,1);
                var product = Node(root,"product",GasNodeKind.Storage,0,100,1);
                var tail = Node(root,"tail",GasNodeKind.Storage,50,50,1);
                Link(root,source,separator);
                var oxygenLine = Link(root,separator,product); oxygenLine.fromPort = GasOutputPort.OxygenProduct;
                var tailLine = Link(root,separator,tail); tailLine.fromPort = GasOutputPort.TailGas;
                var sim = root.AddComponent<GasNetworkSimulator>(); sim.ResetSimulation();
                var before = separator.gas; sim.Step(.1f);
                MixtureNear(separator.gas,before,"Full tail tank must stop separator input and both outputs.");
                Assert(oxygenLine.lastFlowMolPerSecond == 0 && separator.status.Contains("tail"),"Stopped machine explains the blocked branch.");
                tail.initialPressureKPa = 0; sim.ResetSimulation(); sim.Step(.1f);
                Assert(product.gas.oxygen > 0,"Open outlets enable oxygen production.");
                Near(product.gas.nitrogen+product.gas.carbonDioxide+product.gas.waterVapour,0,1e-9,"Product stream contains oxygen only.");
                Near(tail.gas.oxygen,0,1e-9,"Tail stream excludes oxygen.");
                Assert(sim.VerifyConservation(out string message),message);
                before = separator.gas; tailLine.isOpen = false; sim.Step(.1f);
                MixtureNear(separator.gas,before,"Closing an outlet interlocks the separator.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static GasNode Node(GameObject root,string name,GasNodeKind kind,float pressure,float maximum,float volume)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform);
            var node = go.AddComponent<GasNode>(); node.displayName = name; node.kind = kind; node.initialPressureKPa = pressure; node.maxPressureKPa = maximum; node.volumeM3 = volume; node.throughputMolPerSecond = 100; return node;
        }
        static GasLink Link(GameObject root,GasNode source,GasNode destination)
        {
            var go = new GameObject(source.name+"_to_"+destination.name); go.transform.SetParent(root.transform);
            var link = go.AddComponent<GasLink>(); link.from = source; link.to = destination; link.maxFlowMolPerSecond = 50; link.conductanceMolPerSecondPerKPa = 1; return link;
        }
        static void MixtureNear(GasMixture a,GasMixture b,string message) { for (int i = 0; i < 4; i++) Near(a[i],b[i],1e-8,message+" Species="+i); }
        static void Near(double actual,double expected,double tolerance,string message) => Assert(Math.Abs(actual-expected) <= tolerance,message+$" Expected={expected:R} Actual={actual:R}");
        static void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException("Deep Pressure self-test failed: "+message); }
    }
}
