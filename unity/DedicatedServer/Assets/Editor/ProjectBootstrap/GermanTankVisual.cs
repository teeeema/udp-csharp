using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PredictionDemo.EditorTools
{
    public static class GermanTankVisual
    {
        public const string ModelPath = "Assets/ThirdParty/Low Poly German Tanks/Pz-VI-Tiger/Pz-VI Tiger.fbx";
        private const string MaterialPath = "Assets/ThirdParty/Low Poly German Tanks/TigerURP.mat";

        [MenuItem("PR4/Maps/Apply supplied Tiger visual")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before editing the player prefab.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/Low Poly German Tanks/Pz-VI-Tiger/colorAtlas.png");
            if (model == null || atlas == null)
                throw new InvalidOperationException("Import the supplied German Tanks model and atlas first.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", atlas);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", .15f);
            material.SetFloat("_Smoothness", .2f);
            EditorUtility.SetDirty(material);

            const string prefabPath = "Assets/Prefabs/PredictedPlayer.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var visual = root.transform.Find("Visual");
                if (visual == null) throw new InvalidOperationException("Existing Visual container is required.");
                for (int i = visual.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(visual.GetChild(i).gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
                instance.name = "Supplied Tiger I — visual only";
                instance.transform.SetParent(visual, false);
                // Scale from the hull, excluding the overhanging barrel. Collision stays on the existing simulation.
                var hull = instance.GetComponent<Renderer>();
                if (hull == null) throw new InvalidOperationException("Tiger hull renderer missing.");
                var bounds = hull.bounds;
                instance.transform.localScale *= Mathf.Min(1.86f / bounds.size.x, 3.2f / bounds.size.z);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                var fullBounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    fullBounds.Encapsulate(renderer.bounds);
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                }
                instance.transform.position += new Vector3(-hull.bounds.center.x, -.95f - fullBounds.min.y, -hull.bounds.center.z);
                if (instance.GetComponentsInChildren<Collider>().Length != 0 || instance.GetComponentsInChildren<MonoBehaviour>().Length != 0)
                    throw new InvalidOperationException("The visual model must contain no colliders or gameplay scripts.");
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
