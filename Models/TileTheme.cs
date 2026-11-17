using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace DuolingoWidget.Models
{
    public sealed class TileTheme
    {
        public string Id { get; private set; }
        public int Row { get; private set; }
        public int Col { get; private set; }
        public Brush DigitBrush { get; private set; }
        public Brush DigitOutlineBrush { get; private set; }
        public Visibility DigitOutlineVisibility => Row == 3 ? Visibility.Visible : Visibility.Collapsed;
        public bool ShowPhrase { get; private set; }
        public Visibility PhraseVisibility => ShowPhrase ? Visibility.Visible : Visibility.Collapsed;
        public Thickness LargeContentMargin => Row == 3
            ? new Thickness(0, 44.5, 0, 0)
            : new Thickness(0, 16, 0, 0);

        public string LargeBackgroundPath { get; private set; }
        public string MediumBackgroundPath { get; private set; }
        public string WideBackgroundPath { get; private set; }
        public string SmallBackgroundPath { get; private set; }
        public string LargeDuoPath { get; private set; }
        public string MediumDuoPath { get; private set; }
        public string WideDuoPath { get; private set; }
        public string LargeFirePath { get; private set; }
        public string MediumFirePath { get; private set; }
        public string WideFirePath { get; private set; }
        public string SmallFire32Path { get; private set; }
        public string SmallFire64Path { get; private set; }

        public BitmapImage LargeBackgroundImage { get; private set; }
        public BitmapImage MediumBackgroundImage { get; private set; }
        public BitmapImage WideBackgroundImage { get; private set; }
        public BitmapImage SmallBackgroundImage { get; private set; }
        public BitmapImage LargeDuoImage { get; private set; }
        public BitmapImage MediumDuoImage { get; private set; }
        public BitmapImage WideDuoImage { get; private set; }
        public BitmapImage LargeFireImage { get; private set; }
        public BitmapImage MediumFireImage { get; private set; }
        public BitmapImage WideFireImage { get; private set; }
        public BitmapImage SmallFire32Image { get; private set; }
        public BitmapImage SmallFire64Image { get; private set; }

        internal TileTheme(TileThemeInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            Row = info.Row;
            Col = info.Col;
            Id = info.Id;
            ShowPhrase = info.ShowPhrase;
            DigitBrush = new SolidColorBrush(Color.FromArgb(
                info.DigitAlpha,
                info.DigitRed,
                info.DigitGreen,
                info.DigitBlue));
            DigitOutlineBrush = new SolidColorBrush(Color.FromArgb(110, 255, 150, 0));

            LargeBackgroundPath = info.BackgroundPath("large");
            MediumBackgroundPath = info.BackgroundPath("medium");
            WideBackgroundPath = info.BackgroundPath("wide");
            SmallBackgroundPath = info.BackgroundPath("small");
            LargeDuoPath = info.DuoPath("large");
            MediumDuoPath = info.DuoPath("medium");
            WideDuoPath = info.DuoPath("wide");
            LargeFirePath = info.FirePath("large");
            MediumFirePath = info.FirePath("medium");
            WideFirePath = info.FirePath("wide");
            SmallFire32Path = info.FirePath("small_32px");
            SmallFire64Path = info.FirePath("small_64px");

            LargeBackgroundImage = Image(LargeBackgroundPath);
            MediumBackgroundImage = Image(MediumBackgroundPath);
            WideBackgroundImage = Image(WideBackgroundPath);
            SmallBackgroundImage = Image(SmallBackgroundPath);
            LargeDuoImage = Image(LargeDuoPath);
            MediumDuoImage = Image(MediumDuoPath);
            WideDuoImage = Image(WideDuoPath);
            LargeFireImage = Image(LargeFirePath);
            MediumFireImage = Image(MediumFirePath);
            WideFireImage = Image(WideFirePath);
            SmallFire32Image = Image(SmallFire32Path);
            SmallFire64Image = Image(SmallFire64Path);
        }

        private static BitmapImage Image(string path) => new BitmapImage(new Uri(path));
    }
}
