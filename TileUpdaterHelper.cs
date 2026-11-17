using System;
using System.Globalization;
using System.Threading.Tasks;
using DuolingoWidget.Services;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;

namespace DuolingoWidget
{
    public static class TileUpdaterHelper
    {
        public const string TileNotificationVersion = "4";

        public static void SetDiagnosticError(string stage, Exception exception)
        {
            string errorCode = unchecked((uint)exception.HResult)
                .ToString("X8", CultureInfo.InvariantCulture);
            ApplicationData.Current.LocalSettings.Values[
                BackgroundTileTaskRegistration.LastErrorSettingKey] =
                stage + ": " + exception.GetType().Name + " (0x" + errorCode + ")";
        }

        public static async Task UpdateTileAsync()
        {
            var settings = ApplicationData.Current.LocalSettings;
            string themeId = settings.Values["ThemeId"] as string ?? "1_1";
            string imageVersion = settings.Values["TileImageVersion"] as string ?? string.Empty;

            var tileFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync("tiles");
            string filePrefix = themeId + (string.IsNullOrEmpty(imageVersion) ? "" : "_" + imageVersion);
            string smallUri = await GetTileImageUriAsync(tileFolder, filePrefix + "_small.png");
            string mediumUri = await GetTileImageUriAsync(tileFolder, filePrefix + "_medium.png");
            string wideUri = await GetTileImageUriAsync(tileFolder, filePrefix + "_wide.png");
            string largeUri = await GetTileImageUriAsync(tileFolder, filePrefix + "_large.png");

            var template = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///TileTemplates/Tile.xml"));
            string xml = await FileIO.ReadTextAsync(template);
            xml = xml.Replace("{smallUri}", smallUri)
                     .Replace("{mediumUri}", mediumUri)
                     .Replace("{wideUri}", wideUri)
                     .Replace("{largeUri}", largeUri);

            var tileDocument = new XmlDocument();
            tileDocument.LoadXml(xml);
            TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(tileDocument));
        }

        private static async Task<string> GetTileImageUriAsync(StorageFolder folder, string fileName)
        {
            var file = await folder.GetFileAsync(fileName);
            return "ms-appdata:///local/tiles/" + file.Name;
        }
    }
}
