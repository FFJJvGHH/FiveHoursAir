using UnityEngine;
namespace DeepPressure
{
    [CreateAssetMenu(menuName="Deep Pressure/Content/Catalog",fileName="GameplayCatalog")]
    public sealed class DeepGameplayCatalog : ScriptableObject
    {
        [HideInInspector] public int contentRevision;
        public DeepItemDefinition[] items=System.Array.Empty<DeepItemDefinition>();
        public DeepBuildingDefinition[] buildings=System.Array.Empty<DeepBuildingDefinition>();
        public DeepTechDefinition[] technologies=System.Array.Empty<DeepTechDefinition>();
        public DeepRecipeDefinition[] recipes=System.Array.Empty<DeepRecipeDefinition>();
        public DeepItemDefinition FindItem(string id){if(items!=null)foreach(var x in items)if(x!=null&&x.id==id)return x;return null;}
        public DeepBuildingDefinition FindBuilding(string id){if(buildings!=null)foreach(var x in buildings)if(x!=null&&x.id==id)return x;return null;}
        public DeepTechDefinition FindTech(string id){if(technologies!=null)foreach(var x in technologies)if(x!=null&&x.id==id)return x;return null;}
        public DeepRecipeDefinition FindRecipe(string id){if(recipes!=null)foreach(var x in recipes)if(x!=null&&x.id==id)return x;return null;}
    }
}
