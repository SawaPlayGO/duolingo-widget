using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DuolingoWidget.Models;
using Windows.Storage;

namespace DuolingoWidget.Services
{
    internal static class BackgroundTileRefreshService
    {
        private static readonly SemaphoreSlim RefreshLock = new SemaphoreSlim(1, 1);

        public static async Task<bool> RefreshAsync()
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (!await RefreshLock.WaitAsync(0))
            {
                settings.Values[BackgroundTileTaskRegistration.LastBackgroundTaskStageSettingKey] =
                    "REFRESH_BUSY";
                await BackgroundTaskDiagnostics.WriteAsync(
                    "headless refresh skipped: another refresh is active");
                return false;
            }

            try
            {
                string username = settings.Values["DuolingoUsername"] as string ?? string.Empty;
                if (string.IsNullOrWhiteSpace(username))
                {
                    settings.Values[BackgroundTileTaskRegistration.LastBackgroundTaskStageSettingKey] =
                        "NO_USER";
                    await BackgroundTaskDiagnostics.WriteAsync(
                        "headless refresh stopped: username is empty");
                    return false;
                }

                await BackgroundTaskDiagnostics.WriteAsync("headless refresh entered");
                DuolingoProfile profile = await DuolingoApiClient.GetProfileAsync(
                    username,
                    isBackgroundTask: true);

                DateTimeOffset currentTime = DateTimeOffset.UtcNow;
                string streak = profile.Streak.ToString(CultureInfo.InvariantCulture);
                string period = ThemeSelector.GetCurrentPeriod(profile, currentTime);
                string selectedThemeId = period == ThemeSelector.NoStreakPeriod
                    ? string.Empty
                    : settings.Values[ThemeSelector.ManualPresetSettingKeyPrefix + period] as string ??
                        string.Empty;
                string themeId = ThemeSelector.SelectThemeId(profile, currentTime, selectedThemeId);
                string currentSmallVariant = ThemeSelector.GetDailySmallTileVariant(
                    profile,
                    currentTime);
                string renderKey = themeId + "|" + streak + "|" + currentSmallVariant;
                string[] renderedKey = (settings.Values["RenderedTileKey"] as string ?? string.Empty)
                    .Split('|');
                string imageVersion = settings.Values["TileImageVersion"] as string ?? string.Empty;
                bool cachedImagesMatch = renderedKey.Length == 3 &&
                    renderedKey[0] == themeId &&
                    renderedKey[1] == streak &&
                    renderedKey[2] == currentSmallVariant &&
                    !string.IsNullOrEmpty(imageVersion) &&
                    await CachedImagesExistAsync(themeId, imageVersion);

                if (!cachedImagesMatch)
                {
                    settings.Values[BackgroundTileTaskRegistration.LastBackgroundTaskStageSettingKey] =
                        "HEADLESS_RENDER";
                    await BackgroundTaskDiagnostics.WriteAsync(
                        "headless render started theme=" + themeId +
                        " streak=" + streak +
                        " smallVariant=" + currentSmallVariant);
                    imageVersion = await HeadlessTileRenderer.RenderThemeAsync(
                        TileThemeInfoCatalog.GetById(themeId),
                        streak,
                        currentSmallVariant);
                    settings.Values["RenderedTileKey"] = renderKey;
                    settings.Values["TileImageVersion"] = imageVersion;
                    await BackgroundTaskDiagnostics.WriteAsync(
                        "headless render completed theme=" + themeId +
                        " imageVersion=" + imageVersion);
                }

                settings.Values["ThemeId"] = themeId;
                await BackgroundTaskDiagnostics.WriteAsync(
                    "updating tile notification theme=" + themeId + " streak=" + streak);
                await TileUpdaterHelper.UpdateTileAsync();
                settings.Values["TileNotificationVersion"] =
                    TileUpdaterHelper.TileNotificationVersion;
                settings.Values[BackgroundTileTaskRegistration.LastBackgroundTaskStageSettingKey] =
                    "COMPLETE";
                settings.Values.Remove(BackgroundTileTaskRegistration.LastBackgroundTaskErrorSettingKey);
                settings.Values.Remove(
                    BackgroundTileTaskRegistration.LastBackgroundTaskCancellationSettingKey);
                settings.Values.Remove(BackgroundTileTaskRegistration.LastErrorSettingKey);
                await BackgroundTaskDiagnostics.WriteAsync("headless background refresh completed");
                return true;
            }
            finally
            {
                RefreshLock.Release();
            }
        }

        private static async Task<bool> CachedImagesExistAsync(string themeId, string imageVersion)
        {
            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "tiles", CreationCollisionOption.OpenIfExists);
            string[] fileNames =
            {
                themeId + "_" + imageVersion + "_small.png",
                themeId + "_" + imageVersion + "_medium.png",
                themeId + "_" + imageVersion + "_wide.png",
                themeId + "_" + imageVersion + "_large.png"
            };

            foreach (string fileName in fileNames)
            {
                if (await folder.TryGetItemAsync(fileName) is not StorageFile)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
