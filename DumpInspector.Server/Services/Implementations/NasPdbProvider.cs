using DumpInspector.Server.Services.Interfaces;
using System.Net.Http.Headers;

namespace DumpInspector.Server.Services.Implementations
{
    public class NasPdbProvider : IPdbProvider
    {
        private readonly HttpClient _http;
        private readonly DumpInspector.Server.Models.NasSettings? _nas;

        public NasPdbProvider(HttpClient http, Microsoft.Extensions.Options.IOptions<DumpInspector.Server.Models.CrashDumpSettings> options)
        {
            _http = http;
            _nas = options.Value.Nas;
            if (_nas?.BaseUrl != null) _http.BaseAddress = new Uri(_nas.BaseUrl);
            if (!string.IsNullOrEmpty(_nas?.Username) && !string.IsNullOrEmpty(_nas?.Password))
            {
                var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_nas.Username}:{_nas.Password}"));
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            }
        }

        public async Task<byte[]?> GetPdbAsync(string pdbName)
        {
            if (_nas == null || string.IsNullOrEmpty(_nas.BaseUrl)) return null;
            var path = _nas.RemotePdbPath?.Trim('/') ?? "pdb";
            var url = $"/{path}/{pdbName}";
            try
            {
                var res = await _http.GetAsync(url);
                if (!res.IsSuccessStatusCode) return null;
                return await res.Content.ReadAsByteArrayAsync();
            }
            catch
            {
                return null;
            }
        }
    }
}
