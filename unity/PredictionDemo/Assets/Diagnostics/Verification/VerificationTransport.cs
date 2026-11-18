using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
namespace PredictionDemo {
public class VerificationTransport : PredictionDemo.Security.ISecureDriverConstructor {
 readonly int delay,loss;
 public VerificationTransport(int delay,int loss){this.delay=delay;this.loss=loss;}
 public void CreateDriver(UnityTransport t,out NetworkDriver d,out NetworkPipeline u,out NetworkPipeline us,out NetworkPipeline r){
  var settings=t.GetDefaultNetworkSettings();settings.WithSimulatorStageParameters(maxPacketCount:2048,mode:ApplyMode.ReceivedPacketsOnly,packetDelayMs:delay,packetDropPercentage:loss,randomSeed:8123);
  d=PredictionDemo.Security.SecureDriverConstructor.CreateSecureDriver(t,settings);
  u=d.CreatePipeline(typeof(FragmentationPipelineStage),typeof(SimulatorPipelineStage));
  us=d.CreatePipeline(typeof(FragmentationPipelineStage),typeof(UnreliableSequencedPipelineStage),typeof(SimulatorPipelineStage));
  r=d.CreatePipeline(typeof(ReliableSequencedPipelineStage),typeof(SimulatorPipelineStage));
  UnityEngine.Debug.Log($"[VERIFY] UTP simulator RECEIVE delay={delay}ms loss={loss}% seed=8123");
 }
}
}
