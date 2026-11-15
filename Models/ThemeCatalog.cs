using System.Collections.Generic;

namespace DuolingoWidget.Models
{
    public static class ThemeCatalog
    {
        public static TileTheme Default { get; } = new TileTheme(1, 1, "#284D1B", 0.5, true);
        public static IReadOnlyList<TileTheme> Themes { get; } = CreateThemes();

        public static TileTheme GetById(string id)
        {
            foreach (var theme in Themes)
            {
                if (theme.Id == id)
                {
                    return theme;
                }
            }

            return Themes[0];
        }

        private static IReadOnlyList<TileTheme> CreateThemes()
        {
            var themes = new List<TileTheme>();

            for (int row = 1; row <= 3; row++)
            {
                int lastColumn = row == 3 ? 4 : 5;
                for (int col = 1; col <= lastColumn; col++)
                {
                    string color;
                    double opacity;
                    bool showPhrase;

                    if (row == 1 && col == 1)
                    {
                        color = "#284D1B";
                        opacity = 0.5;
                        showPhrase = true;
                    }
                    else if (row == 3)
                    {
                        color = "#FFFFFF";
                        opacity = 0.8;
                        showPhrase = false;
                    }
                    else
                    {
                        color = "#FFFFFF";
                        opacity = 0.8;
                        showPhrase = true;
                    }

                    themes.Add(new TileTheme(row, col, color, opacity, showPhrase));
                }
            }

            return themes;
        }
    }
}
