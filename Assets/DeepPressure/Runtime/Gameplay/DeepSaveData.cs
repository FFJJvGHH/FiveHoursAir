using System;
using UnityEngine;
namespace DeepPressure
{
    // Plain, versioned data only: a save never embeds Unity instance IDs or executable type names.
    [Serializable] public sealed class DeepSaveData
    {
        public int version = 1;
        public string sceneName, savedUtc, title;
        public int width,height,nextOrderId,defaultOrderPriority = 5;
        public float simulationTime,speed = 1;
        public bool paused;
        public TerrainKind[] terrain;
        public string[] terrainTiles;
        public DeepSavedItem[] inventory;
        public string[] technologies;
        public DeepSavedBuilding[] buildings;
        public DeepSavedWorker[] workers;
        public DeepSavedOrder[] orders;
        public DeepSavedNode[] nodes;
        public DeepSavedLink[] links;
        public DeepSavedRoom[] rooms;
        public GasMixture displacedGas;
        public DeepSavedExploration exploration;
        public double networkElapsed,networkRemainder;
        public int networkSteps;
        public float networkStepSeconds;
        public bool hasCamera;
        public Vector3 cameraPosition;
        public float cameraSize;
        public int overlay;
        public int systemsRevision;
        public float nextPrintingTime; public int printingGeneration;
        public bool wiredPower,lifeSupport;
        public Vector2Int[] wires;
        public DeepProductionTarget[] production;
        public float stableAirSeconds;
        public GasMixture[] atmosphereCells;
        public double[] atmosphereTemperatures;
        public float atmosphereDiffusion,atmosphereConduction,nextProductionCheck;
        public bool hazardsEnabled;
        public int hazardEventCount;
        public string lastHazardMessage;
        public DeepSavedHazard[] hazards;
        public DeepSavedIgnition[] ignitionCooldowns;
    }
    [Serializable] public sealed class DeepSaveSlotInfo
    {
        public string slotId,title,savedUtc,detail;
        public float simulationTime;
        public bool exists,isValid,recoveredFromBackup;
    }
    [Serializable] public struct DeepSavedItem { public string id; public int amount; }
    [Serializable] public sealed class DeepSavedBuilding
    {
        public string id,definitionId,name;
        public Vector2Int origin;
        public Vector3 position,scale;
        public Quaternion rotation;
        public bool isOn,isConstructed,active;
        public float fuelRemainder;
        public float batteryEnergy,fuelSecondsRemaining;
    }
    [Serializable] public sealed class DeepSavedWorker
    {
        public string id,name;
        public Vector3 position;
        public Vector2Int[] path;
        public int currentOrderId = -1;
        public float moveSpeed,workSpeed;
        public int digPreference,buildPreference,researchPreference,craftPreference,pipePreference;
        public bool automationPaused;
        public float airReserveSeconds=90;
        public float health=100,diedAtSeconds=-1; public string deathCause;
        public float nextWorkSearchTime,environmentEfficiency = 1;
        public bool environmentUnsafe;
    }
    [Serializable] public sealed class DeepSavedOrder
    {
        public int id,priority = 5,batches = 1;
        public DeepWorkKind kind;
        public DeepWorkState state;
        public string label,statusReason,buildingDefinitionId,technologyId,recipeId,fromNodeId,toNodeId,targetBuildingId,workerId,requestedWorkerId;
        public Vector2Int targetCell,workCell,pickupCell;
        public DeepSavedItem[] reservation;
        public bool reservationSettled,fetchingMaterials,materialsCollected;
        public GasOutputPort fromPort;
        public float completedSeconds,totalSeconds,nextRetryTime;
    }
    [Serializable] public sealed class DeepSavedNode
    {
        public string id,buildingId,path,name;
        public Vector3 position;
        public GasNodeKind kind;
        public GasMixture gas;
        public float volume,temperature,maxPressure,targetPressure,throughput;
        public bool enabled;
    }
    [Serializable] public sealed class DeepSavedLink
    {
        public string id,fromId,toId;
        public GasOutputPort port;
        public bool isOpen,allowReverse,enabled;
        public float valve,conductance,maxFlow;
    }
    [Serializable] public sealed class DeepSavedRoom
    { public Vector2Int anchor; public int cells; public GasMixture gas; public double temperature; }
    [Serializable] public sealed class DeepSavedExploration
    {
        public bool[] visible;
        public bool hasIsolationEquipment;
        public DeepSavedRegion[] regions;
    }
    [Serializable] public sealed class DeepSavedRegion
    {
        public string id;
        public DeepExplorationState state;
        public bool hasSample;
        public DeepExploration.RegionSample sample;
    }
    [Serializable] public sealed class DeepSavedHazard
    {
        public DeepHazardKind kind;
        public Vector2Int cell;
        public Vector2 direction;
        public float strength,time;
        public string message;
    }
    [Serializable] public struct DeepSavedIgnition { public Vector2Int cell; public float time; }
}
