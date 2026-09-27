using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public sealed partial class DeepInventory
    {
        internal void RestoreSavedInventory(DeepSavedItem[] entries,int reserved)
        {
            amounts.Clear(); heldUnits = reserved;
            foreach (var entry in entries) amounts.Add(entry.id,entry.amount);
        }
    }
    public sealed partial class GasNetworkSimulator
    {
        internal double SaveRemainder => accumulatedTime;
        internal void RestoreSavedClock(double elapsed,int steps,double remainder)
        {
            ElapsedSeconds = elapsed; StepCount = steps; accumulatedTime = remainder;
            InitialTotal = TotalInventory(); LastValidation = "All 4 species conserved";
        }
    }
    public sealed partial class DeepExploration
    {
        internal DeepSavedExploration CaptureDiscovery()
        {
            Initialize(); if (!initialized) return null;
            var entries = new List<DeepSavedRegion>();
            foreach (var region in world.GetComponentsInChildren<DeepPressureRegion>(true))
            {
                bool hasSample = samples.TryGetValue(region,out var sample);
                entries.Add(new DeepSavedRegion { id = region.stableId,state = GetState(region),hasSample = hasSample,sample = sample });
            }
            return new DeepSavedExploration { visible = (bool[])visible.Clone(),regions = entries.ToArray(),hasIsolationEquipment = hasIsolationEquipment };
        }
        internal void RestoreDiscovery(DeepSavedExploration data)
        {
            Initialize(); if (!initialized || data == null) return;
            if (data.visible == null || data.visible.Length != visible.Length) throw new InvalidOperationException("探索数据尺寸与关卡不匹配");
            visible = (bool[])data.visible.Clone(); hasIsolationEquipment = data.hasIsolationEquipment;
            states.Clear(); samples.Clear();
            foreach (var region in world.GetComponentsInChildren<DeepPressureRegion>(true))
                foreach (var entry in data.regions)
                    if (entry.id == region.stableId) { states[region] = entry.state; if (entry.hasSample) samples[region] = entry.sample; break; }
            for (int i = 0; i < revealValues.Length; i++) revealValues[i] = visible[i] ? 1 : 0;
            UpdateRevealTexture(0); RebuildDiscoveryTexture(); RefreshGasTexture(); ApplyProperties();
        }
    }
}
