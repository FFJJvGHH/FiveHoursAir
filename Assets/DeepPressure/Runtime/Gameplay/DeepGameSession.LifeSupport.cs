using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public sealed partial class DeepGameSession
    {
        [Header("Life support — gameplay parameters")]
        public bool lifeSupportEnabled;
        // NASA's 0.84 kg O2/person/day is 26.25 mol. At 600 s per game cycle,
        // 0.07 mol/s uses 1.344 kg/cycle: a 60% survival-game margin, not 4.8 kg.
        // Reference: https://ntrs.nasa.gov/citations/20100040625
        public const float DefaultBreathingMolPerSecond = .07f;
        public double MinimumOxygenPartialKPa=>SimulationTuning.minimumOxygenKPa;
        public double MaximumOxygenPartialKPa=>SimulationTuning.maximumOxygenKPa;
        public double MaximumCarbonDioxidePartialKPa=>SimulationTuning.carbonDangerKPa;
        [HideInInspector] public float breathingMolPerSecond=DefaultBreathingMolPerSecond;
        public DeepSimulationTuning simulationTuning;
        public DeepSimulationTuning SimulationTuning=>simulationTuning!=null?simulationTuning:simulationTuning=DeepSimulationTuning.LoadDefault();
        float appliedBreathing=-1,appliedDiffusion=-1,appliedSuffocation=-1;
        public float OxygenSupplyRate {get;private set;}
        public float OxygenDemandRate {get;private set;}
        public float CarbonRemovalRate {get;private set;}
        public float StableAirSeconds {get;private set;}
        public int UnsafeWorkerCount {get;private set;}
        public int HypoxicWorkerCount {get;private set;}
        public int AirWarningWorkerCount {get;private set;}
        public string AirStatus=>!lifeSupportEnabled?"环境预览":IsColonyLost?"殖民地无人生还":UnsafeWorkerCount>0?UnsafeWorkerCount+" 人气氛危险 · 查看当地氧压 / CO₂":AirWarningWorkerCount>0?AirWarningWorkerCount+" 人空气不适 · 效率降低":"工作区气氛正常";
        readonly Dictionary<DeepBuildingInstance,string> gasFacilityStatus=new Dictionary<DeepBuildingInstance,string>();
        DeepAtmosphereField atmosphere;
        public DeepAtmosphereField Atmosphere
        {
            get
            {
                if(!lifeSupportEnabled||world==null)return null;
                var tuning=SimulationTuning;
                if(!Mathf.Approximately(appliedBreathing,tuning.breathingMolPerSecond))breathingMolPerSecond=appliedBreathing=Mathf.Max(.001f,tuning.breathingMolPerSecond);
                if(!Mathf.Approximately(appliedSuffocation,tuning.suffocationDamagePerSecond))suffocationDamagePerSecond=appliedSuffocation=Mathf.Max(0,tuning.suffocationDamagePerSecond);
                if(atmosphere==null)atmosphere=world.GetComponent<DeepAtmosphereField>();
                if(atmosphere==null)atmosphere=world.gameObject.AddComponent<DeepAtmosphereField>();
                if(!Mathf.Approximately(appliedDiffusion,tuning.diffusionPerSecond))atmosphere.diffusionPerSecond=appliedDiffusion=Mathf.Clamp(tuning.diffusionPerSecond,.05f,3);
                atmosphere.initialCarbonDioxideFraction=tuning.initialCarbonDioxideFraction;
                if(!atmosphere.IsInitialized)atmosphere.Initialize(world);
                return atmosphere;
            }
        }
        public string GasFacilityStatus(DeepBuildingInstance building)=>building!=null&&gasFacilityStatus.TryGetValue(building,out var status)?status:"";
        public bool IsBreathable(DeepPressureRoom room)
        {
            return room!=null&&HasBreathingOxygen(room.gas,room.PressureKPa);
        }
        public Vector2Int BreathingCell(Vector2Int feet)
        {
            var head=feet+Vector2Int.up;
            return world!=null&&world.IsInside(head)&&world.GetTerrain(head.x,head.y)==TerrainKind.Empty?head:feet;
        }
        public bool IsBreathableAt(Vector2Int feet)
        {
            if(!lifeSupportEnabled)return true;
            var field=Atmosphere;if(field==null)return false;
            var cell=BreathingCell(feet);
            return HasBreathingOxygen(field.Sample(cell),field.PressureKPa(cell));
        }
        // Breathing availability is independent of toxicity, pressure exposure and
        // inert-gas percentage. Only an insufficient O2 partial pressure spends reserve.
        bool HasBreathingOxygen(GasMixture gas,double pressure)=>gas.Total>1e-8&&pressure*gas.oxygen/gas.Total>=SimulationTuning.minimumOxygenKPa;
        public string LocalAirStatus(Vector2Int feet)
        {
            var field=Atmosphere;if(field==null)return "环境预览";
            var cell=BreathingCell(feet);var gas=field.Sample(cell);double pressure=field.PressureKPa(cell);
            double oxygen=gas.Total>0?pressure*gas.oxygen/gas.Total:0;
            double carbon=gas.Total>0?pressure*gas.carbonDioxide/gas.Total:0;
            string condition=AirProblem(gas,pressure)??AirDiscomfort(oxygen,carbon)??"氧气充足";
            return condition+" · O₂ "+oxygen.ToString("0.0")+" kPa · CO₂ "+(gas.Total>0?100*gas.carbonDioxide/gas.Total:0).ToString("0.00")+"%";
        }
        string AirProblem(GasMixture gas,double pressure)
        {
            var tuning=SimulationTuning;
            string problem=null;
            if(!HasBreathingOxygen(gas,pressure))AppendAirProblem(ref problem,"缺氧 · 氧分压不足");
            if(gas.Total>1e-8)
            {
                if(pressure*gas.carbonDioxide/gas.Total>=tuning.carbonDangerKPa)AppendAirProblem(ref problem,"二氧化碳中毒风险");
                if(gas.processVapor/gas.Total>=tuning.processVaporDangerFraction)AppendAirProblem(ref problem,"工业蒸气中毒风险");
                if(pressure*gas.oxygen/gas.Total>tuning.maximumOxygenKPa)AppendAirProblem(ref problem,"高氧损伤风险");
            }
            if(pressure<tuning.minimumPressureKPa)AppendAirProblem(ref problem,"低压损伤风险");
            if(pressure>tuning.maximumPressureKPa)AppendAirProblem(ref problem,"高压损伤风险");
            return problem;
        }
        static void AppendAirProblem(ref string current,string next)=>current=current==null?next:current+" / "+next;
        string AirDiscomfort(double oxygen,double carbon)=>carbon>=SimulationTuning.carbonWarningKPa?"CO₂ 偏高 · 作业不适":oxygen<SimulationTuning.oxygenWarningKPa?"氧气偏低 · 尚可呼吸":null;
        void ApplyAirExposureDamage(DeepWorker worker,GasMixture gas,double pressure,float dt)
        {
            var tuning=SimulationTuning;
            double carbon=gas.Total>0?pressure*gas.carbonDioxide/gas.Total:0;
            double oxygen=gas.Total>0?pressure*gas.oxygen/gas.Total:0;
            double vapor=gas.Total>0?gas.processVapor/gas.Total:0;
            if(carbon>=tuning.carbonDangerKPa)
                DamageWorker(worker,dt*tuning.carbonToxicityDamagePerSecond*Mathf.Clamp((float)(carbon/Math.Max(.001,tuning.carbonDangerKPa)),1,4),"二氧化碳中毒");
            if(vapor>=tuning.processVaporDangerFraction)
                DamageWorker(worker,dt*tuning.processVaporDamagePerSecond*Mathf.Clamp((float)(vapor/Math.Max(.001,tuning.processVaporDangerFraction)),1,4),"工业蒸气中毒");
            if(pressure<tuning.minimumPressureKPa)
                DamageWorker(worker,dt*tuning.pressureExposureDamagePerSecond*Mathf.Clamp01((float)(1-pressure/Math.Max(.001,tuning.minimumPressureKPa))),"低压损伤");
            else if(pressure>tuning.maximumPressureKPa)
                DamageWorker(worker,dt*tuning.pressureExposureDamagePerSecond*Mathf.Clamp((float)(pressure/Math.Max(.001,tuning.maximumPressureKPa)-1),0,4),"高压损伤");
            if(oxygen>tuning.maximumOxygenKPa)
                DamageWorker(worker,dt*tuning.oxygenExcessDamagePerSecond*Mathf.Clamp((float)(oxygen/Math.Max(.001,tuning.maximumOxygenKPa)-1),0,4),"高氧损伤");
        }
        void UpdateWorkerAirReadings(DeepWorker worker,GasMixture gas,double pressure)
        {
            var tuning=SimulationTuning;
            double carbon=gas.Total>0?pressure*gas.carbonDioxide/gas.Total:0;
            double oxygen=gas.Total>0?pressure*gas.oxygen/gas.Total:0;
            string problem=AirProblem(gas,pressure);
            worker.breathingUnsafe=!HasBreathingOxygen(gas,pressure);
            worker.environmentUnsafe=problem!=null;
            worker.environmentCondition=problem??AirDiscomfort(oxygen,carbon);
            float comfort=carbon>=tuning.carbonImpairmentKPa?tuning.impairedWorkEfficiency:carbon>=tuning.carbonWarningKPa?tuning.warningWorkEfficiency:oxygen<tuning.oxygenWarningKPa?.9f:1;
            worker.environmentEfficiency=worker.breathingUnsafe&&worker.airReserveSeconds<25?.5f:worker.environmentUnsafe?Mathf.Min(comfort,tuning.impairedWorkEfficiency):comfort;
        }
        void CountWorkerAirReading(DeepWorker worker)
        {
            if(worker.breathingUnsafe)HypoxicWorkerCount++;
            if(worker.environmentUnsafe)UnsafeWorkerCount++;
            else if(worker.environmentEfficiency<1)AirWarningWorkerCount++;
        }
        /// <summary>Rebuild derived UI state after a paused restore. This never
        /// advances simulation, exchanges gas, restores reserve, or applies damage.</summary>
        public void RefreshAirReadings()
        {
            UnsafeWorkerCount=HypoxicWorkerCount=AirWarningWorkerCount=0;
            if(lifeSupportEnabled)breathingMolPerSecond=appliedBreathing=Mathf.Max(.001f,SimulationTuning.breathingMolPerSecond);
            var field=lifeSupportEnabled?atmosphere:null;
            if(lifeSupportEnabled&&(field==null||!field.IsInitialized))field=Atmosphere;
            foreach(var worker in Workers)
            {
                if(worker==null)continue;
                if(field==null||!worker.IsAlive||!worker.isActiveAndEnabled)
                {
                    worker.breathingUnsafe=worker.environmentUnsafe=false;worker.environmentCondition=null;worker.environmentEfficiency=1;
                    continue;
                }
                var cell=BreathingCell(worker.Cell);
                UpdateWorkerAirReadings(worker,field.Sample(cell),field.PressureKPa(cell));
                CountWorkerAirReading(worker);
            }
            OxygenDemandRate=lifeSupportEnabled?AliveWorkerCount*Mathf.Max(0,breathingMolPerSecond):0;
        }
        void TickLifeSupport(float dt)
        {
            OxygenSupplyRate=OxygenDemandRate=CarbonRemovalRate=0;UnsafeWorkerCount=HypoxicWorkerCount=AirWarningWorkerCount=0;
            if(!lifeSupportEnabled||dt<=0)return;
            var field=Atmosphere;
            gasFacilityStatus.Clear();
            foreach(var building in Buildings)
            {
                if(building!=null)building.lastRoomGasTransferMolPerSecond=0;
                if(building==null||building.definition==null||!building.definition.exchangesRoomGas)continue;
                if(building.definition.id=="oxygen_diffuser"){TickOxygenDiffuser(building,field,dt);continue;}
                var def=building.definition;var node=building.GetComponentInChildren<GasNode>();
                if(node==null)continue;
                if(!building.IsOperational){gasFacilityStatus[building]=building.isOn?"等待供电":"已关闭";continue;}
                var room=world.RoomAt(building.origin);
                if(room==null){gasFacilityStatus[building]="端口必须位于空腔中";continue;}
                double limit=Math.Max(.01,def.gasTransferMolPerSecond)*dt;
                if(def.gasMode==DeepGasFacilityMode.Recover)
                {
                    var reagent=catalog.FindItem("process_reagent");
                    if(reagent==null||inventory.AvailableCapacity<1){gasFacilityStatus[building]="仓满，停止收集";continue;}
                    var packet=field.TakeSpecies(building.origin,5,Math.Min(limit,Math.Max(0,5-node.gas.processVapor)));
                    building.lastRoomGasTransferMolPerSecond=(float)packet.Total/dt;
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
                    building.lastRoomGasTransferMolPerSecond=(float)packet.Total/dt;
                    node.lastInflowMolPerSecond+=packet.Total/dt;gasFacilityStatus[building]=def.gasMode==DeepGasFacilityMode.Scrub?"正在回收 CO₂":"正在采集所在房间气体";
                }
                else if(def.gasMode==DeepGasFacilityMode.Supply||def.gasMode==DeepGasFacilityMode.Exhaust)
                {
                    double total=node.gas.Total;
                    if(total<=1e-12){gasFacilityStatus[building]="等待管网来气";continue;}
                    if(def.gasMode==DeepGasFacilityMode.Supply&&node.gas.oxygen/total<=.01){gasFacilityStatus[building]="等待含氧气体";continue;}
                    var offered=node.gas.Scaled(Math.Min(limit,total)/total);
                    var packet=field.AddDistributed(building.origin,offered,def.gasMode==DeepGasFacilityMode.Supply?135:180,
                        def.gasMode==DeepGasFacilityMode.Supply?21:double.PositiveInfinity,node.temperatureC);
                    double amount=packet.Total;
                    if(amount<=0){gasFacilityStatus[building]="附近气压已满足";continue;}
                    node.gas-=packet;network?.RegisterExternalExchange(packet.Scaled(-1));
                    building.lastRoomGasTransferMolPerSecond=(float)amount/dt;
                    OxygenSupplyRate+=(float)packet.oxygen/dt;node.lastOutflowMolPerSecond+=amount/dt;
                    gasFacilityStatus[building]=def.gasMode==DeepGasFacilityMode.Supply?"正在向房间供气":"正在向隔离区排气";
                }
            }
            foreach(var worker in Workers)
            {
                if(worker==null||!worker.isActiveAndEnabled||!worker.IsAlive)continue;
                var breathCell=BreathingCell(worker.Cell);
                var local=field.Sample(breathCell);double pressure=field.PressureKPa(breathCell);
                worker.breathingUnsafe=!HasBreathingOxygen(local,pressure);
                if(!worker.breathingUnsafe)worker.airReserveSeconds=Mathf.Min(SimulationTuning.airReserveSeconds,worker.airReserveSeconds+dt*SimulationTuning.airReserveRecoveryPerSecond);
                else
                {
                    float unprotected=Mathf.Max(0,dt-worker.airReserveSeconds);
                    worker.airReserveSeconds=Mathf.Max(0,worker.airReserveSeconds-dt);
                    if(unprotected>0)DamageWorker(worker,unprotected*Mathf.Max(0,suffocationDamagePerSecond),"窒息");
                }
                ApplyAirExposureDamage(worker,local,pressure,dt);
                if(!worker.IsAlive)continue;
                UpdateWorkerAirReadings(worker,local,pressure);CountWorkerAirReading(worker);
                double used=field.TakeSpecies(breathCell,0,Math.Max(0,breathingMolPerSecond)*dt).oxygen;
                field.Add(breathCell,new GasMixture{carbonDioxide=used});
                OxygenDemandRate+=Mathf.Max(0,breathingMolPerSecond);
            }
            OxygenDemandRate=AliveWorkerCount*Mathf.Max(0,breathingMolPerSecond);
            StableAirSeconds=UnsafeWorkerCount==0&&AliveWorkerCount>0?StableAirSeconds+dt:0;
            field.Tick(dt);TickGasHazards(dt);field.SyncRooms();
        }
        void TickOxygenDiffuser(DeepBuildingInstance building,DeepAtmosphereField field,float dt)
        {
            if(!building.IsOperational){gasFacilityStatus[building]="已关闭";return;}
            if(world.RoomAt(building.origin)==null){gasFacilityStatus[building]="出口被阻挡";return;}
            var tuning=SimulationTuning;float rate=Mathf.Max(.01f,tuning.algaeConversionMolPerSecond);
            // A prepaid culture can fix at most 30 mol carbon. Every emitted mol O2
            // requires an existing mol CO2; culture fuel alone never creates oxygen.
            if(building.fuelSecondsRemaining<=0)
            {
                var algae=building.definition.fuelItem;
                if(algae==null||!inventory.TryConsume(algae,1)){gasFacilityStatus[building]="等待藻类";return;}
                building.fuelSecondsRemaining=tuning.cultureCapacityMol/rate;
            }
            double amount=field.Photosynthesize(building.origin,Math.Min(dt,building.fuelSecondsRemaining)*rate,tuning.algaeTargetOxygenKPa,tuning.algaeAirReachCells);
            building.fuelSecondsRemaining=Mathf.Max(0,building.fuelSecondsRemaining-(float)amount/rate);
            OxygenSupplyRate+=(float)amount/dt;CarbonRemovalRate+=(float)amount/dt;building.lastRoomGasTransferMolPerSecond=(float)amount/dt;
            gasFacilityStatus[building]=amount>0?"光合作用 CO₂ → O₂ · "+((float)amount/dt).ToString("0.00")+" mol/s":"待机 · CO₂ 不足或附近氧压已满足";
        }
    }
}
