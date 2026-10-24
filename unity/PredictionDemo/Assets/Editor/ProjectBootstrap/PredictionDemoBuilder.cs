using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PredictionDemo.EditorTools
{
    public static class PredictionDemoBuilder
    {
        private const string ScenePath = "Assets/Scenes/PredictionDemo.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/PredictedPlayer.prefab";
        private static Material _armor, _dark, _metal, _sand, _concrete, _rust, _teal, _light, _rock, _paint;

        [MenuItem("PR4/Build Demo Assets")]
        public static void BuildDemoAssets()
        {
            foreach (var folder in new[] { "Assets/Prefabs", "Assets/Materials", "Assets/Models" })
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            _armor = Material("Player", new Color(.19f, .37f, .31f), .45f);
            _dark = Material("TankDarkMetal", new Color(.055f, .075f, .085f), .3f);
            _metal = Material("TankGunMetal", new Color(.27f, .32f, .31f), .65f);
            _sand = Material("Floor", new Color(.61f, .46f, .30f));
            _concrete = Material("ConcreteBunker", new Color(.46f, .43f, .34f));
            _rust = Material("MilitaryCrate", new Color(.48f, .20f, .095f), .25f);
            _teal = Material("SignalTeal", new Color(.12f, .71f, .64f), .15f, true);
            _light = Material("WarmLight", new Color(1, .62f, .22f), 0, true);
            _rock = Material("DesertRock", new Color(.49f, .32f, .22f));
            _paint = Material("IvoryPaint", new Color(.87f, .79f, .57f));
            var player = CreatePlayer();
            CreateScene(player);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[PR4][Builder] DUSTLINE open test world and detailed tank saved.");
        }

        [MenuItem("PR4/Build macOS Demo")]
        public static void BuildMacDemo()
        {
            BuildDemoAssets();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/PR4PredictionDemo.app",
                target = BuildTarget.StandaloneOSX, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"macOS build failed: {report.summary.result}");
            Debug.Log($"[PR4][Builder] macOS build complete: {report.summary.outputPath}");
        }

        private static GameObject CreatePlayer()
        {
            var root = new GameObject("PredictedPlayer");
            root.AddComponent<NetworkObject>();
            root.AddComponent<PredictedPlayer>();
            root.AddComponent<TankPresentation>();
            root.AddComponent<TankWeapon>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            // The root travels at y=1; tracks reach down to the ground.
            Armor("Hull", visual, new Vector3(0, -.38f, 0), new Vector3(1.22f, .48f, 1.95f), _armor);
            Armor("Deck", visual, new Vector3(0, -.08f, -.12f), new Vector3(1.18f, .17f, 1.65f), _armor);
            for (int side = -1; side <= 1; side += 2)
            {
                Armor("Track", visual, new Vector3(side * .72f, -.57f, 0), new Vector3(.38f, .53f, 2.14f), _dark);
                for (int i = 0; i < 6; i++)
                {
                    var wheel = Part("Wheel_" + side + "_" + i, visual, PrimitiveType.Cylinder,
                        new Vector3(side * .93f, -.6f, -.8f + i * .32f), new Vector3(.29f, .045f, .29f), _metal);
                    wheel.localRotation = Quaternion.Euler(0, 0, 90);
                    Part("WheelHub", wheel, PrimitiveType.Cylinder, new Vector3(0, side * .6f, 0), new Vector3(.45f, .6f, .45f), _dark);
                }
                for (int i = 0; i < 13; i++)
                    Part("TrackTread", visual, PrimitiveType.Cube, new Vector3(side * .73f, -.28f, -.96f + i * .16f), new Vector3(.4f, .035f, .065f), _metal);
                Armor("SideSkirt", visual, new Vector3(side * .79f, -.14f, -.1f), new Vector3(.18f, .19f, 1.8f), _armor);
                Part("Headlight", visual, PrimitiveType.Cube, new Vector3(side * .47f, -.22f, .99f), new Vector3(.15f, .1f, .04f), _light);
                Part("TailLight", visual, PrimitiveType.Cube, new Vector3(side * .47f, -.3f, -1), new Vector3(.12f, .06f, .04f), _rust);
            }
            Part("TurretRing", visual, PrimitiveType.Cylinder, new Vector3(0, .04f, -.13f), new Vector3(.85f, .05f, .85f), _dark);
            Armor("Turret", visual, new Vector3(0, .22f, -.16f), new Vector3(.95f, .38f, 1.02f), _armor);
            Armor("Mantlet", visual, new Vector3(0, .2f, .4f), new Vector3(.42f, .27f, .29f), _metal);
            var barrel = Part("Barrel", visual, PrimitiveType.Cylinder, new Vector3(0, .2f, .99f), new Vector3(.12f, .62f, .12f), _metal);
            barrel.localRotation = Quaternion.Euler(90, 0, 0);
            Armor("BarrelMuzzle", visual, new Vector3(0, .2f, 1.64f), new Vector3(.23f, .19f, .24f), _dark);
            Part("Bore", visual, PrimitiveType.Cube, new Vector3(0, .2f, 1.765f), new Vector3(.11f, .085f, .012f), _dark);
            Part("Hatch", visual, PrimitiveType.Cylinder, new Vector3(-.2f, .44f, -.22f), new Vector3(.34f, .04f, .34f), _dark);
            Part("Periscope", visual, PrimitiveType.Cube, new Vector3(-.2f, .51f, -.1f), new Vector3(.18f, .07f, .09f), _teal);
            for (int i = 0; i < 5; i++)
                Part("EngineVent", visual, PrimitiveType.Cube, new Vector3(0, .025f, -.61f - i * .065f), new Vector3(.7f, .025f, .028f), _dark);
            Part("Antenna", visual, PrimitiveType.Cylinder, new Vector3(.4f, .68f, -.49f), new Vector3(.016f, .36f, .016f), _dark);
            Part("AntennaTip", visual, PrimitiveType.Sphere, new Vector3(.4f, 1.06f, -.49f), Vector3.one * .05f, _light);
            Part("ArmorStripe", visual, PrimitiveType.Cube, new Vector3(.3f, .42f, -.15f), new Vector3(.095f, .012f, .63f), _paint);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateScene(GameObject player)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = new GameObject("Outpost — environment").transform;
            Part("Desert", ground, PrimitiveType.Cube, new Vector3(0, -.22f, 0), new Vector3(500, .4f, 500), _sand, true);
            Part("MotorPoolApron", ground, PrimitiveType.Cube, new Vector3(0, -.01f, -4.8f), new Vector3(16, .025f, 7), _concrete);
            // Thin markings are decorative: every obstacle uses a single, explicit collider.
            for (int x = -6; x <= 6; x += 3)
                Part("ParkingStripe", ground, PrimitiveType.Cube, new Vector3(x, .012f, -5.9f), new Vector3(.055f, .014f, 3.5f), _paint);
            for (int z = -8; z <= 8; z += 2)
                Part("RouteDash", ground, PrimitiveType.Cube, new Vector3(-6, .008f, z), new Vector3(.1f, .012f, .7f), _paint);
            Block("CenterBunker", new Vector3(0, 1, 1.5f), new Vector3(2.4f, 2, 2.4f), _concrete);
            Part("BunkerRoof", ground, PrimitiveType.Cube, new Vector3(0, 2.08f, 1.5f), new Vector3(2.7f, .2f, 2.7f), _metal);
            Part("BunkerDoor", ground, PrimitiveType.Cube, new Vector3(0, .75f, .285f), new Vector3(.8f, 1.5f, .03f), _dark);
            Part("DoorStatus", ground, PrimitiveType.Cube, new Vector3(.56f, 1.3f, .26f), new Vector3(.08f, .2f, .05f), _teal);
            for (int side = -1; side <= 1; side += 2)
            {
                Part("BunkerSlit", ground, PrimitiveType.Cube, new Vector3(side * 1.21f, 1.4f, 1.5f), new Vector3(.03f, .23f, 1.5f), _dark);
                for (int i = 0; i < 3; i++)
                {
                    var z = 1 + i * 2;
                    Block("Perimeter", new Vector3(side * 9, .95f, z), new Vector3(.7f, 1.9f, 1.9f), _concrete);
                    Part("WallCap", ground, PrimitiveType.Cube, new Vector3(side * 9, 1.94f, z), new Vector3(.84f, .15f, 1.95f), _metal);
                }
            }
            Crate(new Vector3(-3.6f, .75f, 3.5f));
            Crate(new Vector3(3.6f, .75f, -3.5f));
            Barrier(new Vector3(-3.5f, .6f, -2.5f));
            Barrier(new Vector3(3.5f, .6f, 4));
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(-2.5f + i * 1.5f, .3f, 8.6f);
                Part("FuelDrum", ground, PrimitiveType.Cylinder, p, new Vector3(.45f, .3f, .45f), i % 2 == 0 ? _rust : _metal);
                Part("DrumBand", ground, PrimitiveType.Cylinder, p + Vector3.up * .12f, new Vector3(.46f, .018f, .46f), _dark);
            }
            // Large silhouettes outside the playable boundary give the outpost a place in the world.
            Hangar(new Vector3(-16, 0, 12));
            Tower(new Vector3(11.5f, 0, 10.5f));
            Tower(new Vector3(-11.5f, 0, -9));
            var random = new System.Random(4203);
            for (int i = 0; i < 28; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radius = 85 + (float)random.NextDouble() * 65;
                var pos = new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
                var scale = new Vector3(6 + (float)random.NextDouble() * 10, 4 + (float)random.NextDouble() * 11, 6 + (float)random.NextDouble() * 8);
                var rock = Armor("Mesa", ground, pos + Vector3.up * scale.y * .32f, scale, _rock);
                rock.rotation = Quaternion.Euler(6, i * 37, 8);
                rock.gameObject.AddComponent<BoxCollider>();
                Armor("MesaCap", ground, pos + Vector3.up * scale.y * .85f, new Vector3(scale.x * .85f, .5f, scale.z * .85f), _sand);
            }
            CreateOpenRange(ground);
            var light = new GameObject("Late afternoon sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.8f; light.color = new Color(1, .82f, .61f);
            light.transform.rotation = Quaternion.Euler(38, -32, 0);
            light.shadows = LightShadows.Soft; light.shadowStrength = .8f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .57f, .65f);
            RenderSettings.ambientEquatorColor = new Color(.42f, .36f, .28f);
            RenderSettings.ambientGroundColor = new Color(.22f, .18f, .14f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.69f, .57f, .43f); RenderSettings.fogDensity = .006f;
            RenderSettings.skybox = null;
            QualitySettings.shadowDistance = 80;
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(11, 17, -20);
            cameraObject.transform.LookAt(new Vector3(0, 0, 1));
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 44; camera.nearClipPlane = .1f; camera.farClipPlane = 350;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
            cameraObject.AddComponent<AudioListener>(); cameraObject.AddComponent<CameraFollow>();
            var network = new GameObject("NetworkSystem");
            var manager = network.AddComponent<NetworkManager>();
            var transport = network.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, PlayerPrefab = null, TickRate = 50 };
            var bootstrap = network.AddComponent<NetworkBootstrap>();
            var settings = new SerializedObject(bootstrap);
            settings.FindProperty("playerPrefab").objectReferenceValue = player;
            settings.ApplyModifiedPropertiesWithoutUndo();
            network.AddComponent<TankGameHud>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateOpenRange(Transform ground)
        {
            var road = Material("RangeRoad", new Color(.25f, .27f, .25f));
            Part("EastWestTestRoad", ground, PrimitiveType.Cube, new Vector3(0, -.005f, -15), new Vector3(170, .025f, 7), road);
            Part("NorthSouthTestRoad", ground, PrimitiveType.Cube, new Vector3(16, -.005f, 0), new Vector3(7, .025f, 170), road);
            for (int i = -8; i <= 8; i++)
            {
                Part("RoadMark", ground, PrimitiveType.Cube, new Vector3(i * 10, .013f, -15), new Vector3(3, .012f, .12f), _paint);
                Part("RoadMark", ground, PrimitiveType.Cube, new Vector3(16, .013f, i * 10), new Vector3(.12f, .012f, 3), _paint);
            }
            for (int i = 0; i < 5; i++)
            {
                Barrier(new Vector3(-28 + i * 6, .6f, 24));
                Crate(new Vector3(29 + i * 4, .75f, 25));
                Part("DistanceMarker", ground, PrimitiveType.Cube, new Vector3(21, .6f, 20 + i * 10), new Vector3(.3f, 1.2f, .3f), _teal);
            }
            Hangar(new Vector3(-32, 0, 37));
            Tower(new Vector3(40, 0, -32));
        }

        private static void Crate(Vector3 p)
        {
            var root = Block("CargoContainer", p, new Vector3(1.5f, 1.5f, 1.5f), _rust);
            for (int i = -2; i <= 2; i++)
                Part("Rib", root.transform, PrimitiveType.Cube, new Vector3(i * .18f, 0, -.51f), new Vector3(.035f, .94f, .045f), _metal);
            Part("CargoRoof", null, PrimitiveType.Cube, p + Vector3.up * .8f, new Vector3(1.6f, .12f, 1.6f), _metal);
        }

        private static void Barrier(Vector3 p)
        {
            Block("Barrier", p, new Vector3(2.5f, 1.2f, .6f), _concrete);
            for (int i = -2; i <= 2; i++)
            {
                var stripe = Part("HazardStripe", null, PrimitiveType.Cube, p + new Vector3(i * .44f, .1f, -.31f), new Vector3(.19f, .7f, .015f), _rust);
                stripe.rotation = Quaternion.Euler(0, 0, -23);
            }
        }

        private static void Hangar(Vector3 p)
        {
            Part("Hangar", null, PrimitiveType.Cube, p + Vector3.up * 2.2f, new Vector3(8, 4.4f, 7), _metal, true);
            Armor("HangarRoof", null, p + Vector3.up * 4.7f, new Vector3(8.6f, 1.2f, 7.6f), _rust);
            Part("HangarDoor", null, PrimitiveType.Cube, p + new Vector3(0, 1.8f, -3.52f), new Vector3(5.5f, 3.6f, .06f), _dark);
            for (int i = 0; i < 8; i++) Part("DoorSlat", null, PrimitiveType.Cube, p + new Vector3(0, .25f + i * .44f, -3.57f), new Vector3(5.3f, .035f, .05f), _metal);
        }

        private static void Tower(Vector3 p)
        {
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Part("TowerLeg", null, PrimitiveType.Cube, p + new Vector3(x * .8f, 2.4f, z * .8f), new Vector3(.16f, 4.8f, .16f), _metal);
            Part("WatchPost", null, PrimitiveType.Cube, p + Vector3.up * 4.8f, new Vector3(2.4f, 1.5f, 2.4f), _concrete);
            Part("ObservationWindow", null, PrimitiveType.Cube, p + new Vector3(0, 5.05f, -1.21f), new Vector3(1.9f, .5f, .03f), _teal);
            Armor("WatchRoof", null, p + Vector3.up * 5.7f, new Vector3(2.8f, .35f, 2.8f), _rust);
        }

        private static GameObject Block(string name, Vector3 p, Vector3 scale, Material material)
            => Part(name, null, PrimitiveType.Cube, p, scale, material, true).gameObject;

        private static Transform Part(string name, Transform parent, PrimitiveType type, Vector3 p, Vector3 scale, Material material, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        private static Transform Armor(string name, Transform parent, Vector3 p, Vector3 scale, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = ArmorMesh();
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        private static Mesh ArmorMesh()
        {
            const string path = "Assets/Models/ChamferedArmor.asset";
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached != null) return cached;
            var outline = new[] { new Vector2(-.32f,-.5f),new Vector2(.32f,-.5f),new Vector2(.5f,-.32f),new Vector2(.5f,.32f),new Vector2(.32f,.5f),new Vector2(-.32f,.5f),new Vector2(-.5f,.32f),new Vector2(-.5f,-.32f) };
            var verts = new System.Collections.Generic.List<Vector3>();
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 8; i++)
            {
                int n = (i + 1) % 8;
                var a = new Vector3(outline[i].x, -.5f, outline[i].y);
                var b = new Vector3(outline[n].x, -.5f, outline[n].y);
                var c = new Vector3(outline[n].x * .78f, .5f, outline[n].y * .85f);
                var d = new Vector3(outline[i].x * .78f, .5f, outline[i].y * .85f);
                AddTriangle(verts, indices, a, c, b); AddTriangle(verts, indices, a, d, c);
                AddTriangle(verts, indices, Vector3.up * .5f, c, d);
                AddTriangle(verts, indices, Vector3.down * .5f, a, b);
            }
            var mesh = new Mesh { name = "ChamferedArmor" }; mesh.SetVertices(verts); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static void AddTriangle(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> indices, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = verts.Count; verts.Add(a); verts.Add(b); verts.Add(c); indices.Add(start); indices.Add(start+1); indices.Add(start+2);
        }

        private static Material Material(string name, Color color, float metallic = 0, bool emissive = false)
        {
            var path = "Assets/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            // Restore the original flat-colour style, including materials previously textured by an import.
            var defaults = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.shader = defaults.shader;
            material.CopyPropertiesFromMaterial(defaults);
            Object.DestroyImmediate(defaults);
            material.color = color; material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", .25f);
            if (emissive) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 1.4f); }
            EditorUtility.SetDirty(material); return material;
        }
    }
}
