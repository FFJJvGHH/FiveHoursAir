using System;
using UnityEngine;

namespace DeepPressure
{
    [DisallowMultipleComponent]
    public sealed class DeepPressureRegion : MonoBehaviour
    {
        [Tooltip("Stable authoring identifier. Overlap tie break uses this ID, never scene hierarchy order.")]
        public string stableId = "region-001";
        public string displayName = "Habitat";
        public RectInt bounds = new RectInt(0, 0, 8, 8);
        public int priority;
        [Min(0)] public float initialPressureKPa = 100;
        [Range(-100, 500)] public float initialTemperatureC = 22;
        [Tooltip("Mole fractions O2, N2, CO2, H2O. Normalized at initialization.")]
        public Vector4 composition = new Vector4(0.21f, 0.78f, 0.01f, 0);
        [Tooltip("Additional normalized fractions: methane and fictional industrial process vapour.")]
        public Vector2 reactiveFractions;
        public Color overlayColor = new Color(0.2f, 0.7f, 0.85f, 0.22f);
        public bool Contains(Vector2Int cell) => bounds.Contains(cell);

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId)) stableId = Guid.NewGuid().ToString("N");
            bounds.width = Mathf.Max(1, bounds.width); bounds.height = Mathf.Max(1, bounds.height);
        }
        void OnDrawGizmosSelected()
        {
            var world = GetComponentInParent<DeepPressureWorld>();
            if (world == null) world = FindObjectOfType<DeepPressureWorld>();
            if (world == null) return;
            Vector3 lower = world.CellToWorld(new Vector2Int(bounds.xMin, bounds.yMin)) - new Vector3(world.cellSize * .5f, world.cellSize * .5f, 0);
            Vector3 size = new Vector3(bounds.width * world.cellSize, bounds.height * world.cellSize, .1f);
            Gizmos.color = overlayColor; Gizmos.DrawCube(lower + size * .5f, size);
            Gizmos.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, .9f); Gizmos.DrawWireCube(lower + size * .5f, size);
        }
    }
}
