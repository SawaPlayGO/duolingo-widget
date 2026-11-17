using System;
using System.Threading.Tasks;
using DuolingoWidget.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Text;

namespace DuolingoWidget.Services
{
    internal static class HeadlessTileRenderer
    {
        private const int MaxTileImageBytes = 200 * 1024;
        private const int RenderScale = 2;
        private const string SmallDigital = "SmallDigital";
        private const string SmallFire = "SmallFire";
        private const string FontFileUri = "ms-appx:///Assets/Fonts/DIN_Next_Rounded_LT_Pro.ttc";
        private const string FontFamilyUri =
            "ms-appx:///Assets/Fonts/DIN_Next_Rounded_LT_Pro.ttc#DIN Next Rounded LT Pro";

        public static async Task<string> RenderThemeAsync(
            TileThemeInfo theme,
            string streak,
            string smallVariant)
        {
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            if (string.IsNullOrWhiteSpace(streak))
            {
                throw new ArgumentException("Для рендеринга плитки нужен стрик.", nameof(streak));
            }
            if (smallVariant != SmallDigital && smallVariant != SmallFire)
            {
                throw new ArgumentException("Неизвестный вариант маленькой плитки.", nameof(smallVariant));
            }

            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "tiles", CreationCollisionOption.OpenIfExists);
            string imageVersion = Guid.NewGuid().ToString("N");

            using (var device = new CanvasDevice(forceSoftwareRenderer: true))
            using (var fontSet = new CanvasFontSet(new Uri(FontFileUri)))
            {
                device.LowPriority = true;
                if (fontSet.Fonts.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Не удалось загрузить шрифт плитки из пакета приложения.");
                }

                await RenderTileWithFallbackAsync(
                    device, folder, theme, streak, "large", "large", imageVersion);
                await RenderTileWithFallbackAsync(
                    device, folder, theme, streak, "wide", "wide", imageVersion);
                await RenderTileWithFallbackAsync(
                    device, folder, theme, streak, "medium", "medium", imageVersion);
                await RenderTileWithFallbackAsync(
                    device,
                    folder,
                    theme,
                    streak,
                    "small",
                    smallVariant == SmallFire ? "small_64px" : "small_32px",
                    imageVersion,
                    smallVariant);
            }

            return imageVersion;
        }

        private static async Task RenderTileWithFallbackAsync(
            CanvasDevice device,
            StorageFolder folder,
            TileThemeInfo theme,
            string streak,
            string tileSize,
            string fireSize,
            string imageVersion,
            string smallVariant = "")
        {
            string suffix = tileSize == "small" ? "small" : tileSize;
            string fileName = theme.Id + "_" + imageVersion + "_" + suffix + ".png";
            await RenderTileAsync(
                device, folder, theme, streak, tileSize, fireSize, fileName, RenderScale, smallVariant);

            StorageFile file = await folder.GetFileAsync(fileName);
            var properties = await file.GetBasicPropertiesAsync();
            if (properties.Size > MaxTileImageBytes)
            {
                await RenderTileAsync(
                    device, folder, theme, streak, tileSize, fireSize, fileName, 1, smallVariant);
                properties = await file.GetBasicPropertiesAsync();
                if (properties.Size > MaxTileImageBytes)
                {
                    throw new InvalidOperationException(
                        "Размер PNG плитки превышает ограничение 200 КБ: " + fileName);
                }
            }
        }

        private static async Task RenderTileAsync(
            CanvasDevice device,
            StorageFolder folder,
            TileThemeInfo theme,
            string streak,
            string tileSize,
            string fireSize,
            string fileName,
            int scale,
            string smallVariant)
        {
            GetTileSize(tileSize, out int width, out int height);
            using (CanvasBitmap background = await CanvasBitmap.LoadAsync(
                device,
                new Uri(theme.BackgroundPath(tileSize))))
            using (CanvasBitmap fire = await CanvasBitmap.LoadAsync(
                device,
                new Uri(theme.FirePath(fireSize))))
            {
                CanvasBitmap? duo = null;
                try
                {
                    if (tileSize != "small")
                    {
                        duo = await CanvasBitmap.LoadAsync(device, new Uri(theme.DuoPath(tileSize)));
                    }

                    using (var target = new CanvasRenderTarget(
                        device,
                        width * scale,
                        height * scale,
                        96))
                    {
                        using (CanvasDrawingSession drawingSession = target.CreateDrawingSession())
                        {
                            drawingSession.Clear(Colors.Transparent);
                            DrawTile(
                                drawingSession,
                                device,
                                theme,
                                streak,
                                tileSize,
                                smallVariant,
                                background,
                                duo,
                                fire,
                                scale);
                        }

                        StorageFile file = await folder.CreateFileAsync(
                            fileName, CreationCollisionOption.ReplaceExisting);
                        using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await target.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                        }
                    }
                }
                finally
                {
                    duo?.Dispose();
                }
            }
        }

        private static void DrawTile(
            CanvasDrawingSession drawingSession,
            CanvasDevice device,
            TileThemeInfo theme,
            string streak,
            string tileSize,
            string smallVariant,
            CanvasBitmap background,
            CanvasBitmap? duo,
            CanvasBitmap fire,
            int scale)
        {
            GetTileSize(tileSize, out int width, out int height);
            drawingSession.DrawImage(background, ScaledRect(0, 0, width, height, scale));

            switch (tileSize)
            {
                case "large":
                    drawingSession.DrawImage(RequireDuo(duo), ScaledRect(0, 137, 310, 173, scale));
                    DrawLarge(drawingSession, device, theme, streak, fire, scale);
                    break;
                case "wide":
                    drawingSession.DrawImage(RequireDuo(duo), ScaledRect(187, 40.5, 123, 69, scale));
                    DrawWide(drawingSession, device, theme, streak, fire, scale);
                    break;
                case "medium":
                    drawingSession.DrawImage(RequireDuo(duo), ScaledRect(0, 66, 150, 84, scale));
                    DrawMedium(drawingSession, device, theme, streak, fire, scale);
                    break;
                case "small":
                    if (smallVariant == SmallFire)
                    {
                        drawingSession.DrawImage(fire, ScaledRect(3.5, 3.5, 64, 64, scale));
                    }
                    else
                    {
                        DrawSmallDigital(drawingSession, device, theme, streak, fire, scale);
                    }
                    break;
                default:
                    throw new ArgumentException("Неизвестный размер плитки.", nameof(tileSize));
            }
        }

        private static void DrawLarge(
            CanvasDrawingSession drawingSession,
            CanvasDevice device,
            TileThemeInfo theme,
            string streak,
            CanvasBitmap fire,
            int scale)
        {
            double stackX = (310 - 231) / 2d;
            double stackTop = theme.LargeContentMarginTop;
            drawingSession.DrawImage(fire, ScaledRect(stackX + 24, stackTop, 64, 64, scale));
            DrawStreak(
                drawingSession, device, theme, streak,
                stackX + 24 + 64 + 12,
                stackTop + 10,
                107, 44, 107, 64, scale);

            if (theme.ShowPhrase)
            {
                DrawText(
                    drawingSession, device,
                    "Time to practice!", GetDigitColor(theme), FontWeights.Normal,
                    stackX, stackTop + 80, 231, 32, 32, scale);
            }
        }

        private static void DrawWide(
            CanvasDrawingSession drawingSession,
            CanvasDevice device,
            TileThemeInfo theme,
            string streak,
            CanvasBitmap fire,
            int scale)
        {
            double stackHeight = theme.ShowPhrase ? 92 : 56;
            double stackTop = (150 - stackHeight) / 2;
            double fireX = (173 - (56 + 8 + 94)) / 2;
            drawingSession.DrawImage(fire, ScaledRect(fireX, stackTop, 56, 56, scale));
            DrawStreak(
                drawingSession, device, theme, streak,
                fireX + 56 + 8,
                stackTop + 9,
                94, 38, 94, 56, scale);

            if (theme.ShowPhrase)
            {
                DrawText(
                    drawingSession, device,
                    "Time to practice!", GetDigitColor(theme), FontWeights.Normal,
                    0, stackTop + 68, 173, 24, 24, scale);
            }
        }

        private static void DrawMedium(
            CanvasDrawingSession drawingSession,
            CanvasDevice device,
            TileThemeInfo theme,
            string streak,
            CanvasBitmap fire,
            int scale)
        {
            double fireX = (150 - (48 + 8 + 61)) / 2;
            drawingSession.DrawImage(fire, ScaledRect(fireX, 9, 48, 48, scale));
            DrawStreak(
                drawingSession, device, theme, streak,
                fireX + 48 + 8,
                20.5,
                61, 25, 61, 36, scale);
        }

        private static void DrawSmallDigital(
            CanvasDrawingSession drawingSession,
            CanvasDevice device,
            TileThemeInfo theme,
            string streak,
            CanvasBitmap fire,
            int scale)
        {
            double stackX = (71 - 61) / 2;
            double stackTop = (71 - (32 + 4 + 25)) / 2;
            drawingSession.DrawImage(
                fire,
                ScaledRect(stackX + (61 - 32) / 2d, stackTop, 32, 32, scale));
            DrawStreak(
                drawingSession, device, theme, streak,
                stackX,
                stackTop + 32 + 4,
                61, 25, 61, 36, scale);
        }

        private static void DrawStreak(
            CanvasDrawingSession drawingSession,
            CanvasDevice device,
            TileThemeInfo theme,
            string streak,
            double x,
            double y,
            double width,
            double height,
            double maxTextWidth,
            double baseFontSize,
            int scale)
        {
            int digitCount = Math.Max(1, streak.Length);
            double fontSize = digitCount <= 3
                ? baseFontSize
                : Math.Min(baseFontSize, maxTextWidth / (digitCount * 0.55));

            using (var format = CreateTextFormat(fontSize * scale, FontWeights.Bold))
            using (var layout = new CanvasTextLayout(
                device,
                streak,
                format,
                (float)(width * scale),
                (float)(height * scale)))
            {
                if (theme.ShowDigitOutline)
                {
                    Color outline = Color.FromArgb(110, 255, 150, 0);
                    DrawTextLayer(drawingSession, layout, x, y, -1.5, 0, outline, scale);
                    DrawTextLayer(drawingSession, layout, x, y, 1.5, 0, outline, scale);
                    DrawTextLayer(drawingSession, layout, x, y, 0, -1.5, outline, scale);
                    DrawTextLayer(drawingSession, layout, x, y, 0, 1.5, outline, scale);
                    DrawTextLayer(drawingSession, layout, x, y, -1.1, -1.1, outline, scale);
                    DrawTextLayer(drawingSession, layout, x, y, 1.1, -1.1, outline, scale);
                    DrawTextLayer(drawingSession, layout, x, y, -1.1, 1.1, outline, scale);
                    DrawTextLayer(drawingSession, layout, x, y, 1.1, 1.1, outline, scale);
                }

                DrawTextLayer(drawingSession, layout, x, y, 0, 0, GetDigitColor(theme), scale);
            }
        }

        private static void DrawText(
            CanvasDrawingSession drawingSession,
            CanvasDevice device,
            string text,
            Color color,
            FontWeight fontWeight,
            double x,
            double y,
            double width,
            double height,
            double fontSize,
            int scale)
        {
            using (var format = CreateTextFormat(fontSize * scale, fontWeight))
            using (var layout = new CanvasTextLayout(
                device,
                text,
                format,
                (float)(width * scale),
                (float)(height * scale)))
            {
                drawingSession.DrawTextLayout(
                    layout,
                    (float)(x * scale),
                    (float)(y * scale),
                    color);
            }
        }

        private static CanvasTextFormat CreateTextFormat(double fontSize, FontWeight fontWeight)
        {
            return new CanvasTextFormat
            {
                FontFamily = FontFamilyUri,
                FontSize = (float)fontSize,
                FontWeight = fontWeight,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center,
                WordWrapping = CanvasWordWrapping.NoWrap,
                LineSpacing = (float)fontSize
            };
        }

        private static void DrawTextLayer(
            CanvasDrawingSession drawingSession,
            CanvasTextLayout layout,
            double x,
            double y,
            double offsetX,
            double offsetY,
            Color color,
            int scale)
        {
            drawingSession.DrawTextLayout(
                layout,
                (float)((x + offsetX) * scale),
                (float)((y + offsetY) * scale),
                color);
        }

        private static Windows.Foundation.Rect ScaledRect(
            double x,
            double y,
            double width,
            double height,
            int scale)
        {
            return new Windows.Foundation.Rect(
                x * scale,
                y * scale,
                width * scale,
                height * scale);
        }

        private static CanvasBitmap RequireDuo(CanvasBitmap? duo) =>
            duo ?? throw new InvalidOperationException("Не загружено изображение Duo для плитки.");

        private static Color GetDigitColor(TileThemeInfo theme) =>
            Color.FromArgb(theme.DigitAlpha, theme.DigitRed, theme.DigitGreen, theme.DigitBlue);

        private static void GetTileSize(string tileSize, out int width, out int height)
        {
            switch (tileSize)
            {
                case "large":
                    width = 310;
                    height = 310;
                    return;
                case "wide":
                    width = 310;
                    height = 150;
                    return;
                case "medium":
                    width = 150;
                    height = 150;
                    return;
                case "small":
                    width = 71;
                    height = 71;
                    return;
                default:
                    throw new ArgumentException("Неизвестный размер плитки.", nameof(tileSize));
            }
        }
    }
}
