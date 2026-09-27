using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public sealed partial class DeepGameSession
    {
        [Header("Survey work")]
        [Min(.5f)] public float samplingSeconds = 4.5f;
        [Min(.5f)] public float surveyingSeconds = 2.5f;
        [Min(0)] public int firstSampleResearchData = 4;

        public void RefreshExplorationVisibility() { if (exploration != null) exploration.RefreshProximity(this); }

        public bool RequestSample(Vector2Int cell,out string reason) => RequestExplorationWork(cell,DeepWorkKind.Sample,out reason);
        public bool RequestSurvey(Vector2Int cell,out string reason) => RequestExplorationWork(cell,DeepWorkKind.Survey,out reason);

        bool RequestExplorationWork(Vector2Int cell,DeepWorkKind kind,out string reason)
        {
            InitializeSession(); reason = string.Empty;
            if (exploration == null || !world.IsInside(cell)) { reason = "这里没有可勘探的洞层"; return false; }
            var region = world.RegionAt(cell);
            if (region == null) { reason = "请选择洞层；钻探从可达边界完成"; return false; }
            foreach (var existing in Orders)
                if (!existing.IsTerminal && existing.kind == kind && world.RegionAt(existing.targetCell) == region)
                { reason = "该洞层已有勘探工单"; return false; }
            if (kind == DeepWorkKind.Survey && !exploration.TryExplore(cell,out reason)) return false;
            var preview = new DeepWorkOrder { kind = kind,targetCell = cell };
            var positions = ExplorationWorkPositions(preview);
            if (positions.Count == 0 || !AnyWorkerCanReach(positions,out _))
            { reason = kind == DeepWorkKind.Sample ? "没有可达的钻探站位；先开辟边界通路或搭建梯子" : "洞室入口尚不可达；先挖掘通路，工人视野会逐步消除迷雾"; return false; }
            NewOrder(kind,kind == DeepWorkKind.Sample ? "洞层取样" : "现场勘探",cell,kind == DeepWorkKind.Sample ? samplingSeconds : surveyingSeconds);
            reason = kind == DeepWorkKind.Sample ? "已安排取样 · 工人到场测量后获得读数与首次研究数据" : "已安排勘探 · 工人进入视野后逐步探明";
            return true;
        }

        List<Vector2Int> ExplorationWorkPositions(DeepWorkOrder order)
        {
            var positions = new List<Vector2Int>();
            var region = exploration == null ? null : world.RegionAt(order.targetCell);
            if (region == null) { order.statusReason = "目标洞层已不存在"; return positions; }
            if (order.kind == DeepWorkKind.Survey && !exploration.TryExplore(order.targetCell,out string reason))
            { order.statusReason = reason; return positions; }
            int reach = order.kind == DeepWorkKind.Sample ? exploration.drillReachCells : 0;
            RectInt bounds = region.bounds;
            int bestSurveyDistance = int.MaxValue;
            for (int y = Mathf.Max(0,bounds.yMin-reach); y < Mathf.Min(world.height,bounds.yMax+reach); y++)
                for (int x = Mathf.Max(0,bounds.xMin-reach); x < Mathf.Min(world.width,bounds.xMax+reach); x++)
                {
                    var stand = new Vector2Int(x,y);
                    if (!IsKnown(stand) || !IsStandable(stand)) continue;
                    if (order.kind == DeepWorkKind.Sample)
                    { if (exploration.TryProbeFrom(region,stand,out _)) positions.Add(stand); }
                    else
                    {
                        // Survey the furthest requested reachable point already exposed by local sight.
                        // A worker cannot teleport through the unseen portion of a chamber.
                        int distance = Mathf.Abs(stand.x-order.targetCell.x)+Mathf.Abs(stand.y-order.targetCell.y);
                        if (distance < bestSurveyDistance) { positions.Clear(); bestSurveyDistance = distance; }
                        if (distance == bestSurveyDistance) positions.Add(stand);
                    }
                }
            return positions;
        }

        bool CompleteExplorationWork(DeepWorkOrder order,out string reason)
        {
            reason = string.Empty;
            if (exploration == null) { reason = "勘探系统尚未就绪"; return false; }
            // A blocked full-storage completion retries from its recorded work cell. Progress
            // was earned on site; cancelled or still-travelling orders never reach this method.
            var region = world.RegionAt(order.targetCell);
            if (region == null || !IsStandable(order.workCell)) { reason = "现场站位已改变"; return false; }
            if (order.kind == DeepWorkKind.Sample)
            {
                bool first = !exploration.TryGetSample(region,out _);
                var data = catalog == null ? null : catalog.FindItem("research_data");
                var reward = first && data != null && firstSampleResearchData > 0 ? new[] { new DeepItemAmount(data,firstSampleResearchData) } : System.Array.Empty<DeepItemAmount>();
                if (!inventory.CanComplete(null,reward,1,out reason)) return false;
                if (!exploration.CompleteSampleAt(order.targetCell,order.workCell,out reason)) return false;
                inventory.Complete(null,reward,1,out _);
            }
            else
            {
                if (!exploration.TryExplore(order.targetCell,out reason)) return false;
                exploration.RevealLocalSight(order.workCell+Vector2Int.up,exploration.workerSightCells);
                DeepSurveyFeedback.Pulse(world,new RectInt(order.workCell-new Vector2Int(3,2),new Vector2Int(7,5)),order.workCell,new Color(.62f,.83f,1));
            }
            return true;
        }
    }
}
