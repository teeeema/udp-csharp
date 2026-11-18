using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PredictionDemo.EditorTools
{
    /// <summary>Environment-only adaptation of fengf128/Tank, commit 3919a68.</summary>
    public static class TankMapImport
    {
        public const string ScenePath = "Assets/Scenes/TankForestRange.unity";
        private const string Source = "Assets/ThirdParty/Fengf128Tank/";
        private const string Adapted = Source + "Adapted/";

        [MenuItem("PR4/Maps/Create Tank Forest Range")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play mode and save the current scene before creating the map.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("The imported scene already exists; open it instead of overwriting it.");

            var source = AssetDatabase.LoadAssetAtPath<TerrainData>(Source + "New Terrain2.asset");
            if (source == null || source.terrainLayers.Any(x => x == null) || source.treePrototypes.Any(x => x.prefab == null))
                throw new InvalidOperationException("Source terrain dependencies are incomplete.");
            if (!AssetDatabase.IsValidFolder(Adapted.TrimEnd('/')))
                AssetDatabase.CreateFolder(Source.TrimEnd('/'), "Adapted");

            // A scene copy preserves the existing network bootstrap, camera and HUD bindings.
            var original = EditorSceneManager.OpenScene("Assets/Scenes/PredictionDemo.unity");
            EditorSceneManager.SaveScene(original, ScenePath, true);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name != "NetworkSystem" && root.name != "Main Camera")
                    Object.DestroyImmediate(root);

            var environment = new GameObject("Tank source map — SampleScene environment").transform;
            var data = Object.Instantiate(source);
            data.name = "ForestRangeTerrain";
            var instances = data.treeInstances;
            data.treeInstances = Array.Empty<TreeInstance>();
            AssetDatabase.CreateAsset(data, Adapted + "ForestRangeTerrain.asset");
            // Unity 6 can reset the legacy terrain's embedded splat textures when cloning it.
            // Restore them after asset creation so the source's painted routes survive saving.
            data.SetAlphamaps(0, 0, source.GetAlphamaps(0, 0, source.alphamapWidth, source.alphamapHeight));
            var heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
            float minimum = float.MaxValue;
            foreach (var height in heights) minimum = Mathf.Min(minimum, height * data.size.y);
            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Imported terrain — original heights and painted roads";
            terrainObject.transform.SetParent(environment, false);
            terrainObject.transform.position = new Vector3(-50, -minimum, -50);
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.CreateAsset(terrain.materialTemplate, Adapted + "TerrainURP.mat");
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3;
            terrain.detailObjectDistance = 65;

            // Explicit tree objects allow URP materials and trunk collision; source FBX has no collider.
            var treeSource = source.treePrototypes[0].prefab;
            var materialMap = treeSource.GetComponentsInChildren<Renderer>()
                .SelectMany(x => x.sharedMaterials).Distinct().ToDictionary(x => x, ConvertTreeMaterial);
            for (var i = 0; i < instances.Length; i++)
            {
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(treeSource, scene);
                tree.name = "Source tree " + i;
                tree.transform.SetParent(environment, false);
                var instance = instances[i];
                tree.transform.position = terrainObject.transform.position + Vector3.Scale(instance.position, data.size);
                tree.transform.rotation = Quaternion.Euler(0, instance.rotation * Mathf.Rad2Deg, 0);
                tree.transform.localScale *= instance.widthScale;
                var scale = tree.transform.localScale;
                scale.y *= instance.heightScale / instance.widthScale;
                tree.transform.localScale = scale;
                foreach (var renderer in tree.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(x => materialMap[x]).ToArray();
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.radius = .38f;
                trunk.height = 7;
                trunk.center = Vector3.up * 3.5f;
            }

            var boundaryMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            boundaryMaterial.color = new Color(.34f, .36f, .30f);
            AssetDatabase.CreateAsset(boundaryMaterial, Adapted + "RangeBoundary.mat");
            for (int side = -1; side <= 1; side += 2)
            {
                AddBoundary(environment, new Vector3(side * 50.25f, .7f, 0), new Vector3(.5f, 1.4f, 101), boundaryMaterial);
                AddBoundary(environment, new Vector3(0, .7f, side * 50.25f), new Vector3(100, 1.4f, .5f), boundaryMaterial);
            }
            // Keep all four existing player spawns on the source's flat clearing (60..67.5, 25).
            // Move the whole map, preserving its heightfield, vegetation and painted routes.
            environment.position = new Vector3(-12.5f, 0, 21);
            var sun = new GameObject("Forest range daylight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            sun.intensity = 1.5f;
            sun.color = new Color(1, .96f, .87f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, Adapted + "ForestSky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.5f, .62f, .76f);
            RenderSettings.ambientEquatorColor = new Color(.39f, .43f, .35f);
            RenderSettings.ambientGroundColor = new Color(.19f, .20f, .17f);
            RenderSettings.fog = false;
            Lightmapping.lightingSettings = null;
            Lightmapping.Clear();
            DynamicGI.UpdateEnvironment();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            // Deliberately leave the original demo/build configuration available.
            Debug.Log("[TankMapImport] Forest scene saved: " + ScenePath);
        }

        private static Material ConvertTreeMaterial(Material source)
        {
            bool leaves = source.shader.name.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) >= 0;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = source.name + " URP";
            material.SetTexture("_BaseMap", source.mainTexture);
            material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.color : Color.white);
            material.SetFloat("_Smoothness", .12f);
            if (leaves)
            {
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Cutoff", .4f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            AssetDatabase.CreateAsset(material, Adapted + source.name + " URP.mat");
            return material;
        }

        private static void AddBoundary(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Range edge barrier";
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
