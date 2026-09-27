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
        [System.NonSerialized] public bool breathingUnsafe;
        [System.NonSerialized] public string environmentCondition;
        [System.NonSerialized] public float environmentEfficiency=1;
        [System.NonSerialized] public DeepWorkOrder currentOrder;
        internal float nextWorkSearchTime;
        public DeepWorkOrder CurrentOrder => currentOrder;
        public bool IsWorking => IsAlive && currentOrder != null && currentOrder.state == DeepWorkState.Working;
        public bool IsMoving => IsAlive && currentOrder != null && currentOrder.state == DeepWorkState.Moving;
        public Vector2Int Cell => session == null || session.world == null ? Vector2Int.zero : session.world.WorldToCell(transform.position+session.world.transform.up*session.world.cellSize*.05f);
        public string Status
        {
            get
            {
                if(!IsAlive)return "已死亡 · "+deathCause;
                string risk=breathingUnsafe&&airReserveSeconds<=0?"窒息中":environmentUnsafe?environmentCondition:null;
                string activity=currentOrder==null?(automationPaused?"已停止 · 等待继续":"待命"):
                    currentOrder.state==DeepWorkState.Moving?(currentOrder.fetchingMaterials?"前往仓库":currentOrder.materialsCollected?"搬运物料":"前往工作地点"):
                    currentOrder.state==DeepWorkState.Working?currentOrder.label:currentOrder.statusReason;
                return string.IsNullOrEmpty(risk)?activity:risk+" · "+activity;
            }
        }
        readonly Queue<Vector2Int> path = new Queue<Vector2Int>();
        readonly Queue<Vector3> motionWaypoints = new Queue<Vector3>();
        bool traversing;
        int pathNavigationRevision;
        Vector2Int traversalFrom,traversalTo;
        public void SetPath(List<Vector2Int> cells) { path.Clear();motionWaypoints.Clear();traversing=false;pathNavigationRevision=session==null?0:session.NavigationRevision; if (cells != null) foreach (var cell in cells) path.Enqueue(cell); }
        public Vector2Int[] CapturePath() => path.ToArray();
        internal Vector2Int TraversalOrigin=>traversalFrom;
        internal int RemainingMotionWaypoints=>traversing?motionWaypoints.Count:0;
        internal void RestoreTraversal(Vector2Int origin,int remaining)
        {
            if(remaining<=0||path.Count==0)return;
            traversalFrom=origin;traversalTo=path.Peek();BeginTraversal();
            while(motionWaypoints.Count>remaining)motionWaypoints.Dequeue();
            traversing=motionWaypoints.Count>0;
        }
        public void RestorePath(Vector2Int[] cells) { path.Clear();motionWaypoints.Clear();traversing=false;pathNavigationRevision=session==null?0:session.NavigationRevision; if (cells != null) foreach (var cell in cells) path.Enqueue(cell); }
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
                // Finish a valid step before replanning: never snap an airborne worker onto the grid.
                if(!traversing&&pathNavigationRevision!=session.NavigationRevision)
                {
                    if(!RemainingPathValid()){session.RepathWorker(this,"通路改变，沿新路线前往原目标");return false;}
                    pathNavigationRevision=session.NavigationRevision;
                }
                Vector2Int next = path.Peek();
                if(!traversing)
                {
                    traversalFrom=Cell;traversalTo=next;
                    if(traversalFrom!=next&&!DeepNavigation.CanTraverse(session,traversalFrom,next))
                    {session.RepathWorker(this,"路径已改变，重新选择通路");return false;}
                    BeginTraversal();traversing=true;
                }
                if(!session.IsStandable(next)||traversalFrom!=next&&!DeepNavigation.CanTraverse(session,traversalFrom,next))
                {session.RepathWorker(this,"路径已改变，重新选择通路");return false;}
                Vector3 target = motionWaypoints.Peek(); float remaining = Vector3.Distance(transform.position,target);
                if (remaining <= distance+.001f)
                {
                    transform.position = target; distance -= remaining;motionWaypoints.Dequeue();
                    if(motionWaypoints.Count==0){path.Dequeue();traversing=false;}
                }
                else { transform.position = Vector3.MoveTowards(transform.position,target,distance); distance = 0; }
            }
            return path.Count == 0;
        }
        bool RemainingPathValid()
        {
            var previous=Cell;
            foreach(var cell in path)
            {
                if(cell==previous){if(!session.IsStandable(cell))return false;}
                else if(!DeepNavigation.CanTraverse(session,previous,cell))return false;
                previous=cell;
            }
            return true;
        }
        void BeginTraversal()
        {
            motionWaypoints.Clear();var delta=traversalTo-traversalFrom;
            Vector3 destination=session.FootPosition(traversalTo);
            if(Mathf.Abs(delta.x)==2)
            {
                Vector3 lift=session.world.transform.up*session.world.cellSize*.55f;
                motionWaypoints.Enqueue(session.FootPosition(traversalFrom)+lift);
                motionWaypoints.Enqueue(destination+lift);
            }
            else if(delta.x!=0&&delta.y!=0)
            {
                var corner=delta.y>0?traversalFrom+Vector2Int.up:new Vector2Int(traversalTo.x,traversalFrom.y);
                motionWaypoints.Enqueue(session.FootPosition(corner));
            }
            motionWaypoints.Enqueue(destination);
        }
        internal bool ApplyGravity(float dt)
        {
            if (session == null) return false;
            if(traversing&&currentOrder!=null&&currentOrder.state==DeepWorkState.Moving&&DeepNavigation.CanTraverse(session,traversalFrom,traversalTo))return false;
            if (session.IsStandable(Cell))
            {
                if (currentOrder == null) transform.position = session.FootPosition(Cell);
                return false;
            }
            Vector2Int below = Cell+Vector2Int.down;
            if (!session.IsPassable(below)) return false;
            if (currentOrder != null) session.RepathWorker(this,"等待落脚后继续原目标");
            transform.position = Vector3.MoveTowards(transform.position,session.FootPosition(below),dt*4*session.world.cellSize); return true;
        }
        public void TeleportToCell(Vector2Int cell) { motionWaypoints.Clear();traversing=false;if (session != null) transform.position = session.FootPosition(cell); }
    }
}
