namespace GameServer.Simulation;
/// <summary>Read packaged canonical data, or an explicitly selected directory. Invalid data never silently falls back.</summary>
public static class GameDataLoader
{
    public static string DataHash
    {
        get
        {
            using var hash=System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            foreach(var file in new[]{"map_demo.json","match_settings.json","zone_schedule.json"})
            {
                hash.AppendData(System.Text.Encoding.UTF8.GetBytes(file));
                hash.AppendData(System.Text.Encoding.UTF8.GetBytes(ReadGameDataFile(file)));
            }
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
    }
    public static string ReadGameDataFile(string filename)
    {
        if(Path.GetFileName(filename)!=filename)throw new ArgumentException("Game data filename must be a leaf name.");
        var directory=Environment.GetEnvironmentVariable("GAME_DATA_PATH")??Path.Combine(AppContext.BaseDirectory,"GameData");
        return File.ReadAllText(Path.Combine(directory,filename));
    }
}
