using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuolingoWidget.Controls;
using DuolingoWidget.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace DuolingoWidget.Services
{
    public static class TileRenderer
    {
        private const int MaxTileImageBytes = 200 * 1024;
        private static readonly TimeSpan ImageLoadTimeout = TimeSpan.FromSeconds(6);
        private const int RenderScale = 2;
        private const string SmallDigital = "SmallDigital";
        private const string SmallFire = "SmallFire";

        public static async Task RenderThemeAsync(
            TileTheme theme,
            string streak,
            string smallVariant,
            LargeTile largeTile,
            WideTile wideTile,
            MediumTile mediumTile,
            SmallFireTile smallFireTile,
            SmallDigitalTile smallDigitalTile)
        {
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            if (largeTile == null) throw new ArgumentNullException(nameof(largeTile));
            if (wideTile == null) throw new ArgumentNullException(nameof(wideTile));
            if (mediumTile == null) throw new ArgumentNullException(nameof(mediumTile));
            if (smallFireTile == null) throw new ArgumentNullException(nameof(smallFireTile));
            if (smallDigitalTile == null) throw new ArgumentNullException(nameof(smallDigitalTile));
            if (smallVariant != SmallDigital && smallVariant != SmallFire)
            {
                throw new ArgumentException("Неизвестный вариант маленькой плитки.", nameof(smallVariant));
            }

            largeTile.Theme = theme;
            largeTile.Streak = streak;
            wideTile.Theme = theme;
            wideTile.Streak = streak;
            mediumTile.Theme = theme;
            mediumTile.Streak = streak;
            smallFireTile.Theme = theme;
            smallFireTile.Streak = streak;
            smallDigitalTile.Theme = theme;
            smallDigitalTile.Streak = streak;

            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "tiles", CreationCollisionOption.OpenIfExists);
            string imageVersion = Guid.NewGuid().ToString("N");

            await RenderControlAsync(largeTile,
                theme.Id + "_" + imageVersion + "_large.png", folder);
            await RenderControlAsync(wideTile,
                theme.Id + "_" + imageVersion + "_wide.png", folder);
            await RenderControlAsync(mediumTile,
                theme.Id + "_" + imageVersion + "_medium.png", folder);

            FrameworkElement small = smallVariant == SmallFire ? smallFireTile : smallDigitalTile;
            await RenderControlAsync(small,
                theme.Id + "_" + imageVersion + "_small.png", folder);

            ApplicationData.Current.LocalSettings.Values["TileImageVersion"] = imageVersion;
        }

        private static async Task RenderControlAsync(
            FrameworkElement control,
            string fileName,
            StorageFolder folder)
        {
            control.UpdateLayout();
            if (control.ActualWidth <= 0 || control.ActualHeight <= 0)
            {
                throw new InvalidOperationException("Не удалось определить фактический размер контрола плитки.");
            }

            await WaitForImagesAsync(control);
            control.UpdateLayout();

            uint targetWidth = (uint)Math.Round(control.Width * RenderScale);
            uint targetHeight = (uint)Math.Round(control.Height * RenderScale);
            byte[] png = await CapturePngAsync(control, targetWidth, targetHeight);
            if (png.Length > MaxTileImageBytes)
            {
                png = await CapturePngAsync(control, targetWidth / RenderScale, targetHeight / RenderScale);
                if (png.Length > MaxTileImageBytes)
                {
                    throw new InvalidOperationException(
                        "Размер PNG плитки превышает ограничение 200 КБ: " + fileName);
                }
            }

            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBytesAsync(file, png);
        }

        private static async Task<byte[]> CapturePngAsync(
            FrameworkElement control,
            uint targetWidth,
            uint targetHeight)
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(control, (int)targetWidth, (int)targetHeight);
            var pixels = await bitmap.GetPixelsAsync();
            byte[] pixelBytes = new byte[pixels.Length];
            using (var reader = DataReader.FromBuffer(pixels))
            {
                reader.ReadBytes(pixelBytes);
            }

            using (var output = new InMemoryRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)bitmap.PixelWidth,
                    (uint)bitmap.PixelHeight,
                    96,
                    96,
                    pixelBytes);
                await encoder.FlushAsync();

                output.Seek(0);

                using (var reader = new DataReader(output.GetInputStreamAt(0)))
                {
                    await reader.LoadAsync((uint)output.Size);
                    byte[] png = new byte[(int)output.Size];
                    reader.ReadBytes(png);
                    return png;
                }
            }
        }

        private static async Task WaitForImagesAsync(FrameworkElement root)
        {
            var images = new List<Image>();
            CollectImages(root, images);
            var pending = new List<Task>();
            var completePendingImages = new List<Action>();

            foreach (var image in images)
            {
                var bitmap = image.Source as BitmapImage;
                if (bitmap == null || bitmap.PixelWidth > 0)
                {
                    continue;
                }

                var completion = new TaskCompletionSource<bool>();
                RoutedEventHandler opened = (sender, args) => completion.TrySetResult(true);
                ExceptionRoutedEventHandler failed = (sender, args) => completion.TrySetException(
                    new InvalidOperationException("Не удалось загрузить изображение плитки: " + args.ErrorMessage));
                image.ImageOpened += opened;
                image.ImageFailed += failed;

                if (bitmap.PixelWidth > 0)
                {
                    completion.TrySetResult(true);
                }

                pending.Add(DetachHandlersWhenCompleteAsync(image, opened, failed, completion.Task));
                completePendingImages.Add(() => completion.TrySetResult(true));
            }

            Task allImagesLoaded = Task.WhenAll(pending);
            Task timeout = Task.Delay(ImageLoadTimeout);
            if (await Task.WhenAny(allImagesLoaded, timeout) != allImagesLoaded)
            {
                foreach (Action completePendingImage in completePendingImages)
                {
                    completePendingImage();
                }

                await allImagesLoaded;
                throw new TimeoutException(
                    "Превышено время ожидания загрузки изображений для рендеринга плитки.");
            }

            await allImagesLoaded;
        }

        private static async Task DetachHandlersWhenCompleteAsync(
            Image image,
            RoutedEventHandler opened,
            ExceptionRoutedEventHandler failed,
            Task task)
        {
            try
            {
                await task;
            }
            finally
            {
                image.ImageOpened -= opened;
                image.ImageFailed -= failed;
            }
        }

        private static void CollectImages(DependencyObject parent, IList<Image> images)
        {
            var image = parent as Image;
            if (image != null)
            {
                images.Add(image);
            }

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < count; index++)
            {
                CollectImages(VisualTreeHelper.GetChild(parent, index), images);
            }
        }
    }
}
