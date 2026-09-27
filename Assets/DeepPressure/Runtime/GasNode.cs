using System;
using UnityEngine;

namespace DeepPressure
{
    [DisallowMultipleComponent]
    public sealed class GasNode : MonoBehaviour
    {
        public string displayName = "Gas tank";
        public GasNodeKind kind;
        [Min(.01f)] public float volumeM3 = 2;
        [Range(-100, 500)] public float temperatureC = 22;
        [Min(0)] public float initialPressureKPa = 10;
        [Min(.01f)] public float maxPressureKPa = 600;
        [Min(0)] public float targetPressureKPa = 180;
        [Min(0)] public float throughputMolPerSecond = 15;
        public Vector4 initialComposition = new Vector4(.21f, .76f, .02f, .01f);
        [Tooltip("Additional normalized fractions: methane and fictional industrial process vapour.")]
        public Vector2 reactiveFractions;
        [NonSerialized] public GasMixture gas;
        [NonSerialized] public string status = "Ready";
        [NonSerialized] public double lastInflowMolPerSecond, lastOutflowMolPerSecond;
        public double PressureKPa => gas.PressureKPa(volumeM3, temperatureC);
        public double ReceivingLimitKPa => kind == GasNodeKind.Regulator ? Math.Min(maxPressureKPa, targetPressureKPa) : maxPressureKPa;
        public double CapacityMol => Math.Max(0, ReceivingLimitKPa) * 1000 * Math.Max(.001, volumeM3) / (GasMixture.GasConstant * Math.Max(1, temperatureC + 273.15));
        public void ResetInventory()
        {
            gas = GasMixture.FromPressure(initialPressureKPa, volumeM3, temperatureC, initialComposition, reactiveFractions);
            lastInflowMolPerSecond = lastOutflowMolPerSecond = 0; status = "Ready";
        }
    }
}
