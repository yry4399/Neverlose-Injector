using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Loader
{
    public partial class PickerWindow : Window
    {
        private readonly (string Name, string Path)[] _items;
        private readonly Action<int> _onSelect;
        private readonly Border[] _cards;
        private int _selected;
        private bool _closing;

        public PickerWindow(string title, (string Name, string Path)[] items, int selectedIndex, Action<int> onSelect)
        {
            InitializeComponent();

            _items = items;
            _onSelect = onSelect;
            _selected = (selectedIndex >= 0 && selectedIndex < items.Length) ? selectedIndex : -1;

            TitleText.Text = title;
            _cards = new Border[items.Length];
            BuildCards();
            UpdateSelection();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var dur = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, dur) { EasingFunction = ease });
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, dur) { EasingFunction = ease });
        }

        private void BuildCards()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                int index = i;
                var def = _items[i];

                var image = new Image
                {
                    Width = 118,
                    Height = 112,
                    Stretch = Stretch.UniformToFill,
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                var src = Photos.Load(def.Path);
                if (src != null) image.Source = src;

                var imageHost = new Border
                {
                    Width = 118,
                    Height = 112,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x16, 0x22)),
                    ClipToBounds = true,
                    Child = image,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };

                var nameText = new TextBlock
                {
                    Text = def.Name,
                    Foreground = Brushes.White,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                var check = new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(0x3F, 0xA1, 0xFF)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 4, 4, 0),
                    Visibility = Visibility.Collapsed,
                    Child = new TextBlock
                    {
                        Text = "✓",
                        Foreground = Brushes.White,
                        FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                };

                var inner = new Grid();
                inner.Children.Add(imageHost);
                inner.Children.Add(check);

                var stack = new StackPanel();
                stack.Children.Add(inner);
                stack.Children.Add(nameText);

                var card = new Border
                {
                    Width = 158,
                    Margin = new Thickness(5),
                    Padding = new Thickness(8, 8, 8, 8),
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(0x08, 0x11, 0x1D)),
                    BorderThickness = new Thickness(1.2),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x2A, 0x3D)),
                    Cursor = Cursors.Hand,
                    Child = stack,
                };
                card.MouseLeftButtonDown += (s, e) => Choose(index);
                card.MouseEnter += (s, e) => { if (index != _selected) card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x6A, 0x8C)); };
                card.MouseLeave += (s, e) => { if (index != _selected) card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x2A, 0x3D)); };

                _cards[i] = card;
                CardsPanel.Children.Add(card);

                // 记录勾选标记
                card.Tag = check;
            }
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null) continue;
                bool active = i == _selected;
                _cards[i].BorderBrush = new SolidColorBrush(active
                    ? Color.FromRgb(0x3F, 0xA1, 0xFF)
                    : Color.FromRgb(0x18, 0x2A, 0x3D));
                if (_cards[i].Tag is Border check)
                {
                    check.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void Choose(int index)
        {
            if (_closing) return;
            _selected = index;
            UpdateSelection();
            _onSelect?.Invoke(index);
            CloseNow();
        }

        private void CloseNow()
        {
            if (_closing) return;
            _closing = true;
            if (IsVisible) Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_closing) return;
            _closing = true;

            var dur = TimeSpan.FromMilliseconds(150);
            var fade = new DoubleAnimation(0, dur);
            fade.Completed += (s, a) => { if (IsVisible) Close(); };
            Root.BeginAnimation(OpacityProperty, fade);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, dur));
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, dur));
        }
    }
}
