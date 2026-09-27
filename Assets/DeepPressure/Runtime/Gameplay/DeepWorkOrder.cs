using System;
using UnityEngine;
namespace DeepPressure
{
    public enum DeepWorkKind { Build, Dig, Move, Research, Craft, Pipe }
    public enum DeepWorkState { Queued, Moving, Working, Blocked, Completed, Cancelled }
    [Serializable]
    public sealed class DeepWorkOrder
    {
        public int id;
        [Range(1,9)] public int priority = 5;
        public DeepWorkKind kind;
        public DeepWorkState state;
        public string label, statusReason;
        public Vector2Int targetCell;
        public DeepWorker worker;
        public DeepBuildingDefinition buildingDefinition;
        public DeepTechDefinition technology;
        public DeepRecipeDefinition recipe;
        public GasNode fromNode,toNode;
        public GasOutputPort fromPort;
        public bool fetchingMaterials,materialsCollected;
        public int batches = 1;
        public DeepBuildingInstance targetBuilding;
        public float completedSeconds, totalSeconds = 1;
        public float Progress => Mathf.Clamp01(completedSeconds/Mathf.Max(.01f,totalSeconds));
        public bool IsTerminal => state == DeepWorkState.Completed || state == DeepWorkState.Cancelled;
        internal DeepReservation reservation;
        internal DeepWorker requestedWorker;
        internal Vector2Int workCell;
        internal Vector2Int pickupCell;
        internal float nextRetryTime;
    }
}
