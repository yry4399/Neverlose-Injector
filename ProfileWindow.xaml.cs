using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Loader
{
    public partial class ProfileWindow : Window
    {
        private static readonly string SettingsDir =
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NWLoader");
        private static readonly string SettingsFile = System.IO.Path.Combine(SettingsDir, "settings.txt");
        private static readonly string HippoFile = System.IO.Path.Combine(SettingsDir, "hippo.txt");

        private readonly Action<bool> _onThemeChanged;
        private readonly Action<double> _onBgOpacityChanged;
        private readonly Action<double> _onHippoOpacityChanged;
        private readonly Action<double> _onCardsBgOpacityChanged;
        private readonly Action _onPickBackground;
        private readonly Action _onResetBackground;
        private readonly Action _onPickAvatar;
        private readonly Action _onResetAvatar;

        private bool _isRainTheme;
        private bool _closing;
        private bool _suppressEvents = true;

        private static readonly SolidColorBrush ActiveBorder = MakeBrush(0x3F, 0xA1, 0xFF);
        private static readonly SolidColorBrush ActiveBg = MakeBrush(0x12, 0x25, 0x3B);
        private static readonly SolidColorBrush InactiveBorder = MakeBrush(0x18, 0x2A, 0x3D);
        private static readonly SolidColorBrush InactiveBg = MakeBrush(0x08, 0x11, 0x1D);

        public ProfileWindow(
            bool isRainTheme,
            double bgOpacity,
            double hippoOpacity,
            double cardsBgOpacity,
            string bgImagePath,
            string avatarPath,
            string nick,
            Action<bool> onThemeChanged,
            Action<double> onBgOpacityChanged,
            Action<double> onHippoOpacityChanged,
            Action<double> onCardsBgOpacityChanged,
            Action onPickBackground,
            Action onResetBackground,
            Action onPickAvatar,
            Action onResetAvatar)
        {
            InitializeComponent();

            _isRainTheme = isRainTheme;
            _onThemeChanged = onThemeChanged;
            _onBgOpacityChanged = onBgOpacityChanged;
            _onHippoOpacityChanged = onHippoOpacityChanged;
            _onCardsBgOpacityChanged = onCardsBgOpacityChanged;
            _onPickBackground = onPickBackground;
            _onResetBackground = onResetBackground;
            _onPickAvatar = onPickAvatar;
            _onResetAvatar = onResetAvatar;

            SliderBg.Value = Math.Round(bgOpacity * 100);
            TxtBgOpacity.Text = $"{(int)SliderBg.Value}%";
            SliderHippo.Value = Math.Round(hippoOpacity * 100);
            TxtHippoOpacity.Text = $"{(int)SliderHippo.Value}%";
            SliderCards.Value = Math.Round(cardsBgOpacity * 100);
            TxtCardsOpacity.Text = $"{(int)SliderCards.Value}%";

            RefreshProfile(avatarPath, nick);
            RefreshBackground(bgImagePath);
            UpdateThemeButtons();
            _suppressEvents = false;
        }

        public void RefreshProfile(string avatarPath, string nick)
        {
            if (ProfileNick != null) ProfileNick.Text = nick;

            var src = Photos.Load(avatarPath);
            if (src != null && ProfileAvatarBrush != null)
            {
                ProfileAvatarBrush.ImageSource = src;
            }
        }

        public void RefreshBackground(string bgImagePath)
        {
            if (BtnBgPick == null) return;

            string name = null;
            foreach (var item in Photos.Backgrounds)
            {
                if (item.Path == bgImagePath) { name = item.Name; break; }
            }

            BtnBgPick.Content = name == null ? "🖼  选择背景图" : $"🖼  {name}";
        }

        private void PickBg_Click(object sender, RoutedEventArgs e)
        {
            if (_closing) return;
            _onPickBackground?.Invoke();
        }

        private void ResetBg_Click(object sender, RoutedEventArgs e)
        {
            if (_closing) return;
            _onResetBackground?.Invoke();
        }

        private void PickAvatar_Click(object sender, RoutedEventArgs e)
        {
            if (_closing) return;
            _onPickAvatar?.Invoke();
        }

        private void ResetAvatar_Click(object sender, RoutedEventArgs e)
        {
            if (_closing) return;
            _onResetAvatar?.Invoke();
        }

        private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var dur = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, dur) { EasingFunction = ease });
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, dur) { EasingFunction = ease });
        }

        // 拖动窗口：按住标题栏即可移动（点在关闭按钮上时不拖动）
        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left)
                return;

            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep != TitleBar)
            {
                if (dep is Button) return;
                dep = VisualTreeHelper.GetParent(dep);
            }

            DragMove();
        }

        private void UpdateThemeButtons()
        {
            SetButtonActive(BtnRainTheme, _isRainTheme);
            SetButtonActive(BtnClassicTheme, !_isRainTheme);
        }

        private static void SetButtonActive(Button btn, bool active)
        {
            if (btn?.Template?.FindName("BtnBorder", btn) is Border b)
            {
                b.BorderBrush = active ? ActiveBorder : InactiveBorder;
                b.Background = active ? ActiveBg : InactiveBg;
            }
            btn.Foreground = active ? Brushes.White : MakeBrush(0x8E, 0x9E, 0xAF);
        }

        private void RainTheme_Click(object sender, RoutedEventArgs e)
        {
            if (_closing) return;
            _isRainTheme = true;
            UpdateThemeButtons();
            _onThemeChanged?.Invoke(true);
            SaveCurrentSettings();
        }

        private void ClassicTheme_Click(object sender, RoutedEventArgs e)
        {
            if (_closing) return;
            _isRainTheme = false;
            UpdateThemeButtons();
            _onThemeChanged?.Invoke(false);
            SaveCurrentSettings();
        }

        private void SliderBg_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressEvents) return;
            int val = (int)Math.Round(e.NewValue);
            TxtBgOpacity.Text = $"{val}%";
            _onBgOpacityChanged?.Invoke(val / 100.0);
            SaveCurrentSettings();
        }

        private void SliderHippo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressEvents) return;
            int val = (int)Math.Round(e.NewValue);
            TxtHippoOpacity.Text = $"{val}%";
            _onHippoOpacityChanged?.Invoke(val / 100.0);
            SaveCurrentSettings();
        }

        private void SliderCards_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressEvents) return;
            int val = (int)Math.Round(e.NewValue);
            TxtCardsOpacity.Text = $"{val}%";
            _onCardsBgOpacityChanged?.Invoke(val / 100.0);
            SaveCurrentSettings();
        }

        private void SaveCurrentSettings()
        {
            SaveSettings(_isRainTheme, SliderBg.Value / 100.0, SliderHippo.Value / 100.0, SliderCards.Value / 100.0);
        }

        public static void SaveSettings(bool isRain, double bgOpacity, double hippoOpacity, double cardsBgOpacity)
        {
            var data = ReadSettings();
            data["theme"] = isRain ? "rain" : "classic";
            data["bg_opacity"] = string.Format(CultureInfo.InvariantCulture, "{0:0.00}", bgOpacity);
            data["hippo_opacity"] = string.Format(CultureInfo.InvariantCulture, "{0:0.00}", hippoOpacity);
            data["cards_bg_opacity"] = string.Format(CultureInfo.InvariantCulture, "{0:0.00}", cardsBgOpacity);
            WriteSettings(data);
        }

        public static void SaveProfile(string avatarPath, string nick, string bgImagePath)
        {
            var data = ReadSettings();
            data["avatar"] = avatarPath ?? string.Empty;
            data["nick"] = nick ?? string.Empty;
            data["bg_image"] = bgImagePath ?? string.Empty;
            WriteSettings(data);
        }

        public static (bool isRain, double bgOpacity, double hippoOpacity, double cardsBgOpacity) LoadSavedSettings()
        {
            var data = ReadSettings();

            bool isRain = true;
            double bgOpacity = 0.98;
            double hippoOpacity = 0.96;
            double cardsBgOpacity = 1.0;

            if (data.TryGetValue("theme", out string theme))
            {
                isRain = (theme != "classic");
            }
            if (data.TryGetValue("bg_opacity", out string bg) &&
                double.TryParse(bg, NumberStyles.Any, CultureInfo.InvariantCulture, out double bgVal))
            {
                bgOpacity = Math.Clamp(bgVal, 0.20, 1.0);
            }
            if (data.TryGetValue("hippo_opacity", out string hip) &&
                double.TryParse(hip, NumberStyles.Any, CultureInfo.InvariantCulture, out double hipVal))
            {
                hippoOpacity = Math.Clamp(hipVal, 0.0, 1.0);
            }
            if (data.TryGetValue("cards_bg_opacity", out string cb) &&
                double.TryParse(cb, NumberStyles.Any, CultureInfo.InvariantCulture, out double cbVal))
            {
                cardsBgOpacity = Math.Clamp(cbVal, 0.0, 1.0);
            }

            return (isRain, bgOpacity, hippoOpacity, cardsBgOpacity);
        }

        public static (string avatar, string nick, string bgImage) LoadSavedProfile()
        {
            var data = ReadSettings();

            string avatar = data.TryGetValue("avatar", out string a) && !string.IsNullOrWhiteSpace(a) ? a : null;
            string nick = data.TryGetValue("nick", out string n) && !string.IsNullOrWhiteSpace(n) ? n : null;
            string bgImage = data.TryGetValue("bg_image", out string b) && !string.IsNullOrWhiteSpace(b) ? b : null;

            return (avatar, nick, bgImage);
        }

        public static void SaveHippo(int index)
        {
            try
            {
                if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);
                File.WriteAllText(HippoFile, index.ToString(CultureInfo.InvariantCulture));
            }
            catch { }
        }

        public static int LoadSavedHippo()
        {
            try
            {
                if (File.Exists(HippoFile))
                {
                    string txt = File.ReadAllText(HippoFile).Trim();
                    if (int.TryParse(txt, out int val) && val >= 0 && val < Photos.Hippos.Length)
                    {
                        return val;
                    }
                }
            }
            catch { }
            return 0;
        }

        private static Dictionary<string, string> ReadSettings()
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(SettingsFile))
                {
                    foreach (var line in File.ReadAllLines(SettingsFile))
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length != 2) continue;
                        data[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
            catch { }
            return data;
        }

        private static void WriteSettings(Dictionary<string, string> data)
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                var sb = new System.Text.StringBuilder();
                foreach (var kv in data)
                {
                    sb.Append(kv.Key).Append('=').AppendLine(kv.Value);
                }
                File.WriteAllText(SettingsFile, sb.ToString());
            }
            catch { }
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

            var dur = TimeSpan.FromMilliseconds(180);
            var fade = new DoubleAnimation(0, dur);
            fade.Completed += (s, a) => { if (IsVisible) Close(); };
            Root.BeginAnimation(OpacityProperty, fade);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, dur));
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, dur));
        }
    }
}