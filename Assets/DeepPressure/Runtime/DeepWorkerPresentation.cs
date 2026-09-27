using UnityEngine;
namespace DeepPressure
{
    [RequireComponent(typeof(DeepWorker))]
    public sealed class DeepWorkerPresentation:MonoBehaviour
    {
        public Sprite idle;
        public Sprite[] walking,working;
        public Color suitTint=Color.white;
        public Transform carriedCrate;
        DeepWorker worker;
        Vector3 previous;
        float clock;
        void Start(){worker=GetComponent<DeepWorker>();previous=transform.position;}
        void LateUpdate()
        {
            if(worker==null||worker.visualRenderer==null)return;
            clock+=Time.deltaTime;
            Vector3 delta=transform.position-previous;previous=transform.position;
            var sprite=worker.visualRenderer;
            if(Mathf.Abs(delta.x)>.001f)sprite.flipX=delta.x<0;
            if(worker.IsWorking&&working!=null&&working.Length>0)sprite.sprite=working[Mathf.FloorToInt(clock*8)%working.Length];
            else if(worker.IsMoving&&walking!=null&&walking.Length>0)sprite.sprite=walking[Mathf.FloorToInt(clock*10)%walking.Length];
            else sprite.sprite=idle;
            sprite.color=suitTint;
            if(carriedCrate!=null)
            {
                bool carrying=worker.CurrentOrder!=null&&worker.CurrentOrder.materialsCollected&&worker.IsMoving;
                carriedCrate.gameObject.SetActive(carrying);carriedCrate.localPosition=new Vector3(sprite.flipX?-.35f:.35f,.53f,-.05f);
            }
        }
    }
}
