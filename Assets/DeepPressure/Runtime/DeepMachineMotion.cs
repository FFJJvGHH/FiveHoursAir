using UnityEngine;

namespace DeepPressure
{
    /// <summary>Animates only editor-baked visual children. Occupancy, ports and the machine root never move.</summary>
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
        float fanAngle;
        void OnEnable(){if(node==null)node=GetComponent<GasNode>();simulator=GetComponentInParent<GasNetworkSimulator>();}
        void OnDisable(){if(housing!=null)housing.localPosition=housingRestPosition;}
        void LateUpdate()
        {
            if(node==null)return;
            float flow=(float)(node.lastInflowMolPerSecond+node.lastOutflowMolPerSecond);
            bool active=Application.isPlaying&&node.isActiveAndEnabled&&flow>.00001f&&(simulator==null||!simulator.paused);
            float load=Mathf.Clamp01(flow/Mathf.Max(.01f,node.throughputMolPerSecond));
            if(housing!=null)
                housing.localPosition=housingRestPosition+(active?new Vector3(Mathf.Sin(Time.time*53)*.0045f,Mathf.Sin(Time.time*37)*.0025f,0):Vector3.zero);
            if(fan!=null)
            {
                if(active)fanAngle-=Time.deltaTime*(GetComponentInParent<DeepGameSession>()!=null?1:(simulator==null?1:simulator.simulationSpeed))*Mathf.Lerp(100,440,load);
                fan.localRotation=Quaternion.Euler(0,0,fanAngle);
                fan.localPosition=fanRestPosition;
            }
            if(pressureNeedle!=null)
            {
                float pressure=Application.isPlaying?(float)node.PressureKPa:node.initialPressureKPa;
                pressureNeedle.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(125,-125,Mathf.Clamp01(pressure/Mathf.Max(1,node.maxPressureKPa))));
                pressureNeedle.localPosition=needleRestPosition;
            }
            if(statusLight!=null)
            {
                bool stopped=node.status!=null&&(node.status.StartsWith("Stopped")||node.status.StartsWith("Disabled"));
                Color color=stopped?new Color(1,.18f,.09f):active?new Color(.25f,1,.60f):new Color(.85f,.53f,.12f);
                statusLight.color=color*(active?Mathf.Lerp(.62f,1,(Mathf.Sin(Time.time*7)+1)*.5f):.8f);
            }
        }
    }
}
