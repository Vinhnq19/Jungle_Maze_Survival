using System.Text.Json;
namespace GameServer.Simulation;

/// <summary>Versioned external runtime settings. Test acceleration is explicit and forbidden in public hosting.</summary>
public sealed class MatchProfile
{
    public string Version { get; init; } = "1.0.0";
    public string Name { get; init; } = "main";
    public string ConfigHash { get; init; } = "";
    public int MinPlayers { get; init; } = 6;
    public int MaxPlayers { get; init; } = 10;
    public double LobbyDurationSeconds { get; init; } = 15;
    public double LoadingTimeoutSeconds { get; init; } = 30;
    public double DisconnectGraceSeconds { get; init; } = 15;
    public double EndingDurationSeconds { get; init; } = 10;
    public double TargetMatchDurationSeconds { get; init; } = 420;
    public double ZoneTimeScale { get; init; } = 1;
    public float PlayerSpeed { get; init; } = 4;
    public float PlayerRadius { get; init; } = .4f;
    public byte PlayerHp { get; init; } = 100;
    public int InputStaleMs { get; init; } = 250;
    public bool AcceleratedZone { get; init; }
    public static MatchProfile Current
    {
        get
        {
            var name=Environment.GetEnvironmentVariable("MATCH_PROFILE")??"main";
            if(name is not ("main" or "test"))throw new ArgumentException("MATCH_PROFILE must be main or test.");
            if(name=="test"&&string.Equals(Environment.GetEnvironmentVariable("PUBLIC_HOSTING"),"true",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Test match profile is forbidden for public hosting.");
            return Load(name);
        }
    }
    public static MatchProfile CreateMainProfile()=>Load("main");
    public static MatchProfile CreateTestProfile()=>Load("test");
    private static MatchProfile Load(string name)
    {
        using var doc=JsonDocument.Parse(GameDataLoader.ReadGameDataFile("match_settings.json"));
        var r=doc.RootElement;var p=r.GetProperty("profiles").GetProperty(name);
        if(r.GetProperty("tickRate").GetInt32()!=30)throw new ArgumentException("Match tick rate must be 30.");
        var result=new MatchProfile
        {
            Version=r.GetProperty("version").GetString()??"",Name=name,ConfigHash=GameDataLoader.DataHash,
            MinPlayers=p.GetProperty("minPlayers").GetInt32(),MaxPlayers=p.GetProperty("maxPlayers").GetInt32(),
            LobbyDurationSeconds=p.GetProperty("lobbyDurationSeconds").GetDouble(),LoadingTimeoutSeconds=p.GetProperty("loadingTimeoutSeconds").GetDouble(),
            DisconnectGraceSeconds=p.GetProperty("disconnectGraceSeconds").GetDouble(),EndingDurationSeconds=p.GetProperty("endingDurationSeconds").GetDouble(),
            TargetMatchDurationSeconds=r.GetProperty("targetMatchDurationSeconds").GetDouble(),ZoneTimeScale=p.GetProperty("zoneTimeScale").GetDouble(),
            PlayerSpeed=r.GetProperty("playerSpeed").GetSingle(),PlayerRadius=r.GetProperty("playerRadius").GetSingle(),
            PlayerHp=checked((byte)r.GetProperty("playerHp").GetInt32()),InputStaleMs=r.GetProperty("inputStaleMs").GetInt32(),AcceleratedZone=name=="test"
        };
        result.Validate();return result;
    }
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Version)||Version.Length>32||MinPlayers<2||MaxPlayers>10||MinPlayers>MaxPlayers||PlayerHp==0||InputStaleMs<1||InputStaleMs>5000)
            throw new ArgumentException("Invalid version/player/input configuration.");
        foreach(var v in new[]{LobbyDurationSeconds,LoadingTimeoutSeconds,DisconnectGraceSeconds,EndingDurationSeconds,TargetMatchDurationSeconds,ZoneTimeScale,PlayerSpeed,PlayerRadius})
            if(!double.IsFinite(v)||v<=0)throw new ArgumentException("Match timing and physics values must be finite and positive.");
    }
}
