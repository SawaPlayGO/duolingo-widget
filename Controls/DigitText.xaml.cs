using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace DuolingoWidget.Controls
{
    public sealed partial class DigitText : UserControl
    {
        public static readonly DependencyProperty StreakProperty = DependencyProperty.Register(
            nameof(Streak), typeof(string), typeof(DigitText),
            new PropertyMetadata("123", OnTextLayoutPropertyChanged));
        public static readonly DependencyProperty MaxTextWidthProperty = DependencyProperty.Register(
            nameof(MaxTextWidth), typeof(double), typeof(DigitText),
            new PropertyMetadata(0d, OnTextLayoutPropertyChanged));
        public static readonly DependencyProperty BaseFontSizeProperty = DependencyProperty.Register(
            nameof(BaseFontSize), typeof(double), typeof(DigitText),
            new PropertyMetadata(0d, OnTextLayoutPropertyChanged));
        public static readonly DependencyProperty DigitBrushProperty = DependencyProperty.Register(
            nameof(DigitBrush), typeof(Brush), typeof(DigitText),
            new PropertyMetadata(new SolidColorBrush(Colors.White)));
        public static readonly DependencyProperty OutlineBrushProperty = DependencyProperty.Register(
            nameof(OutlineBrush), typeof(Brush), typeof(DigitText),
            new PropertyMetadata(new SolidColorBrush(Colors.Transparent)));
        public static readonly DependencyProperty OutlineVisibilityProperty = DependencyProperty.Register(
            nameof(OutlineVisibility), typeof(Visibility), typeof(DigitText),
            new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty OutlineWidthProperty = DependencyProperty.Register(
            nameof(OutlineWidth), typeof(double), typeof(DigitText), new PropertyMetadata(0d));
        public static readonly DependencyProperty OutlineHeightProperty = DependencyProperty.Register(
            nameof(OutlineHeight), typeof(double), typeof(DigitText), new PropertyMetadata(0d));

        public string Streak
        {
            get => (string)GetValue(StreakProperty);
            set => SetValue(StreakProperty, value);
        }

        public double MaxTextWidth
        {
            get => (double)GetValue(MaxTextWidthProperty);
            set => SetValue(MaxTextWidthProperty, value);
        }

        public double BaseFontSize
        {
            get => (double)GetValue(BaseFontSizeProperty);
            set => SetValue(BaseFontSizeProperty, value);
        }

        public Brush DigitBrush
        {
            get => (Brush)GetValue(DigitBrushProperty);
            set => SetValue(DigitBrushProperty, value);
        }

        public Brush OutlineBrush
        {
            get => (Brush)GetValue(OutlineBrushProperty);
            set => SetValue(OutlineBrushProperty, value);
        }

        public Visibility OutlineVisibility
        {
            get => (Visibility)GetValue(OutlineVisibilityProperty);
            set => SetValue(OutlineVisibilityProperty, value);
        }

        public double OutlineWidth
        {
            get => (double)GetValue(OutlineWidthProperty);
            set => SetValue(OutlineWidthProperty, value);
        }

        public double OutlineHeight
        {
            get => (double)GetValue(OutlineHeightProperty);
            set => SetValue(OutlineHeightProperty, value);
        }

        public DigitText()
        {
            InitializeComponent();
            UpdateTextLayout();
        }

        private static void OnTextLayoutPropertyChanged(
            DependencyObject sender,
            DependencyPropertyChangedEventArgs e)
        {
            ((DigitText)sender).UpdateTextLayout();
        }

        private void UpdateTextLayout()
        {
            if (MaxTextWidth <= 0 || BaseFontSize <= 0)
            {
                return;
            }

            int digitCount = Math.Max(1, string.IsNullOrEmpty(Streak) ? 0 : Streak.Length);
            double fontSize = digitCount <= 3
                ? BaseFontSize
                : Math.Min(BaseFontSize, MaxTextWidth / (digitCount * 0.55));

            FontSize = fontSize;
        }
    }
}
