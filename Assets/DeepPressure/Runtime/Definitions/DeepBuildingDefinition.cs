using UnityEngine;
namespace DeepPressure
{
    public enum DeepBuildingRole { Structure,Ladder,Light,Generator,Research,GasTank,GasRegulator,GasSeparator,Storage,Floor,Fabricator,Vent,Battery,GasPump }
    public enum DeepPortKind { GasIn,GasOut,PowerIn,PowerOut,LiquidIn,LiquidOut }
    [System.Serializable]
    public struct DeepBuildingPort
    {
        public string id,label;
        public DeepPortKind kind;
        [Tooltip("Local position from the building root at the footprint lower-left corner.")]
        public Vector2 localPosition;
    }
    [CreateAssetMenu(menuName="Deep Pressure/Content/Building",fileName="Building")]
    public sealed class DeepBuildingDefinition : ScriptableObject
    {
        public string id,displayName;
        public string category="基础设施";
        [TextArea] public string description;
        public Sprite icon;
        public GameObject prefab;
        public Vector2Int footprint=Vector2Int.one;
        [Min(.1f)] public float workSeconds=5;
        public bool requiresFloor=true,blocksMovement=true;
        public DeepBuildingRole role;
        public string requiredTechId;
        public DeepItemAmount[] cost=System.Array.Empty<DeepItemAmount>();
        [Min(0)] public float powerGenerated,powerRequired;
        public DeepItemDefinition fuelItem;
        [Min(0)] public float fuelUnitsPerSecond;
        [Min(0)] public float batteryCapacity = 600, batteryTransferRate = 20;
        public bool exchangesRoomGas;
        public DeepGasFacilityMode gasMode = DeepGasFacilityMode.Storage;
        public DeepGasAcceptance gasAcceptance = DeepGasAcceptance.Any;
        [Min(0)] public float gasTransferMolPerSecond = 3;
        [Min(0)] public int storageCapacity;
        [Min(.1f)] public float gasStorageVolume = 8;
        public DeepBuildingPort[] ports=System.Array.Empty<DeepBuildingPort>();
        void OnValidate()
        {
            footprint=new Vector2Int(Mathf.Max(1,footprint.x),Mathf.Max(1,footprint.y));
            workSeconds=Mathf.Max(.1f,workSeconds);storageCapacity=Mathf.Max(0,storageCapacity);
            powerGenerated=Mathf.Max(0,powerGenerated);powerRequired=Mathf.Max(0,powerRequired);fuelUnitsPerSecond=Mathf.Max(0,fuelUnitsPerSecond);
        }
    }
}
