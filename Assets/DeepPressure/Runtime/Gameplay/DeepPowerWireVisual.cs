using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    /// <summary>World-space copper conduit follows the exact simulated grid, including branch junctions.</summary>
    [DisallowMultipleComponent]
    public sealed class DeepPowerWireVisual : MonoBehaviour
    {
        public DeepGameSession session;
        Mesh mesh;
        Material material;
        GameObject display;
        float nextRefresh;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();
        void LateUpdate()
        {
            if (session == null) session = GetComponent<DeepGameSession>();
            if (session == null || session.world == null || !session.useWiredPower) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime+.12f; RefreshMesh();
        }
        void RefreshMesh()
        {
            if (display == null)
            {
                display = new GameObject("Power conduit · completed circuit"); display.transform.SetParent(transform,false);
                mesh = new Mesh { name = "Completed wire grid", hideFlags = HideFlags.DontSave }; mesh.MarkDynamic();
                display.AddComponent<MeshFilter>().sharedMesh = mesh;
                material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
                var renderer = display.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = session.showPowerOverlay ? 28 : 7;
            }
            display.GetComponent<MeshRenderer>().sortingOrder = session.showPowerOverlay ? 28 : 7;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            float scale = session.world.cellSize;
            foreach (var cell in session.completedWireCells)
            {
                Vector3 center = transform.InverseTransformPoint(session.world.CellToWorld(cell));
                center.z = -.02f;
                bool live = session.WireIsPowered(cell);
                var copper = live ? new Color(.91f,.68f,.28f,session.showPowerOverlay ? 1 : .7f) : new Color(.35f,.39f,.40f,session.showPowerOverlay ? .85f : .45f);
                Segment(center-new Vector3(.10f,0,0)*scale,center+new Vector3(.10f,0,0)*scale,.10f*scale,copper);
                foreach (var direction in new[] { Vector2Int.right, Vector2Int.up })
                {
                    if (!session.HasWire(cell+direction)) continue;
                    Vector3 end = transform.InverseTransformPoint(session.world.CellToWorld(cell+direction)); end.z = center.z;
                    Segment(center,end,.10f*scale,new Color(.07f,.09f,.1f,.9f));
                    Segment(center,end,(session.showPowerOverlay ? .052f : .035f)*scale,copper);
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
        }
        void Segment(Vector3 start,Vector3 end,float width,Color color)
        {
            Vector3 delta = end-start, normal = new Vector3(-delta.y,delta.x,0).normalized*width*.5f;
            int offset = vertices.Count;
            vertices.Add(start-normal); vertices.Add(start+normal); vertices.Add(end+normal); vertices.Add(end-normal);
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(offset); triangles.Add(offset+1); triangles.Add(offset+2); triangles.Add(offset); triangles.Add(offset+2); triangles.Add(offset+3);
        }
        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            if (display != null) Destroy(display);
        }
    }
}
