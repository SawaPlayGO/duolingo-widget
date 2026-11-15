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
        public TileTheme(int row, int col, string digitColor, double digitOpacity, bool showPhrase)
        {
            Row = row;
            Col = col;
            Id = row + "_" + col;
            ShowPhrase = showPhrase;
            DigitBrush = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(digitOpacity * 255),
                Convert.ToByte(digitColor.Substring(1, 2), 16),
                Convert.ToByte(digitColor.Substring(3, 2), 16),
                Convert.ToByte(digitColor.Substring(5, 2), 16)));
            DigitOutlineBrush = new SolidColorBrush(Color.FromArgb(110, 255, 150, 0));

            LargeBackgroundPath = BackgroundPath("large");
            MediumBackgroundPath = BackgroundPath("medium");
            WideBackgroundPath = BackgroundPath("wide");
            SmallBackgroundPath = BackgroundPath("small");
            LargeDuoPath = DuoPath("large");
            MediumDuoPath = DuoPath("medium");
            WideDuoPath = DuoPath("wide");
            LargeFirePath = FirePath("large");
            MediumFirePath = FirePath("medium");
            WideFirePath = FirePath("wide");
            SmallFire32Path = FirePath("small_32px");
            SmallFire64Path = FirePath("small_64px");

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

        private string BackgroundPath(string size) =>
            "ms-appx:///Assets/backgrounds/bg_" + Id + "_" + size + ".png";

        private string DuoPath(string size) =>
            "ms-appx:///Assets/duo/duo_" + Id + "_" + size + ".png";

        private string FirePath(string size) =>
            "ms-appx:///Assets/fires/fire_" + GetFireState() + "_" + Id + "_" + size + ".png";

        private string GetFireState()
        {
            if (Row == 1) return "off";
            if (Row == 2) return "warning";
            return "on";
        }

        private static BitmapImage Image(string path) => new BitmapImage(new Uri(path));
    }
}
