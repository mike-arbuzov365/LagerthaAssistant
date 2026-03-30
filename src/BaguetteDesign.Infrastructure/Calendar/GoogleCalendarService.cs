namespace BaguetteDesign.Infrastructure.Calendar;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Models;
using BaguetteDesign.Infrastructure.Options;
using Microsoft.Extensions.Logging;

/// <summary>
/// Google Calendar free/busy + event creation via Service Account.
/// When not configured, returns empty slots so the bot gracefully falls back.
/// </summary>
public sealed class GoogleCalendarService : ICalendarService
{
    private const string FreeBusyUrl = "https://www.googleapis.com/calendar/v3/freeBusy";
    private const string EventsBaseUrl = "https://www.googleapis.com/calendar/v3/calendars";

    private static readonly TimeSpan WorkdayStart = TimeSpan.FromHours(9);
    private static readonly TimeSpan WorkdayEnd = TimeSpan.FromHours(18);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly GoogleCalendarOptions _options;
    private readonly IGoogleTokenProvider _tokenProvider;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(
        HttpClient http,
        GoogleCalendarOptions options,
        IGoogleTokenProvider tokenProvider,
        ILogger<GoogleCalendarService> logger)
    {
        _http = http;
        _options = options;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CalendarSlot>> GetAvailableSlotsAsync(
        int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("GoogleCalendarService not configured — returning empty slots");
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var timeMin = now.Date.AddDays(1); // start from tomorrow
        var timeMax = now.Date.AddDays(daysAhead + 1);

        var busySlots = await GetBusySlotsAsync(timeMin, timeMax, cancellationToken);
        return BuildAvailableSlots(timeMin, timeMax, busySlots, _options.SlotDurationMinutes);
    }

    public async Task<string?> BookSlotAsync(
        CalendarSlot slot,
        string clientUserId,
        string summary,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("GoogleCalendarService not configured — skipping event creation");
            return null;
        }

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        var eventBody = new
        {
            summary,
            description = $"Client user ID: {clientUserId}",
            start = new { dateTime = slot.StartUtc.ToString("o"), timeZone = "UTC" },
            end = new { dateTime = slot.EndUtc.ToString("o"), timeZone = "UTC" },
            conferenceData = new
            {
                createRequest = new
                {
                    requestId = Guid.NewGuid().ToString("N"),
                    conferenceSolutionKey = new { type = "hangoutsMeet" }
                }
            }
        };

        var url = $"{EventsBaseUrl}/{Uri.EscapeDataString(_options.CalendarId)}/events?conferenceDataVersion=1";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(eventBody, JsonOpts), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Google Calendar event creation failed: {Status} — {Error}", (int)response.StatusCode, err);
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = doc.RootElement;

        // Extract Meet link
        if (root.TryGetProperty("conferenceData", out var conf)
            && conf.TryGetProperty("entryPoints", out var eps))
        {
            foreach (var ep in eps.EnumerateArray())
            {
                if (ep.TryGetProperty("entryPointType", out var t) && t.GetString() == "video"
                    && ep.TryGetProperty("uri", out var uri))
                    return uri.GetString();
            }
        }

        return null;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>> GetBusySlotsAsync(
        DateTimeOffset timeMin,
        DateTimeOffset timeMax,
        CancellationToken ct)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);

        var body = JsonSerializer.Serialize(new
        {
            timeMin = timeMin.ToString("o"),
            timeMax = timeMax.ToString("o"),
            items = new[] { new { id = _options.CalendarId } }
        }, JsonOpts);

        using var request = new HttpRequestMessage(HttpMethod.Post, FreeBusyUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google Calendar free/busy query failed: {Status}", (int)response.StatusCode);
            return [];
        }

        var busy = new List<(DateTimeOffset, DateTimeOffset)>();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        if (doc.RootElement.TryGetProperty("calendars", out var cals)
            && cals.TryGetProperty(_options.CalendarId, out var cal)
            && cal.TryGetProperty("busy", out var busyArr))
        {
            foreach (var slot in busyArr.EnumerateArray())
            {
                var start = DateTimeOffset.Parse(slot.GetProperty("start").GetString()!);
                var end = DateTimeOffset.Parse(slot.GetProperty("end").GetString()!);
                busy.Add((start, end));
            }
        }

        return busy;
    }

    private static IReadOnlyList<CalendarSlot> BuildAvailableSlots(
        DateTimeOffset timeMin,
        DateTimeOffset timeMax,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> busy,
        int slotMinutes)
    {
        var available = new List<CalendarSlot>();
        var duration = TimeSpan.FromMinutes(slotMinutes);
        var current = timeMin.Date;

        while (current < timeMax.Date)
        {
            var dayStart = new DateTimeOffset(current + WorkdayStart, TimeSpan.Zero);
            var dayEnd = new DateTimeOffset(current + WorkdayEnd, TimeSpan.Zero);
            var slotStart = dayStart;

            while (slotStart.Add(duration) <= dayEnd)
            {
                var slotEnd = slotStart.Add(duration);
                var overlaps = busy.Any(b => b.Start < slotEnd && b.End > slotStart);

                if (!overlaps && slotStart > DateTimeOffset.UtcNow)
                    available.Add(new CalendarSlot(slotStart, slotEnd));

                slotStart = slotEnd;
            }

            current = current.AddDays(1);
        }

        return available;
    }
}
