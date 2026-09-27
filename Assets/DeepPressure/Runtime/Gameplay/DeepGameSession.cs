using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    [DefaultExecutionOrder(-150)]
    public sealed partial class DeepGameSession : MonoBehaviour
    {
        public DeepPressureWorld world;
        public GasNetworkSimulator network;
        public DeepGameplayCatalog catalog;
        public DeepInventory inventory = new DeepInventory();
        public DeepItemAmount[] startingInventory = Array.Empty<DeepItemAmount>();
        public string[] initialUnlockedTechIds = Array.Empty<string>();
        [Min(0)] public int baseStorageCapacity = 300;
        public DeepItemDefinition excavationItem;
        [Min(0)] public int excavationYield = 3;
        [Min(.1f)] public float excavationSeconds = 3;
        public DeepTerrainTile constructedFloorTile;
        public Material playerPipeMaterial;
        public bool menuOpen,paused;
        [Range(.25f,6)] public float speed = 1;
        public float DeltaTime { get; private set; }
        public float SimulationTime { get; private set; }
        public float PowerProduction { get; private set; }
        public float PowerDemand { get; private set; }
        public bool HasPower => PowerProduction+.001f >= PowerDemand;
        public bool IsSimulationPaused => menuOpen || paused;
        [NonSerialized] public List<DeepWorker> Workers = new List<DeepWorker>();
        [NonSerialized] public List<DeepWorkOrder> Orders = new List<DeepWorkOrder>();
        [NonSerialized] public List<DeepBuildingInstance> Buildings = new List<DeepBuildingInstance>();
        readonly HashSet<string> technologies = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<Vector2Int,DeepWorkOrder> placementClaims = new Dictionary<Vector2Int,DeepWorkOrder>();
        readonly Dictionary<Vector2Int,DeepBuildingInstance> occupancy = new Dictionary<Vector2Int,DeepBuildingInstance>();
        readonly HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>(), ladderCells = new HashSet<Vector2Int>();
        DeepExploration exploration;
        [NonSerialized] bool initialized;
        int nextOrderId = 1;
        void Start() => InitializeSession();
        public void InitializeSession()
        {
            if (world == null) world = GetComponentInParent<DeepPressureWorld>();
            if (world == null) return;
            world.EnsureRuntimeState();
            if (initialized) return;
            if (network == null) network = world.GetComponent<GasNetworkSimulator>();
            if(network!=null)network.EnsureInitialized();
            exploration = world.GetComponent<DeepExploration>();
            initialized = true; inventory = inventory ?? new DeepInventory();
            nextPrintingTime = Mathf.Max(1,printingIntervalSeconds);
            Workers.Clear(); Workers.AddRange(world.GetComponentsInChildren<DeepWorker>(true));
            foreach (var worker in Workers) worker.session = this;
            Buildings.Clear(); Buildings.AddRange(world.GetComponentsInChildren<DeepBuildingInstance>(true));
            foreach (var building in Buildings) building.session = this;
            RebuildOccupancy();
            foreach (string id in initialUnlockedTechIds) if (!string.IsNullOrWhiteSpace(id)) technologies.Add(id);
            RefreshStorageCapacity();
            foreach (var item in startingInventory)
                if (!inventory.TryAdd(item.item,item.amount)) Debug.LogError("Initial warehouse item does not fit or is invalid: "+(item.item == null ? "null" : item.item.id),this);
            if (excavationItem == null && catalog != null) excavationItem = catalog.FindItem("ore");
            UpdatePower(0);
            if(lifeSupportEnabled){var field=Atmosphere;}
            RefreshExplorationVisibility();
            RestoreSessionAfterReload();
        }
        void Update()
        {
            InitializeSession();
            if (!initialized) return;
            Time.timeScale = IsSimulationPaused ? 0 : speed;
            Tick(Mathf.Min(Time.unscaledDeltaTime,.2f));
        }
        public void Tick(float unscaledSeconds)
        {
            InitializeSession();
            if (!initialized) return;
            if (network != null) { network.paused = IsSimulationPaused; network.simulationSpeed = speed; }
            DeltaTime = IsSimulationPaused ? 0 : Mathf.Max(0,unscaledSeconds)*Mathf.Clamp(speed,.25f,6);
            if (DeltaTime <= 0) return;
            SimulationTime += DeltaTime; UpdatePower(DeltaTime);
            TickLifeSupport(DeltaTime);
            TickProductionTargets();
            foreach (var order in Orders)
                if (order.state == DeepWorkState.Blocked && order.worker == null && order.Progress >= 1 && SimulationTime >= order.nextRetryTime)
                    TryComplete(order);
            foreach (var worker in Workers)
            {
                if (worker == null || !worker.isActiveAndEnabled || !worker.IsAlive) continue;
                if (worker.ApplyGravity(DeltaTime)) continue;
                if (worker.currentOrder == null) ClaimOrder(worker);
                if (worker.currentOrder != null) TickWorker(worker,DeltaTime);
            }
            RefreshExplorationVisibility();
        }
        public Vector3 FootPosition(Vector2Int cell) => world.CellToWorld(cell)-world.transform.up*world.cellSize*.5f;
        public Vector3 BuildingPosition(Vector2Int cell) => world.CellToWorld(cell)-world.transform.TransformVector(new Vector3(.5f,.5f,0)*world.cellSize);
        public bool IsTechUnlocked(string id) => string.IsNullOrWhiteSpace(id) || technologies.Contains(id);
        public DeepBuildingInstance BuildingAt(Vector2Int cell)
        {
            return occupancy.TryGetValue(cell,out var building) ? building : null;
        }
        public void RebuildOccupancy()
        {
            occupancy.Clear(); blockedCells.Clear(); ladderCells.Clear();
            foreach (var building in Buildings)
            {
                if (building == null || !building.isConstructed || building.definition == null) continue;
                foreach (Vector2Int cell in building.Bounds.allPositionsWithin)
                {
                    occupancy[cell] = building;
                    if (building.definition.blocksMovement) blockedCells.Add(cell);
                    if (building.definition.role == DeepBuildingRole.Ladder) ladderCells.Add(cell);
                }
            }
            NotifyNavigationChanged();
            InvalidatePowerTopology();
        }
        public bool IsLadder(Vector2Int cell)
        {
            return supportedLadderCells.Contains(cell);
        }
        public bool IsPassable(Vector2Int cell)
        {
            if (world == null || !IsKnown(cell) || !ClearBodyCell(cell) || !ClearBodyCell(cell+Vector2Int.up)) return false;
            return true;
        }
        bool ClearBodyCell(Vector2Int cell)
        {
            if (!world.IsInside(cell) || world.GetTerrain(cell.x,cell.y) != TerrainKind.Empty || blockedCells.Contains(cell)) return false;
            // Planning reserves construction space, but only completed structures obstruct movement.
            return true;
        }
        public bool IsStandable(Vector2Int cell) => IsPassable(cell) && (IsLadder(cell) || IsSupport(cell+Vector2Int.down));
        bool IsSupport(Vector2Int cell)
        {
            if (world.GetTerrain(cell.x,cell.y) != TerrainKind.Empty) return true;
            return blockedCells.Contains(cell);
        }
        bool IsKnown(Vector2Int cell) => exploration == null || exploration.IsVisible(cell);
        public void ToggleBuilding(DeepBuildingInstance building)
        {
            if (building == null || !Buildings.Contains(building)) return;
            building.isOn = !building.isOn; UpdatePower(0);
            building.GetComponent<DeepMachineMotion>()?.NotifySwitchFeedback();
            DeepParticleFeedback.Emit(DeepFeedbackKind.Switch,building.transform.position);
        }
        public void RefreshStorageCapacity()
        {
            int capacity = Mathf.Max(0,baseStorageCapacity);
            foreach (var building in Buildings) if (building != null) capacity += building.StorageCapacity;
            inventory.SetCapacity(capacity);
        }
        void UpdatePower(float dt)
        {
            if(useWiredPower){UpdateWiredPower(dt);return;}
            PowerProduction = PowerDemand = 0;
            foreach (var building in Buildings)
            {
                if (building == null || building.definition == null || !building.isConstructed) continue;
                var def = building.definition;
                if (building.isOn && def.role == DeepBuildingRole.Generator)
                {
                    bool fuel = def.fuelItem == null || def.fuelUnitsPerSecond <= 0 || inventory.GetAmount(def.fuelItem) > 0;
                    if (fuel && def.fuelItem != null && def.fuelUnitsPerSecond > 0 && dt > 0)
                    {
                        building.fuelRemainder += dt*def.fuelUnitsPerSecond;
                        int units = Mathf.FloorToInt(building.fuelRemainder);
                        if (units > 0)
                        {
                            int burn = Mathf.Min(units,inventory.GetAmount(def.fuelItem)); inventory.TryConsume(def.fuelItem,burn);
                            building.fuelRemainder -= burn; fuel = burn == units;
                            if (!fuel) building.fuelRemainder = 0;
                        }
                    }
                    building.powered = fuel;
                    if (fuel) PowerProduction += def.powerGenerated;
                }
                else if (building.isOn) PowerDemand += Mathf.Max(0,def.powerRequired);
            }
            foreach (var building in Buildings)
            {
                if (building == null || building.definition == null) continue;
                if (building.definition.role != DeepBuildingRole.Generator) building.powered = building.isOn && (building.definition.powerRequired <= 0 || HasPower);
                building.RefreshVisualState();
            }
        }
        void OnDisable()
        {
            if (!Application.isPlaying) return;
            PreserveSessionForReload();
            Time.timeScale = 1;
        }
    }
}
