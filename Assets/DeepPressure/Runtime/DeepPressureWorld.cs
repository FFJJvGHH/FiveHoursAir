using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DeepPressure
{
    public enum TerrainKind { Empty, Soil, Sandstone, Shale, Basalt, Metal }
    [Serializable]
    public sealed class DeepPressureRoom
    {
        public int id, cellCount;
        public bool isOpen;
        public double volumeM3, temperatureC;
        public GasMixture gas;
        public readonly List<Vector2Int> cells = new List<Vector2Int>();
        public double PressureKPa => gas.PressureKPa(volumeM3, temperatureC);
    }

    /// <summary>Serialized map, not a runtime map generator. Background tilemaps never obstruct rooms.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class DeepPressureWorld : MonoBehaviour
    {
        public DeepLevelDefinition levelDefinition;
        [Min(1)] public int width = 64, height = 36;
        [Min(.01f)] public float cellSize = 1;
        [Min(.01f)] public float crossSectionDepthM = 1;
        public Tilemap terrain, background;
        public TerrainKind[] terrainKinds;
        [Header("Unassigned void initial atmosphere")]
        [Min(0)] public float defaultPressureKPa = 20;
        public float defaultTemperatureC = 18;
        public Vector4 defaultComposition = new Vector4(.05f, .85f, .1f, 0);
        readonly List<DeepPressureRoom> rooms = new List<DeepPressureRoom>();
        public IReadOnlyList<DeepPressureRoom> Rooms => rooms;
        int[] roomMap;
        DeepPressureRegion[] regions = Array.Empty<DeepPressureRegion>();
        public IReadOnlyList<DeepPressureRegion> Regions => regions;
        public bool HasValidTerrainData => terrainKinds != null && terrainKinds.Length == width * height;
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.y / cellSize));
        }
        public Vector3 CellToWorld(Vector2Int cell) => transform.TransformPoint(new Vector3((cell.x + .5f) * cellSize, (cell.y + .5f) * cellSize));
        public bool IsInside(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        public TerrainKind GetTerrain(int x, int y) => x < 0 || y < 0 || x >= width || y >= height || !HasValidTerrainData ? TerrainKind.Metal : terrainKinds[y * width + x];
        public DeepTerrainTile MaterialAt(Vector2Int cell) => terrain == null || !IsInside(cell) ? null : terrain.GetTile<DeepTerrainTile>(new Vector3Int(cell.x,cell.y,0));
        public void SetTerrain(int x, int y, TerrainKind kind)
        {
            if (!HasValidTerrainData) throw new InvalidOperationException("Map data must be initialized by the editor before editing cells.");
            if (!IsInside(new Vector2Int(x, y))) throw new ArgumentOutOfRangeException(nameof(x));
            terrainKinds[y * width + x] = kind;
        }
        public DeepPressureRegion RegionAt(Vector2Int cell)
        {
            DeepPressureRegion result = null;
            foreach (var region in regions)
            {
                if (region == null || !region.Contains(cell)) continue;
                if (result == null || region.priority > result.priority ||
                    (region.priority == result.priority && (Area(region) < Area(result) ||
                    (Area(region) == Area(result) && string.CompareOrdinal(region.stableId, result.stableId) < 0)))) result = region;
            }
            return result;
        }
        static long Area(DeepPressureRegion region) => (long)region.bounds.width * region.bounds.height;
        public DeepPressureRoom RoomAt(Vector2Int cell)
        {
            if (!IsInside(cell) || roomMap == null || roomMap.Length != width * height) return null;
            int id = roomMap[cell.y * width + cell.x];
            return id < 0 || id >= rooms.Count ? null : rooms[id];
        }
        void Awake()
        {
            SyncTerrainFromTilemap(); RebuildRooms();
            var materialField=GetComponent<DeepTerrainMaterialField>();
            if(materialField==null)materialField=gameObject.AddComponent<DeepTerrainMaterialField>();
            materialField.world=this;materialField.RefreshField();
        }
        [ContextMenu("Sync Terrain Data From Painted Tilemap")]
        public void SyncTerrainFromTilemap()
        {
            if (terrain == null) return;
            if (terrain.layoutGrid != null) terrain.layoutGrid.cellSize = new Vector3(cellSize,cellSize,1);
            if (!ValidateTerrainAlignment(out string alignmentMessage)) Debug.LogError(alignmentMessage,this);
            terrainKinds = new TerrainKind[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                DeepTerrainTile tile = terrain.GetTile<DeepTerrainTile>(new Vector3Int(x, y, 0));
                terrainKinds[y * width + x] = tile == null ? TerrainKind.Empty : tile.kind;
            }
        }
        public bool ValidateTerrainAlignment(out string message)
        {
            if (terrain == null) { message = "Foreground Tilemap is not assigned."; return false; }
            if (terrain.layoutGrid == null) { message = "Foreground Tilemap requires a Grid parent."; return false; }
            if (!IdentityLocal(terrain.transform) || !IdentityLocal(terrain.layoutGrid.transform))
            { message = "Grid and Tilemap children must have local position (0,0,0), rotation (0,0,0), scale (1,1,1). Move the World root instead."; return false; }
            if (terrain.layoutGrid.transform.parent != transform)
            { message = "The terrain Grid must be a direct child of DeepPressureWorld so authored cells and query coordinates agree."; return false; }
            message = "Grid / Tilemap coordinates aligned."; return true;
        }
        static bool IdentityLocal(Transform target) => target.localPosition.sqrMagnitude < 1e-8 && (target.localScale-Vector3.one).sqrMagnitude < 1e-8 && Quaternion.Angle(target.localRotation,Quaternion.identity) < .001f;
        public GasMixture displacedGas;
        public void RebuildRoomsPreservingGas()
        {
            var oldRooms=new List<DeepPressureRoom>(rooms);
            RebuildRooms();
            if(oldRooms.Count==0)return;
            foreach(var room in rooms)room.gas=default;
            var thermal=new double[rooms.Count];
            foreach(var old in oldRooms)
            {
                var overlap=new Dictionary<int,int>();int count=0;
                foreach(var cell in old.cells)
                {var current=RoomAt(cell);if(current==null)continue;overlap.TryGetValue(current.id,out int n);overlap[current.id]=n+1;count++;}
                if(count==0){displacedGas+=old.gas;continue;}
                foreach(var pair in overlap)
                {GasMixture part=old.gas.Scaled((double)pair.Value/count);rooms[pair.Key].gas+=part;thermal[pair.Key]+=part.Total*(old.temperatureC+273.15);}
            }
            foreach(var room in rooms)if(room.gas.Total>1e-9)room.temperatureC=thermal[room.id]/room.gas.Total-273.15;
        }
        [ContextMenu("Rebuild Room Topology (reset room atmosphere)")]
        public void RebuildRooms()
        {
            rooms.Clear();
            regions = GetComponentsInChildren<DeepPressureRegion>(true);
            roomMap = new int[Mathf.Max(0, width * height)];
            for (int i = 0; i < roomMap.Length; i++) roomMap[i] = -1;
            if (!HasValidTerrainData) return;
            var queue = new Queue<Vector2Int>();
            Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (terrainKinds[index] != TerrainKind.Empty || roomMap[index] >= 0) continue;
                var room = new DeepPressureRoom { id = rooms.Count };
                rooms.Add(room); queue.Enqueue(new Vector2Int(x, y)); roomMap[index] = room.id;
                double temperatureMolSum = 0, fallbackTemperatureSum = 0;
                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue(); room.cells.Add(cell); room.cellCount++;
                    if (cell.x == 0 || cell.y == 0 || cell.x == width - 1 || cell.y == height - 1) room.isOpen = true;
                    var region = RegionAt(cell);
                    double temp = region == null ? defaultTemperatureC : region.initialTemperatureC;
                    double volume = cellSize * cellSize * crossSectionDepthM;
                    GasMixture mixture = GasMixture.FromPressure(region == null ? defaultPressureKPa : region.initialPressureKPa, volume, temp, region == null ? defaultComposition : region.composition,region==null?Vector2.zero:region.reactiveFractions);
                    room.volumeM3 += volume; room.gas += mixture; temperatureMolSum += mixture.Total * (temp + 273.15); fallbackTemperatureSum += temp;
                    foreach (Vector2Int direction in directions)
                    {
                        Vector2Int next = cell + direction;
                        if (!IsInside(next)) continue;
                        int nextIndex = next.y * width + next.x;
                        if (terrainKinds[nextIndex] != TerrainKind.Empty || roomMap[nextIndex] >= 0) continue;
                        roomMap[nextIndex] = room.id; queue.Enqueue(next);
                    }
                }
                room.temperatureC = room.gas.Total > 0 ? temperatureMolSum / room.gas.Total - 273.15 : fallbackTemperatureSum / room.cellCount;
            }
        }
        public static float DensityKgPerM3(TerrainKind kind)
        {
            switch (kind) { case TerrainKind.Soil: return 1500; case TerrainKind.Sandstone: return 2200; case TerrainKind.Shale: return 2500; case TerrainKind.Basalt: return 2900; case TerrainKind.Metal: return 7800; default: return 0; }
        }
    }
}
