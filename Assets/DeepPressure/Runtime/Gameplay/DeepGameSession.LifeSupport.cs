using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public sealed partial class DeepGameSession
    {
        [Header("Life support — gameplay parameters")]
        public bool lifeSupportEnabled;
        [Min(.001f)] public float breathingMolPerSecond=.25f;
        public float OxygenSupplyRate {get;private set;}
        public float OxygenDemandRate {get;private set;}
        public float CarbonRemovalRate {get;private set;}
        public float StableAirSeconds {get;private set;}
        public int UnsafeWorkerCount {get;private set;}
        public string AirStatus=>!lifeSupportEnabled?"环境预览":UnsafeWorkerCount>0?UnsafeWorkerCount+" 人气氛不适":"工作区气氛正常";
        readonly Dictionary<DeepBuildingInstance,string> gasFacilityStatus=new Dictionary<DeepBuildingInstance,string>();
        public string GasFacilityStatus(DeepBuildingInstance building)=>building!=null&&gasFacilityStatus.TryGetValue(building,out var status)?status:"";
        public bool IsBreathable(DeepPressureRoom room)
        {
            if(room==null||room.gas.Total<=1e-8)return false;
            double oxygen=room.PressureKPa*room.gas.oxygen/room.gas.Total;
            return room.PressureKPa>=55&&room.PressureKPa<=180&&oxygen>=12&&oxygen<=32&&room.gas.carbonDioxide/room.gas.Total<.075;
        }
        void TickLifeSupport(float dt)
        {
            OxygenSupplyRate=OxygenDemandRate=CarbonRemovalRate=0;UnsafeWorkerCount=0;
            if(!lifeSupportEnabled||dt<=0)return;
            gasFacilityStatus.Clear();
            foreach(var building in Buildings)
            {
                if(building==null||building.definition==null||!building.definition.exchangesRoomGas)continue;
                var def=building.definition;var node=building.GetComponentInChildren<GasNode>();
                if(node==null)continue;
                if(!building.IsOperational){gasFacilityStatus[building]=building.isOn?"等待供电":"已关闭";continue;}
                var room=world.RoomAt(building.origin);
                if(room==null){gasFacilityStatus[building]="端口必须位于空腔中";continue;}
                double limit=Math.Max(.01,def.gasTransferMolPerSecond)*dt;
                if(def.gasMode==DeepGasFacilityMode.Collect||def.gasMode==DeepGasFacilityMode.Scrub)
                {
                    double roomVolume=Math.Max(.001,room.volumeM3);
                    double capacity=Math.Max(0,GasMixture.FromPressure(node.maxPressureKPa,node.volumeM3,node.temperatureC,Vector4.one).Total-node.gas.Total);
                    GasMixture packet;
                    if(def.gasMode==DeepGasFacilityMode.Scrub)packet=new GasMixture{carbonDioxide=Math.Min(Math.Min(limit,capacity),room.gas.carbonDioxide)};
                    else packet=room.gas.Scaled(Math.Min(Math.Min(limit,capacity),room.gas.Total)/Math.Max(1e-9,room.gas.Total));
                    if(packet.Total<=1e-6){gasFacilityStatus[building]=capacity<=.001?"出口满载，等待接管":"附近没有可抽取气体";continue;}
                    room.gas-=packet;node.gas+=packet;CarbonRemovalRate+=(float)packet.carbonDioxide/dt;
                    node.lastInflowMolPerSecond+=packet.Total/dt;gasFacilityStatus[building]=def.gasMode==DeepGasFacilityMode.Scrub?"正在回收 CO₂":"正在采集所在房间气体";
                }
                else if(def.gasMode==DeepGasFacilityMode.Supply||def.gasMode==DeepGasFacilityMode.Exhaust)
                {
                    double total=node.gas.Total;
                    double capacity=Math.Max(0,GasMixture.FromPressure(def.gasMode==DeepGasFacilityMode.Supply?135:180,room.volumeM3,room.temperatureC,Vector4.one).Total-room.gas.Total);
                    double amount=Math.Min(limit,Math.Min(capacity,total));
                    if(def.gasMode==DeepGasFacilityMode.Supply)
                    {
                        double desired=GasMixture.FromPressure(21,room.volumeM3,room.temperatureC,new Vector4(1,0,0,0)).oxygen-room.gas.oxygen;
                        double fraction=total>0?node.gas.oxygen/total:0;
                        amount=fraction>.01?Math.Min(amount,Math.Max(0,desired)/fraction):0;
                    }
                    if(amount<=1e-6){gasFacilityStatus[building]=total<.001?"等待管网来气":"目标压力已满足";continue;}
                    var packet=node.gas.Scaled(amount/total);node.gas-=packet;room.gas+=packet;
                    OxygenSupplyRate+=(float)packet.oxygen/dt;node.lastOutflowMolPerSecond+=amount/dt;
                    gasFacilityStatus[building]=def.gasMode==DeepGasFacilityMode.Supply?"正在向房间供气":"正在向隔离区排气";
                }
            }
            foreach(var worker in Workers)
            {
                if(worker==null||!worker.isActiveAndEnabled)continue;
                var room=world.RoomAt(worker.Cell);bool safe=IsBreathable(room);
                worker.environmentUnsafe=!safe;
                if(safe)worker.airReserveSeconds=Mathf.Min(90,worker.airReserveSeconds+dt*2);
                else{UnsafeWorkerCount++;worker.airReserveSeconds=Mathf.Max(0,worker.airReserveSeconds-dt);}
                worker.environmentEfficiency=!safe&&worker.airReserveSeconds<25?.5f:1;
                double used=room==null?0:Math.Min(room.gas.oxygen,Math.Max(0,breathingMolPerSecond)*dt);
                if(room!=null){room.gas.oxygen-=used;room.gas.carbonDioxide+=used;}
                OxygenDemandRate+=(float)used/dt;
            }
            StableAirSeconds=UnsafeWorkerCount==0&&Workers.Count>0?StableAirSeconds+dt:0;
        }
    }
}
