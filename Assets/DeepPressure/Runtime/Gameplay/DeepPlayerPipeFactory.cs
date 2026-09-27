using UnityEngine;
namespace DeepPressure
{
    public static class DeepPlayerPipeFactory
    {
        public static GasLink Create(DeepGameSession session,GasNode from,GasNode to,GasOutputPort port)
        {
            var go = new GameObject("玩家管道 "+from.displayName+" → "+to.displayName); go.transform.SetParent(session.world.transform,false);
            var link = go.AddComponent<GasLink>(); link.from = from; link.to = to; link.fromPort = port;
            link.maxFlowMolPerSecond = 3; link.conductanceMolPerSecondPerKPa = .06f;
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.enabled = false;
            Vector3 start = PortPosition(from,port,false), end = PortPosition(to,GasOutputPort.Mixed,true);
            start.z = end.z = -.2f;
            if (Mathf.Abs(start.x-end.x) < .01f || Mathf.Abs(start.y-end.y) < .01f)
            { line.positionCount = 2; line.SetPositions(new[] { start,end }); }
            else { line.positionCount = 3; line.SetPositions(new[] { start,new Vector3(end.x,start.y,start.z),end }); }
            var meshObject = new GameObject("工业管身"); meshObject.transform.SetParent(go.transform,false);
            var filter = meshObject.AddComponent<MeshFilter>(); var renderer = meshObject.AddComponent<MeshRenderer>(); renderer.sortingOrder = 28;
            Material material = session.playerPipeMaterial;
            if (material == null)
                foreach (var existing in session.world.GetComponentsInChildren<DeepPipeVisual>(true))
                    if (existing.pipeRenderer != null && existing.pipeRenderer.sharedMaterial != null) { material = existing.pipeRenderer.sharedMaterial; break; }
            bool ownMaterial = material == null;
            if (material == null)
            {
                Shader shader = Shader.Find("DeepPressure/IndustrialPipe"); if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader) { name = "玩家工业管线" };
            }
            renderer.sharedMaterial = material;
            var visual = go.AddComponent<DeepPipeVisual>(); visual.pipeMesh = filter; visual.pipeRenderer = renderer;
            visual.gasColor = port == GasOutputPort.OxygenProduct ? new Color(.45f,.88f,.79f) : port == GasOutputPort.TailGas ? new Color(.91f,.61f,.32f) : new Color(.56f,.68f,.72f);
            visual.RebuildGeometry(); go.AddComponent<DeepPipeRoute>().CaptureEndpoints();
            var lifetime = go.AddComponent<DeepPlayerPipeLifetime>(); lifetime.ownedMaterial = ownMaterial ? material : null; lifetime.meshFilter = filter;
            return link;
        }
        static Vector3 PortPosition(GasNode node,GasOutputPort port,bool inlet)
        {
            var placement = node.GetComponentInParent<DeepDevicePlacement>();
            if (placement == null) return node.transform.position;
            return placement.transform.TransformPoint(inlet ? placement.inletOffset : port == GasOutputPort.TailGas ? placement.tailOffset : placement.productOffset);
        }
    }
    public sealed class DeepPlayerPipeLifetime : MonoBehaviour
    {
        public Material ownedMaterial;
        public MeshFilter meshFilter;
        void OnDestroy()
        {
            if (ownedMaterial != null) { if (Application.isPlaying) Destroy(ownedMaterial); else DestroyImmediate(ownedMaterial); }
            if (!Application.isPlaying && meshFilter != null && meshFilter.sharedMesh != null) DestroyImmediate(meshFilter.sharedMesh);
        }
    }
}
