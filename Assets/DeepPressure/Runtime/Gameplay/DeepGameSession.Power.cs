using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public sealed partial class DeepGameSession
    {
        [Header("Physical power distribution")]
        [Tooltip("Legacy test fixtures may disable this. All authored colony scenes use completed wires.")]
        public bool useWiredPower = true;
        [HideInInspector] public int powerContentRevision;
        public List<Vector2Int> completedWireCells = new List<Vector2Int>();
        [Min(.1f)] public float wireWorkSeconds = 1.3f;
        public float StoredEnergy { get; private set; }
        public float StorageEnergyCapacity { get; private set; }
        public int PowerCircuitCount => powerCircuits.Count;
        public int WireRevision { get; private set; }
        public bool showPowerOverlay;
        readonly Dictionary<Vector2Int,int> wireCircuit = new Dictionary<Vector2Int,int>();
        readonly List<PowerCircuit> powerCircuits = new List<PowerCircuit>();
        readonly Dictionary<DeepBuildingInstance,int> buildingCircuit = new Dictionary<DeepBuildingInstance,int>();
        readonly HashSet<Vector2Int> liveWires = new HashSet<Vector2Int>();
        int lastWireCount = -1, lastBuildingCount = -1;
        bool powerTopologyDirty = true;
        static readonly Vector2Int[] PowerNeighbors = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        sealed class PowerCircuit
        {
            public readonly List<Vector2Int> cells = new List<Vector2Int>();
            public readonly List<DeepBuildingInstance> buildings = new List<DeepBuildingInstance>();
        }

        public bool HasWire(Vector2Int cell) => completedWireCells != null && completedWireCells.Contains(cell);
        public bool WireIsPowered(Vector2Int cell) => liveWires.Contains(cell);
        public Vector2Int PowerTerminal(DeepBuildingInstance building)
        {
            if (building == null || building.definition == null) return default;
            foreach (var port in building.definition.ports)
                if (port.kind == DeepPortKind.PowerIn || port.kind == DeepPortKind.PowerOut)
                    return building.origin + new Vector2Int(Mathf.FloorToInt(port.localPosition.x),Mathf.FloorToInt(port.localPosition.y));
            return building.origin;
        }
        public void InvalidatePowerTopology() { powerTopologyDirty = true; WireRevision++; }
        public bool CanWire(Vector2Int cell,out string reason)
        {
            InitializeSession();
            if (world == null || !world.IsInside(cell) || !IsKnown(cell)) { reason = "只能在已探明区域铺线"; return false; }
            if (HasWire(cell)) { reason = "这里已有电线"; return false; }
            foreach (var order in Orders)
                if (!order.IsTerminal && order.kind == DeepWorkKind.Wire && order.targetCell == cell) { reason = "已安排此格电线施工"; return false; }
            var alloy = catalog == null ? null : catalog.FindItem("alloy");
            if (alloy == null) { reason = "电线材料尚未配置"; return false; }
            return inventory.CanAfford(new[] { new DeepItemAmount(alloy,1) },out reason);
        }
        public bool RequestWire(Vector2Int cell,out string reason)
        {
            if (!CanWire(cell,out reason)) return false;
            if (!inventory.TryReserve(new[] { new DeepItemAmount(catalog.FindItem("alloy"),1) },out var reservation,out reason)) return false;
            var order = NewOrder(DeepWorkKind.Wire,"铺设电线",cell,wireWorkSeconds);
            order.reservation = reservation; reason = "已安排铺线；完工后接通相邻电线"; return true;
        }
        public bool RemoveWire(Vector2Int cell,out string reason)
        {
            if (!HasWire(cell)) { reason = "此处没有已完成电线"; return false; }
            completedWireCells.Remove(cell); InvalidatePowerTopology(); UpdatePower(0);
            reason = "电线已拆除；所在支路重新计算供电"; return true;
        }
        List<Vector2Int> WireWorkPositions(Vector2Int cell)
        {
            var result = WorkPositions(new RectInt(cell,Vector2Int.one));
            if (IsStandable(cell)) result.Add(cell);
            return result;
        }
        bool CompleteWireOrder(DeepWorkOrder order,out string reason)
        {
            reason = string.Empty;
            if (world == null || !world.IsInside(order.targetCell)) { reason = "电线位置无效"; return false; }
            if (HasWire(order.targetCell)) { reason = "此格已有电线，取消重复施工可退回物料"; return false; }
            completedWireCells.Add(order.targetCell); inventory.Commit(order.reservation);
            InvalidatePowerTopology(); UpdatePower(0); return true;
        }
        public string PowerStatus(DeepBuildingInstance building)
        {
            if (building == null || building.definition == null) return string.Empty;
            var def = building.definition;
            if (!building.isOn) return "已关闭";
            bool electrical = def.powerRequired > 0 || def.role == DeepBuildingRole.Generator || def.role == DeepBuildingRole.Battery;
            if (!electrical) return "无需电力";
            if (useWiredPower && !buildingCircuit.ContainsKey(building)) return "未接电线 · 接口格 " + PowerTerminal(building);
            if (def.role == DeepBuildingRole.Generator)
            {
                if (def.fuelItem != null && building.fuelSecondsRemaining <= .0001f && inventory.GetAmount(def.fuelItem) == 0) return "燃料耗尽 · 开采页岩补充燃料";
                return building.powered ? "发电中 · 本线路供电" : "待机 · 无负载或电池已满";
            }
            if (def.role == DeepBuildingRole.Battery) return "储能 " + building.batteryEnergy.ToString("0") + " / " + def.batteryCapacity.ToString("0") + " J";
            return building.powered ? "供电正常" : "本线路功率不足";
        }
        public DeepItemAmount[] ExcavationOutputs(Vector2Int cell)
        {
            var fuel = catalog == null ? null : catalog.FindItem("fuel");
            var alloy = catalog == null ? null : catalog.FindItem("alloy");
            if (world.GetTerrain(cell.x,cell.y) == TerrainKind.Shale && fuel != null)
                return excavationItem == null ? new[] { new DeepItemAmount(fuel,2) } : new[] { new DeepItemAmount(fuel,2),new DeepItemAmount(excavationItem,2) };
            if (world.GetTerrain(cell.x,cell.y) == TerrainKind.Metal && alloy != null) return new[] { new DeepItemAmount(alloy,1) };
            return excavationItem == null || excavationYield <= 0 ? System.Array.Empty<DeepItemAmount>() : new[] { new DeepItemAmount(excavationItem,excavationYield) };
        }
        void RebuildPowerCircuits()
        {
            powerTopologyDirty = false; lastWireCount = completedWireCells.Count; lastBuildingCount = Buildings.Count;
            wireCircuit.Clear(); buildingCircuit.Clear(); powerCircuits.Clear();
            var remaining = new HashSet<Vector2Int>(completedWireCells);
            var queue = new Queue<Vector2Int>();
            foreach (var origin in completedWireCells)
            {
                if (!remaining.Remove(origin)) continue;
                int id = powerCircuits.Count; var circuit = new PowerCircuit(); powerCircuits.Add(circuit); queue.Enqueue(origin);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue(); circuit.cells.Add(cell); wireCircuit[cell] = id;
                    foreach (var direction in PowerNeighbors) if (remaining.Remove(cell+direction)) queue.Enqueue(cell+direction);
                }
            }
            foreach (var building in Buildings)
            {
                if (building == null || !building.isConstructed || building.definition == null) continue;
                var def = building.definition;
                if (def.powerRequired <= 0 && def.role != DeepBuildingRole.Generator && def.role != DeepBuildingRole.Battery) continue;
                if (wireCircuit.TryGetValue(PowerTerminal(building),out int id)) { powerCircuits[id].buildings.Add(building); buildingCircuit[building] = id; }
            }
        }
        void UpdateWiredPower(float dt)
        {
            completedWireCells = completedWireCells ?? new List<Vector2Int>();
            if (powerTopologyDirty || lastWireCount != completedWireCells.Count || lastBuildingCount != Buildings.Count) RebuildPowerCircuits();
            PowerProduction = PowerDemand = StoredEnergy = StorageEnergyCapacity = 0; liveWires.Clear();
            foreach (var building in Buildings)
            {
                if (building == null || building.definition == null) continue;
                var def = building.definition;
                building.powered = building.isConstructed && building.isOn && def.powerRequired <= 0 && def.role != DeepBuildingRole.Generator && def.role != DeepBuildingRole.Battery;
                if (building.isConstructed && building.isOn) PowerDemand += def.powerRequired;
                if (def.role == DeepBuildingRole.Battery) building.batteryEnergy = Mathf.Clamp(building.batteryEnergy,0,def.batteryCapacity);
            }
            foreach (var circuit in powerCircuits) SimulateCircuit(circuit,dt);
            foreach (var building in Buildings)
            {
                if (building == null || building.definition == null) continue;
                if (building.definition.role == DeepBuildingRole.Battery && building.isConstructed)
                { StoredEnergy += building.batteryEnergy; StorageEnergyCapacity += building.definition.batteryCapacity; }
                building.RefreshVisualState();
            }
            if (Application.isPlaying && world != null && GetComponent<DeepPowerWireVisual>() == null)
            { var visual = gameObject.AddComponent<DeepPowerWireVisual>(); visual.session = this; }
        }
        void SimulateCircuit(PowerCircuit circuit,float dt)
        {
            float demand = 0, batteryHeadroom = 0, batteryPower = 0, generation = 0;
            foreach (var building in circuit.buildings)
            {
                if (!building.isOn) continue;
                var def = building.definition; demand += def.powerRequired;
                if (def.role == DeepBuildingRole.Battery)
                {
                    batteryHeadroom += Mathf.Min(def.batteryTransferRate,dt > 0 ? (def.batteryCapacity-building.batteryEnergy)/dt : def.batteryCapacity-building.batteryEnergy);
                    batteryPower += Mathf.Min(def.batteryTransferRate,dt > 0 ? building.batteryEnergy/dt : building.batteryEnergy);
                }
            }
            foreach (var building in circuit.buildings)
            {
                var def = building.definition;
                if (!building.isOn || def.role != DeepBuildingRole.Generator || demand+batteryHeadroom <= generation+.001f) continue;
                float fraction = FuelFraction(building,dt); float output = def.powerGenerated*fraction;
                building.powered = output > .0001f; generation += output;
            }
            float available = generation+batteryPower, served = 0;
            // Stable building order gives predictable load shedding rather than shutting the whole colony down.
            foreach (var building in circuit.buildings)
            {
                var def = building.definition;
                if (!building.isOn || def.powerRequired <= 0) continue;
                if (served+def.powerRequired <= available+.001f) { building.powered = true; served += def.powerRequired; }
            }
            float surplus = generation-served;
            foreach (var building in circuit.buildings)
            {
                var def = building.definition;
                if (!building.isOn || def.role != DeepBuildingRole.Battery) continue;
                building.powered = generation > .001f || building.batteryEnergy > .001f;
                if (dt <= 0) continue;
                float change = surplus >= 0 ? Mathf.Min(surplus,def.batteryTransferRate,(def.batteryCapacity-building.batteryEnergy)/dt) : -Mathf.Min(-surplus,def.batteryTransferRate,building.batteryEnergy/dt);
                building.batteryEnergy = Mathf.Clamp(building.batteryEnergy+change*dt,0,def.batteryCapacity); surplus -= change;
            }
            PowerProduction += generation + Mathf.Max(0,served-generation);
            if (generation+batteryPower > .001f) foreach (var cell in circuit.cells) liveWires.Add(cell);
        }
        float FuelFraction(DeepBuildingInstance building,float dt)
        {
            var def = building.definition;
            if (def.fuelItem == null || def.fuelUnitsPerSecond <= 0) return 1;
            float availableSeconds = Mathf.Max(0,building.fuelSecondsRemaining);
            if (dt <= 0) return availableSeconds > 0 || inventory.GetAmount(def.fuelItem) > 0 ? 1 : 0;
            // Purchase a whole fuel unit before it produces any energy. Unspent burn time is saved per generator.
            float secondsPerUnit = 1/def.fuelUnitsPerSecond;
            int needed = Mathf.Max(0,Mathf.CeilToInt((dt-availableSeconds)/secondsPerUnit));
            int burn = Mathf.Min(needed,inventory.GetAmount(def.fuelItem));
            if (burn > 0 && inventory.TryConsume(def.fuelItem,burn)) availableSeconds += burn*secondsPerUnit;
            float runningSeconds = Mathf.Min(dt,availableSeconds);
            building.fuelSecondsRemaining = Mathf.Max(0,availableSeconds-runningSeconds);
            return runningSeconds/dt;
        }
    }
}
