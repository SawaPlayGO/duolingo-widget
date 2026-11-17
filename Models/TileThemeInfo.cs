using System;
using System.Collections.Generic;

namespace DuolingoWidget.Models
{
    internal sealed class TileThemeInfo
    {
        public string Id { get; }
        public int Row { get; }
        public int Col { get; }
        public byte DigitAlpha { get; }
        public byte DigitRed { get; }
        public byte DigitGreen { get; }
        public byte DigitBlue { get; }
        public bool ShowPhrase { get; }
        public bool ShowDigitOutline => Row == 3;
        public double LargeContentMarginTop => Row == 3 ? 44.5 : 16;

        public TileThemeInfo(int row, int col, string digitColor, double digitOpacity, bool showPhrase)
        {
            Row = row;
            Col = col;
            Id = row + "_" + col;
            DigitAlpha = (byte)Math.Round(digitOpacity * 255);
            DigitRed = Convert.ToByte(digitColor.Substring(1, 2), 16);
            DigitGreen = Convert.ToByte(digitColor.Substring(3, 2), 16);
            DigitBlue = Convert.ToByte(digitColor.Substring(5, 2), 16);
            ShowPhrase = showPhrase;
        }

        public string BackgroundPath(string size) =>
            "ms-appx:///Assets/backgrounds/bg_" + Id + "_" + size + ".png";

        public string DuoPath(string size) =>
            "ms-appx:///Assets/duo/duo_" + Id + "_" + size + ".png";

        public string FirePath(string size)
        {
            string state = Row == 1 ? "off" : Row == 2 ? "warning" : "on";
            return "ms-appx:///Assets/fires/fire_" + state + "_" + Id + "_" + size + ".png";
        }
    }

    internal static class TileThemeInfoCatalog
    {
        private static readonly IReadOnlyList<TileThemeInfo> AllThemes = CreateThemes();

        public static IReadOnlyList<TileThemeInfo> Themes => AllThemes;

        public static TileThemeInfo GetById(string id)
        {
            foreach (TileThemeInfo theme in AllThemes)
            {
                if (theme.Id == id)
                {
                    return theme;
                }
            }

            return AllThemes[0];
        }

        private static IReadOnlyList<TileThemeInfo> CreateThemes()
        {
            var themes = new List<TileThemeInfo>();

            for (int row = 1; row <= 3; row++)
            {
                int lastColumn = row == 3 ? 4 : 5;
                for (int col = 1; col <= lastColumn; col++)
                {
                    bool isDefault = row == 1 && col == 1;
                    string color = isDefault ? "#284D1B" : "#FFFFFF";
                    double opacity = isDefault ? 0.5 : 0.8;
                    bool showPhrase = row != 3;
                    themes.Add(new TileThemeInfo(row, col, color, opacity, showPhrase));
                }
            }

            return themes;
        }
    }
}
