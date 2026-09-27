using System;

namespace DeepPressure
{
    /// <summary>Bounded game reaction. Merely mixing gases does not ignite them; an energized ignition source is required.</summary>
    public static class DeepGasChemistry
    {
        public static bool TryIgnite(ref GasMixture gas,double seconds,bool energizedIgnition,out double methaneBurned)
        {
            methaneBurned = 0;
            if (!energizedIgnition || seconds <= 0 || gas.Total <= .00001) return false;
            double fuelFraction = gas.methane/gas.Total, oxygenFraction = gas.oxygen/gas.Total;
            if (fuelFraction < .04 || fuelFraction > .30 || oxygenFraction < .12) return false;
            methaneBurned = Math.Min(gas.methane,gas.oxygen*.5)*Math.Min(1,seconds*3);
            if (methaneBurned <= 1e-8) return false;
            // CH4 + 2 O2 -> CO2 + 2 H2O. The six-species inventory conserves C, H and O atoms.
            gas.methane -= methaneBurned; gas.oxygen -= methaneBurned*2;
            gas.carbonDioxide += methaneBurned; gas.waterVapour += methaneBurned*2;
            return true;
        }
    }
}
