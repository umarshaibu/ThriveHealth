using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Hospitals a patient can visit: their own hospital group, and others nearby from OpenStreetMap.</summary>
[Route("api/v1/hospitals")]
public class HospitalsApiController : ApiControllerBase
{
    // Public OpenStreetMap query servers are often busy, so each is tried in turn.
    private static readonly string[] OverpassUrls =
    {
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    };
    private const int RadiusMetres = 15000;
    private const int MaxResults = 40;
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HospitalsApiController> _log;

    public HospitalsApiController(ApplicationDbContext db, IHttpClientFactory http, IMemoryCache cache, ILogger<HospitalsApiController> log)
    {
        _db = db; _http = http; _cache = cache; _log = log;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiHospital>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var yours = FacilityId;
        var rows = await _db.Facilities.AsNoTracking()
            .Where(f => f.IsActive && f.Latitude != null && f.Longitude != null)
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.Address, f.Lga, f.State, f.Phone, f.Latitude, f.Longitude })
            .ToListAsync(ct);

        return Ok(rows.Select(f => new ApiHospital(
            $"f{f.Id}", f.Name,
            string.Join(", ", new[] { f.Address, f.Lga, f.State }.Where(p => !string.IsNullOrWhiteSpace(p))),
            f.Phone, f.Latitude!.Value, f.Longitude!.Value, f.Id == yours, InNetwork: true)).ToList());
    }

    /// <summary>
    /// Other hospitals near a point. Coordinates are rounded to about 1 km before leaving this server,
    /// and only the rounded point is sent, so the map service never learns who is asking or exactly where.
    /// </summary>
    [HttpGet("nearby")]
    [EnableRateLimiting("hospitals-nearby")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiHospital>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Nearby([FromQuery] double lat, [FromQuery] double lng, CancellationToken ct)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
            return BadRequest(new ApiError("Invalid location.", "bad_location"));

        var roundedLat = Math.Round(lat, 2);
        var roundedLng = Math.Round(lng, 2);
        var key = $"hospitals-nearby:{roundedLat.ToString(CultureInfo.InvariantCulture)}:{roundedLng.ToString(CultureInfo.InvariantCulture)}";
        if (_cache.TryGetValue(key, out List<ApiHospital>? cached) && cached is not null) return Ok(cached);

        var results = await QueryOverpassAsync(roundedLat, roundedLng, ct);
        // Only successful lookups are kept, so a temporary outage doesn't stick for a day.
        if (results is null) return Ok(Array.Empty<ApiHospital>());
        _cache.Set(key, results, CacheFor);
        return Ok(results);
    }

    private async Task<List<ApiHospital>?> QueryOverpassAsync(double lat, double lng, CancellationToken ct)
    {
        var query = string.Create(CultureInfo.InvariantCulture,
            $"[out:json][timeout:15];nwr[\"amenity\"=\"hospital\"](around:{RadiusMetres},{lat},{lng});out center tags {MaxResults};");
        var client = _http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);

        foreach (var url in OverpassUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = query })
                };
                request.Headers.TryAddWithoutValidation("User-Agent", "ThriveHealth/1.0");
                using var response = await client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogInformation("Nearby hospital server {Url} returned {Status}", url, (int)response.StatusCode);
                    continue;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                return Parse(doc);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
            {
                _log.LogInformation(ex, "Nearby hospital server {Url} failed", url);
            }
        }

        _log.LogWarning("All nearby hospital servers failed");
        return null;
    }

    private static List<ApiHospital> Parse(JsonDocument doc)
    {
        var list = new List<ApiHospital>();
        foreach (var e in doc.RootElement.GetProperty("elements").EnumerateArray())
        {
            if (!e.TryGetProperty("tags", out var tags) || !tags.TryGetProperty("name", out var name)) continue;
            var point = e.TryGetProperty("center", out var center) ? center : e;
            if (!point.TryGetProperty("lat", out var pLat) || !point.TryGetProperty("lon", out var pLon)) continue;

            string? Tag(string k) => tags.TryGetProperty(k, out var v) ? v.GetString() : null;
            var address = string.Join(", ", new[] { $"{Tag("addr:housenumber")} {Tag("addr:street")}".Trim(), Tag("addr:city") }
                .Where(p => !string.IsNullOrWhiteSpace(p)));
            list.Add(new ApiHospital($"o{e.GetProperty("id").GetInt64()}", name.GetString() ?? "Hospital",
                address.Length > 0 ? address : null, Tag("phone") ?? Tag("contact:phone"),
                pLat.GetDouble(), pLon.GetDouble(), IsYours: false, InNetwork: false));
        }
        return list;
    }
}
