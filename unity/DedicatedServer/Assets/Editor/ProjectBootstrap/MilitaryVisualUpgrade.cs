using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PredictionDemo.EditorTools
{
    // Visual-only layer over the existing lab. Never replaces network roots or colliders.
    public static class MilitaryVisualUpgrade
    {
        private const string Base = "Assets/ThirdParty/MilitaryRange/";
        [Serializable] private class TextureMaps { public string diffuse, normal, orm; }
        [Serializable] private class Surface { public string name, sourceName; public float[] color; public float metallic, roughness; public bool alphaClip, doubleSided; public TextureMaps textures; }
        [Serializable] private class Model { public string asset; public int triangles; public Surface[] materials; }
        private static readonly string[] Assets = { "Abrams", "Warehouse", "concrete_road_barrier", "rock_07", "grass_bermuda_01", "wild_rooibos_bush", "barrel_03", "tree_small_02" };

        [MenuItem("PR4/Apply Imported Military Visuals")]
        public static void Apply()
        {
            foreach (var name in Assets) PrepareModel(name);
            ReplaceTankVisual();
            var previous = GameObject.Find("Imported visual environment");
            if (previous != null) Object.DestroyImmediate(previous);
            var environment = new GameObject("Imported visual environment").transform;
            var existing = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var renderer in existing)
            {
                var go = renderer.gameObject;
                if (go.name == "Barrier")
                {
                    renderer.enabled = false;
                    Fit("concrete_road_barrier", environment, renderer.bounds.center - Vector3.up * renderer.bounds.extents.y, renderer.bounds.size, false);
                }
                else if (go.name == "Hangar")
                {
                    renderer.enabled = false;
                    Fit("Warehouse", environment, renderer.bounds.center - Vector3.up * renderer.bounds.extents.y, renderer.bounds.size, false);
                }
                else if (go.name == "Mesa")
                {
                    renderer.enabled = false;
                    Fit("rock_07", environment, renderer.bounds.center - Vector3.up * renderer.bounds.extents.y, renderer.bounds.size, false);
                }
                else if (go.name == "FuelDrum")
                {
                    renderer.enabled = false;
                    Fit("barrel_03", environment, renderer.bounds.center - Vector3.up * renderer.bounds.extents.y, renderer.bounds.size, false);
                }
                else if (new[] { "HazardStripe", "MesaCap", "HangarRoof", "HangarDoor", "DoorSlat", "DrumBand" }.Contains(go.name)) renderer.enabled = false;
            }
            RefreshExistingSurfaces();
            AddTerrain(environment);
            AddNature(environment);
            ConfigureLighting(environment);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[VisualUpgrade] Imported assets applied; functional roots, collider transforms and lab logic retained.");
        }

        private static void PrepareModel(string name)
        {
            var folder = Base + name + "/";
            var model = JsonUtility.FromJson<Model>(File.ReadAllText(folder + "materials.json"));
            var importer = AssetImporter.GetAtPath(folder + name + ".fbx") as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Model not imported: " + name);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
            importer.addCollider = false; importer.isReadable = false;
            importer.SaveAndReimport();
            foreach (var surface in model.materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(folder + surface.name + ".mat");
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, folder + surface.name + ".mat"); }
                material.color = surface.color.Length >= 4 ? new Color(surface.color[0], surface.color[1], surface.color[2], surface.color[3]) : Color.white;
                material.SetFloat("_Metallic", surface.metallic); material.SetFloat("_Smoothness", 1 - surface.roughness);
                if (name == "Abrams") { material.SetFloat("_Metallic", .3f); material.SetFloat("_Smoothness", .23f); }
                material.enableInstancing = true; material.SetFloat("_Cull", surface.doubleSided ? 0 : 2);
                if (!string.IsNullOrEmpty(surface.textures.diffuse))
                {
                    material.SetTexture("_BaseMap", Texture(folder + surface.textures.diffuse, false, false)); material.color = Color.white;
                }
                if (!string.IsNullOrEmpty(surface.textures.normal))
                {
                    material.SetTexture("_BumpMap", Texture(folder + surface.textures.normal, true, false)); material.EnableKeyword("_NORMALMAP");
                }
                if (!string.IsNullOrEmpty(surface.textures.orm))
                {
                    var map = PackOrm(folder + surface.textures.orm);
                    material.SetTexture("_MetallicGlossMap", map); material.SetTexture("_OcclusionMap", map);
                    material.SetFloat("_Smoothness", 1); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
                }
                if (surface.alphaClip) { material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .35f); material.EnableKeyword("_ALPHATEST_ON"); material.renderQueue = 2450; }
                if (name == "Abrams")
                {
                    if (surface.sourceName == "body")
                    {
                        // PredictedPlayer already tints only a material named Player, via PropertyBlock.
                        var armor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Player.mat");
                        material.CopyPropertiesFromMaterial(armor); material.SetFloat("_Metallic", .4f); material.SetFloat("_Smoothness", .26f);
                    }
                    else if (surface.sourceName == "guide") material.color = new Color(.08f, .09f, .07f);
                    else if (surface.sourceName == "wheels") material.color = new Color(.13f, .15f, .10f);
                }
                EditorUtility.SetDirty(material);
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".fbx");
            var instance = new GameObject(name);
            Object.Instantiate(source, instance.transform);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var mats = renderer.sharedMaterials.Select(imported =>
                {
                    var surface = model.materials.FirstOrDefault(s => s.name == imported.name);
                    if (surface == null) throw new InvalidOperationException("Unknown source material: " + imported.name);
                    return name == "Abrams" && surface.sourceName == "body"
                        ? AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Player.mat")
                        : AssetDatabase.LoadAssetAtPath<Material>(folder + surface.name + ".mat");
                }).ToArray();
                renderer.sharedMaterials = mats;
            }
            PrefabUtility.SaveAsPrefabAsset(instance, folder + name + ".prefab"); Object.DestroyImmediate(instance);
        }

        private static Texture2D Texture(string path, bool normal, bool linear)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !linear; importer.maxTextureSize = 1024;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = !normal && !linear;
            importer.isReadable = linear; importer.mipmapEnabled = true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D PackOrm(string source)
        {
            var path = Path.ChangeExtension(source, null) + "_UnityMask.png";
            if (!File.Exists(path))
            {
                var input = Texture(source, false, true); var pixels = input.GetPixels();
                for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color(pixels[i].b, pixels[i].r, 0, 1 - pixels[i].g);
                var output = new Texture2D(input.width, input.height, TextureFormat.RGBA32, false, true); output.SetPixels(pixels); output.Apply();
                File.WriteAllBytes(path, output.EncodeToPNG()); Object.DestroyImmediate(output); AssetDatabase.ImportAsset(path);
            }
            return Texture(path, false, true);
        }

        private static void ReplaceTankVisual()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GermanTankVisual.ModelPath) != null)
            {
                GermanTankVisual.Apply();
                return;
            }
            const string path = "Assets/Prefabs/PredictedPlayer.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.transform.Find("Visual");
                if (visual == null) throw new InvalidOperationException("Existing Visual container is required.");
                for (var i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Base + "Abrams/Abrams.prefab"), visual);
                instance.name = "Imported Abrams — Sketlux and yd";
                // Preserve the existing chassis footprint and fixed root height y=1.
                var bounds = BoundsOf(instance);
                var scale = Mathf.Min(1.86f / bounds.size.x, 3.2f / bounds.size.z);
                instance.transform.localScale *= scale;
                bounds = BoundsOf(instance);
                instance.transform.position += new Vector3(-bounds.center.x, -.95f - bounds.min.y, -.1f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No imported renderers: " + root.name);
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }

        private static GameObject Fit(string asset, Transform parent, Vector3 ground, Vector3 dimensions, bool uniform)
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Base + asset + "/" + asset + ".prefab"), parent);
            instance.name = asset;
            var bounds = BoundsOf(instance);
            var scale = new Vector3(dimensions.x / bounds.size.x, dimensions.y / bounds.size.y, dimensions.z / bounds.size.z);
            instance.transform.localScale = uniform ? Vector3.one * Mathf.Min(scale.x, scale.y, scale.z) : scale;
            bounds = BoundsOf(instance); instance.transform.position += ground - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var lod = instance.AddComponent<LODGroup>(); lod.SetLODs(new[] { new LOD(asset.Contains("grass") ? .025f : .008f, instance.GetComponentsInChildren<Renderer>()) }); lod.RecalculateBounds();
            instance.isStatic = true; return instance;
        }

        private static void RefreshExistingSurfaces()
        {
            var concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ConcreteBunker.mat");
            concrete.color = new Color(.72f,.72f,.69f);
            concrete.SetTexture("_BaseMap",Texture(Base+"Surfaces/concrete_floor_diff_1k.jpg",false,false));
            concrete.SetTexture("_BumpMap",Texture(Base+"Surfaces/concrete_floor_nor_gl_1k.jpg",true,false));
            concrete.EnableKeyword("_NORMALMAP"); concrete.SetFloat("_Smoothness",.12f); concrete.mainTextureScale=Vector2.one*2;
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MilitaryCrate.mat");
            metal.color = new Color(.28f,.31f,.24f); metal.SetFloat("_Smoothness",.22f);
            var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SignalTeal.mat");
            glass.color = new Color(.12f,.21f,.24f); glass.SetColor("_EmissionColor",new Color(.015f,.025f,.025f)); glass.SetFloat("_Metallic",.5f);glass.SetFloat("_Smoothness",.75f);
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(x=>x.name=="EastWestTestRoad" || x.name=="NorthSouthTestRoad"))
            {
                var path=Base+renderer.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){ material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path); }
                material.SetTexture("_BaseMap",Texture(Base+"Surfaces/gravelly_sand_diff_1k.jpg",false,false));
                material.SetTexture("_BumpMap",Texture(Base+"Surfaces/gravelly_sand_nor_gl_1k.jpg",true,false));
                material.EnableKeyword("_NORMALMAP");material.color=new Color(.75f,.71f,.62f);material.SetFloat("_Smoothness",.08f);
                material.mainTextureScale=renderer.name=="EastWestTestRoad"?new Vector2(42,2):new Vector2(2,42);renderer.sharedMaterial=material;EditorUtility.SetDirty(material);
            }
            EditorUtility.SetDirty(concrete);EditorUtility.SetDirty(metal);EditorUtility.SetDirty(glass);
        }

        private static void AddTerrain(Transform parent)
        {
            const string path = Base + "RangeTerrain.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null) { data = new TerrainData(); AssetDatabase.CreateAsset(data, path); }
            data.heightmapResolution = 257; data.size = new Vector3(500, 24, 500);
            var hills = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(x => x.name == "Mesa").Select(x=>x.bounds).ToArray();
            var heights = new float[257, 257];
            for (int z = 0; z < 257; z++) for (int x = 0; x < 257; x++)
            {
                float wx = x / 256f * 500 - 250, wz = z / 256f * 500 - 250;
                float height = 0;
                // Elevated scenery stays inside the existing blocked mesa footprints.
                foreach (var hill in hills)
                {
                    float dx = (wx - hill.center.x) / hill.extents.x, dz = (wz - hill.center.z) / hill.extents.z;
                    float falloff = Mathf.Max(0, 1 - dx * dx - dz * dz);
                    height = Mathf.Max(height, falloff * falloff * Mathf.Min(hill.size.y * .7f, 14));
                }
                heights[z, x] = height / 24;
            }
            data.SetHeights(0, 0, heights);
            const string layerPath = Base + "RangeGround.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layerPath); }
            layer.diffuseTexture = Texture(Base + "Surfaces/brown_mud_leaves_01_diff_1k.jpg", false, false);
            layer.normalMapTexture = Texture(Base + "Surfaces/brown_mud_leaves_01_nor_gl_1k.jpg", true, false);
            layer.tileSize = Vector2.one * 4; layer.smoothness = .05f; data.terrainLayers = new[] { layer };
            var go = Terrain.CreateTerrainGameObject(data); go.name = "Visual terrain — original flat collision retained";
            go.transform.SetParent(parent); go.transform.position = new Vector3(-250, -.018f, -250);
            Object.DestroyImmediate(go.GetComponent<TerrainCollider>());
            go.GetComponent<Terrain>().heightmapPixelError = 8;
            var old = GameObject.Find("Desert"); if (old != null) old.GetComponent<Renderer>().enabled = false;
            EditorUtility.SetDirty(data); EditorUtility.SetDirty(layer);
        }

        private static void AddNature(Transform parent)
        {
            var random = new System.Random(6422);
            for (int i = 0; i < 130; i++)
            {
                var x = (float)random.NextDouble() * 130 - 65; var z = (float)random.NextDouble() * 130 - 65;
                // Keep both long test roads, the motor pool and prior smoke-test route clear.
                if (Mathf.Abs(x - 16) < 7 || Mathf.Abs(z + 15) < 7 || Mathf.Abs(z + 30) < 4 || (Mathf.Abs(x) < 12 && Mathf.Abs(z) < 12)) continue;
                var asset = i % 5 == 0 ? "wild_rooibos_bush" : "grass_bermuda_01";
                var size = i % 5 == 0 ? new Vector3(1.3f, .9f, 1.3f) : new Vector3(.65f, .35f, .65f);
                var instance = Fit(asset, parent, new Vector3(x, -.01f, z), size, true);
                instance.transform.Rotate(0, (float)random.NextDouble() * 360, 0);
            }
            foreach (var p in new[] { new Vector3(-24,0,14),new Vector3(25,0,42),new Vector3(-36,0,-40),new Vector3(45,0,-7) })
                Fit("tree_small_02", parent, p, new Vector3(6, 8, 6), true);
            foreach (var p in new[] { new Vector3(-22,0,-3),new Vector3(38,0,12),new Vector3(-45,0,30),new Vector3(32,0,-42) })
                Fit("rock_07", parent, p, new Vector3(4, 2.5f, 3), true);
        }

        private static void ConfigureLighting(Transform parent)
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(Base + "RangeSky.mat");
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(sky, Base + "RangeSky.mat"); }
            sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(Base + "Surfaces/dry_field_1k.hdr")); sky.SetFloat("_Exposure", .8f); sky.SetFloat("_Rotation", 110);
            RenderSettings.skybox = sky; RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.fog = true; RenderSettings.fogDensity = .004f; RenderSettings.fogColor = new Color(.58f,.62f,.61f);
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(x=>x.type==LightType.Directional);
            sun.intensity = 2.2f; sun.color = new Color(1,.94f,.82f); sun.shadows = LightShadows.Soft; sun.shadowBias = .04f;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("Expected the existing URP pipeline.");
            pipeline.supportsHDR = true; pipeline.msaaSampleCount = 4; pipeline.shadowDistance = 90; EditorUtility.SetDirty(pipeline);
            var camera = Camera.main; camera.clearFlags = CameraClearFlags.Skybox; var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = true; additional.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            const string profilePath = Base + "RangePostProcessing.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            var tone = profile.TryGet<Tonemapping>(out var t) ? t : profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var color = profile.TryGet<ColorAdjustments>(out var c) ? c : profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.1f); color.contrast.Override(10); color.saturation.Override(-7);
            var bloom = profile.TryGet<Bloom>(out var b) ? b : profile.Add<Bloom>(true); bloom.threshold.Override(1.2f); bloom.intensity.Override(.12f);
            // Persist newly-created VolumeComponents as subassets, not transient instances.
            foreach (var component in profile.components) if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            var volume = new GameObject("Range color grading").AddComponent<Volume>(); volume.transform.SetParent(parent); volume.isGlobal = true; volume.sharedProfile = profile;
            var probe = new GameObject("Outpost reflection probe").AddComponent<ReflectionProbe>(); probe.transform.SetParent(parent); probe.transform.position = new Vector3(0,3,0);
            probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.OnAwake; probe.resolution = 128; probe.size = new Vector3(70,12,70); probe.boxProjection = true;
            DynamicGI.UpdateEnvironment();
            EditorUtility.SetDirty(profile); EditorUtility.SetDirty(sky);
        }
    }
}
