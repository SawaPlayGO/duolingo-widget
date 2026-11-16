using System;
using System.Globalization;
using System.Threading.Tasks;
using DuolingoWidget.Models;
using DuolingoWidget.Services;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;

namespace DuolingoWidget
{
    public static class TileUpdaterHelper
    {
        public const bool TestModeEnabled = true;
        public const string LastTileUpdateSettingKey = "LastTileUpdateAt";

        public static async Task UpdateTileAsync(bool updateTimestamp = true)
        {
            var settings = ApplicationData.Current.LocalSettings;
            string themeId = settings.Values["ThemeId"] as string ?? ThemeCatalog.Default.Id;
            string imageVersion = settings.Values["TileImageVersion"] as string ?? string.Empty;
            TileTheme theme = ThemeCatalog.GetById(themeId);
            string tileUpdateTime = updateTimestamp
                ? DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                : settings.Values[LastTileUpdateSettingKey] as string ?? "--:--:--";
            string backgroundTime = settings.Values[
                BackgroundTileTaskRegistration.LastStartedSettingKey] as string ?? "--:--:--";

            var tileFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync("tiles");
            string filePrefix = theme.Id + (string.IsNullOrEmpty(imageVersion) ? "" : "_" + imageVersion);
            string smallUri = await GetTileImageUriAsync(tileFolder, filePrefix + "_small.png");
            string mediumUri = await GetTileImageUriAsync(tileFolder, filePrefix + "_medium.png");
            string wideUri = await GetTileImageUriAsync(tileFolder, filePrefix + "_wide.png");
            string largeUri = await GetTileImageUriAsync(tileFolder, filePrefix + "_large.png");

            var template = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///TileTemplates/Tile.xml"));
            string xml = await FileIO.ReadTextAsync(template);
            string debugTimestamp = TestModeEnabled
                ? "<text hint-style=\"caption\" hint-align=\"right\" hint-wrap=\"false\" hint-maxLines=\"1\">T " +
                    tileUpdateTime +
                    "</text>"
                : string.Empty;
            string debugBackgroundTimestamp = TestModeEnabled
                ? "<text hint-style=\"caption\" hint-align=\"right\" hint-wrap=\"false\" hint-maxLines=\"1\">BG " +
                    backgroundTime +
                    "</text>"
                : string.Empty;
            xml = xml.Replace("{smallUri}", smallUri)
                     .Replace("{mediumUri}", mediumUri)
                     .Replace("{wideUri}", wideUri)
                     .Replace("{largeUri}", largeUri)
                     .Replace("{debugTimestamp}", debugTimestamp)
                     .Replace("{debugBackgroundTimestamp}", debugBackgroundTimestamp);

            var tileDocument = new XmlDocument();
            tileDocument.LoadXml(xml);
            TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(tileDocument));
            if (updateTimestamp)
            {
                settings.Values[LastTileUpdateSettingKey] = tileUpdateTime;
            }
        }

        private static async Task<string> GetTileImageUriAsync(StorageFolder folder, string fileName)
        {
            var file = await folder.GetFileAsync(fileName);
            return "ms-appdata:///local/tiles/" + file.Name;
        }
    }
}
