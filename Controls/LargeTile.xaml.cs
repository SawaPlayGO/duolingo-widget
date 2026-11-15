using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using DuolingoWidget.Models;

namespace DuolingoWidget.Controls
{
    public sealed partial class LargeTile : UserControl
    {
        public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
            nameof(Theme), typeof(TileTheme), typeof(LargeTile),
            new PropertyMetadata(ThemeCatalog.Default, OnThemeChanged));
        public static readonly DependencyProperty StreakProperty = DependencyProperty.Register(
            nameof(Streak), typeof(string), typeof(LargeTile), new PropertyMetadata("123"));

        public TileTheme Theme
        {
            get => (TileTheme)GetValue(ThemeProperty);
            set => SetValue(ThemeProperty, value);
        }

        public string Streak
        {
            get => (string)GetValue(StreakProperty);
            set => SetValue(StreakProperty, value);
        }

        public LargeTile()
        {
            InitializeComponent();
            Theme = ThemeCatalog.Default;
            Streak = "123";
            UpdateFireImageSource();
        }

        private static void OnThemeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((LargeTile)sender).UpdateFireImageSource();
        }

        private void UpdateFireImageSource()
        {
            if (FireImage != null)
            {
                FireImage.Source = Theme.LargeFireImage;
            }
        }
    }
}
