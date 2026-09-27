using UnityEngine;

namespace DeepPressure
{
    /// <summary>An authored furnishing remains one selectable object, including its contact shadow.
    /// Its mounting metadata is separate from functional, player-constructed buildings.</summary>
    [SelectionBase,DisallowMultipleComponent]
    public sealed class DeepFurnishingMount : MonoBehaviour
    {
        public SpriteRenderer artwork;
        public Vector2Int supportCell;
        public float visibleFootLocalY;
    }
}
