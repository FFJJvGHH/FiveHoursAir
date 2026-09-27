using UnityEngine;
namespace DeepPressure
{
    // Short, generated confirmation tones; follows the game's volume setting.
    public sealed class DeepInterfaceFeedback : MonoBehaviour
    {
        static AudioSource source;
        static AudioClip positive, negative;
        static float lastPlayed;
        public static void Play(bool success)
        {
            if(!Application.isPlaying || success&&Time.unscaledTime-lastPlayed<.07f)return;
            lastPlayed=Time.unscaledTime;
            if(source==null)
            {
                var owner=new GameObject("Interface feedback");
                source=owner.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.ignoreListenerPause=true;
            }
            if(positive==null)positive=Tone(740,1000,.085f);
            if(negative==null)negative=Tone(240,170,.13f);
            if(!success)source.Stop();
            source.PlayOneShot(success?positive:negative,.16f);
        }
        static AudioClip Tone(float first,float last,float duration)
        {
            const int rate=22050;var samples=new float[(int)(rate*duration)];float phase=0;
            for(int i=0;i<samples.Length;i++){float t=i/(float)samples.Length;phase+=Mathf.Lerp(first,last,t)*2*Mathf.PI/rate;samples[i]=Mathf.Sin(phase)*Mathf.Sin(Mathf.PI*t)*.35f;}
            var clip=AudioClip.Create("Interface tone",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
    }
}
