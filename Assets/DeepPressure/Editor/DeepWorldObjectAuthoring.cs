using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepPressure.Editor
{
    /// <summary>Migrates old decorative lights to selectable, movable, saveable building entities.</summary>
    public static class DeepWorldObjectAuthoring
    {
        [MenuItem("深压/关卡/升级地形与完整灯具对象")]
        public static void UpgradeOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before upgrading authored objects.");
            DeepTerrainArtBaker.Prepare();
            var catalog = DeepCatalogBuilder.CreateOrUpdateCatalog();
            int repaired = 0;
            foreach (var world in UnityEngine.Object.FindObjectsOfType<DeepPressureWorld>())
            {
                repaired += RepairWorld(world,catalog);
                world.terrain.RefreshAllTiles();
                if (world.background != null) world.background.RefreshAllTiles();
                EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Organic terrain refreshed; " + repaired + " lamps consolidated as complete building objects. Save the scene to retain migration.");
        }

        public static int RepairWorld(DeepPressureWorld world, DeepGameplayCatalog catalog, Transform buildingParent = null)
        {
            if (world == null || catalog == null) return 0;
            var field=world.GetComponent<DeepTerrainMaterialField>();
            if(field==null)field=world.gameObject.AddComponent<DeepTerrainMaterialField>();
            field.world=world;field.RefreshField();EditorUtility.SetDirty(field);
            var definition = catalog.FindBuilding("lamp");
            if (definition == null) return 0;
            if (buildingParent == null)
            {
                buildingParent = world.transform.Find("09 • Buildings from content catalog");
                if (buildingParent == null)
                {
                    var container = new GameObject("09 • Buildings from content catalog");
                    container.transform.SetParent(world.transform,false); buildingParent = container.transform;
                }
            }
            int count = 0;
            foreach (var light in world.GetComponentsInChildren<Light2D>(true).ToArray())
            {
                if (light.lightType == Light2D.LightType.Global) continue;
                var instance = light.GetComponentInParent<DeepBuildingInstance>();
                if (instance != null && instance.definition != null && instance.definition.role != DeepBuildingRole.Light) continue;
                var fixture = instance != null ? instance.transform : light.transform.parent;
                bool authoredFixture = fixture != null && (fixture.name == "Wall lamp" || instance != null);
                if (!authoredFixture)
                {
                    // Old scenes contain illumination with no visible/selectable lamp. Instantiate the complete authored object.
                    if (definition.prefab == null) continue;
                    var origin = world.WorldToCell(light.transform.position);
                    var replacement = (GameObject)PrefabUtility.InstantiatePrefab(definition.prefab,world.gameObject.scene);
                    replacement.transform.SetParent(buildingParent,false);
                    replacement.transform.position = CellOrigin(world,origin);
                    instance = replacement.GetComponent<DeepBuildingInstance>(); instance.definition = definition; instance.origin = origin;
                    instance.RefreshOwnedComponents();
                    foreach (var owned in instance.lights)
                    {
                        owned.color=light.color; owned.intensity=light.intensity;
                        owned.pointLightInnerRadius=light.pointLightInnerRadius; owned.pointLightOuterRadius=light.pointLightOuterRadius;
                    }
                    UnityEngine.Object.DestroyImmediate(light.gameObject);
                    count++; continue;
                }
                if (instance == null) instance = fixture.gameObject.AddComponent<DeepBuildingInstance>();
                instance.definition = definition;
                // Move the root to its occupied grid cell while preserving every authored visual/light offset.
                // Moving/deleting/duplicating this root now acts on the whole fixture.
                Vector2Int cell = world.WorldToCell(fixture.position);
                Vector3 target = CellOrigin(world,cell), delta = target-fixture.position;
                var children = fixture.Cast<Transform>().ToArray();
                fixture.position = target;
                foreach (var child in children) child.position -= delta;
                fixture.SetParent(buildingParent,true);
                fixture.name = definition.displayName + " · " + cell.x + "," + cell.y;
                instance.origin = cell; instance.RefreshOwnedComponents(); EditorUtility.SetDirty(instance); count++;
            }
            return count;
        }
        static Vector3 CellOrigin(DeepPressureWorld world, Vector2Int cell) => world.transform.TransformPoint(new Vector3(cell.x*world.cellSize,cell.y*world.cellSize,0));
    }

    [CustomEditor(typeof(DeepBuildingInstance))]
    public sealed class DeepBuildingInstanceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var instance = (DeepBuildingInstance)target;
            if (instance.definition != null)
            {
                EditorGUILayout.LabelField(instance.definition.displayName,EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("整体物体：移动、复制、删除此根对象会同时处理灯具外观、光源与功能。子对象仅承载表现。",MessageType.Info);
            }
            DrawDefaultInspector();
        }
        void OnSceneGUI()
        {
            if (Application.isPlaying) return;
            var instance = (DeepBuildingInstance)target;
            var world = instance.GetComponentInParent<DeepPressureWorld>();
            if (world == null) return;
            var cell = world.WorldToCell(instance.transform.position);
            if (cell != instance.origin)
            {
                Undo.RecordObject(instance,"Move complete building"); instance.origin = cell; EditorUtility.SetDirty(instance);
            }
            var size = instance.definition == null ? Vector2Int.one : instance.definition.footprint;
            Handles.color = new Color(.4f,.95f,.85f,.75f);
            Vector3 center = world.transform.TransformPoint(new Vector3((cell.x+size.x*.5f)*world.cellSize,(cell.y+size.y*.5f)*world.cellSize,0));
            Handles.DrawWireCube(center,new Vector3(size.x*world.cellSize,size.y*world.cellSize,.02f));
        }
    }
}
