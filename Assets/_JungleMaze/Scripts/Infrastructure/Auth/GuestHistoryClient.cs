using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using JungleMaze.App.Ports;
using System.Collections.Generic;

namespace JungleMaze.Infrastructure.Auth
{
    /// <summary>Optional HTTPS-only persistent identity; every failure leaves RoomCode gameplay available.</summary>
    public sealed class GuestHistoryClient : IPlayerHistory
    {
        private readonly GuestCredentialStore _store;
        private readonly HttpClient _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        private GuestCredential _identity;
        private Uri _endpoint;
        private int _identityGeneration;
        public string Status { get; private set; } = "Lịch sử chưa kết nối";
        public string HistoryJson { get; private set; } = "";
        public bool IsReady => _identity != null;
        public IReadOnlyList<PlayerMatchHistory> Rows { get; private set; } = Array.Empty<PlayerMatchHistory>();
        private sealed class HistoryPage { public PlayerMatchHistory[] Items; }
        public GuestHistoryClient(GuestCredentialStore store) => _store = store;

        public static bool IsSecureEndpoint(Uri endpoint) => endpoint != null &&
            string.IsNullOrEmpty(endpoint.UserInfo) && string.IsNullOrEmpty(endpoint.Query) && string.IsNullOrEmpty(endpoint.Fragment) &&
            (endpoint.Scheme == Uri.UriSchemeHttps || endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback);

        public async Task<bool> EnsureGuestAsync(string baseUrl, CancellationToken cancellation = default)
        {
            var generation = ++_identityGeneration;
            // Invalidate before the first await: a failed endpoint switch must not reuse Local proof on AWS.
            var previous = _identity; var previousEndpoint = _endpoint;
            _identity = null; _endpoint = null; Rows = Array.Empty<PlayerMatchHistory>(); HistoryJson = "";
            try
            {
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint) || !IsSecureEndpoint(endpoint))
                { Status = "Lịch sử cần HTTPS; Local cho phép HTTP loopback"; return false; }
                _endpoint = endpoint; // Account JWT can still obtain a ticket if optional Guest creation fails.
                var identity = previousEndpoint != null && previousEndpoint.GetLeftPart(UriPartial.Authority) == endpoint.GetLeftPart(UriPartial.Authority)
                    ? previous : _store.Load(endpoint);
                if (identity == null)
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint.GetLeftPart(UriPartial.Authority) + "/api/guests")))
                    {
                        request.Content = JsonBody(new { });
                        using (var response = await _http.SendAsync(request, cancellation))
                        {
                            response.EnsureSuccessStatusCode();
                            identity = JsonConvert.DeserializeObject<GuestCredential>(await response.Content.ReadAsStringAsync());
                            if (identity == null || string.IsNullOrEmpty(identity.ProfileId) || string.IsNullOrEmpty(identity.Proof))
                                throw new InvalidOperationException("Invalid Guest response");
                            if (generation != _identityGeneration) return false;
                            _store.Save(endpoint, identity);
                            identity = _store.Load(endpoint); // Another process may have atomically created the shared profile first.
                        }
                    }
                }
                if (generation != _identityGeneration) return false;
                _identity = identity; _endpoint = endpoint;
                Status = "Hồ sơ Guest đã sẵn sàng";
                return true;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                if (generation == _identityGeneration) Status = "Lịch sử không khả dụng; vẫn chơi bằng RoomCode";
                return false;
            }
        }

        public async Task<string> TicketAsync(Guid attemptId, string roomCode, string accountToken = null,
            CancellationToken cancellation = default)
        {
            if (_endpoint == null || _identity == null && string.IsNullOrEmpty(accountToken)) return null;
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, Url("api/game-tickets")))
                {
                    SetProof(request, accountToken);
                    request.Content = JsonBody(new { attemptId, roomCode });
                    using (var response = await _http.SendAsync(request, cancellation))
                    {
                        response.EnsureSuccessStatusCode();
                        var data = Newtonsoft.Json.Linq.JObject.Parse(await response.Content.ReadAsStringAsync());
                        return data.Value<string>("ticket");
                    }
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            { Status = "Chơi ẩn danh: chưa gắn được hồ sơ lịch sử"; return null; }
        }

        public async Task RefreshHistoryAsync(string accountToken = null, CancellationToken cancellation = default)
        {
            if (_endpoint == null || _identity == null && string.IsNullOrEmpty(accountToken)) return;
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, Url("api/me/matches?page=1&pageSize=20")))
                {
                    SetProof(request, accountToken);
                    using (var response = await _http.SendAsync(request, cancellation))
                    {
                        response.EnsureSuccessStatusCode();
                        HistoryJson = await response.Content.ReadAsStringAsync();
                        Rows = JsonConvert.DeserializeObject<HistoryPage>(HistoryJson)?.Items ?? Array.Empty<PlayerMatchHistory>();
                        Status = "Lịch sử đã cập nhật";
                    }
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            { Rows = Array.Empty<PlayerMatchHistory>(); HistoryJson = ""; Status = "Lịch sử online không khả dụng"; }
        }

        private void SetProof(HttpRequestMessage request, string accountToken)
        {
            if (!string.IsNullOrEmpty(accountToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            else request.Headers.Add("X-Guest-Credential", _identity.Proof);
        }
        private Uri Url(string path) => new Uri(_endpoint.GetLeftPart(UriPartial.Authority) + "/" + path);
        private static StringContent JsonBody(object value) => new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json");
        public void Dispose() => _http.Dispose();
    }
}
