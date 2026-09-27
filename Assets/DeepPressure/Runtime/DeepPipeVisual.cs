using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeepPressure
{
    /// <summary>Editor-baked pipe geometry. The disabled LineRenderer remains the editable route.
    /// Play mode only updates an existing mesh and material properties; it never creates level objects.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(GasLink), typeof(LineRenderer))]
    [DefaultExecutionOrder(200)]
    public sealed class DeepPipeVisual : MonoBehaviour
    {
        public MeshFilter pipeMesh;
        public MeshRenderer pipeRenderer;
        public SpriteRenderer valveIndicator;
        [Range(.18f, .30f)] public float diameter = .24f;
        [Range(.12f, .7f)] public float elbowRadius = .36f;
        public Color gasColor = new Color(.3f, .84f, .8f);
        [HideInInspector] public float routeLength;
        readonly List<Vector3> smoothRoute = new List<Vector3>();
        readonly List<float> lengths = new List<float>();
        Vector3[] routeSnapshot;
        LineRenderer route;
        GasLink link;
        GasNetworkSimulator simulator;
        MaterialPropertyBlock properties;
        Mesh runtimeMesh;
        float phase;
        float previousDiameter, previousRadius;

        void OnEnable()
        {
            route = GetComponent<LineRenderer>(); link = GetComponent<GasLink>();
            simulator = GetComponentInParent<GasNetworkSimulator>();
            routeSnapshot = null;
        }
        void OnDisable()
        {
            if (runtimeMesh != null)
            { if (Application.isPlaying) Destroy(runtimeMesh); else DestroyImmediate(runtimeMesh); runtimeMesh = null; }
        }
        void LateUpdate()
        {
            if (route == null) route = GetComponent<LineRenderer>();
            if (link == null) link = GetComponent<GasLink>();
            if (pipeMesh == null || pipeRenderer == null || route.positionCount < 2) return;
            route.enabled = false;
            if (RouteChanged()) RebuildGeometry();
            bool closed = !link.isOpen || link.valve <= .0001f || !link.isActiveAndEnabled;
            bool paused = simulator != null && simulator.paused;
            float flow = Application.isPlaying && !closed && !paused ? (float)link.lastFlowMolPerSecond : 0;
            float strength = Mathf.Clamp01(Mathf.Abs(flow) / Mathf.Max(.01f, link.maxFlowMolPerSecond));
            if (Mathf.Abs(flow) > .00001f)
                phase += Time.deltaTime * (GetComponentInParent<DeepGameSession>()!=null?1:(simulator == null ? 1 : simulator.simulationSpeed)) * Mathf.Sign(flow) * (.35f + strength * 1.8f);
            if (properties == null) properties = new MaterialPropertyBlock();
            pipeRenderer.GetPropertyBlock(properties);
            properties.SetColor("_GasColor", gasColor);
            properties.SetFloat("_FlowPhase", phase);
            properties.SetFloat("_FlowStrength", Mathf.Abs(flow) > .00001f ? Mathf.Lerp(.45f, 1, strength) : 0);
            properties.SetFloat("_ValveClosed", closed ? 1 : 0);
            pipeRenderer.SetPropertyBlock(properties);
            if (valveIndicator != null && smoothRoute.Count > 1)
            {
                Vector3 direction;
                Vector3 point = PointAtDistance(Mathf.Min(1.15f, routeLength * .3f), out direction);
                valveIndicator.transform.position = pipeMesh.transform.TransformPoint(point + new Vector3(-direction.y, direction.x, 0) * (diameter * .5f + .13f) + Vector3.back * .10f);
                valveIndicator.color = closed ? new Color(1,.19f,.10f) : Mathf.Abs(flow) > .00001f ? new Color(.38f,1,.68f) : new Color(.91f,.60f,.16f);
            }
        }
        bool RouteChanged()
        {
            if (routeSnapshot == null || routeSnapshot.Length != route.positionCount || previousDiameter != diameter || previousRadius != elbowRadius) return true;
            for (int i = 0; i < routeSnapshot.Length; i++)
                if ((WorldPoint(i) - routeSnapshot[i]).sqrMagnitude > .0000001f) return true;
            return false;
        }
        Vector3 WorldPoint(int index) => route.useWorldSpace ? route.GetPosition(index) : route.transform.TransformPoint(route.GetPosition(index));

        public void RebuildGeometry()
        {
            if (route == null) route = GetComponent<LineRenderer>();
            if (pipeMesh == null || route.positionCount < 2) return;
            routeSnapshot = new Vector3[route.positionCount];
            var points = new List<Vector3>();
            for (int i = 0; i < route.positionCount; i++)
            {
                routeSnapshot[i] = WorldPoint(i);
                Vector3 local = pipeMesh.transform.InverseTransformPoint(routeSnapshot[i]);
                if (points.Count == 0 || (points[points.Count - 1] - local).sqrMagnitude > .000001f) points.Add(local);
            }
            smoothRoute.Clear(); lengths.Clear(); routeLength = 0;
            if (points.Count < 2) { if (pipeMesh.sharedMesh != null) pipeMesh.sharedMesh.Clear(); return; }
            smoothRoute.Add(points[0]);
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 incoming = (points[i] - points[i - 1]).normalized;
                Vector3 outgoing = (points[i + 1] - points[i]).normalized;
                if (Vector3.Dot(incoming, outgoing) > .999f) { smoothRoute.Add(points[i]); continue; }
                float radius = Mathf.Min(elbowRadius, Vector3.Distance(points[i], points[i - 1]) * .40f, Vector3.Distance(points[i + 1], points[i]) * .40f);
                Vector3 before = points[i] - incoming * radius, after = points[i] + outgoing * radius;
                smoothRoute.Add(before);
                for (int step = 1; step <= 8; step++)
                {
                    float t = step / 8f;
                    smoothRoute.Add((1-t)*(1-t)*before + 2*(1-t)*t*points[i] + t*t*after);
                }
            }
            smoothRoute.Add(points[points.Count - 1]); lengths.Add(0);
            for (int i = 1; i < smoothRoute.Count; i++) { routeLength += Vector3.Distance(smoothRoute[i-1],smoothRoute[i]); lengths.Add(routeLength); }
            Mesh mesh = pipeMesh.sharedMesh;
            if (Application.isPlaying)
            {
                if (runtimeMesh == null) { runtimeMesh = mesh == null ? new Mesh() : Instantiate(mesh); runtimeMesh.name = "Live pipe geometry"; pipeMesh.sharedMesh = runtimeMesh; }
                mesh = runtimeMesh;
            }
            if (mesh == null) { mesh = new Mesh { name = "Baked industrial pipe" }; pipeMesh.sharedMesh = mesh; }
            var data = new PipeMeshData();
            // Wall shoes render behind the cylinder. Their small front-facing bolt heads remain visible.
            for (float distance = 1.25f; distance < routeLength - .5f; distance += 3.2f)
            {
                Vector3 tangent, center = PointAtDistance(distance, out tangent);
                Vector3 side = new Vector3(-tangent.y,tangent.x,0);
                data.Quad(center + Vector3.forward*.07f, tangent, side, .25f, diameter*2.8f, new Color(.28f,.34f,.38f));
                foreach (float sign in new[]{-1f,1f})
                    data.Quad(center + side*(diameter*.98f)*sign + Vector3.back*.01f, tangent, side, .075f,.075f,new Color(.84f,.86f,.80f));
            }
            data.Tube(smoothRoute, lengths, diameter*.5f, Color.white, true);
            AddFlange(data, Mathf.Min(.08f,routeLength*.2f));
            AddFlange(data, Mathf.Max(.1f,routeLength-.08f));
            for(float distance=1.25f; distance<routeLength-.5f;distance+=3.2f)AddFlange(data,distance);
            // A collar on both sides of each elbow makes straight sections and elbow fittings readable.
            for(int i=1;i<points.Count-1;i++)
            {
                float nearest=0,minimum=float.MaxValue;
                for(int j=0;j<smoothRoute.Count;j++){float d=(smoothRoute[j]-points[i]).sqrMagnitude;if(d<minimum){minimum=d;nearest=lengths[j];}}
                AddFlange(data,Mathf.Clamp(nearest-elbowRadius*.85f,.1f,routeLength-.1f));
                AddFlange(data,Mathf.Clamp(nearest+elbowRadius*.85f,.1f,routeLength-.1f));
            }
            data.Apply(mesh);
            previousDiameter=diameter;previousRadius=elbowRadius;
#if UNITY_EDITOR
            if(!Application.isPlaying)UnityEditor.EditorUtility.SetDirty(mesh);
#endif
        }
        void AddFlange(PipeMeshData data,float distance)
        {
            Vector3 tangent,center=PointAtDistance(distance,out tangent);
            float r=diameter*.5f;
            data.Profile(center,tangent,new[]{-.09f,-.065f,.065f,.09f},new[]{r*1.03f,r*1.47f,r*1.47f,r*1.03f},new Color(.78f,.82f,.79f));
            data.Profile(center,tangent,new[]{-.017f,.017f},new[]{r*1.49f,r*1.49f},new Color(.28f,.34f,.37f));
        }
        public Vector3 PointAtDistance(float distance,out Vector3 tangent)
        {
            if(smoothRoute.Count<2){tangent=Vector3.right;return Vector3.zero;}
            distance=Mathf.Clamp(distance,0,routeLength);
            for(int i=1;i<lengths.Count;i++)if(distance<=lengths[i])
            {tangent=(smoothRoute[i]-smoothRoute[i-1]).normalized;return Vector3.Lerp(smoothRoute[i-1],smoothRoute[i],Mathf.InverseLerp(lengths[i-1],lengths[i],distance));}
            tangent=(smoothRoute[smoothRoute.Count-1]-smoothRoute[smoothRoute.Count-2]).normalized;return smoothRoute[smoothRoute.Count-1];
        }

        sealed class PipeMeshData
        {
            const int Sides=16;
            readonly List<Vector3> vertices=new List<Vector3>(), normals=new List<Vector3>();
            readonly List<Vector2> uvs=new List<Vector2>();
            readonly List<Color> colors=new List<Color>();
            readonly List<int> triangles=new List<int>();
            int Add(Vector3 p,Vector3 n,Vector2 uv,Color color){int index=vertices.Count;vertices.Add(p);normals.Add(n);uvs.Add(uv);colors.Add(color);return index;}
            void Stitch(int previous,int current)
            {for(int j=0;j<Sides;j++){triangles.Add(previous+j);triangles.Add(current+j);triangles.Add(current+j+1);triangles.Add(previous+j);triangles.Add(current+j+1);triangles.Add(previous+j+1);}}
            int Ring(Vector3 center,Vector3 tangent,float radius,float distance,Color color,bool stripe)
            {
                int first=vertices.Count;Vector3 side=new Vector3(-tangent.y,tangent.x,0).normalized;
                for(int j=0;j<=Sides;j++)
                {float angle=j*Mathf.PI/Sides;Vector3 normal=side*Mathf.Cos(angle)+Vector3.back*Mathf.Sin(angle);Add(center+normal*radius,normal,new Vector2(distance,stripe?j/(Sides*2f):2),color);}
                return first;
            }
            public void Tube(List<Vector3> points,List<float> lengths,float radius,Color color,bool stripe)
            {
                int previous=-1;
                for(int i=0;i<points.Count;i++)
                {Vector3 tangent=(i==0?points[1]-points[0]:i==points.Count-1?points[i]-points[i-1]:points[i+1]-points[i-1]).normalized;int current=Ring(points[i],tangent,radius,lengths[i],color,stripe);if(previous>=0)Stitch(previous,current);previous=current;}
            }
            public void Profile(Vector3 center,Vector3 tangent,float[] offsets,float[] radii,Color color)
            {int previous=-1;for(int i=0;i<offsets.Length;i++){int current=Ring(center+tangent*offsets[i]+Vector3.back*.012f,tangent,radii[i],0,color,false);if(previous>=0)Stitch(previous,current);previous=current;}}
            public void Quad(Vector3 center,Vector3 tangent,Vector3 side,float width,float height,Color color)
            {
                int first=vertices.Count;Add(center-tangent*width*.5f-side*height*.5f,Vector3.back,new Vector2(0,2),color);Add(center+tangent*width*.5f-side*height*.5f,Vector3.back,new Vector2(0,2),color);Add(center+tangent*width*.5f+side*height*.5f,Vector3.back,new Vector2(0,2),color);Add(center-tangent*width*.5f+side*height*.5f,Vector3.back,new Vector2(0,2),color);
                triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);
            }
            public void Apply(Mesh mesh){mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();}
        }
    }
}
