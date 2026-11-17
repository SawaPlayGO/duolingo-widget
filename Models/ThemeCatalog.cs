using System.Collections.Generic;

namespace DuolingoWidget.Models
{
    public static class ThemeCatalog
    {
        public static IReadOnlyList<TileTheme> Themes { get; } = CreateThemes();
        public static TileTheme Default { get; } = Themes[0];

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
            foreach (TileThemeInfo info in TileThemeInfoCatalog.Themes)
            {
                themes.Add(new TileTheme(info));
            }

            return themes;
        }
    }
}
