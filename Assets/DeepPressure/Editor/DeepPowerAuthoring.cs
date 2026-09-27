using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeepPressure.Editor
{
    /// <summary>One-time migration of the abandoned station's existing copper bus and emergency fuel reserve.</summary>
    public static class DeepPowerAuthoring
    {
        public static void Upgrade(DeepGameSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before upgrading authored power infrastructure.");
            session.catalog = DeepCatalogBuilder.CreateOrUpdateCatalog();
            session.useWiredPower = true;
            if (session.powerContentRevision < 1)
            {
                var starting = new List<DeepItemAmount>(session.startingInventory ?? Array.Empty<DeepItemAmount>());
                var fuel = session.catalog.FindItem("fuel");
                if (fuel != null && !starting.Exists(x=>x.item == fuel)) starting.Add(new DeepItemAmount(fuel,18));
                session.startingInventory = starting.ToArray();
                var buildings = session.world.GetComponentsInChildren<DeepBuildingInstance>(true);
                var generators = new List<DeepBuildingInstance>();
                foreach (var building in buildings)
                    if (building.definition != null && building.isConstructed && building.definition.role == DeepBuildingRole.Generator) generators.Add(building);
                var wires = new HashSet<Vector2Int>(session.completedWireCells ?? new List<Vector2Int>());
                foreach (var building in buildings)
                {
                    if (building.definition == null || !building.isConstructed || building.definition.powerRequired <= 0) continue;
                    DeepBuildingInstance closest = null; int bestDistance = int.MaxValue;
                    Vector2Int end = session.PowerTerminal(building);
                    foreach (var generator in generators)
                    {
                        Vector2Int start = session.PowerTerminal(generator);
                        int distance = Mathf.Abs(start.x-end.x)+Mathf.Abs(start.y-end.y);
                        if (distance < bestDistance) { bestDistance = distance; closest = generator; }
                    }
                    if (closest == null) continue;
                    Vector2Int cell = session.PowerTerminal(closest); wires.Add(cell);
                    while (cell.x != end.x) { cell.x += Math.Sign(end.x-cell.x); wires.Add(cell); }
                    while (cell.y != end.y) { cell.y += Math.Sign(end.y-cell.y); wires.Add(cell); }
                }
                session.completedWireCells = new List<Vector2Int>(wires);
                session.completedWireCells.Sort((a,b)=>a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                session.powerContentRevision = 1;
            }
            var visual = session.GetComponent<DeepPowerWireVisual>();
            if (visual == null) visual = session.gameObject.AddComponent<DeepPowerWireVisual>();
            visual.session = session; EditorUtility.SetDirty(visual); EditorUtility.SetDirty(session);
            session.InvalidatePowerTopology();
        }
    }
}
