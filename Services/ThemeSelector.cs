using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DuolingoWidget.Models;

namespace DuolingoWidget.Services
{
    public static class ThemeSelector
    {
        public const string NoStreakPeriod = "none";
        public const string MorningPeriod = "morning";
        public const string AfternoonPeriod = "afternoon";
        public const string ActivePeriod = "active";
        public const string ManualPresetSettingKeyPrefix = "ManualPreset.";

        public static TileTheme Select(
            DuolingoProfile profile,
            DateTimeOffset currentTime,
            string? selectedThemeId = null)
        {
            return ThemeCatalog.GetById(SelectThemeId(profile, currentTime, selectedThemeId));
        }

        public static string SelectThemeId(
            DuolingoProfile profile,
            DateTimeOffset currentTime,
            string? selectedThemeId = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Streak <= 0)
            {
                return "1_1";
            }

            string period = GetCurrentPeriod(profile, currentTime);
            return SelectThemeIdForPeriod(profile, currentTime, period, selectedThemeId);
        }

        public static string GetCurrentPeriod(DuolingoProfile profile, DateTimeOffset currentTime)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Streak <= 0)
            {
                return NoStreakPeriod;
            }

            TimeZoneInfo timeZone = ResolveTimeZone(profile);
            DateTimeOffset userTime = TimeZoneInfo.ConvertTime(currentTime, timeZone);
            if (profile.StreakEndDate == userTime.Date)
            {
                return ActivePeriod;
            }

            return userTime.Hour < 12
                ? MorningPeriod
                : AfternoonPeriod;
        }

        public static string GetDailySmallTileVariant(
            DuolingoProfile profile,
            DateTimeOffset currentTime)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            TimeZoneInfo timeZone = ResolveTimeZone(profile);
            DateTime userDate = TimeZoneInfo.ConvertTime(currentTime, timeZone).Date;
            long dayNumber = userDate.Ticks / TimeSpan.TicksPerDay;
            return dayNumber % 2 == 0 ? "SmallFire" : "SmallDigital";
        }

        public static TileTheme SelectForPeriod(
            DuolingoProfile profile,
            DateTimeOffset currentTime,
            string period,
            string? selectedThemeId = null)
        {
            return ThemeCatalog.GetById(
                SelectThemeIdForPeriod(profile, currentTime, period, selectedThemeId));
        }

        private static string SelectThemeIdForPeriod(
            DuolingoProfile profile,
            DateTimeOffset currentTime,
            string period,
            string? selectedThemeId)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Streak <= 0)
            {
                return "1_1";
            }

            IReadOnlyList<string> availableThemeIds = GetPresetThemeIdsForPeriod(period);
            if (!string.IsNullOrEmpty(selectedThemeId))
            {
                foreach (string themeId in availableThemeIds)
                {
                    if (themeId == selectedThemeId)
                    {
                        return themeId;
                    }
                }
            }

            TimeZoneInfo timeZone = ResolveTimeZone(profile);
            DateTimeOffset userTime = TimeZoneInfo.ConvertTime(currentTime, timeZone);
            GetThemeRange(period, out int row, out int firstColumn, out int lastColumn);
            return SelectDailyTheme(
                profile.Username,
                userTime.Date,
                period,
                row,
                firstColumn,
                lastColumn);
        }

        public static IReadOnlyList<TileTheme> GetPresetsForPeriod(string period)
        {
            var themes = new List<TileTheme>();
            foreach (string themeId in GetPresetThemeIdsForPeriod(period))
            {
                themes.Add(ThemeCatalog.GetById(themeId));
            }

            return themes;
        }

        private static IReadOnlyList<string> GetPresetThemeIdsForPeriod(string period)
        {
            GetThemeRange(period, out int row, out int firstColumn, out int lastColumn);
            var themeIds = new List<string>(lastColumn - firstColumn + 1);
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                themeIds.Add(row + "_" + column);
            }

            return themeIds;
        }

        private static string SelectDailyTheme(
            string username,
            DateTime date,
            string period,
            int row,
            int firstColumn,
            int lastColumn)
        {
            int columnCount = lastColumn - firstColumn + 1;
            byte[] seed = SHA256.HashData(Encoding.UTF8.GetBytes(
                username + "|" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + period));
            int selectedColumn = firstColumn + (int)(BitConverter.ToUInt32(seed, 0) % (uint)columnCount);
            return row + "_" + selectedColumn;
        }

        private static void GetThemeRange(
            string period,
            out int row,
            out int firstColumn,
            out int lastColumn)
        {
            switch (period)
            {
                case MorningPeriod:
                    row = 1;
                    firstColumn = 2;
                    lastColumn = 5;
                    return;
                case AfternoonPeriod:
                    row = 2;
                    firstColumn = 1;
                    lastColumn = 5;
                    return;
                case ActivePeriod:
                    row = 3;
                    firstColumn = 1;
                    lastColumn = 4;
                    return;
                default:
                    throw new ArgumentException("Неизвестный период пресета.", nameof(period));
            }
        }

        private static TimeZoneInfo ResolveTimeZone(DuolingoProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.TimeZoneId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            if (TryParseOffset(profile.TimeZoneOffset, out TimeSpan offset))
            {
                return TimeZoneInfo.CreateCustomTimeZone("DuolingoProfileTimeZone", offset, "Duolingo", "Duolingo");
            }

            return TimeZoneInfo.Local;
        }

        private static bool TryParseOffset(string? value, out TimeSpan offset)
        {
            offset = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (normalized.Length == 5 && normalized[0] != '+' && normalized[0] != '-')
            {
                normalized = "+" + normalized;
            }

            if (normalized.Length != 5 ||
                !int.TryParse(normalized.Substring(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int hours) ||
                !int.TryParse(normalized.Substring(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) ||
                hours > 14 || minutes > 59)
            {
                return false;
            }

            offset = new TimeSpan(hours, minutes, 0);
            if (normalized[0] == '-')
            {
                offset = -offset;
            }

            return true;
        }
    }
}
