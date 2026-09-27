using System;
using UnityEngine;

namespace DeepPressure
{
    public static class DeepAtmosphereTests
    {
        public static string RunAll()
        {
            ConservativeDiffusion(); TopologyAndRestore(); PhotosynthesisAndDistributedSupply();
            return "Local atmosphere: 3 groups passed. Conservative diffusion, wall isolation, finite excavation/restore, CO2-limited photosynthesis and pressure-limited distributed supply.";
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
        static void PhotosynthesisAndDistributedSupply()
        {
            using(var f=new Fixture())
            {
                var source=new Vector2Int(1,1);
                for(int y=0;y<f.world.height;y++)for(int x=0;x<f.world.width;x++)f.field.TakeSpecies(new Vector2Int(x,y),2,100000);
                var noCarbon=f.field.TotalInventory();
                Assert(f.field.Photosynthesize(source,20)==0,"Algae cannot create oxygen without CO2.");
                Near(f.field.TotalInventory(),noCarbon,"Starved algae leave gas unchanged.");
                f.field.TakeSpecies(source,0,2);f.field.Add(source,new GasMixture{carbonDioxide=2});
                var before=f.field.TotalInventory();double converted=f.field.Photosynthesize(source,10);
                var after=f.field.TotalInventory();
                Assert(Math.Abs(converted-2)<1e-8,"Photosynthesis is capped by available CO2.");
                Assert(Math.Abs(after.oxygen-before.oxygen-converted)<1e-8&&Math.Abs(before.carbonDioxide-after.carbonDioxide-converted)<1e-8,"Each CO2 mol releases exactly one O2 mol.");
                Assert(Math.Abs(before.Total-after.Total)<1e-8,"Photosynthesis leaves total gas moles unchanged.");
                before=after;var acrossWall=f.field.Sample(new Vector2Int(5,1));double neighboringOxygen=f.field.Sample(new Vector2Int(2,1)).oxygen;
                var delivered=f.field.AddDistributed(source,new GasMixture{oxygen=100},135,23,radius:8);
                Assert(delivered.oxygen>0&&delivered.oxygen<100,"An outlet accepts only finite pressure capacity.");
                Near(f.field.TotalInventory(),before+delivered,"Only the accepted packet enters the field.");
                Near(f.field.Sample(new Vector2Int(5,1)),acrossWall,"An outlet cannot inject through a solid wall.");
                Assert(f.field.Sample(new Vector2Int(2,1)).oxygen>neighboringOxygen,"A fan supplies adjacent connected air.");
                Assert(f.field.AddDistributed(source,new GasMixture{oxygen=100},135,23,radius:8).Total<1e-8,"A saturated area rejects surplus gas.");
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
