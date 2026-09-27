using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepPressure.Editor
{
    /// <summary>Read-only checks of authored visuals, plus discovery checks in a disposable preview scene.</summary>
    public static class DeepVisualValidation
    {
        sealed class Results
        {
            public readonly List<string> passed = new List<string>(), failed = new List<string>(), notes = new List<string>();
            public void Check(string name, Action action)
            {
                try { action(); passed.Add(name); }
                catch (Exception exception) { failed.Add(name + " :: " + exception.GetBaseException().Message); }
            }
        }

        [MenuItem("深压/验证/视觉与连接验收 %#F10")]
        public static void Run()
        {
            RequireEditorIdle();
            string output = ValidateActiveScene(out bool success);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","outputs","visual-validation.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,output,new UTF8Encoding(false));
            if (success) Debug.Log(output + "\nReport: " + path);
            else
            {
                Debug.LogError(output + "\nReport: " + path);
                throw new InvalidOperationException("Deep Pressure visual validation failed. See " + path);
            }
        }

        public static string ValidateActiveScene(out bool success)
        {
            RequireEditorIdle();
            var results = new Results();
            Scene scene = SceneManager.GetActiveScene();
            DeepPressureWorld world = null;
            results.Check("Active scene has exactly one active DeepPressureWorld", () =>
            {
                Require(scene.IsValid() && scene.isLoaded,"No loaded active scene.");
                var worlds = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DeepPressureWorld>(true)).Where(item => item.gameObject.activeInHierarchy).ToArray();
                Require(worlds.Length == 1,"Expected one active world, found " + worlds.Length + ". Open the authored level first.");
                world = worlds[0];
                Require(world.HasValidTerrainData,"Serialized terrain size does not match the grid.");
            });
            // Exactly one invocation; its own fixtures clean up after themselves.
            results.Check("Existing simulation and exploration regression tests", () => results.notes.Add(DeepPressureSelfTests.RunAll()));

            for (int index = 1; index <= (int)TerrainKind.Metal; index++)
            {
                TerrainKind kind = (TerrainKind)index;
                results.Check(kind + " atlas / 141 blob sprites / rects / aligned normal and AO", () => ValidateTerrain(kind));
            }
            results.Check("47 eight-neighbor masks and diagonal excavation refresh", ValidateBlobAdjacency);
            if (world != null)
            {
                results.Check("Foreground actually uses V2 modular tile assets", () => ValidateUsedTiles(world));
                results.Check("Natural material seams use a live terrain-only palette field", () =>
                {
                    var field=world.GetComponent<DeepTerrainMaterialField>();
                    Require(field!=null && field.enabled && field.world==world,"Material transition field is missing or references another world.");
                    field.RefreshField();
                    var block=new MaterialPropertyBlock();world.terrain.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().GetPropertyBlock(block);
                    Require(block.GetFloat("_TerrainBlendEnabled")>.5f && block.GetTexture("_TerrainPaletteTex")!=null,"Terrain transition palette is not bound to foreground renderer.");
                });
                results.Check("Local lights belong to complete selectable building objects", () => ValidateLightOwnership(world));
                GasLink[] links = world.GetComponentsInChildren<GasLink>(true);
                results.Check("Authored scene contains connected gas links", () => Require(links.Length > 0,"No GasLink in active world."));
                foreach (GasLink item in links)
                {
                    GasLink link = item;
                    results.Check("Pipe mesh: " + link.name, () => ValidatePipe(link,results));
                }
                results.Check("Lit sprite props bind actual normal and AO textures", () => ValidateProps(world,results));
                results.Check("Fog/gas overlay references, map coverage and draw order", () => ValidateOverlays(world));
                results.Check("Initial unknown cells are concealed; taking a sample does not reveal them", () => ValidateInitialDiscovery(world,results));
            }
            foreach (string shaderName in new[] { "DeepPressure/FogOfWar", "DeepPressure/GasAtmosphere", "DeepPressure/Sprite-Lit-AO" })
            {
                string name = shaderName;
                results.Check("Imported shader diagnostics: " + name, () =>
                {
                    Shader shader = Shader.Find(name);
                    Require(shader != null,"Shader asset cannot be found.");
                    Require(!ShaderUtil.ShaderHasError(shader),"Unity reports shader compilation errors. Inspect the shader asset and Console.");
                });
            }
            success = results.failed.Count == 0;
            var report = new StringBuilder();
            report.AppendLine("DEEP PRESSURE — VISUAL / CONNECTION VALIDATION");
            report.AppendLine("UTC: " + DateTime.UtcNow.ToString("u"));
            report.AppendLine("Scene: " + scene.path);
            report.AppendLine((success ? "PASS" : "FAIL") + " | passed " + results.passed.Count + " | failed " + results.failed.Count);
            foreach (string value in results.passed) report.AppendLine("[PASS] " + value);
            foreach (string value in results.failed) report.AppendLine("[FAIL] " + value);
            foreach (string value in results.notes) report.AppendLine("[NOTE] " + value);
            report.AppendLine("Scope: asset data, saved mesh data, bindings, shader import diagnostics and initial discovery textures. These checks do not certify composition, visual polish, final GPU pixels or every shader variant; inspect the rendered game view as well.");
            return report.ToString();
        }

        static void ValidateTerrain(TerrainKind kind)
        {
            string stem = DeepTerrainArtBaker.Folder + "/" + kind;
            var tile = AssetDatabase.LoadAssetAtPath<DeepTerrainTile>(stem + ".asset");
            Require(tile != null,"Missing V2 tile asset. Run the terrain baker before validation.");
            Require(tile.kind == kind,"Terrain kind does not match its asset.");
            Require(tile.variants != null && tile.variants.Length == 141 && tile.variationCount == 3 && tile.useDiagonalConnections,"Expected 47 eight-neighbor masks x 3 variations.");
            Require(tile.connectAcrossMaterials,"Material boundaries would be treated as exposed air edges.");
            Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_Color.png");
            Require(color != null && color.width > 0 && color.height > 0,"Missing/empty color atlas.");
            ValidateTextureTriplet(stem + "_Color.png",true);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(stem + "_Color.png").OfType<Sprite>().ToArray();
            Require(sprites.Length == 141,"Imported atlas has " + sprites.Length + " actual sprites, expected 141.");
            var names = new HashSet<string>(); var spriteSet = new HashSet<Sprite>(sprites);
            for (int index = 0; index < tile.variants.Length; index++)
            {
                Sprite sprite = tile.variants[index];
                Require(sprite != null && spriteSet.Contains(sprite),"Tile variant references a missing sprite or another atlas.");
                Require(names.Add(sprite.name),"Duplicate sprite in adjacency/variation table: " + sprite.name);
                Rect rect = sprite.rect;
                Require(Finite(rect.x) && Finite(rect.y) && Finite(rect.width) && Finite(rect.height),"Non-finite sprite rectangle.");
                Require(rect.width > 0 && rect.height > 0 && rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= color.width && rect.yMax <= color.height,"Sprite rectangle is outside its atlas.");
                Require(Mathf.Abs(rect.width - DeepTerrainArtBaker.PixelsPerCell) < .01f && Mathf.Abs(rect.height - DeepTerrainArtBaker.PixelsPerCell) < .01f,"Sprite cell resolution disagrees with baker contract.");
                Require(Mathf.Abs(sprite.pixelsPerUnit - DeepTerrainArtBaker.PixelsPerCell) < .01f,"One tile no longer represents one world unit.");
                Require((sprite.pivot - rect.size * .5f).sqrMagnitude < .01f,"Sprite pivots are not centered consistently.");
                for (int other = 0; other < index; other++) Require(!rect.Overlaps(tile.variants[other].rect),"Atlas sprite rectangles overlap.");
            }
        }

        static void ValidateBlobAdjacency()
        {
            Require(DeepTerrainTile.BlobMasks.Length == 47,"Blob masks must contain exactly 47 distinct normalized configurations.");
            for (int mask=0;mask<256;mask++)
                Require(DeepTerrainTile.BlobMasks[DeepTerrainTile.BlobIndex(mask)] == DeepTerrainTile.NormalizeBlobMask(mask),"Mask lookup does not preserve normalized adjacency.");
            var tile=AssetDatabase.LoadAssetAtPath<DeepTerrainTile>(DeepTerrainArtBaker.Folder+"/Basalt.asset");
            Require(tile!=null && tile.useDiagonalConnections,"Bake blob terrain first.");
            Scene preview=EditorSceneManager.NewPreviewScene(); GameObject root=null;
            try
            {
                root=new GameObject("Blob adjacency fixture"); SceneManager.MoveGameObjectToScene(root,preview); root.AddComponent<Grid>();
                var child=new GameObject("Tilemap");child.transform.SetParent(root.transform,false);
                var map=child.AddComponent<UnityEngine.Tilemaps.Tilemap>();
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)map.SetTile(new Vector3Int(x,y,0),tile);
                Sprite filled=map.GetSprite(Vector3Int.zero);
                map.SetTile(new Vector3Int(1,1,0),null);
                Sprite concave=map.GetSprite(Vector3Int.zero);
                Require(filled!=null && concave!=null && filled!=concave,"Removing only a diagonal must immediately update the center sprite.");
                map.SetTile(new Vector3Int(1,1,0),tile);
                Require(map.GetSprite(Vector3Int.zero)==filled,"Restoring the diagonal must restore its original deterministic sprite.");
                Require(DeepTerrainArtBaker.ExposedEdgeDistance(1,126,0,TerrainKind.Basalt,0)<0,"Exposed rock corners must be rounded away.");
                Require(DeepTerrainArtBaker.ExposedEdgeDistance(64,64,0,TerrainKind.Basalt,0)>0,"Isolated rock cells must retain a solid center.");
                Require(DeepTerrainArtBaker.ExposedEdgeDistance(125,125,15,TerrainKind.Basalt,0)<0,"Missing diagonal must carve a concave corner.");
                Require(DeepTerrainArtBaker.ExposedEdgeDistance(125,125,255,TerrainKind.Basalt,0)>0,"Connected interior must remain opaque.");
            }
            finally { if(root!=null)UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview); }
        }

        static void ValidateLightOwnership(DeepPressureWorld world)
        {
            int count=0;
            foreach(var light in world.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))
            {
                if(light.lightType==UnityEngine.Rendering.Universal.Light2D.LightType.Global)continue;
                var owner=light.GetComponentInParent<DeepBuildingInstance>();
                Require(owner!=null && owner.definition!=null,"Detached local light: "+light.name);
                Require(owner.GetComponentsInChildren<SpriteRenderer>(true).Length>0,"Light owner has no visible fixture: "+owner.name);
                Require(owner.lights!=null && owner.lights.Contains(light),"Light is absent from its owning building's state controls.");
                Require(owner.origin==world.WorldToCell(owner.transform.position),"Building origin differs from its root position: "+owner.name);
                Require(typeof(DeepBuildingInstance).IsDefined(typeof(SelectionBaseAttribute),true),"Building root is not the editor selection base.");count++;
            }
            Require(count>0,"No complete local light fixtures exist.");
        }

        static void ValidateTextureTriplet(string colorPath, bool multiple)
        {
            string normalPath = colorPath.Replace("_Color.png","_Normal.png"), aoPath = colorPath.Replace("_Color.png","_AO.png");
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath);
            Require(color != null && normal != null && ao != null,"Missing Color / Normal / AO member for " + colorPath);
            Require(color.width == normal.width && color.height == normal.height && color.width == ao.width && color.height == ao.height,"Color / Normal / AO dimensions differ for " + colorPath);
            var colorImporter = AssetImporter.GetAtPath(colorPath) as TextureImporter;
            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            var aoImporter = AssetImporter.GetAtPath(aoPath) as TextureImporter;
            Require(colorImporter != null && normalImporter != null && aoImporter != null,"Missing texture importer.");
            Require(colorImporter.textureType == TextureImporterType.Sprite && colorImporter.sRGBTexture,"Color must be a sprite color texture.");
            if (multiple) Require(colorImporter.spriteImportMode == SpriteImportMode.Multiple,"Color atlas was reset to Single sprite import.");
            Require(normalImporter.textureType == TextureImporterType.NormalMap && !normalImporter.sRGBTexture && !normalImporter.convertToNormalmap,"Normal texture must use its supplied normal data in linear space.");
            Require(aoImporter.textureType == TextureImporterType.Default && !aoImporter.sRGBTexture,"AO must be linear data.");
            SecondarySpriteTexture[] secondary = colorImporter.secondarySpriteTextures;
            Require(secondary != null && secondary.Any(item => item.name == "_NormalMap" && item.texture == normal),"Sprite does not bind its matching _NormalMap.");
            Require(secondary.Any(item => item.name == "_AOMap" && item.texture == ao),"Sprite does not bind its matching _AOMap.");
        }

        static void ValidateUsedTiles(DeepPressureWorld world)
        {
            Require(world.terrain != null,"Foreground tilemap is missing.");
            var used = new HashSet<DeepTerrainTile>();
            foreach (Vector3Int position in world.terrain.cellBounds.allPositionsWithin)
            {
                var tile = world.terrain.GetTile<DeepTerrainTile>(position);
                if (tile == null) continue;
                used.Add(tile);
                Require(world.terrain.GetSprite(position) != null,"Visible foreground cell has no adjacency sprite: " + position);
            }
            Require(used.Count > 0,"Foreground has no terrain tiles.");
            foreach (DeepTerrainTile tile in used)
                Require(AssetDatabase.GetAssetPath(tile).StartsWith(DeepTerrainArtBaker.Folder + "/",StringComparison.Ordinal),"Scene still uses a legacy terrain tile: " + tile.name);
        }

        static void ValidatePipe(GasLink link, Results results)
        {
            Require(link.from != null && link.to != null && link.from != link.to,"Invalid logical gas endpoints.");
            var visual = link.GetComponent<DeepPipeVisual>();
            var route = link.GetComponent<LineRenderer>();
            Require(visual != null,"GasLink has no DeepPipeVisual.");
            Require(route != null && !route.enabled,"The legacy LineRenderer still renders or is missing.");
            Require(route.positionCount >= 2,"Editable route has fewer than two control points.");
            for (int i = 0; i < route.positionCount; i++) Require(Finite(route.GetPosition(i)),"Route contains non-finite coordinates.");
            Require(visual.pipeMesh != null && visual.pipeRenderer != null,"Pipe visual mesh references are incomplete.");
            Require(visual.pipeRenderer.enabled && visual.pipeRenderer.sharedMaterial != null,"Pipe mesh renderer is disabled or lacks a material.");
            Mesh mesh = visual.pipeMesh.sharedMesh;
            Require(mesh != null && mesh.vertexCount > 0,"Pipe geometry is absent. Validation does not rebuild it.");
            Require(mesh.isReadable,"Pipe mesh is not readable for inspection.");
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Vector2[] uv = mesh.uv; Color[] colors = mesh.colors; int[] triangles = mesh.triangles;
            Require(normals.Length == vertices.Length && colors.Length == vertices.Length && uv.Length == vertices.Length,"Mesh normal/color/UV channel does not match vertex count.");
            Require(triangles.Length > 0 && triangles.Length % 3 == 0,"Mesh has no complete triangles.");
            for (int i = 0; i < vertices.Length; i++)
            {
                Require(Finite(vertices[i]) && Finite(normals[i]) && Finite(uv[i].x) && Finite(uv[i].y) && Finite(colors[i]),"Mesh contains NaN or Infinity at vertex " + i);
                Require(normals[i].sqrMagnitude > 1e-8f,"Mesh contains a zero normal.");
            }
            foreach (int index in triangles) Require(index >= 0 && index < vertices.Length,"Triangle index is outside vertex buffer.");
            Require(Finite(mesh.bounds.center) && Finite(mesh.bounds.size),"Mesh bounds are invalid.");
            Require(Finite(visual.routeLength) && visual.routeLength > 0,"Baked route length is invalid.");
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh))) results.notes.Add(link.name + ": mesh is held by the scene rather than a separate .asset; confirm scene-save persistence in visual QA.");
        }

        static void ValidateProps(DeepPressureWorld world, Results results)
        {
            var checkedPaths = new HashSet<string>(); int renderers = 0;
            foreach (SpriteRenderer renderer in world.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null) continue;
                string path = AssetDatabase.GetAssetPath(renderer.sprite);
                if (!path.EndsWith("_Color.png",StringComparison.Ordinal)) continue;
                if (path.StartsWith(DeepTerrainArtBaker.Folder + "/",StringComparison.Ordinal)) continue;
                renderers++;
                Material material = renderer.sharedMaterial;
                Require(material != null && material.HasProperty("_NormalMap") && material.HasProperty("_AOMap"),"Prop material cannot read normal and AO: " + renderer.name);
                if (checkedPaths.Add(path)) ValidateTextureTriplet(path,false);
            }
            Require(renderers > 0,"No multi-map foreground sprite props found in the active world.");
            results.notes.Add("Validated " + renderers + " prop renderers across " + checkedPaths.Count + " texture triplets.");
        }

        static void ValidateOverlays(DeepPressureWorld world)
        {
            var exploration = world.GetComponent<DeepExploration>();
            Require(exploration != null && exploration.world == world,"Exploration does not reference this world.");
            SpriteRenderer fog = exploration.fogRenderer, gas = exploration.gasRenderer;
            Require(fog != null && gas != null,"Fog/gas overlay references are missing.");
            Require(fog.sharedMaterial != null && fog.sharedMaterial.shader != null && fog.sharedMaterial.shader.name == "DeepPressure/FogOfWar","Wrong fog shader.");
            Require(gas.sharedMaterial != null && gas.sharedMaterial.shader != null && gas.sharedMaterial.shader.name == "DeepPressure/GasAtmosphere","Wrong gas shader.");
            Require(fog.enabled && fog.gameObject.activeInHierarchy,"Fog renderer is not active.");
            ValidateMapCoverage(world,fog); ValidateMapCoverage(world,gas);
            int fogLayer = SortingLayer.GetLayerValueFromID(fog.sortingLayerID);
            foreach (Renderer renderer in world.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == fog || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                int layer = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
                Require(fogLayer > layer || (fogLayer == layer && fog.sortingOrder > renderer.sortingOrder),"Renderer can draw over unknown fog: " + renderer.name);
            }
        }

        static void ValidateMapCoverage(DeepPressureWorld world, SpriteRenderer renderer)
        {
            Require(renderer.sprite != null,"Overlay sprite is missing: " + renderer.name);
            Bounds bounds = renderer.sprite.bounds;
            foreach (Vector2 corner in new[] { Vector2.zero,new Vector2(world.width,0),new Vector2(0,world.height),new Vector2(world.width,world.height) })
            {
                Vector3 point = renderer.transform.InverseTransformPoint(world.transform.TransformPoint(new Vector3(corner.x * world.cellSize,corner.y * world.cellSize,0)));
                Require(point.x >= bounds.min.x-.002f && point.x <= bounds.max.x+.002f && point.y >= bounds.min.y-.002f && point.y <= bounds.max.y+.002f,"Overlay does not cover the grid boundary: " + renderer.name);
            }
        }

        static void ValidateInitialDiscovery(DeepPressureWorld authored, Results results)
        {
            var source = authored.GetComponent<DeepExploration>();
            Require(source != null,"Exploration component is missing.");
            Scene originalActive = SceneManager.GetActiveScene();
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                Require(SceneManager.GetActiveScene() == originalActive,"Preview scene unexpectedly changed the active scene.");
                root = new GameObject("VisualValidation_DiscoveryFixture") { hideFlags = HideFlags.HideAndDontSave }; root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root,preview);
                var world = root.AddComponent<DeepPressureWorld>();
                world.width = authored.width; world.height = authored.height; world.cellSize = authored.cellSize;
                world.crossSectionDepthM = authored.crossSectionDepthM; world.terrainKinds = (TerrainKind[])authored.terrainKinds.Clone();
                world.defaultPressureKPa = authored.defaultPressureKPa; world.defaultTemperatureC = authored.defaultTemperatureC; world.defaultComposition = authored.defaultComposition;
                foreach (DeepPressureRegion region in authored.GetComponentsInChildren<DeepPressureRegion>(true))
                {
                    var child = new GameObject(region.name); child.transform.SetParent(root.transform,false);
                    var copy = child.AddComponent<DeepPressureRegion>(); copy.stableId = region.stableId; copy.displayName = region.displayName;
                    copy.bounds = region.bounds; copy.priority = region.priority; copy.initialPressureKPa = region.initialPressureKPa;
                    copy.initialTemperatureC = region.initialTemperatureC; copy.composition = region.composition;
                }
                world.RebuildRooms();
                var fogObject = new GameObject("Fog fixture"); fogObject.transform.SetParent(root.transform,false);
                var gasObject = new GameObject("Gas fixture"); gasObject.transform.SetParent(root.transform,false);
                var exploration = root.AddComponent<DeepExploration>(); exploration.world = world;
                exploration.initialExploredAreas = source.initialExploredAreas == null ? Array.Empty<RectInt>() : (RectInt[])source.initialExploredAreas.Clone();
                exploration.drillReachCells = source.drillReachCells;
                exploration.fogRenderer = fogObject.AddComponent<SpriteRenderer>(); exploration.gasRenderer = gasObject.AddComponent<SpriteRenderer>();
                exploration.IsVisible(Vector2Int.zero); // Initializes the fixture, never the authored component.
                var fogBlock = new MaterialPropertyBlock(); exploration.fogRenderer.GetPropertyBlock(fogBlock);
                var gasBlock = new MaterialPropertyBlock(); exploration.gasRenderer.GetPropertyBlock(gasBlock);
                var visibility = fogBlock.GetTexture("_VisibilityTex") as Texture2D;
                var gasTexture = gasBlock.GetTexture("_GasTex") as Texture2D;
                var pressure = gasBlock.GetTexture("_PressureTex") as Texture2D;
                var reveal = fogBlock.GetTexture("_RevealTex") as Texture2D;
                Require(visibility != null && gasTexture != null && pressure != null,"Discovery display textures were not bound.");
                Require(visibility.width == world.width && visibility.height == world.height,"Discovery texture does not match grid dimensions.");
                Require(visibility.filterMode == FilterMode.Point,"Discovery filter can leak neighboring visibility across cells.");
                Require(fogBlock.GetFloat("_DiscoveryActive") > .5f,"Fog was not activated for runtime rendering.");
                Color32[] pixels = visibility.GetPixels32(), gasPixels = gasTexture.GetPixels32(), pressurePixels = pressure.GetPixels32();
                Color32[] revealPixels = reveal == null ? null : reveal.GetPixels32();
                int knownVoid = 0, unknownVoid = 0;
                for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
                {
                    var cell = new Vector2Int(x,y); int index = y * world.width + x; bool visible = exploration.IsVisible(cell);
                    Require((pixels[index].r > 127) == visible,"Logical discovery and fog texture disagree.");
                    if (!visible)
                    {
                        Require(gasPixels[index].r == 0 && gasPixels[index].g == 0 && gasPixels[index].b == 0 && gasPixels[index].a == 0 && pressurePixels[index].g == 0,"Hidden cell exposes atmosphere display data.");
                        if (revealPixels != null) Require(revealPixels[index].r == 0,"Initial unknown cell already has reveal progress.");
                    }
                    if (world.GetTerrain(x,y) != TerrainKind.Empty) continue;
                    if (visible) knownVoid++; else unknownVoid++;
                    bool inStart = exploration.initialExploredAreas.Any(area => area.Contains(cell));
                    if (inStart) Require(visible,"A configured initial open cell is hidden.");
                    else Require(!visible,"An unconfigured initial empty cell has been revealed.");
                }
                Require(knownVoid > 0 && unknownVoid > 0,"The authored start must include both explored and unknown cave space.");
                int samples = 0;
                foreach (DeepPressureRegion region in world.Regions)
                {
                    Vector2Int target = region.bounds.position;
                    if (exploration.GetState(region) != DeepExplorationState.Unknown || !exploration.TrySample(target,out string unused)) continue;
                    samples++;
                    Require(exploration.TryGetSample(region,out DeepExploration.RegionSample sample),"Successful sample has no stored record.");
                    Require(Finite(sample.pressureKPa) && Finite(sample.temperatureC),"Sample readout is not finite.");
                    for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
                        Require(exploration.IsVisible(new Vector2Int(x,y)) == (pixels[y*world.width+x].r > 127),"Taking a sample revealed map cells.");
                }
                Require(samples > 0,"No unknown authored region can be sampled from the initial boundary; check probe reach and layout.");
                results.notes.Add("Discovery fixture: " + knownVoid + " visible void cells, " + unknownVoid + " unknown void cells, " + samples + " regions sampled without revealing; authored state unchanged.");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [MenuItem("深压/验证/视觉与连接验收 %#F10",true)]
        static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        static void RequireEditorIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Visual validation is disabled during Play mode. Stop Play before running this editor-only check; the active session has not been modified.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Wait for script compilation and asset import to finish before validating.");
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Color value) => Finite(value.r) && Finite(value.g) && Finite(value.b) && Finite(value.a);
    }
}
