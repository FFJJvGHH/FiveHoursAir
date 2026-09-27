using System;
using UnityEngine;

namespace DeepPressure
{
    public static class DeepAtmosphereTests
    {
        public static string RunAll()
        {
            ConservativeDiffusion(); TopologyAndRestore();
            return "Local atmosphere: 2 groups passed. Six-species conservative diffusion, finite pressure fronts and wall isolation; digging does not create gas, displaced inventory preserved, local take/add and save restore.";
        }
        static void ConservativeDiffusion()
        {
            using (var f = new Fixture())
            {
                var source = new Vector2Int(1,1);
                f.field.Add(source,new GasMixture { methane = 12,processVapor = 6 },65);
                var before = f.field.TotalInventory();
                f.field.Tick(.1f);
                Assert(f.field.Sample(new Vector2Int(2,1)).methane > 0,"Gas travels into adjacent cells.");
                Assert(f.field.Sample(new Vector2Int(3,1)).methane < 1e-10,"A first substep does not instantly mix an entire room.");
                Assert(f.field.Sample(new Vector2Int(5,1)).methane < 1e-10,"A solid wall blocks the reactive gas front.");
                for (int i = 0; i < 80; i++) f.field.Tick(.2f);
                Near(f.field.TotalInventory(),before,"Diffusion conserves all six species.");
                foreach (var gas in f.field.CaptureCells()) Assert(gas.IsFiniteAndNonnegative,"Bounded simultaneous flux never creates negative inventory.");
                double thermal = 0;
                foreach (var room in f.world.Rooms) thermal += room.gas.Total;
                Assert(Math.Abs(thermal-before.Total) < 1e-7,"Room readings aggregate the local field without duplicating gas.");
            }
        }
        static void TopologyAndRestore()
        {
            using (var f = new Fixture())
            {
                f.field.Add(new Vector2Int(3,1),new GasMixture { methane = 10 });
                var before = f.field.TotalInventory();
                f.world.SetTerrain(4,1,TerrainKind.Empty); f.world.RebuildRooms(); f.field.RebuildAfterTerrainChange();
                Assert(f.field.Sample(new Vector2Int(4,1)).Total == 0,"Excavating a new void cannot create an authored refill of gas.");
                Near(f.field.TotalInventory(),before,"Opening a chamber preserves finite inventory.");
                f.field.Tick(1);
                Assert(f.field.Sample(new Vector2Int(5,1)).methane > 0,"Gas crosses a newly opened wall over time.");
                var packet = f.field.TakeSpecies(new Vector2Int(3,1),4,1);
                Assert(packet.methane > 0 && packet.oxygen == 0,"A selective extractor removes the requested industrial gas only.");
                f.field.Add(new Vector2Int(7,1),packet); Near(f.field.TotalInventory(),before,"Local extraction and reinjection conserve inventory.");
                f.world.SetTerrain(2,1,TerrainKind.Metal); f.world.RebuildRooms(); f.field.RebuildAfterTerrainChange();
                Near(f.field.TotalInventory()+f.world.displacedGas,before,"Construction redistributes gas without deleting it.");
                var save = f.field.CaptureCells(); var heat = f.field.CaptureTemperatures();
                f.field.Take(new Vector2Int(7,1),100000); f.field.RestoreCells(save,heat);
                Near(f.field.TotalInventory()+f.world.displacedGas,before,"Saving and loading preserve local concentration and temperature.");
            }
        }
        sealed class Fixture : IDisposable
        {
            readonly GameObject root;
            public readonly DeepPressureWorld world;
            public readonly DeepAtmosphereField field;
            public Fixture()
            {
                root = new GameObject("Local atmosphere regression"); root.SetActive(false);
                world = root.AddComponent<DeepPressureWorld>(); world.width = 9; world.height = 4;
                world.terrainKinds = new TerrainKind[36];
                for (int i = 0; i < 36; i++) world.terrainKinds[i] = TerrainKind.Basalt;
                for (int y = 1; y <= 2; y++) for (int x = 1; x <= 7; x++) if (x != 4) world.SetTerrain(x,y,TerrainKind.Empty);
                world.defaultPressureKPa = 100; world.defaultComposition = new Vector4(.21f,.79f,0,0); world.RebuildRooms();
                field = root.AddComponent<DeepAtmosphereField>(); field.Initialize(world);
            }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
        static void Near(GasMixture a,GasMixture b,string message)
        {
            for (int s = 0; s < GasMixture.SpeciesCount; s++) Assert(Math.Abs(a[s]-b[s]) < 1e-7,message+" Species "+s);
        }
        static void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException("Local atmosphere regression: "+message); }
    }
}
