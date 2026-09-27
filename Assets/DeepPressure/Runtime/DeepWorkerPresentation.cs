using System;
using UnityEngine;

namespace DeepPressure
{
    /// <summary>Presentation follows simulated motion without moving the navigation root.</summary>
    [RequireComponent(typeof(DeepWorker))]
    public sealed class DeepWorkerPresentation : MonoBehaviour
    {
        [Serializable] public struct FrameAnchor { public Sprite sprite; public float visibleFootY; }
        public Sprite idle;
        public Sprite[] walking,working;
        public Sprite[] idleDetails,carrying,climbing;
        public FrameAnchor[] frameAnchors = Array.Empty<FrameAnchor>();
        public Color suitTint = Color.white;
        public Transform carriedCrate;
        public Vector3 groundedFootLocalPosition;
        public Vector3 authoredScale = Vector3.one;
        public bool anchorsBaked;
        DeepWorker worker;
        Vector3 previous;
        float clock,walkCycle,workCycle,idleElapsed,personality;
        bool wasWorking;

        void Start()
        {
            worker=GetComponent<DeepWorker>(); previous=transform.position;
            int hash=17;foreach(char c in worker.displayName??name)hash=unchecked(hash*31+c);
            personality=(hash&1023)/1023f;clock=personality*11.3f;
            if(worker.visualRenderer!=null&&!anchorsBaked)
            {
                authoredScale=worker.visualRenderer.transform.localScale;
                groundedFootLocalPosition=worker.visualRenderer.transform.localPosition;
                groundedFootLocalPosition.y+=Foot(idle)*authoredScale.y;
            }
        }

        void LateUpdate()
        {
            if(worker==null||worker.visualRenderer==null)return;
            Vector3 delta=transform.position-previous;previous=transform.position;
            if(worker.session!=null&&worker.session.IsSimulationPaused)return;
            float dt=Time.deltaTime;if(dt<=0)return;
            clock+=dt;
            float cell=worker.session!=null&&worker.session.world!=null?worker.session.world.cellSize:1;
            Vector3 localDelta=worker.session!=null&&worker.session.world!=null?worker.session.world.transform.InverseTransformVector(delta):delta;
            bool moving=localDelta.sqrMagnitude>cell*cell*.000001f;
            bool ladder=moving&&Mathf.Abs(localDelta.y)>Mathf.Abs(localDelta.x)*1.3f;
            bool hasParcel=worker.CurrentOrder!=null&&worker.CurrentOrder.materialsCollected&&!worker.CurrentOrder.fetchingMaterials;
            var sprite=worker.visualRenderer;
            if(Mathf.Abs(localDelta.x)>.001f)sprite.flipX=localDelta.x<0;
            else if(worker.IsWorking&&worker.CurrentOrder!=null&&worker.session!=null)
            {
                float targetX=worker.session.world.CellToWorld(worker.CurrentOrder.targetCell).x-transform.position.x;
                if(Mathf.Abs(targetX)>.12f)sprite.flipX=targetX<0;
            }

            Sprite frame=idle;
            bool isWorking=worker.IsWorking;
            if(isWorking)
            {
                if(!wasWorking)workCycle=0;
                workCycle+=dt*(worker.CurrentOrder.kind==DeepWorkKind.Dig?7:5.5f)*worker.workSpeed;
                frame=Frame(working,workCycle,idle);idleElapsed=0;
            }
            else if(moving)
            {
                // Step phase follows distance: a blocked worker never runs in place.
                walkCycle+=localDelta.magnitude/Mathf.Max(.01f,cell)*(ladder?4.2f:4.8f);
                frame=Frame(ladder?climbing:hasParcel?carrying:walking,walkCycle,idle);idleElapsed=0;
            }
            else
            {
                idleElapsed+=dt;
                float gesture=Mathf.Repeat(idleElapsed+personality*9,10.8f+personality*3);
                int index=gesture<.13f?3:gesture>5.4f&&gesture<6.3f?2:gesture>2.8f&&gesture<4.2f?1:0;
                frame=idleDetails!=null&&idleDetails.Length>index&&idleDetails[index]!=null?idleDetails[index]:idle;
            }
            wasWorking=isWorking;
            if(frame!=null)sprite.sprite=frame;
            sprite.color=suitTint;
            // Every pose and breathing share the visible boot baseline, not padded sprite bounds.
            float breathing=!isWorking&&!moving?1+Mathf.Sin(clock*(1.8f+personality*.23f))*.0035f:1;
            Vector3 scale=authoredScale;scale.y*=breathing;sprite.transform.localScale=scale;
            Vector3 position=groundedFootLocalPosition;position.y-=Foot(sprite.sprite)*scale.y;
            sprite.transform.localPosition=position;
            if(carriedCrate!=null)
            {
                carriedCrate.gameObject.SetActive(hasParcel&&!isWorking);
                float sway=moving?Mathf.Sin(walkCycle*Mathf.PI*.5f)*.012f:0;
                carriedCrate.localPosition=new Vector3(sprite.flipX?-.30f:.30f,.74f+sway,-.05f);
            }
        }

        static Sprite Frame(Sprite[] frames,float phase,Sprite fallback)
        {return frames==null||frames.Length==0?fallback:frames[Mathf.FloorToInt(phase)%frames.Length]??fallback;}
        public float Foot(Sprite sprite)
        {
            if(sprite==null)return 0;
            if(frameAnchors!=null)foreach(var anchor in frameAnchors)if(anchor.sprite==sprite)return anchor.visibleFootY;
            return 0;
        }
    }
}
