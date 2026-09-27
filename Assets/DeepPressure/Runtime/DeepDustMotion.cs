using UnityEngine;
namespace DeepPressure
{
    public sealed class DeepDustMotion:MonoBehaviour
    {
        public float phase,range=1,speed=.08f;
        Vector3 origin;SpriteRenderer sprite;Color tint;
        void Start(){origin=transform.localPosition;sprite=GetComponent<SpriteRenderer>();tint=sprite.color;}
        void Update(){float t=Time.time*speed+phase;transform.localPosition=origin+new Vector3(Mathf.Sin(t*1.7f)*range,Mathf.Cos(t)*range*.35f,0);Color c=tint;c.a=tint.a*(.3f+.7f*Mathf.Pow(Mathf.Sin(t*2.1f)*.5f+.5f,2));sprite.color=c;}
    }
}
