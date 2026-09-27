using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace DeepPressure
{
    [SelectionBase]
    [DisallowMultipleComponent]
    public sealed class DeepBuildingInstance : MonoBehaviour
    {
        public DeepBuildingDefinition definition;
        public Vector2Int origin;
        public bool isOn = true, isConstructed = true, powered;
        [Min(0)] public float batteryEnergy, fuelSecondsRemaining;
        [System.NonSerialized] public float lastRoomGasTransferMolPerSecond;
        public Light2D[] lights;
        public SpriteRenderer[] glowRenderers;
        public Transform visualRoot;
        public DeepGameSession session;
        public RectInt Bounds => new RectInt(origin,definition == null ? Vector2Int.one : definition.footprint);
        public bool IsOperational => isConstructed && isOn && (definition == null || (definition.role == DeepBuildingRole.Generator ? powered : definition.powerRequired <= 0 || powered));
        public int StorageCapacity => definition == null || !isConstructed ? 0 : Mathf.Max(0,definition.storageCapacity);
        internal float fuelRemainder;
        GasNode[] gasNodes;
        void Awake() { RefreshOwnedComponents(); }
        void OnValidate() { RefreshOwnedComponents(); }
        /// <summary>The building owns its renderers, light sources and functional nodes as one entity.</summary>
        public void RefreshOwnedComponents()
        {
            lights = GetComponentsInChildren<Light2D>(true);
            gasNodes = GetComponentsInChildren<GasNode>(true);
            if (visualRoot != null && visualRoot != transform && !visualRoot.IsChildOf(transform)) visualRoot = null;
            if (visualRoot == null) visualRoot = transform.Find("Visual");
            if (visualRoot == null) visualRoot = transform;
            if (definition != null && definition.role == DeepBuildingRole.Light)
            {
                var renderers = GetComponentsInChildren<SpriteRenderer>(true);
                var glows = new System.Collections.Generic.List<SpriteRenderer>();
                if (glowRenderers != null) foreach (var renderer in glowRenderers)
                    if (renderer != null && renderer.transform.IsChildOf(transform) && !glows.Contains(renderer)) glows.Add(renderer);
                foreach (var renderer in renderers)
                    if ((renderer.name == "Luminous lamp" || renderer.name == "Emission strip") && !glows.Contains(renderer)) glows.Add(renderer);
                glowRenderers = glows.ToArray();
            }
        }
        public void RefreshVisualState()
        {
            bool on = IsOperational;
            if (lights != null) foreach (var light in lights) if (light != null) light.enabled = on;
            if (glowRenderers != null) foreach (var glow in glowRenderers) if (glow != null) glow.enabled = on;
            if (gasNodes == null) gasNodes = GetComponentsInChildren<GasNode>(true);
            foreach (var node in gasNodes) if (node != null) node.enabled = on;
        }
    }
}
