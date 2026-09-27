using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public enum DeepHazardKind { PressureShock, Combustion }
    public sealed class DeepHazardEvent
    {
        public DeepHazardKind kind;
        public Vector2Int cell;
        public Vector2 direction;
        public float strength, time;
        public string message;
    }
    public sealed partial class DeepGameSession
    {
        [Header("Atmospheric hazards — game balance values")]
        public bool hazardsEnabled = true;
        [Min(1)] public float breachShockThresholdKPa = 40;
        public string LastHazardMessage { get; private set; }
        public int HazardEventCount { get; private set; }
        public readonly List<DeepHazardEvent> HazardEvents = new List<DeepHazardEvent>();
        readonly Dictionary<Vector2Int,BreachSnapshot> pendingBreaches = new Dictionary<Vector2Int,BreachSnapshot>();
        readonly Dictionary<Vector2Int,float> lastIgnitionEvent = new Dictionary<Vector2Int,float>();
        sealed class BreachSnapshot { public double difference; public Vector2 direction; }

        double LocalPressure(Vector2Int cell)
        {
            var field = Atmosphere;
            if (field != null && field.IsInitialized) return field.PressureKPa(cell);
            var room = world.RoomAt(cell); return room == null ? 0 : room.PressureKPa;
        }
        BreachSnapshot MeasureBreach(Vector2Int cell)
        {
            double high = -1, low = double.MaxValue; Vector2Int highCell = cell, lowCell = cell; int openSides = 0;
            foreach (var direction in PowerNeighbors)
            {
                Vector2Int neighbor = cell+direction;
                if (!world.IsInside(neighbor) || world.GetTerrain(neighbor.x,neighbor.y) != TerrainKind.Empty) continue;
                double pressure = LocalPressure(neighbor); openSides++;
                if (pressure > high) { high = pressure; highCell = neighbor; }
                if (pressure < low) { low = pressure; lowCell = neighbor; }
            }
            return openSides >= 2 ? new BreachSnapshot { difference = high-low,direction = ((Vector2)(lowCell-highCell)).normalized } : null;
        }
        public string ExcavationRisk(Vector2Int cell)
        {
            // Preserve callers/save compatibility; hidden chambers have no remote warning.
            return string.Empty;
        }
        public void PrepareExcavationHazard(Vector2Int cell)
        {
            pendingBreaches.Remove(cell);
            if (!hazardsEnabled || world == null) return;
            breachShockThresholdKPa=Mathf.Max(1,SimulationTuning.pressureShockThresholdKPa);
            var breach = MeasureBreach(cell);
            if (breach != null && breach.difference >= breachShockThresholdKPa) pendingBreaches[cell] = breach;
        }
        public void ResolveExcavationHazard(Vector2Int cell)
        {
            if (!pendingBreaches.TryGetValue(cell,out var breach)) return;
            pendingBreaches.Remove(cell);
            float strength = Mathf.Clamp((float)(breach.difference/breachShockThresholdKPa),1,4);
            AffectNearbyWorkers(cell,breach.direction,strength);
            RecordHazard(DeepHazardKind.PressureShock,cell,breach.direction,strength,"凿穿高压隔层："+breach.difference.ToString("0")+" kPa 压差冲击");
        }
        void AffectNearbyWorkers(Vector2Int cell,Vector2 direction,float strength)
        {
            foreach (var worker in Workers)
            {
                if (worker == null || !worker.IsAlive || Vector2Int.Distance(worker.Cell,cell) > 2.5f+strength*.25f) continue;
                float distance=Vector2Int.Distance(worker.Cell,cell);
                float exposure=Mathf.Lerp(1,.35f,Mathf.Clamp01((distance-1)/2.5f));
                DamageWorker(worker,strength*strength*SimulationTuning.pressureShockDamageScale*exposure,"气压冲击"); if(!worker.IsAlive)continue;
                worker.airReserveSeconds = Mathf.Max(0,worker.airReserveSeconds-8*strength);
                Vector2Int step = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y) ? new Vector2Int(direction.x >= 0 ? 1 : -1,0) : new Vector2Int(0,direction.y >= 0 ? 1 : -1);
                var target = worker.Cell+step;
                if (!IsPassable(target)) continue;
                RequeueWorker(worker,"受到气压冲击，重新规划工作路线"); worker.TeleportToCell(target);
            }
        }
        void RecordHazard(DeepHazardKind kind,Vector2Int cell,Vector2 direction,float strength,string message)
        {
            LastHazardMessage = message; HazardEventCount++;
            HazardEvents.Add(new DeepHazardEvent { kind = kind,cell = cell,direction = direction,strength = strength,time = SimulationTime,message = message });
            if (HazardEvents.Count > 20) HazardEvents.RemoveAt(0);
            DeepHazardEffects.Spawn(this,kind,cell,direction,strength);
        }
        void TickGasHazards(float dt)
        {
            if (!hazardsEnabled || !lifeSupportEnabled || dt <= 0) return;
            var field = Atmosphere;
            var visited = new HashSet<Vector2Int>();
            foreach (var building in Buildings)
            {
                if (building == null || building.definition == null || !building.IsOperational) continue;
                var role = building.definition.role;
                if (role != DeepBuildingRole.Generator && role != DeepBuildingRole.Fabricator) continue;
                foreach (Vector2Int cell in building.Bounds.allPositionsWithin)
                {
                    if (!visited.Add(cell) || world.GetTerrain(cell.x,cell.y) != TerrainKind.Empty) continue;
                    var room = world.RoomAt(cell); if (room == null) continue;
                    bool local = field != null && field.IsInitialized;
                    var before = local ? field.Sample(cell) : room.gas;
                    var after = before;
                    if (!DeepGasChemistry.TryIgnite(ref after,dt,true,out double burned)) continue;
                    double temperature = local ? field.TemperatureC(cell) : room.temperatureC;
                    double rise = Math.Min(80,burned*2000/Math.Max(.001,before.Total));
                    if (local)
                    {
                        field.TakeSpecies(cell,4,burned); field.TakeSpecies(cell,0,burned*2);
                        field.Add(cell,new GasMixture { carbonDioxide = burned,waterVapour = burned*2 });
                        field.SetTemperature(cell,Math.Min(350,temperature+rise));
                    }
                    else { room.gas = after; room.temperatureC = Math.Min(350,temperature+rise); }
                    if (!lastIgnitionEvent.TryGetValue(cell,out float previous) || SimulationTime-previous > .8f)
                    {
                        lastIgnitionEvent[cell] = SimulationTime;
                        float strength = Mathf.Clamp((float)(rise/20),.5f,3);
                        AffectNearbyWorkers(cell,Vector2.up,strength);
                        RecordHazard(DeepHazardKind.Combustion,cell,Vector2.up,strength,"可燃气遇通电热作设备燃烧：消耗氧气并升温增压");
                    }
                }
            }
            if (field != null && field.IsInitialized) field.SyncRooms();
        }
    }
}
