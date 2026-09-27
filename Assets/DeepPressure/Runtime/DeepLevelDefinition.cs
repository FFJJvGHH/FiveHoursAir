using UnityEngine;
namespace DeepPressure
{
    [System.Serializable] public struct DeepCaveDefinition
    {
        public string id,displayName;
        public RectInt bounds;
        public float pressureKPa,temperatureC;
        public Vector4 composition;
    }
    [CreateAssetMenu(menuName="Deep Pressure/Content/Level",fileName="LevelDefinition")]
    public sealed class DeepLevelDefinition:ScriptableObject
    {
        public string levelName="深井七号站";
        public int width=128,height=80;
        public Vector2Int starterBaseOffset=new Vector2Int(12,40);
        public DeepCaveDefinition[] caves;
        public DeepGameplayCatalog catalog;
    }
}
