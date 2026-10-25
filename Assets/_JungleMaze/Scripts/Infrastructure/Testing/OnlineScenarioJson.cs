using JungleMaze.App.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JungleMaze.Infrastructure.Testing
{
    /// <summary>Không bỏ qua flag test gõ sai hoặc feature chưa có implementation.</summary>
    public static class OnlineScenarioJson
    {
        public static bool TryParse(string json, out OnlineScenario scenario, out string error)
        {
            scenario = null;
            try
            {
                var root = JObject.Parse(json);
                root.Remove("$schema"); root.Remove("description");
                scenario = root.ToObject<OnlineScenario>(JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }));
                error = scenario == null ? "Scenario required" : scenario.Validate();
                return error == null;
            }
            catch (JsonException) { error = "Invalid scenario fields/types; unknown fields are not ignored"; return false; }
        }
    }
}
