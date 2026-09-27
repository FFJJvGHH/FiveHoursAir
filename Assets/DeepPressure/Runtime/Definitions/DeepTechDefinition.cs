using UnityEngine;
namespace DeepPressure
{
    [CreateAssetMenu(menuName="Deep Pressure/Content/Technology",fileName="Technology")]
    public sealed class DeepTechDefinition : ScriptableObject
    {
        public string id,displayName;
        [TextArea] public string description;
        public Sprite icon;
        public string branch = "气体工程";
        [Range(0,4)] public int tier;
        [Min(.1f)] public float workSeconds=30;
        public string[] prerequisiteIds=System.Array.Empty<string>();
        public DeepItemAmount[] cost=System.Array.Empty<DeepItemAmount>();
        [Tooltip("Authoring summary; building.requiredTechId is the actual unlock condition.")]
        public string[] unlockBuildingIds=System.Array.Empty<string>();
        public string[] unlockRecipeIds=System.Array.Empty<string>();
    }
}
