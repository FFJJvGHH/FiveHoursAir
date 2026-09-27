using System;
using UnityEngine;

namespace DeepPressure
{
    /// <summary>Finite inventories in mol. Species order: oxygen, nitrogen, carbon dioxide, water vapour.</summary>
    [Serializable]
    public struct GasMixture
    {
        public double oxygen, nitrogen, carbonDioxide, waterVapour;
        public const double GasConstant = 8.31446261815324; // Pa m3 / (mol K)
        public double Total => oxygen + nitrogen + carbonDioxide + waterVapour;
        public double this[int index]
        {
            get { switch (index) { case 0: return oxygen; case 1: return nitrogen; case 2: return carbonDioxide; default: return waterVapour; } }
            set { switch (index) { case 0: oxygen = value; break; case 1: nitrogen = value; break; case 2: carbonDioxide = value; break; default: waterVapour = value; break; } }
        }
        public static GasMixture FromPressure(double kPa, double volumeM3, double temperatureC, Vector4 fractions)
        {
            double total = Math.Max(0, kPa) * 1000 * Math.Max(0.001, volumeM3) / (GasConstant * Math.Max(1, temperatureC + 273.15));
            double sum = Math.Max(0, fractions.x) + Math.Max(0, fractions.y) + Math.Max(0, fractions.z) + Math.Max(0, fractions.w);
            if (sum <= 0) return default;
            return new GasMixture { oxygen = total * Math.Max(0, fractions.x) / sum, nitrogen = total * Math.Max(0, fractions.y) / sum,
                carbonDioxide = total * Math.Max(0, fractions.z) / sum, waterVapour = total * Math.Max(0, fractions.w) / sum };
        }
        public double PressureKPa(double volumeM3, double temperatureC) => Total * GasConstant * Math.Max(1, temperatureC + 273.15) / (Math.Max(0.001, volumeM3) * 1000);
        public GasMixture Scaled(double factor) => new GasMixture { oxygen = oxygen * factor, nitrogen = nitrogen * factor, carbonDioxide = carbonDioxide * factor, waterVapour = waterVapour * factor };
        public GasMixture Filter(GasOutputPort port)
        {
            if (port == GasOutputPort.OxygenProduct) return new GasMixture { oxygen = oxygen };
            if (port == GasOutputPort.TailGas) return new GasMixture { nitrogen = nitrogen, carbonDioxide = carbonDioxide, waterVapour = waterVapour };
            return this;
        }
        public bool IsFiniteAndNonnegative => FiniteNonnegative(oxygen) && FiniteNonnegative(nitrogen) && FiniteNonnegative(carbonDioxide) && FiniteNonnegative(waterVapour);
        static bool FiniteNonnegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= -1e-9;
        public static GasMixture operator +(GasMixture a, GasMixture b) => new GasMixture { oxygen = a.oxygen + b.oxygen, nitrogen = a.nitrogen + b.nitrogen, carbonDioxide = a.carbonDioxide + b.carbonDioxide, waterVapour = a.waterVapour + b.waterVapour };
        public static GasMixture operator -(GasMixture a, GasMixture b) => a + b.Scaled(-1);
        public override string ToString() => $"O2 {oxygen:F2} | N2 {nitrogen:F2} | CO2 {carbonDioxide:F2} | H2O {waterVapour:F2} mol";
    }
    public enum GasNodeKind { Reservoir, Regulator, Separator, Storage }
    public enum GasOutputPort { Mixed, OxygenProduct, TailGas }
}
