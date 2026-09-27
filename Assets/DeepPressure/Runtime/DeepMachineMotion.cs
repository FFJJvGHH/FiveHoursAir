using UnityEngine;

namespace DeepPressure
{
    /// <summary>Only instruments move. Chassis feet, occupancy and connection ports stay fixed.</summary>
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class DeepMachineMotion:MonoBehaviour
    {
        public GasNode node;
        public Transform housing,fan,pressureNeedle;
        public SpriteRenderer statusLight;
        public Vector3 housingRestPosition;
        public Vector3 fanRestPosition;
        public Vector3 needleRestPosition;
        GasNetworkSimulator simulator;
        DeepBuildingInstance building;
        DeepGameSession session;
        float fanAngle,fanSpeed,clock;
        void OnEnable()
        {
            if(node==null)node=GetComponent<GasNode>();
            simulator=GetComponentInParent<GasNetworkSimulator>();building=GetComponentInParent<DeepBuildingInstance>();
            session=GetComponentInParent<DeepGameSession>();
        }
        void OnDisable(){if(housing!=null)housing.localPosition=housingRestPosition;}
        void LateUpdate()
        {
            if(housing!=null)housing.localPosition=housingRestPosition;
            if(!Application.isPlaying){RefreshInstruments(false,false);return;}
            if(session==null&&building!=null)session=building.session;
            if((session!=null&&session.IsSimulationPaused)||(simulator!=null&&simulator.paused))return;
            float dt=Time.deltaTime;clock+=dt;
            float flow=node==null?0:(float)(node.lastInflowMolPerSecond+node.lastOutflowMolPerSecond);
            bool on=building!=null?building.IsOperational:node!=null&&node.isActiveAndEnabled;
            bool active=on&&flow>.00001f;
            float load=Mathf.Clamp01(flow/(node==null?1:Mathf.Max(.01f,node.throughputMolPerSecond)));
            if(building!=null&&building.definition!=null)
            {
                if(building.definition.role==DeepBuildingRole.Generator){active=on;load=on?1:0;}
                else if(session!=null)foreach(var order in session.Orders)
                    if(order.targetBuilding==building&&order.state==DeepWorkState.Working){active=on;load=1;break;}
            }
            bool fault=building!=null&&building.isOn&&building.isConstructed&&!on;
            if(node!=null&&node.status!=null)fault|=node.status.StartsWith("Stopped")||node.status.StartsWith("Disabled");
            fanSpeed=Mathf.MoveTowards(fanSpeed,active?Mathf.Lerp(95,360,load):0,dt*580);
            if(fan!=null)
            {
                fanAngle-=dt*fanSpeed;
                fan.localRotation=Quaternion.Euler(0,0,fanAngle);
                fan.localPosition=fanRestPosition;
            }
            RefreshInstruments(active,fault);
        }
        void RefreshInstruments(bool active,bool fault)
        {
            if(pressureNeedle!=null&&node!=null)
            {
                float pressure=Application.isPlaying?(float)node.PressureKPa:node.initialPressureKPa;
                pressureNeedle.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(125,-125,Mathf.Clamp01(pressure/Mathf.Max(1,node.maxPressureKPa))));
                pressureNeedle.localPosition=needleRestPosition;
            }
            if(statusLight!=null)
            {
                bool switchedOff=building!=null&&!building.isOn;
                Color color=switchedOff?new Color(.18f,.25f,.28f):fault?new Color(1,.34f,.13f):active?new Color(.28f,.95f,.68f):new Color(.50f,.63f,.65f);
                statusLight.color=color*(fault?Mathf.Lerp(.55f,1,(Mathf.Sin(clock*3)+1)*.5f):1);
            }
        }
    }
}
