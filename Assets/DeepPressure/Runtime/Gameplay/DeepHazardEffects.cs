using UnityEngine;

namespace DeepPressure
{
    /// <summary>Readable expanding fronts and directional jets mark a breach, instead of a single-point flash.</summary>
    public sealed class DeepHazardEffects : MonoBehaviour
    {
        DeepGameSession session;
        DeepHazardKind kind;
        Vector2 direction;
        float age, strength;
        Material material;
        LineRenderer[] rings, jets;
        public static void Spawn(DeepGameSession owner,DeepHazardKind kind,Vector2Int cell,Vector2 direction,float strength)
        {
            if (!Application.isPlaying || owner == null) return;
            var go = new GameObject(kind == DeepHazardKind.PressureShock ? "Pressure front" : "Ignition flash");
            go.transform.SetParent(owner.transform,false); go.transform.position = owner.world.CellToWorld(cell);
            var effect = go.AddComponent<DeepHazardEffects>(); effect.session = owner; effect.kind = kind; effect.direction = direction; effect.strength = strength; effect.Create();
            for (int i = 0; i < 5; i++) DeepParticleFeedback.Emit(kind == DeepHazardKind.PressureShock ? DeepFeedbackKind.Dig : DeepFeedbackKind.Failure,go.transform.position+new Vector3((i-2)*.25f,0,0));
        }
        void Create()
        {
            material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            rings = new[] { Line("Leading pressure front",49),Line("Trailing front",49) };
            jets = new LineRenderer[7]; for (int i = 0; i < jets.Length; i++) jets[i] = Line("Directional jet",3);
        }
        LineRenderer Line(string label,int count)
        {
            var go = new GameObject(label); go.transform.SetParent(transform,false); var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.positionCount = count; line.useWorldSpace = false; line.sortingOrder = 42; line.numCapVertices = 2; line.widthMultiplier = .045f;
            return line;
        }
        void Update()
        {
            if (session == null) { Destroy(gameObject); return; }
            age += session.DeltaTime; float duration = kind == DeepHazardKind.PressureShock ? 1.1f : .8f;
            if (age >= duration) { Destroy(gameObject); return; }
            float progress = age/duration, eased = 1-Mathf.Pow(1-progress,3), alpha = Mathf.Pow(1-progress,2);
            Color tint = kind == DeepHazardKind.PressureShock ? new Color(.65f,.92f,1,alpha*.8f) : new Color(1,.52f,.15f,alpha);
            float radius = Mathf.Lerp(.15f,1.2f+strength*.5f,eased)*session.world.cellSize;
            for (int r = 0; r < rings.Length; r++)
            {
                rings[r].startColor = rings[r].endColor = tint; rings[r].widthMultiplier = (.065f-r*.02f)*(1-progress*.5f);
                for (int i = 0; i < 49; i++) { float a=i*Mathf.PI*2/48; rings[r].SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),-.1f)*radius*(1-r*.2f)); }
            }
            var right = new Vector2(-direction.y,direction.x);
            for (int i = 0; i < jets.Length; i++)
            {
                float spread = (i-3)*.22f;
                Vector2 axis = (direction+right*spread).normalized;
                float distance = radius*(.7f+.09f*i);
                jets[i].SetPosition(0,axis*distance*.45f); jets[i].SetPosition(1,axis*distance*.85f); jets[i].SetPosition(2,axis*distance);
                jets[i].startColor = tint; jets[i].endColor = new Color(tint.r,tint.g,tint.b,0); jets[i].widthMultiplier = .035f;
            }
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
