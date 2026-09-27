using UnityEngine;

namespace DeepPressure
{
    /// <summary>Map-scale instrument feedback. Lines are presentation only and never uncover cells.</summary>
    public sealed class DeepSurveyFeedback : MonoBehaviour
    {
        DeepWorker worker;
        DeepPressureWorld world;
        LineRenderer ring,scope,beam;
        Material lineMaterial;
        RectInt area;
        Vector2 origin;
        Color tint;
        float started;
        bool isPulse;
        const int Segments = 64;
        readonly Vector3[] ringPoints = new Vector3[Segments];

        public static void Ensure(DeepWorker worker)
        {
            if (worker.GetComponent<DeepSurveyFeedback>() != null) return;
            var effect = worker.gameObject.AddComponent<DeepSurveyFeedback>();
            effect.worker = worker; effect.world = worker.session.world;
        }

        public static void Pulse(DeepPressureWorld world,RectInt scope,Vector2Int origin,Color tint)
        {
            if (world == null || !Application.isPlaying) return;
            var go = new GameObject("Survey area pulse"); go.transform.SetParent(world.transform,false);
            var effect = go.AddComponent<DeepSurveyFeedback>();
            effect.world = world; effect.area = scope; effect.origin = (Vector2)origin+Vector2.one*.5f;
            effect.tint = tint; effect.isPulse = true; effect.started = Time.unscaledTime;
        }

        void CreateVisuals()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            lineMaterial = new Material(shader) { name = "Survey instrument lines",hideFlags = HideFlags.DontSave };
            ring = MakeLine("Sweep arc",Segments,true);
            scope = MakeLine("Measured scope",4,true);
            beam = MakeLine("Probe cable",3,false);
        }
        LineRenderer MakeLine(string name,int count,bool loop)
        {
            var go = new GameObject(name); go.transform.SetParent(transform,false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true; line.loop = loop; line.positionCount = count;
            line.widthMultiplier = .035f*world.cellSize; line.sortingOrder = 32765;
            line.numCapVertices = 2; line.numCornerVertices = 2;
            return line;
        }

        void LateUpdate()
        {
            if (world == null) return;
            if (ring == null) CreateVisuals();
            if (ring == null) return;
            if (isPulse)
            {
                float elapsed = Time.unscaledTime-started,t = Mathf.Clamp01(elapsed/2.2f);
                if (t >= 1) { Destroy(gameObject); return; }
                float radius = Mathf.Lerp(.5f,Mathf.Max(area.width,area.height)*.82f,1-Mathf.Pow(1-t,2));
                DrawRing(origin,radius,tint,(1-t)*.85f,true);
                DrawScope(tint,(1-t)*.45f); beam.enabled = false;
                return;
            }
            var order = worker == null ? null : worker.CurrentOrder;
            bool active = order != null && order.state == DeepWorkState.Working && (order.kind == DeepWorkKind.Sample || order.kind == DeepWorkKind.Survey);
            ring.enabled = scope.enabled = beam.enabled = active;
            if (!active) return;
            var region = world.RegionAt(order.targetCell);
            if (region == null) return;
            area = region.bounds;
            var exploration = world.GetComponent<DeepExploration>();
            Vector2Int probe = order.targetCell;
            if (exploration != null) exploration.TryProbeFrom(region,worker.Cell,out probe);
            origin = (Vector2)probe + Vector2.one*.5f;
            tint = order.kind == DeepWorkKind.Sample ? new Color(.35f,.91f,.80f) : new Color(.62f,.83f,1);
            float wave = Mathf.Repeat(order.completedSeconds*.6f,1);
            DrawRing(origin,Mathf.Lerp(.25f,2.8f,wave),tint,(1-wave)*.7f,false);
            DrawScope(tint,.16f+.22f*order.Progress);
            Vector3 hand = worker.transform.position + world.transform.up*world.cellSize*.85f;
            Vector3 target = MapPoint(origin);
            beam.SetPosition(0,hand); beam.SetPosition(1,Vector3.Lerp(hand,target,.45f)+world.transform.up*.12f*world.cellSize); beam.SetPosition(2,target);
            beam.startColor = new Color(tint.r,tint.g,tint.b,.8f); beam.endColor = new Color(tint.r,tint.g,tint.b,.3f);
        }

        void DrawRing(Vector2 center,float radius,Color color,float alpha,bool clip)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = i*(Mathf.PI*2/Segments);
                Vector2 p = center + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                if (clip) p = new Vector2(Mathf.Clamp(p.x,area.xMin+.12f,area.xMax-.12f),Mathf.Clamp(p.y,area.yMin+.12f,area.yMax-.12f));
                ringPoints[i] = MapPoint(p);
            }
            ring.SetPositions(ringPoints); color.a = alpha; ring.startColor = ring.endColor = color;
        }
        void DrawScope(Color color,float alpha)
        {
            scope.SetPosition(0,MapPoint(new Vector2(area.xMin+.12f,area.yMin+.12f)));
            scope.SetPosition(1,MapPoint(new Vector2(area.xMax-.12f,area.yMin+.12f)));
            scope.SetPosition(2,MapPoint(new Vector2(area.xMax-.12f,area.yMax-.12f)));
            scope.SetPosition(3,MapPoint(new Vector2(area.xMin+.12f,area.yMax-.12f)));
            color.a = alpha; scope.startColor = scope.endColor = color;
        }
        Vector3 MapPoint(Vector2 point) => world.transform.TransformPoint(new Vector3(point.x*world.cellSize,point.y*world.cellSize,-.1f));
        void OnDestroy() { if (lineMaterial != null) { if (Application.isPlaying) Destroy(lineMaterial); else DestroyImmediate(lineMaterial); } }
    }
}
