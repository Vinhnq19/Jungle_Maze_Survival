extern alias Smoke;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Networking;
using JungleBackend.Protocol;
using LiteNetLib.Utils;
using ClientOptions = Smoke::NetSmokeClient.Configuration.ClientOptions;
using SmokeSession = Smoke::NetSmokeClient.Networking.SmokeSession;

var passed = 0;
void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); Console.WriteLine($"PASS {++passed}: {name}"); }
int Port(SocketType type) { using var s = new Socket(AddressFamily.InterNetwork, type, type == SocketType.Dgram ? ProtocolType.Udp : ProtocolType.Tcp); s.Bind(new IPEndPoint(IPAddress.Loopback,0));return ((IPEndPoint)s.LocalEndPoint!).Port; }
var keys = new[] { "WS_ENABLED", "WS_PORT", "WS_BIND_ADDRESS" };
var saved = keys.ToDictionary(k=>k, Environment.GetEnvironmentVariable);
try
{
    var options = new ServerOptions(IPAddress.Loopback, Port(SocketType.Dgram),10,30,1,"RoomCode","DUAL_TEST");
    var hub = new SessionHub(options, new ServerMetrics());
    var attempt = Guid.NewGuid();
    var join = new JoinRequest(2,JoinAuthMode.RoomCode,"DUAL_TEST","Test",attempt);
    var udp = new FakePeer("Udp"); var ws = new FakePeer("Wss");
    Check(hub.Join(join,udp),"v2 reserves UDP slot");hub.Connected(udp);
    var identity=udp.Messages.Last();
    Check(hub.Join(join,ws),"same initial attempt transfers UDP -> WSS");hub.Connected(ws);
    Check(hub.ReservedPeers==1 && identity.SequenceEqual(ws.Messages.Last()),"transfer preserves one slot and server PlayerId");
    hub.Disconnect(udp);hub.Receive(udp,new byte[]{1,1,0,0,0},false);
    Check(hub.ReservedPeers==1,"late UDP disconnect/input cannot release WSS slot");
    Check(hub.Validate(join,"Udp")=="AttemptUsed","late UDP cannot recreate the migrated attempt");
    hub.Receive(ws,new byte[]{1,1,0,0,0},true);
    Check(hub.Validate(join,"Wss")=="AttemptUsed","a committed application cannot be taken over");
    hub.Disconnect(ws);Check(hub.ReservedPeers==0,"disconnect releases shared slot");
    var udpExpired=new FakePeer("Udp");var expired=join with{AttemptId=Guid.NewGuid()};hub.Join(expired,udpExpired);hub.Connected(udpExpired);hub.Disconnect(udpExpired);
    Check(hub.Join(expired,new FakePeer("Wss")) && hub.ReservedPeers==1,"retired unconfirmed UDP can complete once through WSS");hub.CloseAll();
    var slow = new FakePeer("Wss"){Writable=false};hub.Join(join with{AttemptId=Guid.NewGuid()},slow);hub.Connected(slow);
    Check(hub.ReservedPeers==0,"slow outgoing consumer releases its slot");
    var totalHub=new SessionHub(options with{Rooms=new RoomOptions(MaxTotalPlayers:12)},new ServerMetrics());
    var second=(JungleBackend.Rooms.RoomView)totalHub.Rooms.Create(new(){OwnerId="owner",Name="Second",MaxPlayers=10}).Body!;
    var totalPeers=new List<FakePeer>();
    for(var i=0;i<12;i++)
    {
        var p=new FakePeer(i%2==0?"Udp":"Wss");totalPeers.Add(p);
        Check(totalHub.Join(join with{RoomCode=i<10?"DUAL_TEST":second.RoomCode!,AttemptId=Guid.NewGuid()},p),"mixed total reserve "+i);
    }
    Check(totalHub.Validate(join with{RoomCode=second.RoomCode!,AttemptId=Guid.NewGuid()},"Wss")=="ServerFull","mixed transports share global twelve-session cap");
    totalHub.Disconnect(totalPeers[0]);
    Check(totalHub.Join(join with{RoomCode=second.RoomCode!,AttemptId=Guid.NewGuid()},new FakePeer("Wss"))&&totalHub.ReservedPeers==12,"returned UDP slot can be reused by WSS in another room");totalHub.CloseAll();

    var configPath=Path.GetTempFileName();
    try
    {
        foreach(var (transport,url,version,valid) in new[]{("Wss","ws://127.0.0.1:7781/game",2,true),
            ("Auto","wss://junglemaze.duckdns.org/game",2,true),("Wss","ws://192.168.1.20/game",2,false),
            ("Wss","wss://user:password@example.com/game",2,false),("Wss","wss://example.com/game?secret=x",2,false),
            ("Auto","wss://example.com/game",1,false),("Bad","wss://example.com/game",2,false)})
        {
            File.WriteAllText(configPath,JsonSerializer.Serialize(new{mode="Local",gameServerHost="127.0.0.1",gameServerPort=7777,
                authMode="RoomCode",protocolVersion=version,connectTimeoutMs=500,transport,webSocketUrl=url}));
            var acceptedConfig=true;try{ClientOptions.Load(configPath);}catch(ArgumentException){acceptedConfig=false;}
            Check(acceptedConfig==valid,"transport config validation: "+transport+"/v"+version+"/"+valid);
        }
        File.WriteAllText(configPath,JsonSerializer.Serialize(new{mode="Local",gameServerHost="127.0.0.1",gameServerPort=7777,
            authMode="RoomCode",protocolVersion=2,connectTimeoutMs=500,transport="Wss",webSocketUrl="ws://localhost/game",webSocketConnectIp="192.168.1.2"}));
        var rejectedPin=false;try{ClientOptions.Load(configPath);}catch(ArgumentException){rejectedPin=true;}
        Check(rejectedPin,"a loopback ws URI cannot be pinned to a remote plaintext destination");
    }
    finally{File.Delete(configPath);}

    using var network=new NetworkHost(options,new ServerMetrics());network.Start();
    var wsPort=Port(SocketType.Stream);
    Environment.SetEnvironmentVariable("WS_ENABLED","true");Environment.SetEnvironmentVariable("WS_PORT",wsPort.ToString());Environment.SetEnvironmentVariable("WS_BIND_ADDRESS","127.0.0.1");
    await using var server=await WebSocketHost.StartFromEnvironment(network.Hub,50);
    void Pump(Func<bool> done,int timeout=5000)
    {
        var started=Stopwatch.StartNew();
        while(!done()) { server!.PollEvents();network.PollEvents();if(started.ElapsedMilliseconds>timeout)throw new TimeoutException("Dual test deadline");Thread.Sleep(1); }
    }
    ClientOptions Config(string transport,int? port=null,string? pin=null)=>new(){Mode="Local",GameServerHost="127.0.0.1",GameServerPort=port??options.Port,
        AuthMode="RoomCode",ProtocolVersion=2,ConnectTimeoutMs=500,Transport=transport,WebSocketUrl=$"ws://localhost:{wsPort}/game",WebSocketConnectIp=pin};
    using(var client=new SmokeSession(1,Config("Wss"),"DUAL_TEST","Wss",quiet:true))
    {
        client.Start();Pump(()=>{client.Update();return client.Pongs>2||client.Failed;});
        Check(client.Joined&&!client.Failed,"actual WebSocket binary handshake and ping/pong");
    }
    Pump(()=>network.ReservedPeers==0);
    using(var client=new SmokeSession(1,Config("Wss",pin:"127.0.0.1"),"DUAL_TEST","Pinned",quiet:true))
    {
        client.Start();Pump(()=>{client.Update();return client.Pongs>0||client.Failed;});
        Check(client.Joined&&!client.Failed,"explicit TCP pin works through the custom HTTP handler without hosts edits");
    }
    Pump(()=>network.ReservedPeers==0);
    using(var client=new SmokeSession(1,Config("Udp"),"DUAL_TEST","Udp",quiet:true))
    {
        client.Start();Pump(()=>{client.Update();return client.Pongs>0||client.Failed;});Check(client.Joined&&!client.Failed,"actual UDP v2 joins/pongs");
    }
    Pump(()=>network.ReservedPeers==0);
    using(var client=new SmokeSession(1,Config("Auto",Port(SocketType.Dgram)),"DUAL_TEST","Auto",quiet:true))
    {
        client.Start();Pump(()=>{client.Update();return client.Pongs>0||client.Failed;},10000);
        var report=JsonSerializer.SerializeToElement(client.Report(1));
        Check(client.Joined&&!client.Failed&&report.GetProperty("transport").GetString()=="Wss","Auto falls back from blocked UDP to WSS");
        Check(report.GetProperty("fallbackReason").GetString() is not null,"fallback reason recorded without secret nonce");
    }
    Pump(()=>network.ReservedPeers==0);
    using(var client=new SmokeSession(1,Config("Auto"),"WRONG","Wrong",quiet:true))
    {
        client.Start();Pump(()=>{client.Update();return client.Finished;});
        Check(client.FailureReason=="InvalidRoomCode"&&JsonSerializer.SerializeToElement(client.Report(1)).GetProperty("transport").GetString()=="Udp","business rejection does not trigger fallback");
    }
    var clients=new List<SmokeSession>();
    try
    {
        for(var i=0;i<11;i++){var c=new SmokeSession(i,Config(i%2==0?"Udp":"Wss"),"DUAL_TEST","Mixed"+i,quiet:true);clients.Add(c);c.Start();}
        Pump(()=>{foreach(var c in clients)c.Update();return clients.All(c=>c.Joined||c.Failed);});
        Check(clients.Count(c=>c.Joined)==10&&clients.Count(c=>c.FailureReason=="RoomFull")==1,"mixed UDP/WSS total ten, eleventh RoomFull");
        Check(network.ReservedPeers==10,"both transports use one room registry");
    }
    finally{foreach(var c in clients)c.Dispose();}
    Pump(()=>network.ReservedPeers==0);

    async Task<byte[]> Raw(byte[] data, WebSocketMessageType type, bool fragment=false)
    {
        using var socket=new ClientWebSocket();await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{wsPort}/game"),CancellationToken.None);
        if(fragment){await socket.SendAsync(data.AsMemory(0,data.Length/2),type,false,CancellationToken.None);await socket.SendAsync(data.AsMemory(data.Length/2),type,true,CancellationToken.None);}
        else await socket.SendAsync(data.AsMemory(),type,true,CancellationToken.None);
        var buffer=new byte[4096];var receive=socket.ReceiveAsync(buffer.AsMemory(),CancellationToken.None).AsTask();
        Pump(()=>receive.IsCompleted);var read=await receive;
        return buffer[..read.Count];
    }
    var accepted=await Raw(NetworkProtocol.WriteConnect(JoinAuthMode.RoomCode,"DUAL_TEST","Fragmented",2,Guid.NewGuid()).CopyData(),WebSocketMessageType.Binary,true);
    Check(accepted[0]==NetworkProtocol.JoinAccepted,"fragmented binary handshake assembled before parse");Pump(()=>network.ReservedPeers==0);
    foreach(var (payload,type,reason) in new[]{(new byte[]{1},WebSocketMessageType.Text,"BinaryRequired"),(new byte[4097],WebSocketMessageType.Binary,"PayloadTooLarge"),(new byte[]{1},WebSocketMessageType.Binary,"MalformedRequest")})
    {
        var rejected=await Raw(payload,type);
        Check(NetworkProtocol.TryReadStringMessage(new NetDataReader(rejected),NetworkProtocol.JoinRejected,256,64,out var actual)&&actual==reason,"WebSocket rejects "+reason);
    }
    // A process-only DNS pin MUST NOT turn off the normal TLS certificate checks.
    using(var key=System.Security.Cryptography.RSA.Create(2048))
    {
        var request=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost",key,
            System.Security.Cryptography.HashAlgorithmName.SHA256,System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
        var tlsListener=new TcpListener(IPAddress.Loopback,0);tlsListener.Start();
        var tlsPort=((IPEndPoint)tlsListener.LocalEndpoint).Port;
        var receivedHttp=false;
        var serving=Task.Run(async()=>
        {
            try
            {
                using var tcp=await tlsListener.AcceptTcpClientAsync();
                using var stream=new System.Net.Security.SslStream(tcp.GetStream());
                await stream.AuthenticateAsServerAsync(cert);
                using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(3));
                receivedHttp=await stream.ReadAsync(new byte[4096],deadline.Token)>0;
            }
            catch(Exception error) when(error is IOException or OperationCanceledException or System.Security.Authentication.AuthenticationException){}
        });
        try
        {
            using var client=new SmokeSession(1,new ClientOptions{Mode="Local",GameServerHost="127.0.0.1",GameServerPort=options.Port,
                AuthMode="RoomCode",ProtocolVersion=2,ConnectTimeoutMs=500,Transport="Wss",WebSocketUrl=$"wss://localhost:{tlsPort}/game",WebSocketConnectIp="127.0.0.1"},"DUAL_TEST","BadTls",quiet:true);
            client.Start();Pump(()=>{client.Update();return client.Failed;});
            await serving.WaitAsync(TimeSpan.FromSeconds(3));
            Check(client.FailureReason=="WssConnectFailed"&&!client.Joined&&!receivedHttp,"pinned WSS rejects untrusted TLS before sending an HTTP Upgrade");
        }
        finally{tlsListener.Stop();}
    }
    Console.WriteLine($"PASS {passed} dual transport checks.");return 0;
}
catch(Exception e){Console.Error.WriteLine(e);return 1;}
finally{foreach(var pair in saved)Environment.SetEnvironmentVariable(pair.Key,pair.Value);}

sealed class FakePeer(string transport):IGamePeer
{
    public string Transport=>transport;
    public bool IsOpen{get;private set;}=true;
    public bool Writable=true;
    public List<byte[]> Messages=new();
    public bool TrySend(byte[] packet,bool reliable){if(!Writable)return false;Messages.Add(packet);return true;}
    public void Close(string reason){IsOpen=false;}
}
