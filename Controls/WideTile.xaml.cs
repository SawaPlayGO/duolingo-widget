using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using DuolingoWidget.Models;

namespace DuolingoWidget.Controls
{
    public sealed partial class WideTile : UserControl
    {
        public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
            nameof(Theme), typeof(TileTheme), typeof(WideTile),
            new PropertyMetadata(ThemeCatalog.Default, OnThemeChanged));
        public static readonly DependencyProperty StreakProperty = DependencyProperty.Register(
            nameof(Streak), typeof(string), typeof(WideTile), new PropertyMetadata("123"));

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

        public WideTile()
        {
            InitializeComponent();
            Theme = ThemeCatalog.Default;
            Streak = "123";
            UpdateFireImageSource();
        }

        private static void OnThemeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((WideTile)sender).UpdateFireImageSource();
        }

        private void UpdateFireImageSource()
        {
            if (FireImage != null)
            {
                FireImage.Source = Theme.WideFireImage;
            }
        }
    }
}
