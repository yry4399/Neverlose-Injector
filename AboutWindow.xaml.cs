using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Loader
{
    public partial class AboutWindow : Window
    {
        private bool _closing;

        public AboutWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var dur = TimeSpan.FromMilliseconds(260);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.9, 1, dur) { EasingFunction = ease });
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.9, 1, dur) { EasingFunction = ease });
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_closing) return;
            _closing = true;

            var dur = TimeSpan.FromMilliseconds(180);
            var fade = new DoubleAnimation(0, dur);
            fade.Completed += (s, a) => { if (IsLoaded) Close(); };
            Root.BeginAnimation(OpacityProperty, fade);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.9, dur));
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.9, dur));
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
    }
}
