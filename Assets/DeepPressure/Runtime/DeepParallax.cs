using UnityEngine;
namespace DeepPressure
{
    public sealed class DeepParallax : MonoBehaviour
    {
        [Range(0,1)] public float cameraFollow=.06f;
        Vector3 initialPosition,cameraOrigin;
        Camera targetCamera;
        void Start(){initialPosition=transform.position;targetCamera=Camera.main;if(targetCamera!=null)cameraOrigin=targetCamera.transform.position;}
        void LateUpdate(){if(targetCamera!=null){var delta=targetCamera.transform.position-cameraOrigin;delta.z=0;transform.position=initialPosition+delta*cameraFollow;}}
    }
}
