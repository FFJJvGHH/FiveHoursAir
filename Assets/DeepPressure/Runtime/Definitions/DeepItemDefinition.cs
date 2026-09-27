using UnityEngine;
namespace DeepPressure
{
    [CreateAssetMenu(menuName="Deep Pressure/Content/Item",fileName="Item")]
    public sealed class DeepItemDefinition : ScriptableObject
    {
        [Tooltip("Stable ID. Renaming this file must not change existing save references.")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public Color tint = Color.white;
    }
    [System.Serializable]
    public struct DeepItemAmount
    {
        public DeepItemDefinition item;
        [Min(0)] public int amount;
        public DeepItemAmount(DeepItemDefinition item,int amount) { this.item=item;this.amount=amount; }
    }
}
