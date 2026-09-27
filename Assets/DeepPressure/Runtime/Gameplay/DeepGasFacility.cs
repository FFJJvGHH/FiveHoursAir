using System;
using UnityEngine;
namespace DeepPressure
{
    public enum DeepGasFacilityMode { Collect, Supply, Exhaust, Scrub, Storage, Recover }
    public enum DeepGasAcceptance { Any, Oxygen, Waste }
    /// <summary>Gas interfaces use the same finite inventory as the pipe network.</summary>
    public static class DeepGasFacility
    {
        public static bool Accepts(GasNode destination,GasMixture packet)
        {
            var building=destination==null?null:destination.GetComponentInParent<DeepBuildingInstance>();
            if(building==null||building.definition==null||packet.Total<1e-9)return true;
            float oxygen=(float)(packet.oxygen/packet.Total);
            switch(building.definition.gasAcceptance)
            {
                case DeepGasAcceptance.Oxygen:return oxygen>=.75f;
                case DeepGasAcceptance.Waste:return oxygen<=.25f;
                default:return true;
            }
        }
        public static string Function(DeepBuildingDefinition definition)
        {
            if(definition==null)return "";
            if(definition.exchangesRoomGas)switch(definition.gasMode)
            {
                case DeepGasFacilityMode.Collect:return "从所在房间抽气 → 原气管网";
                case DeepGasFacilityMode.Supply:return "管网 → 房间；按氧分压停止供气";
                case DeepGasFacilityMode.Exhaust:return "管网 → 所在房间；请放在隔离区";
                case DeepGasFacilityMode.Scrub:return "抽取所在房间 CO₂ → 尾气管网";
                case DeepGasFacilityMode.Recover:return "工业蒸气 → 密封试剂；制造电子元件";
            }
            switch(definition.gasAcceptance)
            {
                case DeepGasAcceptance.Oxygen:return "储存含氧 ≥75% 的产品气体";
                case DeepGasAcceptance.Waste:return "储存含氧 ≤25% 的尾气";
                default:return definition.role==DeepBuildingRole.GasSeparator?"原气 → 氧气产品 + 独立尾气":definition.role==DeepBuildingRole.GasRegulator?"限制下游压力和输送流量":"储存混合原气，缓冲供需波动";
            }
        }
    }
}
