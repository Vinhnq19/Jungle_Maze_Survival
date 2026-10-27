using System;
using JungleMaze.App.Match;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.App.Telemetry;
using JungleMaze.Core.Ticking;
using JungleMaze.Infrastructure.Telemetry;
using UnityEngine;

namespace JungleMaze.Composition
{
    /// <summary>
    /// TEL-01 client composition: perf_client recorder + background JSONL sink, only when the command line carries
    /// <c>-telemetry</c> (optional <c>-telemetryDir &lt;path&gt;</c>, default <c>persistentDataPath/Telemetry</c>).
    /// Without the flag nothing is created: no sink, no thread, no file, no tickable (AC27). ClientConfig is untouched.
    /// Kept in its own file so <c>Bootstrap.cs</c> only carries the one call.
    /// </summary>
    public sealed partial class Bootstrap
    {
        private void ComposeTelemetry(ITickScheduler ticks, MatchSession match, INetworkClient network, RemotePlayerInterpolator remote)
        {
            var options = ClientTelemetryOptions.FromArgs(Environment.GetCommandLineArgs(), Application.persistentDataPath);
            if (!options.Enabled) return;
            var context = ClientTelemetryIdentity.Create(Application.streamingAssetsPath, Application.version, _log);
            var sink = new JsonlTelemetryFileSink(options.Directory, context, _log);
            _registry.Register<ITelemetrySink>(sink); // disposed with the registry: drains the queue and writes _trailer
            ticks.Add(new PerfClientRecorder(match, network, remote, sink));
            _log.Info("[Telemetry] perf_client on, dir=" + options.Directory + " wid=" + context.Wid);
        }
    }
}
