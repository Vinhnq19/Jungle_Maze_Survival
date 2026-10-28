using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Networking;
using JungleBackend.Protocol.Gameplay;

if (args.FirstOrDefault() == "External") { await ExternalChecks.Run(args[1]); return; }
if (args.FirstOrDefault() == "Remote") { await RemoteChecks.Run(args[1], args.Length > 2 ? args[2] : "Mixed"); return; }

// Isolated loopback fixture; never targets a live room or starts EC2.
// Run with Udp, Wss or Mixed, optionally followed by a JSON evidence path.
var passed = new List<string>();
void Check(bool value, string message) { if (!value) throw new Exception(message); passed.Add(message);Console.WriteLine("PASS "+message); }
int Port(SocketType type){using var s=new Socket(AddressFamily.InterNetwork,type,type==SocketType.Stream?ProtocolType.Tcp:ProtocolType.Udp);s.Bind(new IPEndPoint(IPAddress.Loopback,0));return ((IPEndPoint)s.LocalEndPoint!).Port;}
Environment.SetEnvironmentVariable("MATCH_PROFILE","test");
Environment.SetEnvironmentVariable("WS_ENABLED","true");
Environment.SetEnvironmentVariable("WS_BIND_ADDRESS","127.0.0.1");
var wsPort=Port(SocketType.Stream);Environment.SetEnvironmentVariable("WS_PORT",wsPort.ToString());
var udpPort=Port(SocketType.Dgram);
using var host=new NetworkHost(new ServerOptions(IPAddress.Loopback,udpPort,10,30,1,"RoomCode","PHASE2_TEST"),new ServerMetrics());host.Start();
await using var ws=await WebSocketHost.StartFromEnvironment(host.Hub,50);
var clients=new List<MatchNetworkClient>();var clock=Stopwatch.StartNew();double nextTick=0;double nextInput=0;
void Pump(Func<bool> done,int timeoutMs=5000){var deadline=clock.ElapsedMilliseconds+timeoutMs;while(!done()) {ws!.PollEvents();host.PollEvents();foreach(var c in clients)c.Poll();var now=clock.Elapsed.TotalSeconds;while(now>=nextTick){host.Hub.Tick(1.0/30);nextTick+=1.0/30;}if(now>=nextInput){for(var i=0;i<clients.Count;i++)clients[i].Input(0,(i%2==0?.25f:-.25f));nextInput=now+1.0/30;}if(clock.ElapsedMilliseconds>deadline)throw new TimeoutException("Real gameplay probe deadline");Thread.Sleep(1);}}
try {
var mode=args.FirstOrDefault()??"Mixed";
if(mode is not ("Udp" or "Wss" or "Mixed"))throw new ArgumentException("Use Udp, Wss or Mixed.");
for(var i=0;i<10;i++){var c=new MatchNetworkClient(mode=="Mixed"?(i%2==0?"Udp":"Wss"):mode){AutoReady=false};clients.Add(c);await c.ConnectAsync("127.0.0.1",udpPort,$"ws://127.0.0.1:{wsPort}/game","PHASE2_TEST");}
Pump(()=>clients.All(c=>c.PlayerId!=null));
Check(host.ReservedPeers==10,"Ten mixed network sessions reserve exactly ten slots");
await using(var extra=new MatchNetworkClient("Udp")){await extra.ConnectAsync("127.0.0.1",udpPort,$"ws://127.0.0.1:{wsPort}/game","PHASE2_TEST");Pump(()=>{extra.Poll();return extra.Rejected!=null;});Check(extra.Rejected=="RoomFull","Eleventh network client rejected RoomFull");}
foreach(var c in clients){c.AutoReady=true;c.Hello();}
Pump(()=>clients.All(c=>c.Phase==MatchPhase.InGame&&c.SnapshotCount>5));
Check(clients.All(c=>c.Zone.MatchId==c.MatchId&&c.Zone.CurrentRadius>0),"Both transports receive match-scoped zone with snapshots");
var start=clock.ElapsedMilliseconds;Pump(()=>clock.ElapsedMilliseconds-start>5000,6500);
Check(clients.All(c=>c.SnapshotCount>=140&&c.InvalidFrames==0&&c.DuplicateSnapshots==0),"30Hz snapshots: no repeated hooks or invalid frames on ten peers");
// Real combat over the real transport: requests are authoritative on the server and accepted results reach the whole room.
var combatMatch=host.Hub.Rooms.Find("PHASE2_TEST")!.Match;
var attacker=clients[0];var witness=clients[1];
attacker.Attack(1,CombatWeaponId.Hand);
Pump(()=>attacker.AttackResults.Any(r=>r.RequestSeq==1)&&witness.AttackResults.Any(r=>r.RequestSeq==1));
var meleeResult=attacker.AttackResults.First(r=>r.RequestSeq==1);
Check(meleeResult.Outcome==CombatOutcome.Accepted&&meleeResult.NetId==attacker.OwnNetId,"Real UDP/WS melee attack accepted and stamped with attacker NetId");
Check(witness.AttackResults.First(r=>r.RequestSeq==1) is {Outcome:CombatOutcome.Accepted} w&&w.NetId==attacker.OwnNetId,"Other real client receives the accepted attack broadcast");
attacker.Attack(1,CombatWeaponId.Hand);
Pump(()=>attacker.AttackResults.Count(r=>r.RequestSeq==1)==2);
Check(attacker.AttackResults.Last().Outcome==CombatOutcome.Duplicate&&witness.AttackResults.Count(r=>r.RequestSeq==1)==1,"Repeated RequestSeq is Duplicate, answered only to the sender");
var cooldownStart=clock.ElapsedMilliseconds;Pump(()=>clock.ElapsedMilliseconds-cooldownStart>600,1000);
attacker.Reload(2,CombatWeaponId.Hand);
Pump(()=>attacker.ReloadResults.Any(r=>r.RequestSeq==2));
Check(attacker.ReloadResults.First(r=>r.RequestSeq==2).Outcome==CombatOutcome.InvalidWeapon&&!witness.ReloadResults.Any(r=>r.RequestSeq==2),"Reload with bare hands is InvalidWeapon and not broadcast");
Check(combatMatch.TrySetHeldWeapon(attacker.PlayerId!,CombatWeaponId.RubberGun),"Server accepts held RubberGun for the attacker");
attacker.Attack(3,CombatWeaponId.RubberGun);
Pump(()=>attacker.AttackResults.Any(r=>r.RequestSeq==3)&&witness.AttackResults.Any(r=>r.RequestSeq==3));
Check(attacker.AttackResults.First(r=>r.RequestSeq==3) is {Outcome:CombatOutcome.Accepted,Magazine:11}&&witness.AttackResults.Any(r=>r.RequestSeq==3),"Ranged shot accepted over real transport and broadcast to the room");
attacker.Reload(4,CombatWeaponId.RubberGun);
Pump(()=>attacker.ReloadResults.Any(r=>r.RequestSeq==4)&&witness.ReloadResults.Any(r=>r.RequestSeq==4));
var reloadResult=attacker.ReloadResults.First(r=>r.RequestSeq==4);
Check(reloadResult.Outcome==CombatOutcome.Accepted&&reloadResult.ReloadEndTick-reloadResult.Tick==36&&witness.ReloadResults.First(r=>r.RequestSeq==4).Outcome==CombatOutcome.Accepted,"Reload accepted with 1.2 s end tick and broadcast to the room");

// Test-only authoritative damage, not a weapon/combat implementation.
var victim=clients[9]; var killer=clients[8];
host.Hub.Rooms.Find("PHASE2_TEST")!.Match.Health.ApplyDamage(victim.OwnNetId,100,killer.OwnNetId);
Pump(()=>Enumerable.Range(0,victim.Snapshot.PlayerCount).Any(i=>victim.Snapshot.GetPlayer(i).NetId==victim.OwnNetId && victim.Snapshot.GetPlayer(i).IsDead));
var replayRequest=new KillcamRequestPacket{Version=1,MatchId=victim.MatchId,VictimNetId=victim.OwnNetId};
victim.Send(ref replayRequest,true);
Pump(()=>victim.KillcamChunks.Count==2);
Check(victim.KillcamChunks.All(p=>p.Status==0&&p.TotalFrames==150&&p.VictimNetId==victim.OwnNetId&&p.KillerNetId==killer.OwnNetId),"Own filtered 150-frame replay arrives over real transport");
var playerId=clients[0].PlayerId;var token=clients[0].ResumeProof;await clients[0].DisposeAsync();clients.RemoveAt(0);
Pump(()=>host.Hub.ActivePeers==9,700);
Check(host.ReservedPeers==10,"Lost transport holds same server slot");
var resumed=new MatchNetworkClient("Udp");clients.Add(resumed);await resumed.ConnectAsync("127.0.0.1",udpPort,$"ws://127.0.0.1:{wsPort}/game","PHASE2_TEST",token);
Pump(()=>resumed.PlayerId!=null&&resumed.SnapshotCount>1,900);
Check(resumed.PlayerId==playerId&&resumed.ResumeProof!=token&&host.ReservedPeers==10,"Full-room resume retains PlayerId, rotates proof and keeps one slot");
Pump(()=>clients.All(c=>c.Result!=null),50000);
Check(clients.All(c=>c.Result!.Value.Participants!.Length==10),"Real UDP/WS clients receive full ten-participant result");
var oldMatch=clients[0].MatchId;foreach(var c in clients)c.Replay();
Pump(()=>clients.All(c=>c.MatchId!=oldMatch&&c.Phase==MatchPhase.InGame),7000);
Check(host.ReservedPeers==10,"Replay creates next match without new sessions or lost slots");
var report=new{mode,passed,elapsedSeconds=clock.Elapsed.TotalSeconds,clients=clients.Select(c=>new{c.Transport,c.SnapshotCount,c.InvalidFrames,c.DuplicateSnapshots,c.SentInputs}).ToArray()};
if(args.Length>1) { var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})); }
Console.WriteLine($"ALL {passed.Count} REAL GAMEPLAY CHECKS PASS ({mode})");
} finally {foreach(var c in clients)await c.DisposeAsync();}
