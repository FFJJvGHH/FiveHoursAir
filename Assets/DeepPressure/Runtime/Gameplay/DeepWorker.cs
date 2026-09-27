using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    public sealed partial class DeepWorker : MonoBehaviour
    {
        public string displayName = "工人";
        public DeepGameSession session;
        public SpriteRenderer visualRenderer;
        public Transform body,helmet,leftLeg,rightLeg,leftArm,rightArm;
        [Min(.1f)] public float moveCellsPerSecond = 2.2f;
        [Min(.1f)] public float workSpeed = 1;
        [Header("工作偏好 · 0 禁用 / 1 低 / 2 正常 / 3 高")]
        [Range(0,3)] public int digPreference = 2, buildPreference = 2, researchPreference = 2, craftPreference = 2, pipePreference = 2;
        public bool automationPaused;
        [Range(0,100)] public float health = 100;
        [HideInInspector] public string deathCause;
        [HideInInspector] public float diedAtSeconds = -1;
        public bool IsAlive => health > 0;
        [HideInInspector] public float airReserveSeconds=90;
        [System.NonSerialized] public bool environmentUnsafe;
        [System.NonSerialized] public float environmentEfficiency=1;
        [System.NonSerialized] public DeepWorkOrder currentOrder;
        internal float nextWorkSearchTime;
        public DeepWorkOrder CurrentOrder => currentOrder;
        public bool IsWorking => IsAlive && currentOrder != null && currentOrder.state == DeepWorkState.Working;
        public bool IsMoving => IsAlive && currentOrder != null && currentOrder.state == DeepWorkState.Moving;
        public Vector2Int Cell => session == null || session.world == null ? Vector2Int.zero : session.world.WorldToCell(transform.position+session.world.transform.up*session.world.cellSize*.05f);
        public string Status => !IsAlive ? "已死亡 · "+deathCause : environmentUnsafe && airReserveSeconds <= 0 ? "窒息中" : currentOrder == null ? (automationPaused ? "已停止 · 等待继续" : "待命") : currentOrder.state == DeepWorkState.Moving ? (currentOrder.fetchingMaterials ? "前往仓库" : currentOrder.materialsCollected ? "搬运物料" : "前往工作地点") : currentOrder.state == DeepWorkState.Working ? currentOrder.label : currentOrder.statusReason;
        readonly Queue<Vector2Int> path = new Queue<Vector2Int>();
        public void SetPath(List<Vector2Int> cells) { path.Clear(); if (cells != null) foreach (var cell in cells) path.Enqueue(cell); }
        public Vector2Int[] CapturePath() => path.ToArray();
        public void RestorePath(Vector2Int[] cells) { path.Clear(); if (cells != null) foreach (var cell in cells) path.Enqueue(cell); }
        public int GetPreference(DeepWorkKind kind)
        {
            switch (kind)
            {
                case DeepWorkKind.Dig: return Mathf.Clamp(digPreference,0,3);
                case DeepWorkKind.Build: return Mathf.Clamp(buildPreference,0,3);
                case DeepWorkKind.Research: return Mathf.Clamp(researchPreference,0,3);
                case DeepWorkKind.Craft: return Mathf.Clamp(craftPreference,0,3);
                case DeepWorkKind.Pipe: return Mathf.Clamp(pipePreference,0,3);
                case DeepWorkKind.Wire: return Mathf.Clamp(buildPreference,0,3);
                case DeepWorkKind.Sample:
                case DeepWorkKind.Survey: return Mathf.Clamp(researchPreference,0,3);
                default: return 3;
            }
        }
        internal bool Advance(float dt)
        {
            if (session == null || dt <= 0) return path.Count == 0;
            float distance = dt*moveCellsPerSecond*session.world.cellSize*Mathf.Max(.5f,environmentEfficiency);
            while (path.Count > 0 && distance > 0)
            {
                Vector2Int next = path.Peek();
                if (!session.IsStandable(next)) { path.Clear(); session.RequeueWorker(this,"路径已改变"); return false; }
                Vector3 target = session.FootPosition(next); float remaining = Vector3.Distance(transform.position,target);
                if (remaining <= distance+.001f) { transform.position = target; distance -= remaining; path.Dequeue(); }
                else { transform.position = Vector3.MoveTowards(transform.position,target,distance); distance = 0; }
            }
            return path.Count == 0;
        }
        internal bool ApplyGravity(float dt)
        {
            if (session == null) return false;
            if (session.IsStandable(Cell))
            {
                if (currentOrder == null) transform.position = session.FootPosition(Cell);
                return false;
            }
            Vector2Int below = Cell+Vector2Int.down;
            if (!session.IsPassable(below)) return false;
            if (currentOrder != null) session.RequeueWorker(this,"等待落脚");
            transform.position = Vector3.MoveTowards(transform.position,session.FootPosition(below),dt*4*session.world.cellSize); return true;
        }
        public void TeleportToCell(Vector2Int cell) { if (session != null) transform.position = session.FootPosition(cell); }
    }
}
