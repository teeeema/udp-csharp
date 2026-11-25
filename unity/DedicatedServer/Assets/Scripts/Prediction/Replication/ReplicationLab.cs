using System;
using System.IO;
using System.Globalization;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace PredictionDemo {
public class ReplicationLab : MonoBehaviour {
 public static ReplicationLab Instance;
 public bool Optimized; public GameObject PropPrefab;
 NetworkManager nm; MeteredTransport meter; bool spawned; float nextVisibility,nextSample; long sent,received;
 public int VisibleCount,PlayerCount; public float OutKB,InKB; StreamWriter csv;
 bool showStatistics;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
 static void Register() { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }
 static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }
 static void Install() {
  var nm=NetworkManager.Singleton; if(nm==null || nm.GetComponent<ReplicationLab>()!=null) return;
  nm.gameObject.AddComponent<ReplicationLab>();
 }
 void Awake() {
  Instance=this; nm=GetComponent<NetworkManager>();
  var old=GetComponent<UnityTransport>();
  meter=GetComponent<MeteredTransport>(); if(meter==null) meter=gameObject.AddComponent<MeteredTransport>(); nm.NetworkConfig.NetworkTransport=meter;
  nm.OnServerStarted += OnServerStarted; nm.OnServerStopped += OnServerStopped; nm.OnClientStarted += ResetStatistics;
  PropPrefab=Resources.Load<GameObject>("ReplicationProp");
  if(PropPrefab!=null) nm.AddNetworkPrefab(PropPrefab);
  var args=Environment.GetCommandLineArgs(); if(Array.IndexOf(args,"--verify")>=0) gameObject.AddComponent<ReplicationVerification>(); Optimized=Array.IndexOf(args,"--optimized")>=0;
  string path=null; for(int i=0;i<args.Length-1;i++) if(args[i]=="--metrics") path=args[i+1];
  if(path!=null) {Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); csv=new StreamWriter(path); csv.WriteLine("seconds,mode,server,clients,objects,visible,tx_bytes,rx_bytes,tx_KBps,rx_KBps");csv.AutoFlush=true;}
 }
 void ResetStatistics() { sent=received=0; nextSample=0; OutKB=InKB=0; VisibleCount=PlayerCount=0; }
 void OnServerStarted() { spawned=false; nextVisibility=0; ResetStatistics(); }
 void OnServerStopped(bool wasHost) { spawned=false; nextVisibility=0; ResetStatistics(); }
 public void Server() { meter.SetConnectionData("127.0.0.1",7777,"0.0.0.0"); nm.StartServer(); }
 void Update() {
  if(UnityEngine.InputSystem.Keyboard.current?.f4Key.wasPressedThisFrame == true) showStatistics=!showStatistics;
  // Original bootstrap configures its original UnityTransport. Mirror endpoint before connection.
  if(!nm.IsListening) meter.ConnectionData=GetComponent<UnityTransport>().ConnectionData;
  if(nm.IsServer && !spawned) { spawned=true; for(int i=0;i<50;i++) {
   var go=Instantiate(PropPrefab); var prop=go.GetComponent<ReplicatedProp>();
   prop.Index=i; prop.Kind.Reset(i<20?0:i<35?1:i<45?2:3);
   prop.Position.Reset(new Vector3((i%10)*12-54,1,(i/10)*12+12));
   go.GetComponent<NetworkObject>().Spawn();
  }}
  if(nm.IsServer && Time.unscaledTime>=nextVisibility) {
   nextVisibility=Time.unscaledTime+.2f;
   foreach(var prop in FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None))
   foreach(var c in nm.ConnectedClientsList) {
    if(c.ClientId==NetworkManager.ServerClientId || c.PlayerObject==null) continue;
    bool show=!Optimized || ReplicationPolicy.Relevant(prop.transform.position,c.PlayerObject.transform.position);
    bool visible=prop.NetworkObject.IsNetworkVisibleTo(c.ClientId);
    if(show&&!visible) prop.NetworkObject.NetworkShow(c.ClientId);
    if(!show&&visible) prop.NetworkObject.NetworkHide(c.ClientId);
   }
  }
  if(Time.unscaledTime>=nextSample) {
   float duration=nextSample==0?1:Time.unscaledTime-(nextSample-1);nextSample=Time.unscaledTime+1;
   OutKB=(meter.SentBytes-sent)/duration/1000f; InKB=(meter.ReceivedBytes-received)/duration/1000f;
   sent=meter.SentBytes; received=meter.ReceivedBytes;
   int count=FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None).Length;
   int clients=FindObjectsByType<PredictedPlayer>(FindObjectsSortMode.None).Length; PlayerCount=clients;
   VisibleCount=count;
   if(nm.IsServer) foreach(var c in nm.ConnectedClientsList) if(c.ClientId!=NetworkManager.ServerClientId) { VisibleCount=0; foreach(var p in FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None)) if(p.NetworkObject.IsNetworkVisibleTo(c.ClientId)) VisibleCount++; break; }
   csv?.WriteLine(FormattableString.Invariant($"{Time.unscaledTime:F3},{(Optimized?"OPTIMIZED":"BASELINE")},{nm.IsServer},{clients},{count},{VisibleCount},{sent},{received},{OutKB:F3},{InKB:F3}"));
  }
 }
 void OnGUI() {
  if(!showStatistics) return;
  var previous=PredictionHudLayout.BeginScaledGui();
  try {
  GUI.Box(new Rect(10,110,650,115),"PR5 Replication laboratory");
  GUI.Label(new Rect(20,135,630,25),$"{(Optimized?"OPTIMIZED":"BASELINE")} | props {FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None).Length}/50 visible {VisibleCount} players {PlayerCount} | TX {OutKB:F2} RX {InKB:F2} KB/s");
  GUI.Label(new Rect(20,160,630,25),$"NGO transport payload (excludes UDP headers/retransmits) | {(Optimized?"5 Hz / 2 Hz":"30 Hz")}");
  if(nm.IsServer && GUI.Button(new Rect(20,190,180,25),"Switch mode (server)")) Optimized=!Optimized;
  if(!nm.IsListening && GUI.Button(new Rect(210,190,120,25),"Dedicated server")) Server();
  } finally { PredictionHudLayout.EndScaledGui(previous); }
 }
 void OnDestroy(){
  if(nm!=null) {nm.OnServerStarted-=OnServerStarted;nm.OnServerStopped-=OnServerStopped;nm.OnClientStarted-=ResetStatistics;}
  if(Instance==this) Instance=null; csv?.Dispose();
 }
}
}
