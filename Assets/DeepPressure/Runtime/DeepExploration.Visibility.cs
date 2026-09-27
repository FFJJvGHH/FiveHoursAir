using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public sealed partial class DeepExploration
    {
        [Header("Local discovery")]
        [Range(2,12)] public int workerSightCells = 7;
        [Range(1,8)] public int facilitySightCells = 4;
        struct Observer { public Vector2Int cell; public int revision; }
        readonly Dictionary<int,Observer> observers = new Dictionary<int,Observer>();
        readonly HashSet<int> knownFacilities = new HashSet<int>();
        int sightRevision;
        float nextSightRefresh;

        public void RefreshProximity(DeepGameSession session,bool force = false)
        {
            Initialize();
            if (!initialized || session == null) return;
            if (!force && session.SimulationTime < nextSightRefresh) return;
            nextSightRefresh = session.SimulationTime + .18f;
            bool changed = false;
            foreach (var worker in session.Workers)
            {
                if (worker == null || !worker.isActiveAndEnabled) continue;
                changed |= Observe(worker.GetInstanceID(),worker.Cell + Vector2Int.up,workerSightCells,force);
                if (worker.CurrentOrder != null && (worker.CurrentOrder.kind == DeepWorkKind.Sample || worker.CurrentOrder.kind == DeepWorkKind.Survey))
                    DeepSurveyFeedback.Ensure(worker);
            }
            foreach (var building in session.Buildings)
            {
                if (building == null || !building.isActiveAndEnabled || !building.isConstructed || building.definition == null) continue;
                var role = building.definition.role;
                if (role == DeepBuildingRole.Floor || role == DeepBuildingRole.Structure || role == DeepBuildingRole.Ladder) continue;
                int key = building.GetInstanceID();
                // Authored machines behind fog are abandoned until discovered. They must not
                // reveal their entire chamber merely because they exist in the scene hierarchy.
                if (!knownFacilities.Contains(key))
                {
                    bool known = false;
                    foreach (var cell in building.Bounds.allPositionsWithin)
                        if (world.IsInside(cell) && visible[Index(cell)]) { known = true; break; }
                    if (!known) continue;
                    knownFacilities.Add(key);
                }
                Vector2Int source = building.origin + new Vector2Int(Mathf.Max(0,building.Bounds.width/2),Mathf.Max(0,(building.Bounds.height-1)/2));
                changed |= Observe(key,source,facilitySightCells,force);
            }
            if (changed) CommitDiscovery();
        }

        bool Observe(int key,Vector2Int cell,int radius,bool force)
        {
            if (!force && observers.TryGetValue(key,out var old) && old.cell == cell && old.revision == sightRevision) return false;
            observers[key] = new Observer { cell = cell,revision = sightRevision };
            return RevealSight(cell,radius);
        }

        /// <summary>Permanent map memory. The first rock face is visible, the cells behind it are not.</summary>
        public bool RevealLocalSight(Vector2Int origin,int radius)
        {
            Initialize(); if (!initialized) return false;
            bool changed = RevealSight(origin,Mathf.Clamp(radius,1,16));
            if (changed) CommitDiscovery();
            return changed;
        }

        bool RevealSight(Vector2Int origin,int radius)
        {
            if (!world.IsInside(origin)) return false;
            bool changed = false;
            for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
            {
                var target = origin + new Vector2Int(x,y);
                if (x*x+y*y > radius*radius || !world.IsInside(target) || visible[Index(target)] || !HasSightLine(origin,target)) continue;
                visible[Index(target)] = true; changed = true;
            }
            return changed;
        }

        public bool HasSightLine(Vector2Int origin,Vector2Int target)
        {
            if (world == null || !world.IsInside(origin) || !world.IsInside(target)) return false;
            int x = origin.x,y = origin.y,dx = Mathf.Abs(target.x-x),dy = Mathf.Abs(target.y-y);
            int sx = x < target.x ? 1 : -1,sy = y < target.y ? 1 : -1,error = dx-dy;
            while (x != target.x || y != target.y)
            {
                if ((x != origin.x || y != origin.y) && world.GetTerrain(x,y) != TerrainKind.Empty) return false;
                int twice = error*2,oldX = x,oldY = y;
                if (twice > -dy) { error -= dy; x += sx; }
                if (twice < dx) { error += dx; y += sy; }
                // Do not see diagonally through two touching rock corners.
                if (x != oldX && y != oldY && world.GetTerrain(x,oldY) != TerrainKind.Empty && world.GetTerrain(oldX,y) != TerrainKind.Empty) return false;
            }
            return true;
        }

        void CommitDiscovery()
        {
            foreach (var region in world.Regions)
            {
                bool all = true,any = false;
                ForEachInside(region.bounds,cell =>
                {
                    if (world.GetTerrain(cell.x,cell.y) != TerrainKind.Empty) return;
                    any = true; if (!visible[Index(cell)]) all = false;
                });
                if (any && all) states[region] = DeepExplorationState.Explored;
            }
            RebuildDiscoveryTexture(); RefreshGasTexture(); ApplyProperties();
        }

        public bool TryProbeFrom(DeepPressureRegion region,Vector2Int stand,out Vector2Int probe)
        {
            Initialize(); probe = default;
            if (!initialized || region == null || !world.IsInside(stand) || !IsVisible(stand)) return false;
            Vector2Int nearest = default; int shortest = int.MaxValue;
            ForEachInside(region.bounds,cell =>
            {
                if (world.GetTerrain(cell.x,cell.y) != TerrainKind.Empty) return;
                int distance = Mathf.Abs(cell.x-stand.x)+Mathf.Abs(cell.y-stand.y);
                if (distance < shortest) { shortest = distance; nearest = cell; }
            });
            probe = nearest;
            return shortest <= Mathf.Max(1,drillReachCells);
        }

        public bool CompleteSampleAt(Vector2Int target,Vector2Int stand,out string message)
        {
            Initialize();
            var region = initialized ? world.RegionAt(target) : null;
            if (!TryProbeFrom(region,stand,out var probe)) { message = "测量孔超出作业位置的钻探范围"; return false; }
            return RecordSample(region,probe,out message);
        }

        bool RecordSample(DeepPressureRegion region,Vector2Int probe,out string message)
        {
            RegionSample sample = ReadAtmosphere(region,probe);
            samples[region] = sample;
            if (GetState(region) != DeepExplorationState.Explored) states[region] = DeepExplorationState.Sampled;
            scanOrigin = probe; scanStart = Time.time;
            DeepSurveyFeedback.Pulse(world,region.bounds,probe,new Color(.35f,.91f,.80f));
            RebuildDiscoveryTexture(); ApplyProperties();
            message = string.Format("样本：{0:F0} kPa · {1:F0}°C · O₂ {2:P0}",sample.pressureKPa,sample.temperatureC,sample.composition.x);
            return true;
        }

        public void ResetProximityCache()
        {
            observers.Clear(); knownFacilities.Clear(); nextSightRefresh = 0; sightRevision++;
        }
    }
}
