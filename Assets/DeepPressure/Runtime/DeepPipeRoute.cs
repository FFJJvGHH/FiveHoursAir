using UnityEngine;
namespace DeepPressure
{
    [ExecuteAlways,RequireComponent(typeof(GasLink),typeof(LineRenderer))]
    public sealed class DeepPipeRoute : MonoBehaviour
    {
        [SerializeField] Vector3 previousFrom,previousTo;
        [SerializeField] bool initialized;
        public void CaptureEndpoints()
        {
            var link=GetComponent<GasLink>();if(link.from==null||link.to==null)return;
            previousFrom=link.from.transform.position;previousTo=link.to.transform.position;initialized=true;
        }
        void Update()
        {
            var link=GetComponent<GasLink>();var line=GetComponent<LineRenderer>();
            if(link.from==null||link.to==null||line.positionCount<2)return;
            if(!initialized){CaptureEndpoints();return;}
            Vector3 a=link.from.transform.position,b=link.to.transform.position;
            if(a==previousFrom&&b==previousTo)return;
            Vector3 deltaA=a-previousFrom,deltaB=b-previousTo;
            int last=line.positionCount-1;
            var start=line.GetPosition(0);var end=line.GetPosition(last);
            if(last==2)
            {
                bool horizontal=Mathf.Abs(line.GetPosition(1).x-start.x)>Mathf.Abs(line.GetPosition(1).y-start.y);
                line.SetPosition(1,horizontal?new Vector3((end+deltaB).x,(start+deltaA).y,0):new Vector3((start+deltaA).x,(end+deltaB).y,0));
            }
            else if(last>2)
            {
                var next=line.GetPosition(1);var prior=line.GetPosition(last-1);
                next+=Mathf.Abs(next.x-start.x)>Mathf.Abs(next.y-start.y)?new Vector3(0,deltaA.y,0):new Vector3(deltaA.x,0,0);
                prior+=Mathf.Abs(prior.x-end.x)>Mathf.Abs(prior.y-end.y)?new Vector3(0,deltaB.y,0):new Vector3(deltaB.x,0,0);
                line.SetPosition(1,next);line.SetPosition(last-1,prior);
            }
            line.SetPosition(0,start+deltaA);line.SetPosition(last,end+deltaB);
            int pointIndex=0;
            foreach(Transform child in transform)
                if(child.name.StartsWith("Pipe coupling")&&pointIndex<line.positionCount)child.position=line.GetPosition(pointIndex++);
            previousFrom=a;previousTo=b;
        }
    }
}
