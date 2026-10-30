using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Loader
{
    public partial class GameWindow : Window
    {
        // Пути сохраняются в %AppData%\NWLoader\paths.txt
        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NWLoader");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "paths.txt");

        private static readonly Brush PlaceholderBrush = MakeBrush(0x5E, 0x6E, 0x80);
        private static readonly Brush FilledBrush = MakeBrush(0xD5, 0xDE, 0xE8);

        private readonly string _game;          // "csgo" или "cs2"
        private readonly Action<string> _onStart;
        private string _csgoPath;
        private string _cs2Path;
        private bool _closing;

        public GameWindow(string game, Action<string> onStart)
        {
            InitializeComponent();
            _game = game;
            _onStart = onStart;
            LoadPaths();
            UpdateUi();
        }

        private static Brush MakeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private string CurrentGamePath
        {
            get { return _game == "cs2" ? _cs2Path : _csgoPath; }
            set
            {
                if (_game == "cs2") _cs2Path = value;
                else _csgoPath = value;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BuildTimeText.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            ApplyGameHeader();

            var dur = TimeSpan.FromMilliseconds(260);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, dur) { EasingFunction = ease });
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, dur) { EasingFunction = ease });
        }

        private void ApplyGameHeader()
        {
            if (_game == "cs2")
            {
                HeaderTitle.Text = "Counter-Strike 2";
                HeaderIconImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Assets/cs2.png"));
            }
            Title = _game == "cs2" ? "Counter-Strike 2" : "Counter-Strike: Global Offensive";
        }

        private void PickGame_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select CS2 / CS:GO",
                Filter = "CS2 / CS:GO (cs2.exe, csgo.exe)|cs2.exe;csgo.exe|Programs (*.exe)|*.exe",
                CheckFileExists = true
            };
            var current = CurrentGamePath;
            if (!string.IsNullOrEmpty(current) && Directory.Exists(Path.GetDirectoryName(current)))
                dlg.InitialDirectory = Path.GetDirectoryName(current);
            if (dlg.ShowDialog(this) == true)
            {
                CurrentGamePath = dlg.FileName;
                SavePaths();
                UpdateUi();
            }
        }

        private void UpdateUi()
        {
            SetPath(GamePathText, CurrentGamePath, "Select cs2.exe / csgo.exe");
            StartButton.IsEnabled = File.Exists(CurrentGamePath);
        }

        private static void SetPath(System.Windows.Controls.TextBlock text, string path, string placeholder)
        {
            if (string.IsNullOrEmpty(path))
            {
                text.Text = placeholder;
                text.Foreground = PlaceholderBrush;
                text.ToolTip = null;
            }
            else
            {
                text.Text = path;
                text.Foreground = FilledBrush;
                text.ToolTip = path;
            }
        }

        private void LoadPaths()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;
                var lines = File.ReadAllLines(SettingsFile);
                if (lines.Length > 0 && File.Exists(lines[0])) _csgoPath = lines[0];
                if (lines.Length > 1 && File.Exists(lines[1])) _cs2Path = lines[1];
            }
            catch { }
        }

        private void SavePaths()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                File.WriteAllLines(SettingsFile, new[] { _csgoPath ?? "", _cs2Path ?? "" });
            }
            catch { }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (_closing) return;
            _closing = true;
            _onStart?.Invoke(CurrentGamePath);
        }

        public void FadeOut(DoubleAnimation fade, DoubleAnimation shrink)
        {
            _closing = true;
            fade.Completed += (s, a) =>
            {
                if (IsVisible) Close();
            };
            Root.BeginAnimation(OpacityProperty, fade);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        }

        public void CloseNow()
        {
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
