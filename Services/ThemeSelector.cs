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

        public static TileTheme Select(
            DuolingoProfile profile,
            DateTimeOffset currentTime,
            string? selectedThemeId = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Streak <= 0)
            {
                return ThemeCatalog.GetById("1_1");
            }

            string period = GetCurrentPeriod(profile, currentTime);
            return SelectForPeriod(profile, currentTime, period, selectedThemeId);
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
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Streak <= 0)
            {
                return ThemeCatalog.GetById("1_1");
            }

            IReadOnlyList<TileTheme> availableThemes = GetPresetsForPeriod(period);
            if (!string.IsNullOrEmpty(selectedThemeId))
            {
                foreach (TileTheme theme in availableThemes)
                {
                    if (theme.Id == selectedThemeId)
                    {
                        return theme;
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
            GetThemeRange(period, out int row, out int firstColumn, out int lastColumn);
            var themes = new List<TileTheme>(lastColumn - firstColumn + 1);
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                themes.Add(ThemeCatalog.GetById(row + "_" + column));
            }

            return themes;
        }

        private static TileTheme SelectDailyTheme(
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
            return ThemeCatalog.GetById(row + "_" + selectedColumn);
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
