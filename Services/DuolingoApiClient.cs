using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DuolingoWidget.Models;

namespace DuolingoWidget.Services
{
    public static class DuolingoApiClient
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        public static async Task<DuolingoProfile> GetProfileAsync(
            string username,
            bool isBackgroundTask = false)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Введите имя пользователя Duolingo.", nameof(username));
            }

            string fields = Uri.EscapeDataString(
                "users{username,streak,streakData{currentStreak{endDate}},timezone,timezoneOffset}");
            string requestUri = "https://www.duolingo.com/2017-06-30/users?username=" +
                Uri.EscapeDataString(username.Trim()) + "&fields=" + fields;
            var elapsed = Stopwatch.StartNew();
            if (isBackgroundTask)
            {
                await BackgroundTaskDiagnostics.WriteAsync("API request started host=www.duolingo.com");
            }

            try
            {
                using (var response = await Client.GetAsync(requestUri))
                {
                    if (isBackgroundTask)
                    {
                        await BackgroundTaskDiagnostics.WriteAsync(
                            "API response status=" + (int)response.StatusCode +
                            " elapsedMs=" + elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
                    }

                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var document = await JsonDocument.ParseAsync(stream))
                    {
                        if (!document.RootElement.TryGetProperty("users", out JsonElement users) ||
                            users.ValueKind != JsonValueKind.Array ||
                            users.GetArrayLength() == 0)
                        {
                            throw new InvalidOperationException("Пользователь не найден в ответе Duolingo API.");
                        }

                        JsonElement user = users[0];
                        if (user.ValueKind != JsonValueKind.Object)
                        {
                            throw new InvalidOperationException(
                                "Некорректный формат данных пользователя в ответе Duolingo API.");
                        }

                        var profile = new DuolingoProfile
                        {
                            Username = GetString(user, "username") ?? username.Trim(),
                            Streak = GetInt32(user, "streak"),
                            TimeZoneId = GetString(user, "timezone"),
                            TimeZoneOffset = GetString(user, "timezoneOffset")
                        };

                        if (user.TryGetProperty("streakData", out JsonElement streakData) &&
                            streakData.ValueKind == JsonValueKind.Object &&
                            streakData.TryGetProperty("currentStreak", out JsonElement currentStreak))
                        {
                            if (currentStreak.ValueKind == JsonValueKind.Object)
                            {
                                string? endDate = GetString(currentStreak, "endDate");
                                if (DateTime.TryParseExact(
                                    endDate,
                                    "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None,
                                    out DateTime parsedEndDate))
                                {
                                    profile.StreakEndDate = parsedEndDate.Date;
                                }
                            }
                        }

                        if (isBackgroundTask)
                        {
                            await BackgroundTaskDiagnostics.WriteAsync(
                                "API parsed successfully streak=" +
                                profile.Streak.ToString(CultureInfo.InvariantCulture) +
                                " elapsedMs=" +
                                elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
                        }

                        return profile;
                    }
                }
            }
            catch (Exception ex)
            {
                if (isBackgroundTask)
                {
                    await BackgroundTaskDiagnostics.WriteExceptionAsync("API", ex);
                }

                throw;
            }
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int GetInt32(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement value) &&
                   value.TryGetInt32(out int result)
                ? result
                : 0;
        }
    }
}
