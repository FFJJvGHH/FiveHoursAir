using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace DeepPressure
{
    public sealed class DeepLampPulse:MonoBehaviour
    {
        Light2D lamp;float baseIntensity,phase;
        void Start(){lamp=GetComponent<Light2D>();baseIntensity=lamp.intensity;phase=transform.position.x*1.37f;}
        void Update(){if(lamp!=null)lamp.intensity=baseIntensity*(1+.018f*Mathf.Sin(Time.time*.7f+phase)+.009f*Mathf.Sin(Time.time*2.3f+phase));}
    }
}
