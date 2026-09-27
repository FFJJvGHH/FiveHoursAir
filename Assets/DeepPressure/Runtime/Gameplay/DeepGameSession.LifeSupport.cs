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
        DeepAtmosphereField atmosphere;
        public DeepAtmosphereField Atmosphere
        {
            get
            {
                if(!lifeSupportEnabled||world==null)return null;
                if(atmosphere==null)atmosphere=world.GetComponent<DeepAtmosphereField>();
                if(atmosphere==null)atmosphere=world.gameObject.AddComponent<DeepAtmosphereField>();
                if(!atmosphere.IsInitialized)atmosphere.Initialize(world);
                return atmosphere;
            }
        }
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
            var field=Atmosphere;
            gasFacilityStatus.Clear();
            foreach(var building in Buildings)
            {
                if(building==null||building.definition==null||!building.definition.exchangesRoomGas)continue;
                var def=building.definition;var node=building.GetComponentInChildren<GasNode>();
                if(node==null)continue;
                if(!building.IsOperational){gasFacilityStatus[building]=building.isOn?"等待供电":"已关闭";continue;}
                var room=world.RoomAt(building.origin);
                if(room==null){gasFacilityStatus[building]="端口必须位于空腔中";continue;}
                GasMixture local=field.Sample(building.origin);
                double limit=Math.Max(.01,def.gasTransferMolPerSecond)*dt;
                if(def.gasMode==DeepGasFacilityMode.Recover)
                {
                    var reagent=catalog.FindItem("process_reagent");
                    if(reagent==null||inventory.AvailableCapacity<1){gasFacilityStatus[building]="仓满，停止收集";continue;}
                    var packet=field.TakeSpecies(building.origin,5,Math.Min(limit,Math.Max(0,5-node.gas.processVapor)));
                    node.gas+=packet;network?.RegisterExternalExchange(packet);
                    if(node.gas.processVapor>=5&&inventory.TryAdd(reagent,1)){var converted=new GasMixture{processVapor=5};node.gas-=converted;network?.RegisterExternalExchange(converted.Scaled(-1));}
                    gasFacilityStatus[building]=packet.Total>0?"回收工业蒸气 → 密封试剂":"等待工业蒸气扩散至入口";continue;
                }
                if(def.gasMode==DeepGasFacilityMode.Collect||def.gasMode==DeepGasFacilityMode.Scrub)
                {
                    double capacity=Math.Max(0,GasMixture.FromPressure(node.maxPressureKPa,node.volumeM3,node.temperatureC,Vector4.one).Total-node.gas.Total);
                    GasMixture packet;
                    if(def.gasMode==DeepGasFacilityMode.Scrub)packet=field.TakeSpecies(building.origin,2,Math.Min(limit,capacity));
                    else packet=field.Take(building.origin,Math.Min(limit,capacity));
                    if(packet.Total<=0){gasFacilityStatus[building]=capacity<=.001?"出口满载，等待接管":"附近没有可抽取气体";continue;}
                    node.gas+=packet;network?.RegisterExternalExchange(packet);CarbonRemovalRate+=(float)packet.carbonDioxide/dt;
                    node.lastInflowMolPerSecond+=packet.Total/dt;gasFacilityStatus[building]=def.gasMode==DeepGasFacilityMode.Scrub?"正在回收 CO₂":"正在采集所在房间气体";
                }
                else if(def.gasMode==DeepGasFacilityMode.Supply||def.gasMode==DeepGasFacilityMode.Exhaust)
                {
                    double total=node.gas.Total;
                    double capacity=Math.Max(0,GasMixture.FromPressure(def.gasMode==DeepGasFacilityMode.Supply?135:180,field.CellVolumeM3,field.TemperatureC(building.origin),Vector4.one).Total-local.Total);
                    double amount=Math.Min(limit,Math.Min(capacity,total));
                    if(def.gasMode==DeepGasFacilityMode.Supply)
                    {
                        double desired=GasMixture.FromPressure(21,field.CellVolumeM3,field.TemperatureC(building.origin),new Vector4(1,0,0,0)).oxygen-local.oxygen;
                        double fraction=total>0?node.gas.oxygen/total:0;
                        amount=fraction>.01?Math.Min(amount,Math.Max(0,desired)/fraction):0;
                    }
                    if(amount<=1e-6){gasFacilityStatus[building]=total<.001?"等待管网来气":"目标压力已满足";continue;}
                    var packet=node.gas.Scaled(amount/total);node.gas-=packet;field.Add(building.origin,packet,node.temperatureC);network?.RegisterExternalExchange(packet.Scaled(-1));
                    OxygenSupplyRate+=(float)packet.oxygen/dt;node.lastOutflowMolPerSecond+=amount/dt;
                    gasFacilityStatus[building]=def.gasMode==DeepGasFacilityMode.Supply?"正在向房间供气":"正在向隔离区排气";
                }
            }
            foreach(var worker in Workers)
            {
                if(worker==null||!worker.isActiveAndEnabled)continue;
                var local=field.Sample(worker.Cell);double pressure=field.PressureKPa(worker.Cell),total=local.Total;
                double oxygen=total>0?pressure*local.oxygen/total:0;
                bool safe=total>0&&pressure>=55&&pressure<=180&&oxygen>=12&&oxygen<=32&&local.carbonDioxide/total<.075&&local.processVapor/total<.015;
                worker.environmentUnsafe=!safe;
                if(safe)worker.airReserveSeconds=Mathf.Min(90,worker.airReserveSeconds+dt*2);
                else{UnsafeWorkerCount++;worker.airReserveSeconds=Mathf.Max(0,worker.airReserveSeconds-dt);}
                worker.environmentEfficiency=!safe&&worker.airReserveSeconds<25?.5f:1;
                double used=field.TakeSpecies(worker.Cell,0,Math.Max(0,breathingMolPerSecond)*dt).oxygen;
                field.Add(worker.Cell,new GasMixture{carbonDioxide=used});
                OxygenDemandRate+=(float)used/dt;
            }
            StableAirSeconds=UnsafeWorkerCount==0&&Workers.Count>0?StableAirSeconds+dt:0;
            field.Tick(dt);TickGasHazards(dt);field.SyncRooms();
        }
    }
}
