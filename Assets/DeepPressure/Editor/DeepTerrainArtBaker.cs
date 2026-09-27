using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace DeepPressure.Editor
{
    /// <summary>Offline procedural asset baker. Outputs independent tiles, never a flattened map image.</summary>
    public static class DeepTerrainArtBaker
    {
        public const string Folder = "Assets/DeepPressure/Art/TerrainV2";
        public const int PixelsPerCell = 128, Variations = 3, Columns = 12, Rows = 12, Gutter = 2;
        public const int Stride = PixelsPerCell + Gutter * 2;
        static int MaskCount => DeepTerrainTile.BlobMasks.Length;
        const string Version = "terrain-v3.0-128px-47blobmasks-3variations";
        static DeepTerrainTile[] cached;
        static readonly Color[] Palette =
        {
            Color.clear, new Color(.38f,.295f,.245f), new Color(.48f,.405f,.315f),
            new Color(.335f,.305f,.415f), new Color(.275f,.37f,.415f), new Color(.33f,.425f,.465f)
        };
        struct Surface { public Color color; public float height; }
        struct CellFeature { public float nearest, second; public float id; public Vector2 offset; }

        public static Material TerrainMaterial
        {
            get
            {
                string path = Folder + "/TerrainHD2D.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Shader shader = Shader.Find("DeepPressure/Sprite-Lit-AO");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null) throw new InvalidOperationException("A URP 2D Sprite Lit shader is required to bake terrain materials.");
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                else if (material.shader != shader) { material.shader = shader; EditorUtility.SetDirty(material); }
                return material;
            }
        }

        public static DeepTerrainTile[] Prepare() => Prepare(false);
        public static DeepTerrainTile GetTile(TerrainKind kind)
        {
            if (cached == null) cached = Prepare();
            int index = (int)kind;
            return index >= 0 && index < cached.Length ? cached[index] : null;
        }

        [MenuItem("深压/素材/重新烘焙有机地形 V3")]
        public static void Rebuild()
        {
            Prepare(true);
            Debug.Log("Terrain V3: five materials, 705 independent blob tile sprites, aligned Color/Normal/AO atlases generated.");
        }

        static DeepTerrainTile[] Prepare(bool force)
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string marker = Folder + "/BakeManifest.txt";
            bool current = !force && File.Exists(marker) && File.ReadAllText(marker).StartsWith(Version, StringComparison.Ordinal);
            var result = new DeepTerrainTile[(int)TerrainKind.Metal + 1];
            try
            {
                for (int kindIndex = 1; kindIndex <= (int)TerrainKind.Metal; kindIndex++)
                {
                    var kind = (TerrainKind)kindIndex;
                    string path = Folder + "/" + kind + ".asset";
                    var tile = AssetDatabase.LoadAssetAtPath<DeepTerrainTile>(path);
                    bool complete = current && tile != null && tile.variants != null && tile.variants.Length == Variations * MaskCount;
                    if (complete) foreach (Sprite sprite in tile.variants) if (sprite == null) { complete = false; break; }
                    complete &= File.Exists(Folder + "/" + kind + "_Color.png") && File.Exists(Folder + "/" + kind + "_Normal.png") && File.Exists(Folder + "/" + kind + "_AO.png");
                    if (!complete)
                    {
                        EditorUtility.DisplayProgressBar("Deep Pressure • Organic Terrain V3", "Baking " + kind + " eight-neighbor contours", (kindIndex - 1f) / 5);
                        BakeAtlases(kind);
                        if (tile == null) { tile = ScriptableObject.CreateInstance<DeepTerrainTile>(); AssetDatabase.CreateAsset(tile, path); }
                        ConfigureTile(tile, kind);
                    }
                    result[kindIndex] = tile;
                }
                Material material = TerrainMaterial;
                EditorUtility.SetDirty(material);
                File.WriteAllText(marker, Version + "\n5 materials / 128 pixels per cell / 47 eight-neighbor blob masks x 3 variations\nColor + tangent normal + AO, same alpha and atlas rectangles.\n");
                File.WriteAllText(Folder + "/README.md", "# Organic Terrain V3\n\nGenerated offline by DeepTerrainArtBaker.Prepare(). Each of five materials has 47 eight-neighbor blob masks x 3 stable coordinate variations at 128 pixels per cell: 705 independent sprites. Diagonals are normalized unless both adjoining cardinal neighbors exist. Excavating a cell refreshes all eight neighbors, including concave inside corners.\n\nNatural terrain has broad eroded edges, approximately 4–20 pixels of inset, 21-pixel rounded convex corners, and 12-pixel concave cutouts. All variations approach the same endpoint height and tangent, so adjoining exposed edges meet. Interior material boundaries remain filled. Manufactured metal intentionally retains a small machined bevel. Logical collision, pathfinding, pressure rooms and construction retain their editable grid.\n\nColor, tangent normal and AO share the exact same coverage and atlas rectangles. Normal derives from the same relief; AO derives from recesses. Atlases are 1584 x 1584 with 2-pixel extruded gutters. Secondary textures are _NormalMap and _AOMap; use TerrainHD2D.mat and Light2D Normal Map Quality. The folder path remains TerrainV2 to preserve existing asset GUIDs.\n");
                AssetDatabase.SaveAssets(); cached = result;
                return result;
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void ConfigureTile(DeepTerrainTile tile, TerrainKind kind)
        {
            var sprites = new Dictionary<string, Sprite>();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + kind + "_Color.png"))
                if (asset is Sprite sprite) sprites[sprite.name] = sprite;
            tile.kind = kind; tile.variationCount = Variations; tile.variationSeed = 1709 + (int)kind * 83;
            tile.useDiagonalConnections = true; tile.connectAcrossMaterials = true; tile.variants = new Sprite[MaskCount * Variations];
            for (int variant = 0; variant < Variations; variant++) for (int maskIndex = 0; maskIndex < MaskCount; maskIndex++)
            {
                int mask = DeepTerrainTile.BlobMasks[maskIndex];
                string name = SpriteName(kind.ToString(), variant, mask);
                if (!sprites.TryGetValue(name, out Sprite sprite)) throw new InvalidOperationException("Missing baked sprite: " + name);
                tile.variants[variant * MaskCount + maskIndex] = sprite;
            }
            int k = (int)kind;
            tile.densityKgM3 = new[] { 0f, 1500, 2200, 2500, 2900, 7800 }[k];
            tile.excavationResistance = new[] { 0f, 1, 3, 4, 7, 9 }[k];
            tile.porosity = new[] { 0f, .35f, .22f, .07f, .025f, 0 }[k];
            tile.permeability = new[] { 0f, .3f, .65f, .006f, .015f, 0 }[k];
            EditorUtility.SetDirty(tile);
        }

        public static string SpriteName(string kind, int variation, int mask) => kind + "_v" + variation + "_m" + mask.ToString("00");

        static void BakeAtlases(TerrainKind kind)
        {
            int width = Columns * Stride, height = Rows * Stride;
            var colors = new Color32[width * height]; var normals = new Color32[width * height]; var aos = new Color32[width * height];
            for (int variant = 0; variant < Variations; variant++)
            {
                var surface = new Surface[PixelsPerCell * PixelsPerCell];
                for (int y = 0; y < PixelsPerCell; y++) for (int x = 0; x < PixelsPerCell; x++)
                {
                    float u = (x + .5f) / PixelsPerCell, v = (y + .5f) / PixelsPerCell;
                    Surface common = Evaluate(kind, u, v, 0);
                    Surface varied = variant == 0 ? common : Evaluate(kind, u, v, variant * 127);
                    // All variants approach identical periodic edge data before meeting the next tile.
                    float edge = Mathf.Min(u, 1 - u, v, 1 - v);
                    float blend = Smooth(.015f, .17f, edge);
                    surface[y * PixelsPerCell + x] = new Surface { color = Color.Lerp(common.color, varied.color, blend), height = Mathf.Lerp(common.height, varied.height, blend) };
                }
                for (int maskIndex = 0; maskIndex < MaskCount; maskIndex++)
                {
                    int mask = DeepTerrainTile.BlobMasks[maskIndex];
                    int spriteIndex = variant * MaskCount + maskIndex, originX = spriteIndex % Columns * Stride + Gutter, originY = spriteIndex / Columns * Stride + Gutter;
                    var localHeight = new float[surface.Length]; var coverage = new float[surface.Length]; var localColor = new Color[surface.Length];
                    for (int y = 0; y < PixelsPerCell; y++) for (int x = 0; x < PixelsPerCell; x++)
                    {
                        int index = y * PixelsPerCell + x;
                        float distance = ExposedEdgeDistance(x, y, mask, kind, variant);
                        float bevel = Smooth(0, 5.5f, distance);
                        coverage[index] = Mathf.Clamp01(distance + .45f);
                        localHeight[index] = surface[index].height - (1 - bevel) * .19f;
                        Color color = surface[index].color * Mathf.Lerp(.72f, 1, bevel);
                        color += new Color(.013f,.014f,.016f,0) * Mathf.Exp(-Mathf.Pow(distance - 2.6f, 2));
                        color.a = coverage[index]; localColor[index] = color;
                    }
                    for (int y = 0; y < PixelsPerCell; y++) for (int x = 0; x < PixelsPerCell; x++)
                    {
                        int local = y * PixelsPerCell + x, target = (originY + y) * width + originX + x;
                        float left = HeightAt(localHeight, x - 1, y, mask), right = HeightAt(localHeight, x + 1, y, mask);
                        float bottom = HeightAt(localHeight, x, y - 1, mask), top = HeightAt(localHeight, x, y + 1, mask);
                        Vector3 normal = new Vector3(-(right - left) * 5.8f, -(top - bottom) * 5.8f, 1).normalized;
                        float broad = (HeightAt(localHeight, x - 3, y, mask) + HeightAt(localHeight, x + 3, y, mask) + HeightAt(localHeight, x, y - 3, mask) + HeightAt(localHeight, x, y + 3, mask)) * .25f;
                        float cavity = Mathf.Max(0, broad - localHeight[local]);
                        float ao = Mathf.Clamp(1 - cavity * 2.8f, .53f, 1);
                        colors[target] = localColor[local];
                        normals[target] = (Color32)new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, coverage[local]);
                        aos[target] = (Color32)new Color(ao, ao, ao, coverage[local]);
                    }
                    Extrude(colors, width, originX, originY); Extrude(normals, width, originX, originY); Extrude(aos, width, originX, originY);
                }
            }
            string stem = Folder + "/" + kind;
            WriteImage(stem + "_Normal.png", width, height, normals);
            WriteImage(stem + "_AO.png", width, height, aos);
            WriteImage(stem + "_Color.png", width, height, colors);
            AssetDatabase.ImportAsset(stem + "_Normal.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(stem + "_AO.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(stem + "_Color.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(stem + "_Color.png");
            importer.secondarySpriteTextures = new[]
            {
                new SecondarySpriteTexture { name = "_NormalMap", texture = AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_Normal.png") },
                new SecondarySpriteTexture { name = "_AOMap", texture = AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_AO.png") }
            };
            SetSpriteRects(importer, kind.ToString());
            importer.SaveAndReimport();
        }

        static void SetSpriteRects(TextureImporter importer, string kind)
        {
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = new Dictionary<string, UnityEditor.GUID>();
            foreach (SpriteRect rect in provider.GetSpriteRects()) existing[rect.name] = rect.spriteID;
            var rectangles = new SpriteRect[MaskCount * Variations];
            var pairs = new List<SpriteNameFileIdPair>();
            for (int variation = 0; variation < Variations; variation++) for (int maskIndex = 0; maskIndex < MaskCount; maskIndex++)
            {
                int mask = DeepTerrainTile.BlobMasks[maskIndex];
                int index = variation * MaskCount + maskIndex; string name = SpriteName(kind,variation,mask);
                UnityEditor.GUID id = existing.TryGetValue(name,out UnityEditor.GUID previous) ? previous : UnityEditor.GUID.Generate();
                rectangles[index] = new SpriteRect
                {
                    name = name, spriteID = id, pivot = new Vector2(.5f,.5f), alignment = SpriteAlignment.Center,
                    rect = new Rect(index % Columns * Stride + Gutter,index / Columns * Stride + Gutter,PixelsPerCell,PixelsPerCell)
                };
                pairs.Add(new SpriteNameFileIdPair(name,id));
            }
            provider.SetSpriteRects(rectangles);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
            provider.Apply();
        }

        static Surface Evaluate(TerrainKind kind, float u, float v, int variant)
        {
            int seed = (int)kind * 109 + 3181 + variant;
            float grain = Noise(u, v, 48, seed + 1), fine = Noise(u, v, 23, seed + 2), broad = Noise(u, v, 7, seed + 3);
            Color color = Palette[(int)kind];
            float relief = .5f, shade = .94f + (grain - .5f) * .1f + (fine - .5f) * .12f;
            CellFeature feature = Voronoi(u, v, 4, seed);
            float border = feature.second - feature.nearest;
            if (kind == TerrainKind.Soil)
            {
                // Sixteen irregular pebble clusters in a porous fine-grained matrix.
                float pebble = 1 - Smooth(.17f, .33f, feature.nearest);
                float root = Mathf.Abs(Mathf.Sin((u * 3 + .16f * Mathf.Sin(v * Mathf.PI * 6) + .12f * Mathf.Sin(v * Mathf.PI * 2)) * Mathf.PI));
                float roots = (1 - Smooth(.025f, .075f, root)) * Smooth(.26f, .61f, broad);
                float pore = (1 - Smooth(.08f, .16f, grain)) * .25f;
                relief += pebble * (.10f + feature.id * .03f) + roots * .035f - pore * .1f + (fine - .5f) * .03f;
                shade += pebble * (.05f + feature.id * .12f) - roots * .17f - pore * .4f;
                color = Color.Lerp(color, new Color(.42f,.385f,.345f), pebble * .38f);
            }
            else if (kind == TerrainKind.Sandstone)
            {
                // Fine continuous sediment, about fourteen laminae per 1 m tile.
                float layer = v * 14 + Mathf.Sin(u * Mathf.PI * 2) * .36f + Mathf.Sin(u * Mathf.PI * 6) * .11f;
                float wave = Mathf.Abs(Mathf.Sin(layer * Mathf.PI));
                float seam = 1 - Smooth(.035f, .18f, wave);
                float lens = Smooth(.3f,.75f,fine) * Smooth(.05f,.14f,border);
                relief += wave * .038f - seam * .05f + lens * .021f;
                shade += Mathf.Sin(layer * Mathf.PI) * .045f - seam * .16f + (broad - .5f) * .06f;
                color = Color.Lerp(color, new Color(.54f,.475f,.365f), lens * .19f);
            }
            else if (kind == TerrainKind.Shale)
            {
                // Overlapping thin violet flakes, not large smooth strata blocks.
                float lamina = Mathf.Abs(Mathf.Sin((v * 11 + u * 3 + Mathf.Sin(u * Mathf.PI * 2) * .23f) * Mathf.PI));
                float split = 1 - Smooth(.045f, .16f, lamina);
                float fracture = 1 - Smooth(.035f, .10f, border);
                relief += lamina * .06f + feature.id * .025f - split * .05f - fracture * .055f;
                shade += (feature.id - .5f) * .18f - split * .18f - fracture * .14f;
                color = Color.Lerp(color, new Color(.29f,.355f,.43f), feature.id * .25f);
            }
            else if (kind == TerrainKind.Basalt)
            {
                // Sixteen small polygonal joint facets with mineral flecks.
                float joint = 1 - Smooth(.025f, .095f, border);
                float bevel = Smooth(.02f, .13f, border);
                float mineral = Smooth(.75f,.91f,grain) * Smooth(.5f,.7f,broad);
                relief += bevel * .075f + feature.id * .022f - joint * .065f + mineral * .018f;
                shade += (feature.id - .5f) * .15f - joint * .29f + mineral * .24f;
                color = Color.Lerp(color, new Color(.33f,.42f,.445f), broad * .2f);
            }
            else
            {
                // Four inset service plates with sixteen small fasteners, plus narrow machined grooves.
                float panelX = Mathf.Min(Frac(u * 2), 1 - Frac(u * 2));
                float panelY = Mathf.Min(Frac(v * 2), 1 - Frac(v * 2));
                float seam = 1 - Smooth(.018f, .038f, Mathf.Min(panelX,panelY));
                float rivetX = Frac(u * 4) - .5f, rivetY = Frac(v * 4) - .5f;
                float radius = Mathf.Sqrt(rivetX * rivetX + rivetY * rivetY);
                float rivet = 1 - Smooth(.035f, .078f, radius);
                float ring = (1 - Smooth(.073f,.096f,radius)) * Smooth(.055f,.073f,radius);
                float machining = Mathf.Sin(u * Mathf.PI * 100) * .008f;
                relief += rivet * .09f - ring * .045f - seam * .075f + machining * .2f;
                shade += rivet * .22f - ring * .25f - seam * .26f + machining;
            }
            color *= Mathf.Clamp(shade, .54f, 1.35f); color.a = 1;
            return new Surface { color = color, height = relief };
        }

        public static float ExposedEdgeDistance(int x, int y, int mask, TerrainKind kind, int variation)
        {
            float distance = 100;
            float dx = x + .5f, dy = y + .5f;
            if ((mask & 1) == 0) distance = Mathf.Min(distance, PixelsPerCell - dy - EdgeChip(dx, kind, variation, 1));
            if ((mask & 2) == 0) distance = Mathf.Min(distance, PixelsPerCell - dx - EdgeChip(dy, kind, variation, 2));
            if ((mask & 4) == 0) distance = Mathf.Min(distance, dy - EdgeChip(dx, kind, variation, 3));
            if ((mask & 8) == 0) distance = Mathf.Min(distance, dx - EdgeChip(dy, kind, variation, 4));
            distance = Corner(distance, PixelsPerCell-dx, PixelsPerCell-dy, mask, 1, 2, 16, kind);
            distance = Corner(distance, PixelsPerCell-dx, dy, mask, 2, 4, 32, kind);
            distance = Corner(distance, dx, dy, mask, 4, 8, 64, kind);
            distance = Corner(distance, dx, PixelsPerCell-dy, mask, 8, 1, 128, kind);
            return distance;
        }
        static float Corner(float distance, float x, float y, int mask, int first, int second, int diagonal, TerrainKind kind)
        {
            bool a = (mask & first) != 0, b = (mask & second) != 0;
            float inset = kind == TerrainKind.Metal ? 1.2f : 12;
            if (!a && !b)
            {
                // A rounded convex rock corner, rather than four straight tile edges.
                float radius = kind == TerrainKind.Metal ? 3 : 21;
                if (x < inset + radius && y < inset + radius)
                    distance = Mathf.Min(distance, radius - new Vector2(x-inset-radius,y-inset-radius).magnitude);
            }
            else if (a && b && (mask & diagonal) == 0)
            {
                // Match the endpoint erosion of both neighboring exposed edges.
                // Missing diagonal changes this sprite even if all four cardinal neighbors exist.
                distance = Mathf.Min(distance, new Vector2(x,y).magnitude - inset);
            }
            return distance;
        }
        static float EdgeChip(float pixel, TerrainKind kind, int variation, int side)
        {
            if (kind == TerrainKind.Metal) return 1.2f;
            float t = pixel / PixelsPerCell;
            float seed = (int)kind * 1.71f + variation * 2.37f + side * .43f;
            float envelope = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2);
            // Fixed endpoint height and tangent keep every variant/material join watertight.
            // Broad erosion is visible from the play camera; finer chips retain close-up detail.
            float erosion = 5.1f * Mathf.Sin(t * Mathf.PI * 4 + seed)
                + 2.3f * Mathf.Sin(t * Mathf.PI * 10 + seed * 1.7f)
                + 1.2f * Mathf.Sin(t * Mathf.PI * 24 + seed * .7f);
            return 12 + envelope * erosion;
        }
        static float HeightAt(float[] values, int x, int y, int mask)
        {
            if (x < 0) x = (mask & 8) != 0 ? (x + PixelsPerCell) % PixelsPerCell : 0;
            if (x >= PixelsPerCell) x = (mask & 2) != 0 ? x % PixelsPerCell : PixelsPerCell - 1;
            if (y < 0) y = (mask & 4) != 0 ? (y + PixelsPerCell) % PixelsPerCell : 0;
            if (y >= PixelsPerCell) y = (mask & 1) != 0 ? y % PixelsPerCell : PixelsPerCell - 1;
            return values[y * PixelsPerCell + x];
        }
        static CellFeature Voronoi(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells; int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            var result = new CellFeature { nearest = 100, second = 100 };
            for (int yy = iy - 1; yy <= iy + 1; yy++) for (int xx = ix - 1; xx <= ix + 1; xx++)
            {
                float a = Hash(Wrap(xx,cells),Wrap(yy,cells),seed), b = Hash(Wrap(xx,cells),Wrap(yy,cells),seed+17);
                Vector2 offset = new Vector2(x - (xx + .17f + a * .66f), y - (yy + .17f + b * .66f));
                float distance = offset.magnitude;
                if (distance < result.nearest) { result.second = result.nearest; result.nearest = distance; result.id = a; result.offset = offset; }
                else if (distance < result.second) result.second = distance;
            }
            return result;
        }
        static float Noise(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells; int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(Wrap(ix,cells),Wrap(iy,cells),seed),Hash(Wrap(ix+1,cells),Wrap(iy,cells),seed),fx),
                Mathf.Lerp(Hash(Wrap(ix,cells),Wrap(iy+1,cells),seed),Hash(Wrap(ix+1,cells),Wrap(iy+1,cells),seed),fx),fy);
        }
        static float Hash(int x, int y, int seed) => (DeepTerrainTile.StableCellHash(new Vector3Int(x,y,0),seed) & 0x00ffffff) / 16777215f;
        static int Wrap(int value, int size) => (value % size + size) % size;
        static float Frac(float value) => value - Mathf.Floor(value);
        static float Smooth(float min, float max, float value) { float t = Mathf.InverseLerp(min,max,value); return t * t * (3 - 2 * t); }
        static void Extrude(Color32[] atlas, int width, int x, int y)
        {
            for (int oy = -Gutter; oy < PixelsPerCell + Gutter; oy++) for (int ox = -Gutter; ox < PixelsPerCell + Gutter; ox++)
            {
                if (ox >= 0 && ox < PixelsPerCell && oy >= 0 && oy < PixelsPerCell) continue;
                atlas[(y+oy)*width+x+ox] = atlas[(y+Mathf.Clamp(oy,0,PixelsPerCell-1))*width+x+Mathf.Clamp(ox,0,PixelsPerCell-1)];
            }
        }
        static void WriteImage(string path, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width,height,TextureFormat.RGBA32,false,true);
            try { texture.SetPixels32(pixels); texture.Apply(false,false); File.WriteAllBytes(path,texture.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }

    /// <summary>Runs after the generic Art importer, which otherwise forces Single sprite mode.</summary>
    sealed class DeepTerrainV2TextureImporter : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 1000;
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(DeepTerrainArtBaker.Folder + "/",StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear; importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            if (assetPath.EndsWith("_Normal.png",StringComparison.Ordinal))
            { importer.textureType = TextureImporterType.NormalMap; importer.sRGBTexture = false; importer.convertToNormalmap = false; return; }
            if (assetPath.EndsWith("_AO.png",StringComparison.Ordinal))
            { importer.textureType = TextureImporterType.Default; importer.sRGBTexture = false; return; }
            if (!assetPath.EndsWith("_Color.png",StringComparison.Ordinal)) return;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = DeepTerrainArtBaker.PixelsPerCell; importer.alphaIsTransparency = true; importer.sRGBTexture = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteExtrude = 0; importer.SetTextureSettings(settings);
        }
    }
}
