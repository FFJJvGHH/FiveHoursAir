using System;
using UnityEngine;

namespace DeepPressure
{
    /// <summary>Actual work and flow drive instruments; chassis feet and ports stay fixed.</summary>
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class DeepMachineMotion:MonoBehaviour
    {
        [Serializable] public sealed class MovingDetail
        {
            public SpriteRenderer renderer;
            public Vector3 restPosition;
            public float travel=.2f, phase;
            [Range(0,1)] public float opacity=1;
        }
        public GasNode node;
        public Transform housing,fan,pressureNeedle;
        public SpriteRenderer statusLight;
        public SpriteRenderer[] chargeSegments;
        public MovingDetail[] screenScans=Array.Empty<MovingDetail>(),oxygenBubbles=Array.Empty<MovingDetail>();
        public Vector3 housingRestPosition;
        public Vector3 fanRestPosition;
        public Vector3 needleRestPosition;
        GasNetworkSimulator simulator;
        DeepBuildingInstance building;
        DeepGameSession session;
        DeepWorkOrder previousOrder;
        float fanAngle,fanSpeed,clock,completionFlash;
        long previousReceived=-1,previousDispatched=-1;
        float interactionRemaining,interactionDuration=.34f,storedPulse;
        bool receivedPulse;
        Vector3 statusRestScale;
        bool statusScaleCaptured;
        public bool IsAnimatingWork {get;private set;}
        public float RotorSpeed => fanSpeed;
        public bool HasInteractionFeedback => interactionRemaining>0;
        public bool HasStorageFeedback => storedPulse>0;
        void OnEnable()
        {
            if(node==null)node=GetComponent<GasNode>();
            simulator=GetComponentInParent<GasNetworkSimulator>(true);building=GetComponentInParent<DeepBuildingInstance>(true);
            session=GetComponentInParent<DeepGameSession>(true);
            fanAngle=fanSpeed=clock=completionFlash=interactionRemaining=storedPulse=0;previousOrder=null;previousReceived=previousDispatched=-1;
            statusRestScale=statusLight==null?Vector3.one:statusLight.transform.localScale;
            statusScaleCaptured=statusLight!=null;
            RestoreDetails();
        }
        void OnDisable(){RestoreDetails();}
        void RestoreDetails()
        {
            if(housing!=null)housing.localPosition=housingRestPosition;
            if(fan!=null){fan.localPosition=fanRestPosition;fan.localRotation=Quaternion.identity;}
            if(statusLight!=null&&statusScaleCaptured)statusLight.transform.localScale=statusRestScale;
            foreach(var group in new[]{screenScans,oxygenBubbles})if(group!=null)foreach(var detail in group)
                if(detail?.renderer!=null){detail.renderer.transform.localPosition=detail.restPosition;detail.renderer.enabled=false;}
            IsAnimatingWork=false;
        }
        void LateUpdate()
        {
            if(!Application.isPlaying){RefreshInstruments(false,false);return;}
            AdvanceInteraction(Time.unscaledDeltaTime);
            if(session==null&&building!=null)session=building.session;
            if((session!=null&&session.IsSimulationPaused)||(simulator!=null&&simulator.paused))return;
            // The session already scales Time.deltaTime. Standalone demos scale it here.
            AdvanceVisuals(Time.deltaTime*(session==null&&simulator!=null?simulator.simulationSpeed:1));
        }
        public void AdvanceVisuals(float dt)
        {
            if(dt<=0)return;
            if(node==null)node=GetComponent<GasNode>();
            if(simulator==null)simulator=GetComponentInParent<GasNetworkSimulator>(true);
            if(building==null)building=GetComponentInParent<DeepBuildingInstance>(true);
            if(session==null&&building!=null)session=building.session;
            if((session!=null&&session.IsSimulationPaused)||(simulator!=null&&simulator.paused))return;
            clock+=dt;completionFlash=Mathf.Max(0,completionFlash-dt);storedPulse=Mathf.Max(0,storedPulse-dt);
            float flow=node==null?0:(float)(node.lastInflowMolPerSecond+node.lastOutflowMolPerSecond);
            if(building!=null)flow=Mathf.Max(flow,building.lastRoomGasTransferMolPerSecond);
            bool on=building!=null?building.IsOperational:node!=null&&node.isActiveAndEnabled;
            bool active=on&&flow>.00001f;
            float load=Mathf.Clamp01(flow/(node==null?1:Mathf.Max(.01f,node.throughputMolPerSecond)));
            DeepWorkOrder working=null;
            if(building!=null&&building.definition!=null)
            {
                if(building.definition.role==DeepBuildingRole.Generator){active=on;load=on?1:0;}
                if(session!=null)
                {
                    foreach(var order in session.Orders)
                        if(order.targetBuilding==building&&order.state==DeepWorkState.Working&&order.worker!=null&&order.worker.IsAlive)
                        {working=order;active=on;load=1;break;}
                    if(previousOrder!=null&&previousOrder!=working&&previousOrder.state==DeepWorkState.Completed)completionFlash=.65f;
                    if(building.definition.role==DeepBuildingRole.Storage)
                    {
                        long incoming=session.inventory.ReceivedUnits,outgoing=session.inventory.DispatchedUnits;
                        if(previousReceived>=0&&(incoming!=previousReceived||outgoing!=previousDispatched))
                        {storedPulse=.5f;receivedPulse=incoming!=previousReceived;}
                        previousReceived=incoming;previousDispatched=outgoing;
                    }
                    // An available personnel offer is the actual state, not perpetual fake production.
                    if(building.definition.id=="printing_pod"){active=on&&session.PrintingReady;load=.2f;}
                }
            }
            previousOrder=working;IsAnimatingWork=active;
            bool fault=building!=null&&building.isOn&&building.isConstructed&&!on;
            if(node!=null&&node.status!=null)fault|=node.status.StartsWith("Stopped",StringComparison.Ordinal)||node.status.StartsWith("Disabled",StringComparison.Ordinal);
            fanSpeed=Mathf.MoveTowards(fanSpeed,active?Mathf.Lerp(95,360,load):0,dt*580);
            if(fan!=null)
            {
                fanAngle-=dt*fanSpeed;
                fan.localRotation=Quaternion.Euler(0,0,fanAngle);
                fan.localPosition=fanRestPosition;
            }
            if(housing!=null)housing.localPosition=housingRestPosition;
            AnimateDetails(screenScans,active,false);AnimateDetails(oxygenBubbles,active,true);
            RefreshInstruments(active,fault);
        }
        public void NotifySwitchFeedback()
        {
            if(building==null)building=GetComponentInParent<DeepBuildingInstance>(true);
            interactionRemaining=interactionDuration;
            if(statusLight!=null&&!statusScaleCaptured){statusRestScale=statusLight.transform.localScale;statusScaleCaptured=true;}
            RefreshInstruments(IsAnimatingWork,false);
        }
        public void AdvanceInteraction(float realSeconds)
        {
            if(interactionRemaining<=0)return;
            interactionRemaining=Mathf.Max(0,interactionRemaining-Mathf.Max(0,realSeconds));
            RefreshInstruments(IsAnimatingWork,false);
        }
        void AnimateDetails(MovingDetail[] details,bool active,bool rising)
        {
            if(details==null)return;
            foreach(var detail in details)
            {
                if(detail?.renderer==null)continue;
                var sprite=detail.renderer;sprite.enabled=active||(!rising&&completionFlash>0);
                float phase=Mathf.Repeat(clock*(rising?.65f:.75f)+detail.phase,1);
                float offset=rising?phase:Mathf.Sin((phase-.25f)*Mathf.PI*2)*.5f;
                sprite.transform.localPosition=detail.restPosition+Vector3.up*(offset*detail.travel);
                float alpha=rising?Mathf.Sin(phase*Mathf.PI)*.65f:.38f+.20f*Mathf.Sin(clock*6+detail.phase);
                Color color=completionFlash>0&&!rising?DeepArtPalette.Confirmation:DeepArtPalette.Activity;
                color.a=(completionFlash>0&&!rising?.8f:alpha)*detail.opacity;sprite.color=color;
            }
        }
        void RefreshInstruments(bool active,bool fault)
        {
            if(building!=null&&building.definition!=null&&chargeSegments!=null&&chargeSegments.Length>0)
            {
                float charge=Application.isPlaying?building.batteryEnergy/Mathf.Max(1,building.definition.batteryCapacity):0;
                for(int i=0;i<chargeSegments.Length;i++)if(chargeSegments[i]!=null)
                    chargeSegments[i].color=charge>(float)i/chargeSegments.Length?new Color(.67f,.88f,.65f):new Color(.18f,.27f,.29f);
            }
            if(pressureNeedle!=null&&node!=null)
            {
                float pressure=Application.isPlaying?(float)node.PressureKPa:node.initialPressureKPa;
                pressureNeedle.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(125,-125,Mathf.Clamp01(pressure/Mathf.Max(1,node.maxPressureKPa))));
                pressureNeedle.localPosition=needleRestPosition;
            }
            if(statusLight!=null)
            {
                bool switchedOff=building!=null&&!building.isOn;
                Color color=interactionRemaining>0?(switchedOff?DeepArtPalette.Warning:DeepArtPalette.Activity):
                    switchedOff?new Color(.18f,.25f,.28f):fault?DeepArtPalette.Warning:storedPulse>0?(receivedPulse?DeepArtPalette.Activity:DeepArtPalette.Confirmation):
                    completionFlash>0?DeepArtPalette.Confirmation:active?DeepArtPalette.Activity:new Color(.40f,.52f,.53f);
                float pulse=fault?Mathf.Lerp(.65f,1,(Mathf.Sin(clock*3)+1)*.5f):active?Mathf.Lerp(.78f,1,(Mathf.Sin(clock*5)+1)*.5f):1;
                statusLight.color=new Color(color.r*pulse,color.g*pulse,color.b*pulse,color.a);
                if(!statusScaleCaptured){statusRestScale=statusLight.transform.localScale;statusScaleCaptured=true;}
                float envelope=interactionRemaining/interactionDuration;
                statusLight.transform.localScale=statusRestScale*(1+.65f*envelope*envelope+.35f*Mathf.Sin(storedPulse/.5f*Mathf.PI));
            }
        }
    }
}
