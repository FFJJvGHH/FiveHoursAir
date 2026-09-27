using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public enum DeepFeedbackKind { Build,Dig,Craft,Research,Move,Switch,Sample,Failure }
    public sealed class DeepParticleFeedback:MonoBehaviour
    {
        public ParticleSystem sparkPrefab,dustPrefab,pulsePrefab;
        public bool effectsEnabled=true;
        static DeepParticleFeedback active;
        readonly List<ParticleSystem> pool=new List<ParticleSystem>();
        float workTimer;
        void OnEnable(){active=this;}
        void OnDisable(){if(active==this)active=null;}
        public static void Emit(DeepFeedbackKind kind,Vector3 position)
        {if(active!=null)active.Burst(kind,position);}
        void Burst(DeepFeedbackKind kind,Vector3 position)
        {
            if(!effectsEnabled||!Application.isPlaying)return;
            ParticleSystem source=kind==DeepFeedbackKind.Dig?dustPrefab:kind==DeepFeedbackKind.Move||kind==DeepFeedbackKind.Sample||kind==DeepFeedbackKind.Research?pulsePrefab:sparkPrefab;
            if(source==null)return;
            ParticleSystem system=null;
            foreach(var candidate in pool)if(candidate!=null&&!candidate.IsAlive(true)&&candidate.name==source.name){system=candidate;break;}
            if(system==null){system=Instantiate(source,transform);system.name=source.name;pool.Add(system);}
            system.transform.position=position;system.gameObject.SetActive(true);
            var main=system.main;Color tint=kind==DeepFeedbackKind.Failure?new Color(1,.35f,.2f):kind==DeepFeedbackKind.Research?new Color(.45f,.95f,1):kind==DeepFeedbackKind.Move?new Color(.42f,1,.8f):kind==DeepFeedbackKind.Dig?new Color(.65f,.53f,.39f):new Color(1,.73f,.30f);
            main.startColor=tint;system.Clear();system.Play();
        }
        void Update()
        {
            if(Time.timeScale<=0)return;
            workTimer+=Time.deltaTime;if(workTimer<.22f)return;workTimer=0;
            foreach(var worker in GetComponentsInChildren<DeepWorker>())
            {
                if(worker.IsWorking)Burst(DeepFeedbackKind.Build,worker.transform.position+new Vector3(.45f,.9f,0));
                else if(worker.IsMoving&&Random.value<.25f)Burst(DeepFeedbackKind.Dig,worker.transform.position);
            }
        }
    }
}
