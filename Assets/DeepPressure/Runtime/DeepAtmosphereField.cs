using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    /// <summary>Authoritative finite per-cell atmosphere. Neighbor fluxes are accumulated
    /// simultaneously, so diffusion conserves each species and cannot depend on scan order.
    /// The existing room gas is an aggregate for UI/network compatibility, never a second tank.</summary>
    [DisallowMultipleComponent]
    public sealed class DeepAtmosphereField : MonoBehaviour
    {
        public DeepPressureWorld world;
        public const float DefaultDiffusionPerSecond = 2.4f;
        [Range(.05f,3)] public float diffusionPerSecond = DefaultDiffusionPerSecond;
        [Range(0,.5f)] public float thermalConductionPerSecond = .08f;
        [HideInInspector] public float initialCarbonDioxideFraction=.0004f;
        GasMixture[] cells,deltas;
        double[] temperatures,energyDelta;
        bool[] open;
        public bool IsInitialized => world != null && cells != null && cells.Length == world.width*world.height;
        public double CellVolumeM3 => world == null ? 1 : Math.Max(.001,world.cellSize*world.cellSize*world.crossSectionDepthM);
        int Index(Vector2Int cell) => cell.y*world.width+cell.x;
        bool Valid(Vector2Int cell) => IsInitialized && world.IsInside(cell) && open[Index(cell)];

        public void Initialize(DeepPressureWorld source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (IsInitialized && source == world) return;
            world = source; int count = world.width*world.height;
            cells = new GasMixture[count]; deltas = new GasMixture[count];
            temperatures = new double[count]; energyDelta = new double[count]; open = new bool[count];
            for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
            {
                var cell = new Vector2Int(x,y); int i = Index(cell);
                open[i] = world.GetTerrain(x,y) == TerrainKind.Empty;
                var room = open[i] ? world.RoomAt(cell) : null;
                temperatures[i] = room == null ? world.defaultTemperatureC : room.temperatureC;
                if (room != null && room.cellCount > 0)
                {
                    cells[i] = room.gas.Scaled(1d/room.cellCount);
                    // Older authored starter air omitted trace CO2. Replace an equal
                    // amount of N2 once at initialization; this never adds gas or runs
                    // during restore, diffusion or excavation.
                    double total=cells[i].Total;
                    if(total>0&&cells[i].carbonDioxide<=1e-12&&cells[i].nitrogen/total>.7&&cells[i].oxygen/total>.18)
                    {double trace=total*Mathf.Clamp(initialCarbonDioxideFraction,0,.02f);cells[i].nitrogen-=trace;cells[i].carbonDioxide+=trace;}
                }
            }
            SyncRooms();
        }
        public void ResetFromRooms()
        {
            if (world == null) return;
            cells = null; Initialize(world);
        }
        public GasMixture Sample(Vector2Int cell) => Valid(cell) ? cells[Index(cell)] : default;
        public double TemperatureC(Vector2Int cell) => Valid(cell) ? temperatures[Index(cell)] : world == null ? 18 : world.defaultTemperatureC;
        public double PressureKPa(Vector2Int cell) => Sample(cell).PressureKPa(CellVolumeM3,TemperatureC(cell));
        public void SetTemperature(Vector2Int cell,double value)
        {
            if (!Valid(cell) || double.IsNaN(value) || double.IsInfinity(value)) return;
            temperatures[Index(cell)] = Math.Max(-272.15,Math.Min(2000,value));
        }
        public GasMixture Take(Vector2Int cell,double maximumMol)
        {
            if (!Valid(cell) || double.IsNaN(maximumMol) || maximumMol <= 0) return default;
            int i = Index(cell); double total = cells[i].Total;
            if (total <= 1e-12) return default;
            var packet = cells[i].Scaled(Math.Min(maximumMol,total)/total);
            cells[i] -= packet; return packet;
        }
        public GasMixture TakeSpecies(Vector2Int cell,int species,double maximumMol)
        {
            if (!Valid(cell) || species < 0 || species >= GasMixture.SpeciesCount || double.IsNaN(maximumMol) || maximumMol <= 0) return default;
            int i = Index(cell); GasMixture packet = default;
            packet[species] = Math.Min(maximumMol,Math.Max(0,cells[i][species]));
            cells[i] -= packet; return packet;
        }
        public void Add(Vector2Int cell,GasMixture packet,double sourceTemperatureC = double.NaN)
        {
            if (!Valid(cell) || !packet.IsFiniteAndNonnegative || packet.Total <= 0) return;
            int i = Index(cell); double previous = cells[i].Total;
            double source = double.IsNaN(sourceTemperatureC) || double.IsInfinity(sourceTemperatureC) ? temperatures[i] : Math.Max(-272.15,Math.Min(2000,sourceTemperatureC));
            temperatures[i] = (temperatures[i]*previous+source*packet.Total)/(previous+packet.Total);
            cells[i] += packet;
        }

        /// <summary>A fan disperses its finite packet through nearby connected air. Capacity
        /// is computed before injection, including both total and oxygen partial pressure.
        /// Solid walls are never crossed, and the returned packet is the only accepted gas.</summary>
        public GasMixture AddDistributed(Vector2Int source,GasMixture packet,double maximumPressureKPa,
            double targetOxygenKPa = double.PositiveInfinity,double sourceTemperatureC = double.NaN,int radius = 2)
        {
            if (!Valid(source) || !packet.IsFiniteAndNonnegative || packet.Total <= 0) return default;
            var candidates = new List<Vector2Int>(); var capacities = new List<double>();
            double capacity = 0,oxygenFraction = packet.oxygen/packet.Total;
            foreach (var cell in ConnectedArea(source,radius))
            {
                var gas = Sample(cell); double temperature = TemperatureC(cell);
                double available = Math.Max(0,GasMixture.FromPressure(maximumPressureKPa,CellVolumeM3,temperature,Vector4.one).Total-gas.Total);
                if (oxygenFraction > 1e-12 && !double.IsPositiveInfinity(targetOxygenKPa))
                {
                    double desired = GasMixture.FromPressure(targetOxygenKPa,CellVolumeM3,temperature,new Vector4(1,0,0,0)).oxygen;
                    available = Math.Min(available,Math.Max(0,desired-gas.oxygen)/oxygenFraction);
                }
                if (available > 0) { candidates.Add(cell); capacities.Add(available); capacity += available; }
            }
            if (capacity <= 1e-12) return default;
            var accepted = packet.Scaled(Math.Min(1,capacity/packet.Total));
            for (int i = 0; i < candidates.Count; i++) Add(candidates[i],accepted.Scaled(capacities[i]/capacity),sourceTemperatureC);
            return accepted;
        }

        /// <summary>Photosynthesis removes one CO2 molecule for each O2 molecule
        /// released. Carbon is retained in the finite culture; no gas appears when
        /// there is no available CO2. The exchange never changes total gas moles.</summary>
        public double Photosynthesize(Vector2Int source,double maximumMol,double targetOxygenKPa = 23,int radius = 3)
        {
            if (!Valid(source) || maximumMol <= 0 || double.IsNaN(maximumMol)) return 0;
            var area = ConnectedArea(source,radius);var capacities = new double[area.Count];double available=0;
            for(int n=0;n<area.Count;n++)
            {
                int i=Index(area[n]);
                double desired=GasMixture.FromPressure(targetOxygenKPa,CellVolumeM3,temperatures[i],new Vector4(1,0,0,0)).oxygen;
                capacities[n]=Math.Min(cells[i].carbonDioxide,Math.Max(0,desired-cells[i].oxygen));available+=capacities[n];
            }
            if(available<=1e-12)return 0;
            double converted=Math.Min(available,maximumMol);
            for(int n=0;n<area.Count;n++)
            {
                int i=Index(area[n]);double amount=converted*capacities[n]/available;
                cells[i].carbonDioxide-=amount;cells[i].oxygen+=amount;
            }
            return converted;
        }

        List<Vector2Int> ConnectedArea(Vector2Int source,int radius)
        {
            var result=new List<Vector2Int>();if(!Valid(source))return result;
            var queue=new Queue<Vector2Int>();var visited=new HashSet<Vector2Int>();queue.Enqueue(source);visited.Add(source);
            Vector2Int[] directions={Vector2Int.up,Vector2Int.left,Vector2Int.right,Vector2Int.down};
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();result.Add(cell);
                foreach(var direction in directions)
                {
                    var next=cell+direction;
                    if(Math.Abs(next.x-source.x)+Math.Abs(next.y-source.y)>Math.Max(0,radius)||!Valid(next)||!visited.Add(next))continue;
                    queue.Enqueue(next);
                }
            }
            return result;
        }

        void UpgradeLegacyDiffusion()
        {
            // The former default made a throttled outlet look productive while a worker
            // a few tiles away received too little fresh air. Preserve custom pacing.
            var session=world==null?null:world.GetComponent<DeepGameSession>();
            if (Mathf.Approximately(diffusionPerSecond,1.25f)) diffusionPerSecond = session==null?DefaultDiffusionPerSecond:session.SimulationTuning.diffusionPerSecond;
        }

        public void Tick(float seconds)
        {
            if (!IsInitialized || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            // At most 24% per neighbor, below the four-neighbor positivity bound.
            // Substeps preserve the visible pace of a pressure front at high game speed.
            double remaining = Math.Min(8,seconds);
            while (remaining > 1e-7)
            {
                double dt = Math.Min(.1,remaining),mix = Math.Min(.24,Math.Max(0,diffusionPerSecond)*dt);
                Array.Clear(deltas,0,deltas.Length); Array.Clear(energyDelta,0,energyDelta.Length);
                for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
                {
                    int a = y*world.width+x; if (!open[a]) continue;
                    if (x+1 < world.width && open[a+1]) TransferPair(a,a+1,mix,dt);
                    if (y+1 < world.height && open[a+world.width]) TransferPair(a,a+world.width,mix,dt);
                }
                for (int i = 0; i < cells.Length; i++)
                {
                    if (!open[i]) continue;
                    double energy = cells[i].Total*(temperatures[i]+273.15)+energyDelta[i];
                    cells[i] += deltas[i];
                    // Roundoff only; the flux bound prevents meaningful negative inventory.
                    for (int s = 0; s < GasMixture.SpeciesCount; s++) if (cells[i][s] < 0 && cells[i][s] > -1e-10) cells[i][s] = 0;
                    if (cells[i].Total > 1e-10) temperatures[i] = Math.Max(-272.15,energy/cells[i].Total-273.15);
                }
                remaining -= dt;
            }
            SyncRooms();
        }
        void TransferPair(int a,int b,double mix,double dt)
        {
            GasMixture flux = (cells[a]-cells[b]).Scaled(mix);
            deltas[a] -= flux; deltas[b] += flux;
            double transported = 0;
            for (int s = 0; s < GasMixture.SpeciesCount; s++) transported += flux[s]*((flux[s] >= 0 ? temperatures[a] : temperatures[b])+273.15);
            double conduction = (temperatures[a]-temperatures[b])*Math.Min(cells[a].Total,cells[b].Total)*Math.Min(.1,Math.Max(0,thermalConductionPerSecond)*dt);
            energyDelta[a] -= transported+conduction; energyDelta[b] += transported+conduction;
        }

        /// <summary>Call after the world has rebuilt room topology. New void starts empty;
        /// gas displaced by a placed solid moves to adjacent void or the world's sealed inventory.</summary>
        public void RebuildAfterTerrainChange()
        {
            if (!IsInitialized) { if (world != null) Initialize(world); return; }
            var wasOpen = (bool[])open.Clone();
            for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
            {
                int i = y*world.width+x; open[i] = world.GetTerrain(x,y) == TerrainKind.Empty;
                if (open[i] && !wasOpen[i]) { cells[i] = default; temperatures[i] = world.defaultTemperatureC; }
            }
            for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
            {
                int i = y*world.width+x;
                if (open[i] || !wasOpen[i] || cells[i].Total <= 0) continue;
                var displaced = cells[i]; cells[i] = default;
                int neighbors = 0;
                if (x > 0 && open[i-1]) neighbors++;
                if (x+1 < world.width && open[i+1]) neighbors++;
                if (y > 0 && open[i-world.width]) neighbors++;
                if (y+1 < world.height && open[i+world.width]) neighbors++;
                if (neighbors == 0) { world.displacedGas += displaced; continue; }
                var share = displaced.Scaled(1d/neighbors);
                if (x > 0 && open[i-1]) Add(new Vector2Int(x-1,y),share,temperatures[i]);
                if (x+1 < world.width && open[i+1]) Add(new Vector2Int(x+1,y),share,temperatures[i]);
                if (y > 0 && open[i-world.width]) Add(new Vector2Int(x,y-1),share,temperatures[i]);
                if (y+1 < world.height && open[i+world.width]) Add(new Vector2Int(x,y+1),share,temperatures[i]);
            }
            SyncRooms();
        }

        public void SyncRooms()
        {
            if (!IsInitialized) return;
            foreach (var room in world.Rooms)
            {
                GasMixture total = default; double thermal = 0;
                foreach (var cell in room.cells)
                {
                    int i = Index(cell); total += cells[i]; thermal += cells[i].Total*(temperatures[i]+273.15);
                }
                room.gas = total;
                if (total.Total > 1e-10) room.temperatureC = thermal/total.Total-273.15;
            }
        }
        public GasMixture TotalInventory()
        {
            GasMixture total = default;
            if (cells != null) foreach (var gas in cells) total += gas;
            return total;
        }
        public GasMixture[] CaptureCells() => cells == null ? null : (GasMixture[])cells.Clone();
        public double[] CaptureTemperatures() => temperatures == null ? null : (double[])temperatures.Clone();
        public void RestoreCells(GasMixture[] saved,double[] savedTemperatures = null)
        {
            if (!IsInitialized || saved == null || saved.Length != cells.Length || (savedTemperatures != null && savedTemperatures.Length != cells.Length))
                throw new InvalidOperationException("逐格气体存档尺寸不匹配");
            for (int i = 0; i < saved.Length; i++)
            {
                if (!saved[i].IsFiniteAndNonnegative || (!open[i] && saved[i].Total > 1e-8)) throw new InvalidOperationException("逐格气体存档包含无效库存");
                if (savedTemperatures != null && (double.IsNaN(savedTemperatures[i]) || double.IsInfinity(savedTemperatures[i]) || savedTemperatures[i] < -272.15)) throw new InvalidOperationException("逐格气体存档温度无效");
            }
            cells = (GasMixture[])saved.Clone();
            if (savedTemperatures != null) temperatures = (double[])savedTemperatures.Clone();
            UpgradeLegacyDiffusion();
            SyncRooms();
        }
    }
}
