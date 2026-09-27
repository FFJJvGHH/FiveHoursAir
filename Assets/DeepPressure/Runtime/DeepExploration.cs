using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public enum DeepExplorationState { Unknown, Sampled, Explored }

    /// <summary>Discovery state and visual textures only. Never generates terrain or moves workers.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed partial class DeepExploration : MonoBehaviour
    {
        [Serializable]
        public struct RegionSample
        {
            public Vector4 composition;
            public Vector2 reactiveComposition;
            public float pressureKPa, temperatureC;
            public Vector2Int sampleCell;
            public float sampledAtSeconds;
        }

        public DeepPressureWorld world;
        [Tooltip("Editor-authored full-map quad, above terrain, labels and pipes.")]
        public SpriteRenderer fogRenderer;
        [Tooltip("Editor-authored full-map quad using DeepPressure/GasAtmosphere.")]
        public SpriteRenderer gasRenderer;
        [Min(1)] public int drillReachCells = 12;
        [Tooltip("Legacy save field. Equipment never reveals terrain or grants a remote move.")]
        public bool hasIsolationEquipment;
        [Header("Game entry thresholds, not real-world safety guidance")]
        public float minimumPressureKPa = 60, maximumPressureKPa = 135;
        public float minimumOxygenPartialKPa = 16, maximumOxygenPartialKPa = 30;
        [Range(0, 1)] public float maximumCarbonDioxideFraction = .03f;
        public float minimumTemperatureC = 0, maximumTemperatureC = 45;
        [Header("Visuals")]
        [Min(1)] public float visualPressureScaleKPa = 1000;
        [Range(.05f, 2)] public float visualRefreshSeconds = .25f;
        public RectInt[] initialExploredAreas =
        {
            new RectInt(4, 24, 26, 8), new RectInt(5, 13, 31, 8),
            new RectInt(31, 4, 3, 27), new RectInt(28, 26, 6, 3)
        };

        readonly Dictionary<DeepPressureRegion, DeepExplorationState> states = new Dictionary<DeepPressureRegion, DeepExplorationState>();
        readonly Dictionary<DeepPressureRegion, RegionSample> samples = new Dictionary<DeepPressureRegion, RegionSample>();
        static readonly Vector2Int[] Directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        bool[] visible;
        int[] distanceToExplored;
        Texture2D visibilityTexture, compositionTexture, pressureTexture, revealTexture,reactiveTexture;
        float[] revealValues;
        Color32[] revealPixels;
        Vector2 scanOrigin;
        float scanStart;
        Color32[] visibilityPixels, compositionPixels, pressurePixels,reactivePixels;
        MaterialPropertyBlock fogProperties, gasProperties;
        float nextVisualRefresh;
        bool initialized;

        void Awake() => Initialize();

        void Initialize()
        {
            if (initialized) return;
            if (world == null) world = GetComponentInParent<DeepPressureWorld>();
            if (world == null) world = FindObjectOfType<DeepPressureWorld>();
            if (world == null || world.width < 1 || world.height < 1) return;
            initialized = true;
            int count = world.width * world.height;
            visible = new bool[count]; distanceToExplored = new int[count];
            revealValues=new float[count];revealPixels=new Color32[count];
            visibilityPixels = new Color32[count]; compositionPixels = new Color32[count]; pressurePixels = new Color32[count];
            reactivePixels = new Color32[count];
            visibilityTexture = CreateTexture("DeepPressure Discovery", world.width, world.height);
            revealTexture = CreateTexture("DeepPressure Reveal",world.width,world.height);
            compositionTexture = CreateTexture("DeepPressure Gas Fractions", world.width, world.height);
            pressureTexture = CreateTexture("DeepPressure Gas Pressure", world.width, world.height);
            reactiveTexture = CreateTexture("DeepPressure Reactive Gases",world.width,world.height);
            compositionTexture.filterMode = FilterMode.Bilinear;
            reactiveTexture.filterMode = FilterMode.Bilinear;
            pressureTexture.filterMode = FilterMode.Bilinear;
            fogProperties = new MaterialPropertyBlock(); gasProperties = new MaterialPropertyBlock();
            if (initialExploredAreas != null) foreach (RectInt area in initialExploredAreas) RevealRect(area);
            RevealSolidRim();
            for(int i=0;i<count;i++)revealValues[i]=visible[i]?1:0;
            UpdateRevealTexture(0);
            foreach (DeepPressureRegion region in world.GetComponentsInChildren<DeepPressureRegion>(true))
            {
                bool allVisible = true, anyInside = false;
                ForEachInside(region.bounds, cell => { anyInside = true; if (!visible[Index(cell)]) allVisible = false; });
                states[region] = anyInside && allVisible ? DeepExplorationState.Explored : DeepExplorationState.Unknown;
            }
            if (fogRenderer != null) fogRenderer.sortingOrder = 32760;
            RebuildDiscoveryTexture(); RefreshGasTexture(); ApplyProperties();
        }

        void Update()
        {
            if (!initialized) Initialize();
            if(initialized)UpdateRevealTexture(Time.unscaledDeltaTime);
            if (!initialized || Time.unscaledTime < nextVisualRefresh) return;
            nextVisualRefresh = Time.unscaledTime + Mathf.Max(.05f, visualRefreshSeconds);
            RefreshGasTexture(); ApplyProperties();
        }

        public bool IsVisible(Vector2Int cell)
        {
            Initialize();
            return initialized && world.IsInside(cell) && visible[Index(cell)];
        }
        public void RevealAroundWork(Vector2Int cell)
        {
            Initialize();if(!initialized)return;
            // Excavation exposes the cut face, never the room behind an unbroken wall.
            if (RevealSight(cell,2)) CommitDiscovery();
        }
        public void RefreshAfterTerrainChange()
        {Initialize();if(initialized){sightRevision++; RebuildDiscoveryTexture();RefreshGasTexture();ApplyProperties();}}

        public DeepExplorationState GetState(DeepPressureRegion region)
        {
            Initialize();
            return region != null && states.TryGetValue(region, out DeepExplorationState state) ? state : DeepExplorationState.Unknown;
        }

        public bool IsSampled(DeepPressureRegion region)
        {
            DeepExplorationState state = GetState(region);
            return state == DeepExplorationState.Sampled || state == DeepExplorationState.Explored;
        }

        public bool TryGetSample(DeepPressureRegion region, out RegionSample sample)
        {
            Initialize();
            sample = default;
            return region != null && samples.TryGetValue(region, out sample);
        }

        public bool TrySample(Vector2Int cell, out string message)
        {
            Initialize();
            if (!initialized || !world.IsInside(cell)) { message = "超出勘探范围。"; return false; }
            DeepPressureRegion region = world.RegionAt(cell);
            if (region == null) { message = "未发现可取样孔隙。"; return false; }
            if (!FindProbeCell(region, out Vector2Int probe, out int distance))
            { message = "此处为致密岩层。"; return false; }
            if (distance > Mathf.Max(1, drillReachCells))
            { message = "无法取样：距已探索边界 " + distance + " 格，钻探范围为 " + drillReachCells + " 格。"; return false; }
            return RecordSample(region,probe,out message);
        }

        public bool TryExplore(Vector2Int cell, out string message)
        {
            Initialize();
            if (!initialized || !world.IsInside(cell)) { message = "无法探索：目标不在关卡范围内。"; return false; }
            DeepPressureRegion region = world.RegionAt(cell);
            if (region == null) { message = "此处尚未发现洞室。"; return false; }
            if (!samples.TryGetValue(region, out RegionSample previous))
            { message = "需要先取样。"; return false; }
            RegionSample current = ReadAtmosphere(region, previous.sampleCell);
            if (!MeetsEntryThresholds(current, out string reason))
            { message = "进入受阻：" + reason + " · 先通过气路调节环境"; return false; }
            // This API is a permission check. Actual discovery follows the worker's sight;
            // a click or the legacy isolation switch cannot reveal a sealed chamber.
            message = "读数符合进入条件 · 派遣工人抵达入口后逐步探明";
            return true;
        }

        bool MeetsEntryThresholds(RegionSample sample, out string reason)
        {
            if (!Finite(sample.pressureKPa) || !Finite(sample.temperatureC) || !Finite(sample.composition.x) || !Finite(sample.composition.z) || !Finite(sample.reactiveComposition.x) || !Finite(sample.reactiveComposition.y))
            { reason = "传感器读数无效"; return false; }
            if (sample.pressureKPa < minimumPressureKPa || sample.pressureKPa > maximumPressureKPa)
            { reason = string.Format("压力 {0:F1} kPa 超出游戏入口范围 {1:F0}–{2:F0} kPa", sample.pressureKPa, minimumPressureKPa, maximumPressureKPa); return false; }
            float oxygenPartial = sample.pressureKPa * sample.composition.x;
            if (oxygenPartial < minimumOxygenPartialKPa || oxygenPartial > maximumOxygenPartialKPa)
            { reason = string.Format("氧分压 {0:F1} kPa 不符合游戏入口阈值", oxygenPartial); return false; }
            if (sample.composition.z > maximumCarbonDioxideFraction)
            { reason = string.Format("CO₂ 比例 {0:P1} 高于游戏入口阈值 {1:P1}", sample.composition.z, maximumCarbonDioxideFraction); return false; }
            if (sample.reactiveComposition.x > .025f || sample.reactiveComposition.y > .005f)
            { reason = string.Format("活性气体过高 · CH₄ {0:P1} / 工艺蒸气 {1:P1}",sample.reactiveComposition.x,sample.reactiveComposition.y); return false; }
            if (sample.temperatureC < minimumTemperatureC || sample.temperatureC > maximumTemperatureC)
            { reason = string.Format("温度 {0:F1}°C 超出游戏入口范围", sample.temperatureC); return false; }
            reason = "入口读数符合游戏阈值"; return true;
        }

        bool FindProbeCell(DeepPressureRegion region, out Vector2Int probe, out int distance)
        {
            Vector2Int best = default; int bestDistance = int.MaxValue;
            ForEachInside(region.bounds, cell =>
            {
                if (world.GetTerrain(cell.x, cell.y) != TerrainKind.Empty) return;
                int candidate = distanceToExplored[Index(cell)];
                if (candidate < bestDistance) { best = cell; bestDistance = candidate; }
            });
            probe = best; distance = bestDistance;
            return bestDistance < int.MaxValue;
        }

        RegionSample ReadAtmosphere(DeepPressureRegion region, Vector2Int cell)
        {
            var room = world.RoomAt(cell);
            Vector4 composition = region.composition;
            Vector2 reactive = region.reactiveFractions;
            float pressure = region.initialPressureKPa, temperature = region.initialTemperatureC;
            var field = world.GetComponent<DeepAtmosphereField>();
            if (field != null && field.IsInitialized)
            {
                var gas = field.Sample(cell); double total = gas.Total;
                pressure = (float)field.PressureKPa(cell); temperature = (float)field.TemperatureC(cell);
                composition = total > 1e-9 ? new Vector4((float)(gas.oxygen/total),(float)(gas.nitrogen/total),(float)(gas.carbonDioxide/total),(float)(gas.waterVapour/total)) : Vector4.zero;
                reactive = total > 1e-9 ? new Vector2((float)(gas.methane/total),(float)(gas.processVapor/total)) : Vector2.zero;
            }
            else if (room != null)
            {
                pressure = (float)room.PressureKPa; temperature = (float)room.temperatureC;
                double total = room.gas.Total;
                composition = total > 1e-9 ? new Vector4((float)(room.gas.oxygen / total), (float)(room.gas.nitrogen / total), (float)(room.gas.carbonDioxide / total), (float)(room.gas.waterVapour / total)) : Vector4.zero;
                reactive = total > 1e-9 ? new Vector2((float)(room.gas.methane/total),(float)(room.gas.processVapor/total)) : Vector2.zero;
            }
            else
            {
                composition = new Vector4(Mathf.Max(0, composition.x), Mathf.Max(0, composition.y), Mathf.Max(0, composition.z), Mathf.Max(0, composition.w));
                reactive = new Vector2(Mathf.Max(0,reactive.x),Mathf.Max(0,reactive.y));
                float total = composition.x + composition.y + composition.z + composition.w + reactive.x + reactive.y;
                composition = total > 0 ? composition / total : Vector4.zero;
                reactive = total > 0 ? reactive/total : Vector2.zero;
            }
            return new RegionSample { composition = composition,reactiveComposition = reactive, pressureKPa = pressure, temperatureC = temperature, sampleCell = cell, sampledAtSeconds = Time.time };
        }

        void RevealRect(RectInt bounds) => ForEachInside(bounds, cell => visible[Index(cell)] = true);
        void RevealSolidRim()
        {
            var original=(bool[])visible.Clone();
            for(int y=0;y<world.height;y++)for(int x=0;x<world.width;x++)
            {
                var p=new Vector2Int(x,y);if(!original[Index(p)])continue;
                foreach(var d in Directions)
                {var n=p+d;if(world.IsInside(n)&&world.GetTerrain(n.x,n.y)!=TerrainKind.Empty)visible[Index(n)]=true;}
            }
        }
        void ForEachInside(RectInt bounds, Action<Vector2Int> action)
        {
            for (int y = Mathf.Max(0, bounds.yMin); y < Mathf.Min(world.height, bounds.yMax); y++)
                for (int x = Mathf.Max(0, bounds.xMin); x < Mathf.Min(world.width, bounds.xMax); x++) action(new Vector2Int(x, y));
        }
        int Index(Vector2Int cell) => cell.y * world.width + cell.x;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static Texture2D CreateTexture(string name, int width, int height) => new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };

        void RebuildDiscoveryTexture()
        {
            var queue = new Queue<Vector2Int>();
            for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
            {
                var cell = new Vector2Int(x, y); int index = Index(cell);
                bool sampled = world.RegionAt(cell) is DeepPressureRegion region && states.TryGetValue(region, out DeepExplorationState state) && state == DeepExplorationState.Sampled;
                visibilityPixels[index] = new Color32(visible[index] ? (byte)255 : (byte)0, sampled ? (byte)255 : (byte)0, 0, 255);
                distanceToExplored[index] = visible[index] ? 0 : int.MaxValue;
                if (visible[index]) queue.Enqueue(cell);
            }
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue(); int nextDistance = distanceToExplored[Index(cell)] + 1;
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = cell + direction;
                    if (!world.IsInside(next) || distanceToExplored[Index(next)] <= nextDistance) continue;
                    distanceToExplored[Index(next)] = nextDistance; queue.Enqueue(next);
                }
            }
            visibilityTexture.SetPixels32(visibilityPixels); visibilityTexture.Apply(false, false);
        }

        void RefreshGasTexture()
        {
            var field = world.GetComponent<DeepAtmosphereField>();
            bool localized = field != null && field.IsInitialized;
            for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
            {
                var cell = new Vector2Int(x, y); int index = Index(cell);
                compositionPixels[index] = default; pressurePixels[index] = default; reactivePixels[index] = default;
                if (!visible[index] || world.GetTerrain(x, y) != TerrainKind.Empty) continue;
                DeepPressureRoom room = world.RoomAt(cell);
                if (room == null) continue;
                var gas = localized ? field.Sample(cell) : room.gas;
                if (gas.Total <= 1e-9 || !gas.IsFiniteAndNonnegative) continue;
                double total = gas.Total;
                compositionPixels[index] = (Color32)new Color((float)(gas.oxygen / total), (float)(gas.nitrogen / total), (float)(gas.carbonDioxide / total), (float)(gas.waterVapour / total));
                reactivePixels[index] = (Color32)new Color((float)(gas.methane/total),(float)(gas.processVapor/total),0,1);
                int floorDistance=0;
                while(floorDistance<8&&world.GetTerrain(x,y-floorDistance-1)==TerrainKind.Empty)floorDistance++;
                pressurePixels[index] = (Color32)new Color(Mathf.Clamp01((float)(localized ? field.PressureKPa(cell) : room.PressureKPa) / Mathf.Max(1, visualPressureScaleKPa)), 1, (floorDistance+.5f)/8, Mathf.Clamp01(((float)(localized ? field.TemperatureC(cell) : room.temperatureC)+20)/120));
            }
            compositionTexture.SetPixels32(compositionPixels); compositionTexture.Apply(false, false);
            pressureTexture.SetPixels32(pressurePixels); pressureTexture.Apply(false, false);
            reactiveTexture.SetPixels32(reactivePixels); reactiveTexture.Apply(false,false);
        }

        void ApplyProperties()
        {
            if (fogRenderer != null)
            {
                fogRenderer.GetPropertyBlock(fogProperties); SetMapProperties(fogProperties);
                fogProperties.SetTexture("_VisibilityTex", visibilityTexture);fogProperties.SetTexture("_RevealTex",revealTexture);fogProperties.SetVector("_ScanOrigin",new Vector4(scanOrigin.x,scanOrigin.y,0,0));fogProperties.SetFloat("_ScanStart",scanStart);fogProperties.SetFloat("_DiscoveryActive", 1); fogRenderer.SetPropertyBlock(fogProperties);
            }
            if (gasRenderer != null)
            {
                gasRenderer.GetPropertyBlock(gasProperties); SetMapProperties(gasProperties);
                gasProperties.SetTexture("_VisibilityTex", visibilityTexture); gasProperties.SetTexture("_GasTex", compositionTexture);
                gasProperties.SetTexture("_ReactiveTex",reactiveTexture);
                gasProperties.SetTexture("_PressureTex", pressureTexture); gasProperties.SetFloat("_PressureScaleKPa",Mathf.Max(1,visualPressureScaleKPa)); gasRenderer.SetPropertyBlock(gasProperties);
            }
        }
        void SetMapProperties(MaterialPropertyBlock block)
        {
            block.SetMatrix("_WorldToMap", world.transform.worldToLocalMatrix);
            block.SetVector("_GridSize", new Vector4(world.width, world.height, 0, 0));
            block.SetFloat("_CellSize", world.cellSize);
        }
        void OnDestroy()
        {
            ReleaseTexture(visibilityTexture); ReleaseTexture(compositionTexture); ReleaseTexture(pressureTexture);
            ReleaseTexture(revealTexture);
            ReleaseTexture(reactiveTexture);
        }
        void UpdateRevealTexture(float dt)
        {
            for(int i=0;i<revealValues.Length;i++)
            {revealValues[i]=Mathf.MoveTowards(revealValues[i],visible[i]?1:0,dt*.85f);byte value=(byte)(revealValues[i]*255);revealPixels[i]=new Color32(value,0,0,255);}
            revealTexture.SetPixels32(revealPixels);revealTexture.Apply(false,false);
        }
        static void ReleaseTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
        }
    }
}

