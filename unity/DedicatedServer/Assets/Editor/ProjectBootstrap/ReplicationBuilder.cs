using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.Netcode;
namespace PredictionDemo.EditorTools {
public static class ReplicationBuilder {
 [MenuItem("PR5/Create replication prefab")]
 public static void Prepare() {
  Directory.CreateDirectory("Assets/Resources");
  var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="ReplicationProp";
  go.AddComponent<NetworkObject>();go.AddComponent<ReplicatedProp>();
  PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/ReplicationProp.prefab");
  Object.DestroyImmediate(go);AssetDatabase.SaveAssets();
 }
 [MenuItem("PR5/Build macOS")]
 public static void Build() {
  Prepare();
  PlayerSettings.defaultScreenWidth=1920;
  PlayerSettings.defaultScreenHeight=1080;
  PlayerSettings.defaultIsNativeResolution=true;
  PlayerSettings.macRetinaSupport=true;
  PlayerSettings.resizableWindow=true;
  PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
  PlayerSettings.runInBackground=true;
  EditorUserBuildSettings.development=false;
  EditorUserBuildSettings.connectProfiler=false;
  EditorUserBuildSettings.allowDebugging=false;
  QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names,"PC"),true);
  var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
  pipeline.renderScale=1f;
  pipeline.msaaSampleCount=4;
  EditorUtility.SetDirty(pipeline);
  AssetDatabase.SaveAssets();
  var r=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/PredictionDemo.unity"},"Builds/ReplicationLab.app",BuildTarget.StandaloneOSX,BuildOptions.None);
  if(r.summary.result!=BuildResult.Succeeded) throw new System.Exception("Build failed: "+r.summary.result);
 }
}
}
