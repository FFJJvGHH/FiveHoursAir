using UnityEngine;
namespace DeepPressure
{
    [CreateAssetMenu(menuName="Deep Pressure/Content/Recipe",fileName="Recipe")]
    public sealed class DeepRecipeDefinition : ScriptableObject
    {
        public string id,displayName;
        [TextArea] public string description;
        public Sprite icon;
        public DeepItemAmount[] inputs=System.Array.Empty<DeepItemAmount>(),outputs=System.Array.Empty<DeepItemAmount>();
        [Min(.1f)] public float workSeconds=10;
        public string requiredBuildingId,requiredTechId;
    }
}
