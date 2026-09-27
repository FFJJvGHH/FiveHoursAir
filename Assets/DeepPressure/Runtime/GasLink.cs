using System;
using UnityEngine;

namespace DeepPressure
{
    /// <summary>Only explicit endpoints connect. Crossing renderers have no simulation meaning.</summary>
    [DisallowMultipleComponent]
    public sealed class GasLink : MonoBehaviour
    {
        public GasNode from, to;
        public GasOutputPort fromPort = GasOutputPort.Mixed;
        public bool isOpen = true;
        [Tooltip("Reverse flow is allowed only for a Mixed line between plain reservoirs/storage.")]
        public bool allowReverse;
        [Range(0, 1)] public float valve = 1;
        [Min(0)] public float conductanceMolPerSecondPerKPa = .12f;
        [Min(0)] public float maxFlowMolPerSecond = 10;
        [NonSerialized] public double lastFlowMolPerSecond;
        [NonSerialized] public string status = "Idle";
        public bool CanReverse => allowReverse && fromPort == GasOutputPort.Mixed && from != null && to != null &&
            from.kind != GasNodeKind.Regulator && from.kind != GasNodeKind.Separator && to.kind != GasNodeKind.Regulator && to.kind != GasNodeKind.Separator;
        void OnDrawGizmos()
        {
            if (from == null || to == null) return;
            Gizmos.color = !isOpen ? Color.gray : fromPort == GasOutputPort.OxygenProduct ? Color.cyan : fromPort == GasOutputPort.TailGas ? new Color(1,.65f,.2f) : new Color(.65f,.7f,.8f);
            Gizmos.DrawLine(from.transform.position, to.transform.position);
            Vector3 delta = to.transform.position - from.transform.position;
            if (delta.sqrMagnitude < .01f) return;
            Vector3 middle = from.transform.position + delta * .6f, side = new Vector3(-delta.y, delta.x).normalized * .16f;
            Vector3 back = delta.normalized * .32f;
            Gizmos.DrawLine(middle, middle - back + side); Gizmos.DrawLine(middle, middle - back - side);
        }
    }
}
