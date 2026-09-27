using UnityEngine;
namespace DeepPressure
{
    /// <summary>Occupancy is explicit and independent of artwork's transparent margins.</summary>
    public sealed class DeepDevicePlacement : MonoBehaviour
    {
        public Vector2Int footprintSize=new Vector2Int(4,4);
        public Vector2Int footprintOffset=new Vector2Int(-2,-2);
        public Vector2 inletOffset=new Vector2(-1.7f,0);
        public Vector2 productOffset=new Vector2(1.7f,.5f);
        public Vector2 tailOffset=new Vector2(1.7f,-.5f);
        void Start()
        {foreach(var label in GetComponentsInChildren<TextMesh>())label.GetComponent<Renderer>().enabled=false;}
        public RectInt Bounds(DeepPressureWorld world)=>new RectInt(world.WorldToCell(transform.position)+footprintOffset,footprintSize);
        void OnDrawGizmosSelected()
        {
            var world=GetComponentInParent<DeepPressureWorld>();if(world==null)return;
            var bounds=Bounds(world);Gizmos.color=new Color(.3f,.85f,.75f,.8f);
            Gizmos.DrawWireCube(world.transform.TransformPoint(new Vector3(bounds.center.x,bounds.center.y,0)*world.cellSize),new Vector3(bounds.width,bounds.height,0)*world.cellSize);
            Gizmos.color=Color.yellow;Gizmos.DrawSphere(transform.TransformPoint(inletOffset),.12f);
            Gizmos.color=Color.cyan;Gizmos.DrawSphere(transform.TransformPoint(productOffset),.12f);
        }
    }
}
