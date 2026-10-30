using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Loader
{
    public partial class MainWindow : Window
    {
        // Размеры лоадера
        private const double SplashSize = 120;
        private const double MainWidth = 477;
        private const double MainHeight = 336;
        private const double LoadingSeconds = 2.5;

        // Длительность этапов загрузки (мс)
        private const int GameStageMs = 1800;
        private const int DllStageMs = 1500;
        private const int FinishStageMs = 900;

        private const string WebsiteUrl = "https://m.tb.cn/h.jTV07Nv?tk=0Oq9TzN0QXI";
        private const string SupportUrl = "https://m.tb.cn/h.jTV07Nv?tk=0Oq9TzN0QXI";
        private const string MarketUrl  = "https://m.tb.cn/h.jTV07Nv?tk=0Oq9TzN0QXI";

        private AboutWindow _about;
        private GameWindow _game;
        private ProfileWindow _profileWin;
        private PickerWindow _pickerWin;
        private bool _isRainTheme = true;
        private double _bgOpacity = 0.98;
        private double _hippoOpacity = 0.96;
        private double _cardsBgOpacity = 1.0;
        private string _bgImagePath;
        private string _avatarPath = Photos.DefaultAvatarPath;
        private string _nick = Photos.DefaultNick;
        private int _currentHippo = 0;
        private DispatcherTimer _clockTimer;
        private bool _closing;
        private bool _gamePressed;
        private bool _injecting;
        private bool _introPlaying = true;
        private double _currentLabelX = 0;

        public MainWindow()
        {
            InitializeComponent();
            BuildSpinner();
            LocationChanged += MainWindow_LocationChanged;
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (_game != null && _game.IsLoaded && _game.IsVisible)
            {
                _game.Left = this.Left + (this.ActualWidth - _game.ActualWidth) / 2;
                _game.Top = this.Top + (this.ActualHeight - _game.ActualHeight) / 2;
            }
            if (_profileWin != null && _profileWin.IsLoaded && _profileWin.IsVisible)
            {
                _profileWin.Left = this.Left + (this.ActualWidth - _profileWin.ActualWidth) / 2;
                _profileWin.Top = this.Top + (this.ActualHeight - _profileWin.ActualHeight) / 2;
            }
            if (_pickerWin != null && _pickerWin.IsLoaded && _pickerWin.IsVisible)
            {
                _pickerWin.Left = this.Left + (this.ActualWidth - _pickerWin.ActualWidth) / 2;
                _pickerWin.Top = this.Top + (this.ActualHeight - _pickerWin.ActualHeight) / 2;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var (savedRain, savedBg, savedHipOp, savedCardsOp) = ProfileWindow.LoadSavedSettings();
            var profile = ProfileWindow.LoadSavedProfile();
            _isRainTheme = savedRain;
            _bgOpacity = savedBg;
            _hippoOpacity = savedHipOp;
            _cardsBgOpacity = savedCardsOp;
            _bgImagePath = profile.bgImage;
            _avatarPath = profile.avatar ?? Photos.DefaultAvatarPath;
            _nick = profile.nick ?? Photos.DefaultNick;
            ApplyThemeAndOpacity();
            ApplyProfile();

            _currentHippo = ProfileWindow.LoadSavedHippo();
            ApplyHippo(_currentHippo);
            ApplyHippoOpacity();
            ApplyCardsBgOpacity();
            StartClock();
            PlayIntro();
        }

        // ─── Часы под бегемотиком: время текущего ПК ───
        // Каждый тик планируется ровно на начало следующей секунды компа, поэтому часы
        // не сбиваются и не накапливают задержку (обычный Interval = 1 c её копит).
        private void StartClock()
        {
            UpdateClock();
            ScheduleNextSecond();
        }

        private void ScheduleNextSecond()
        {
            if (_closing) return;

            var now = DateTime.Now;
            int delayMs = Math.Max(1, 1000 - now.Millisecond);

            _clockTimer = new DispatcherTimer(DispatcherPriority.Send)
            {
                Interval = TimeSpan.FromMilliseconds(delayMs)
            };
            _clockTimer.Tick += (s, a) =>
            {
                _clockTimer.Stop();
                UpdateClock();
                ScheduleNextSecond();
            };
            _clockTimer.Start();
        }

        private void UpdateClock()
        {
            ClockText.Text = DateTime.Now.ToString("HH:mm");
        }

        // ─── 1) Анимация запуска: квадрат 120х120 раскрывается с центра + YouTube кружок ───
        private void PlayIntro()
        {
            _introPlaying = true;

            var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, spin);

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var open = TimeSpan.FromMilliseconds(450);
            CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, 1, open) { EasingFunction = ease });
            CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.7, 1, open) { EasingFunction = ease });
            Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)));

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(LoadingSeconds) };
            timer.Tick += (s, a) =>
            {
                timer.Stop();
                ExpandToMain();
            };
            timer.Start();
        }

        // ─── 2) Кружок сжимается, загрузчик плавно раскрывается в основной лоадер 477x336 ───
        private void ExpandToMain()
        {
            var delay = TimeSpan.FromMilliseconds(100);
            var dur = TimeSpan.FromMilliseconds(620);
            var ease = new QuarticEase { EasingMode = EasingMode.EaseInOut };
            var outEase = new CubicEase { EasingMode = EasingMode.EaseIn };

            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)) { BeginTime = delay };
            SpinnerLayer.BeginAnimation(OpacityProperty, fade);
            SpinnerScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(200)) { BeginTime = delay, EasingFunction = outEase });
            SpinnerScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(200)) { BeginTime = delay, EasingFunction = outEase });

            var w = new DoubleAnimation(SplashSize, MainWidth, dur) { BeginTime = delay, EasingFunction = ease };
            var h = new DoubleAnimation(SplashSize, MainHeight, dur) { BeginTime = delay, EasingFunction = ease };
            h.Completed += (s, a) => ShowMainContent();

            Card.BeginAnimation(WidthProperty, w);
            Card.BeginAnimation(HeightProperty, h);
        }

        // ─── 3) Появление содержимого главного окна ───
        private void ShowMainContent()
        {
            _introPlaying = false;

            SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            SpinnerLayer.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;

            var dur = TimeSpan.FromMilliseconds(350);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            MainContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            ContentScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, dur) { EasingFunction = ease });
            ContentScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, dur) { EasingFunction = ease });
        }

        // ─── Кружок загрузки: 8 затухающих дуг (стиль буферизации YouTube) ───
        private void BuildSpinner()
        {
            double[] opacities = { 1.0, 0.78, 0.58, 0.42, 0.28, 0.16, 0.07, 0.0 };
            const double size = 56;
            const double thickness = 6;
            double radius = (size - thickness) / 2;
            var center = new Point(size / 2, size / 2);
            double step = 360.0 / opacities.Length;

            for (int i = 0; i < opacities.Length; i++)
            {
                double end = -i * step;
                double start = end - (step - 5);
                var arc = new System.Windows.Shapes.Path
                {
                    Data = CreateArc(center, radius, start, end),
                    Stroke = new SolidColorBrush(Color.FromArgb((byte)(opacities[i] * 255), 255, 255, 255)),
                    StrokeThickness = thickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                SpinnerCanvas.Children.Add(arc);
            }
        }

        private static Geometry CreateArc(Point c, double r, double startDeg, double endDeg)
        {
            Point P(double deg)
            {
                double rad = (deg - 90) * Math.PI / 180.0;
                return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
            }

            var figure = new PathFigure { StartPoint = P(startDeg), IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment(P(endDeg), new Size(r, r), 0, endDeg - startDeg > 180,
                SweepDirection.Clockwise, true));
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        // ─── Карточки игр (CS:GO / CS2) ───
        private static void AnimateGameCard(Border card, Border glow, bool hover)
        {
            var dur = TimeSpan.FromMilliseconds(160);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            double scale = hover ? 1.03 : 1.0;

            var scaleTransform = (ScaleTransform)card.RenderTransform;
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, dur) { EasingFunction = ease });
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, dur) { EasingFunction = ease });
            glow.BeginAnimation(OpacityProperty, new DoubleAnimation(hover ? 0.06 : 0, dur) { EasingFunction = ease });
        }

        private void GameCard_MouseEnter(object sender, MouseEventArgs e)
        {
            var card = (Border)sender;
            AnimateGameCard(card, card == CsgoCard ? CsgoCardGlow : Cs2CardGlow, true);
        }

        private void GameCard_MouseLeave(object sender, MouseEventArgs e)
        {
            var card = (Border)sender;
            _gamePressed = false;
            AnimateGameCard(card, card == CsgoCard ? CsgoCardGlow : Cs2CardGlow, false);
        }

        private void GameCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _gamePressed = true;
            e.Handled = true;
        }

        private void GameCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_gamePressed) return;
            _gamePressed = false;
            OpenGameWindow((string)((Border)sender).Tag);
        }

        private void OpenGameWindow(string game)
        {
            if (_game != null)
            {
                _game.Activate();
                return;
            }
            _game = new GameWindow(game, LaunchCallback) { Owner = this };
            _game.Closed += (s, a) => _game = null;
            _game.Show();
        }

        // ─── Запуск (вызывается из GameWindow по кнопке ▷) ───
        private void LaunchCallback(string exePath)
        {
            if (_injecting) return;
            _injecting = true;

            // Плавно закрываем окно игры GameWindow
            if (_game != null)
            {
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
                var shrink = new DoubleAnimation(0.92, TimeSpan.FromMilliseconds(200));
                var targetGame = _game;
                fade.Completed += (s, a) =>
                {
                    try { targetGame.Close(); } catch { }
                    if (_game == targetGame) _game = null;
                };
                targetGame.Root.BeginAnimation(OpacityProperty, fade);
                targetGame.RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
                targetGame.RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
            }

            BeginInjectVisuals(exePath);
        }

        // ─── Затемнение фона, блюр и появление центральной панели загрузки ───
        private void BeginInjectVisuals(string exePath)
        {
            // Плавное затемнение и блюр
            Backdrop.BeginAnimation(OpacityProperty, new DoubleAnimation(0.48, TimeSpan.FromMilliseconds(380)));
            BackdropBlur.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty,
                new DoubleAnimation(0, 6, TimeSpan.FromMilliseconds(380))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            BackdropScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, TimeSpan.FromMilliseconds(380)));
            BackdropScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, TimeSpan.FromMilliseconds(380)));

            // Панель загрузки появляется из центра
            InjectPanel.Visibility = Visibility.Visible;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(320);
            InjectPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            InjectScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1, dur) { EasingFunction = ease });
            InjectScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1, dur) { EasingFunction = ease });

            RunInjectSequence(exePath);
        }

        // ─── Этапы загрузки рывками ───
        private async void RunInjectSequence(string exePath)
        {
            // 从 exe 路径提取进程名，如 "cs2.exe"
            string processName = !string.IsNullOrEmpty(exePath)
                ? System.IO.Path.GetFileName(exePath)
                : "";

            // 1. Загрузка игры
            await AnimateStageAsync("Loading game", "Waiting for the game process...", 0, 35, GameStageMs);

            // Реальный запуск игры (если процесс игры еще не запущен)
            LaunchGameProcess(exePath);

            // 2. Загрузка DLL — 调用内置注入器（DLL 已打包在程序内）
            string dllName = Injector.PayloadFileName ?? "module.dll";
            InjectTitle.Text = "Loading DLL";
            StageText.Text = "Loading DLL";
            InjectHint.Text = $"Injecting {dllName} into {processName}...";

            var injectResult = await Injector.InjectAsync(processName, Injector.Method.LoadLibrary, 30);

            if (!injectResult.Success)
            {
                // 注入失败 —— 显示错误信息，不关闭窗口
                ShowError(injectResult.Message);
                _injecting = false;
                return;
            }

            // 3. Завершение
            await AnimateStageAsync("Completing", "Injection complete!", 82, 100, FinishStageMs);

            // Финал: надпись "loaded!" и плавное закрытие лоадера
            ShowLoaded();
        }

        private static void LaunchGameProcess(string exePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(exePath) ?? "",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch { }
        }

        private double ProgressTrackWidth()
        {
            // Ширина ползунка: ширина панели 277 - отступы сетки 32 = 245
            return 245;
        }

        private async Task AnimateStageAsync(string title, string hint, double fromPercent, double toPercent, int durationMs)
        {
            InjectTitle.Text = title;
            StageText.Text = title;
            InjectHint.Text = hint;

            double trackWidth = ProgressTrackWidth();
            var rng = new Random();

            int steps = rng.Next(5, 8);
            double currentPercent = fromPercent;
            double percentRange = toPercent - fromPercent;

            for (int i = 0; i < steps; i++)
            {
                double nextPercent;
                if (i == steps - 1)
                    nextPercent = toPercent;
                else
                    nextPercent = currentPercent + (percentRange / steps) * (0.6 + rng.NextDouble() * 0.7);

                nextPercent = Math.Min(nextPercent, toPercent);

                var jerkDur = TimeSpan.FromMilliseconds(110 + rng.Next(50));
                var jerkEase = new CubicEase { EasingMode = EasingMode.EaseOut };

                // Рывок заполнения ползунка
                ProgressTrack.BeginAnimation(WidthProperty,
                    new DoubleAnimation(trackWidth * nextPercent / 100.0, jerkDur) { EasingFunction = jerkEase });

                // Текст этапа сверху следует за ползунком
                AnimateLabelTo(nextPercent, jerkDur);
                InjectPercent.Text = $"{(int)nextPercent}%";

                currentPercent = nextPercent;

                int stepDelay = (durationMs / steps) + rng.Next(-30, 50);
                await Task.Delay(Math.Max(100, stepDelay));
            }
        }

        private void AnimateLabelTo(double percent, TimeSpan duration)
        {
            StageLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double labelWidth = Math.Max(StageLabel.DesiredSize.Width, 1);
            double track = ProgressTrackWidth();

            double targetX = (track * percent / 100.0) - (labelWidth / 2.0);
            targetX = Math.Max(0, Math.Min(targetX, track - labelWidth));

            var anim = new DoubleAnimation
            {
                From = _currentLabelX,
                To = targetX,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _currentLabelX = targetX;
            StageLabel.BeginAnimation(Canvas.LeftProperty, anim);
        }

        // ─── 4) Показ надписи loaded! и плавное закрытие ───
        private void ShowError(string message)
        {
            // 停止进度条相关动画
            StageLabel.Visibility = Visibility.Collapsed;
            InjectPercent.Visibility = Visibility.Collapsed;
            InjectTitle.Visibility = Visibility.Collapsed;
            ProgressTrack.Visibility = Visibility.Collapsed;
            ProgressBackground.Visibility = Visibility.Collapsed;

            // 限制错误信息长度，避免撑破面板
            string shortMsg = message ?? "Unknown error";
            if (shortMsg.Length > 200)
                shortMsg = shortMsg.Substring(0, 200) + "...";

            var errorPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0
            };

            var title = new TextBlock
            {
                Text = "injection failed",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x5F, 0x5F)),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var detail = new TextBlock
            {
                Text = shortMsg,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC8, 0xD0)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 250,
                TextAlignment = TextAlignment.Center
            };

            var okBtn = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 28,
                Margin = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0x3A, 0x4E)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x4A, 0x5A, 0x6E)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontSize = 12
            };

            errorPanel.Children.Add(title);
            errorPanel.Children.Add(detail);
            errorPanel.Children.Add(okBtn);
            Grid.SetRowSpan(errorPanel, 3);
            InjectGrid.Children.Add(errorPanel);

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(260);
            errorPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));

            okBtn.Click += (s, a) =>
            {
                // 关闭注入面板，回到主界面
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s2, a2) =>
                {
                    InjectPanel.Visibility = Visibility.Collapsed;
                    Backdrop.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)));
                    BackdropBlur.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty,
                        new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)));
                    InjectGrid.Children.Remove(errorPanel);
                    ResetInjectPanel();
                };
                errorPanel.BeginAnimation(OpacityProperty, fadeOut);
            };
        }

        private void ResetInjectPanel()
        {
            // 恢复注入面板初始状态，便于下次注入
            StageLabel.Visibility = Visibility.Visible;
            InjectPercent.Visibility = Visibility.Visible;
            InjectTitle.Visibility = Visibility.Visible;
            ProgressTrack.Visibility = Visibility.Visible;
            ProgressBackground.Visibility = Visibility.Visible;
            ProgressTrack.Width = 0;
            InjectPercent.Text = "0%";
            _currentLabelX = 0;
            Canvas.SetLeft(StageLabel, 0);
        }

        private void ShowLoaded()
        {
            InjectHint.Text = string.Empty;
            StageLabel.Visibility = Visibility.Collapsed;
            InjectPercent.Visibility = Visibility.Collapsed;
            InjectTitle.Visibility = Visibility.Collapsed;
            ProgressTrack.Visibility = Visibility.Collapsed;
            ProgressBackground.Visibility = Visibility.Collapsed;

            var loaded = new TextBlock
            {
                Text = "loaded!",
                Foreground = new SolidColorBrush(Color.FromRgb(0x3F, 0xA1, 0xFF)),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.85, 0.85)
            };
            Grid.SetRowSpan(loaded, 3);
            InjectGrid.Children.Add(loaded);

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(260);
            loaded.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            var scale = (ScaleTransform)loaded.RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.85, 1, dur) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.85, 1, dur) { EasingFunction = ease });

            var wait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
            wait.Tick += (s, a) =>
            {
                wait.Stop();
                CloseWithAnimation();
            };
            wait.Start();
        }

        // ─── Плавное закрытие окна (схлопывание к центру) ───
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CloseWithAnimation();
        }

        private void CloseWithAnimation()
        {
            if (_closing) return;
            _closing = true;

            if (_clockTimer != null)
            {
                _clockTimer.Stop();
                _clockTimer = null;
            }

            var dur = TimeSpan.FromMilliseconds(320);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var shrinkX = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };
            var shrinkY = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };
            var fade = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };

            shrinkX.Completed += (s, a) => Application.Current.Shutdown();

            CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkX);
            CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkY);
            Card.BeginAnimation(OpacityProperty, fade);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            if (_about != null)
            {
                _about.Activate();
                return;
            }
            _about = new AboutWindow { Owner = this };
            _about.Closed += (s, a) => _about = null;
            _about.Show();
        }

        private void Website_Click(object sender, RoutedEventArgs e) => OpenUrl(WebsiteUrl);
        private void Support_Click(object sender, RoutedEventArgs e) => OpenUrl(SupportUrl);
        private void Market_Click(object sender, RoutedEventArgs e) => OpenUrl(MarketUrl);

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        public void SetTheme(bool isRain)
        {
            _isRainTheme = isRain;
            ApplyThemeAndOpacity();
        }

        public void SetBgOpacity(double opacity)
        {
            _bgOpacity = Math.Clamp(opacity, 0.20, 1.0);
            ApplyThemeAndOpacity();
        }

        public void SetBgImage(string path)
        {
            _bgImagePath = path;
            ApplyThemeAndOpacity();
            _profileWin?.RefreshBackground(path);
        }

        public void ApplyProfile()
        {
            if (NickText != null) NickText.Text = _nick;

            var src = Photos.Load(_avatarPath);
            if (src != null && AvatarBrush != null)
            {
                AvatarBrush.ImageSource = src;
            }

            _profileWin?.RefreshProfile(_avatarPath, _nick);
        }

        public void ApplyHippo(int index)
        {
            if (Photos.Hippos.Length == 0) return;
            if (index < 0 || index >= Photos.Hippos.Length) index = 0;
            _currentHippo = index;

            var src = Photos.Load(Photos.Hippos[index].Path);
            if (src != null && HippoImage != null)
            {
                HippoImage.Source = src;
            }
            ApplyHippoOpacity();
        }

        public void SetHippoOpacity(double opacity)
        {
            _hippoOpacity = Math.Clamp(opacity, 0.0, 1.0);
            ApplyHippoOpacity();
        }

        private void ApplyHippoOpacity()
        {
            if (HippoBorder != null) HippoBorder.Opacity = _hippoOpacity;
        }

        public void SetCardsBgOpacity(double opacity)
        {
            _cardsBgOpacity = Math.Clamp(opacity, 0.0, 1.0);
            ApplyCardsBgOpacity();
        }

        private void ApplyCardsBgOpacity()
        {
            if (CsgoCard != null) CsgoCard.Background = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(_cardsBgOpacity * 255), 0x08, 0x11, 0x1D));
            if (Cs2Card != null) Cs2Card.Background = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(_cardsBgOpacity * 255), 0x08, 0x11, 0x1D));
        }

        private static readonly SolidColorBrush WhiteBrush = MakeBrush(0xFF, 0xFF, 0xFF);
        private static readonly SolidColorBrush FaintGrayBrush = MakeBrush(0x7C, 0x8C, 0x9E);
        private static readonly SolidColorBrush CustomWhiteBrush = MakeBrush(0xE0, 0xB0, 0xC2);   // 粉色 RGB(224,176,194)
        private static readonly SolidColorBrush CustomGrayBrush = MakeBrush(0x3B, 0xC3, 0xD6);   // 蓝色 RGB(59,195,214)

        private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        private void ApplyTextColors()
        {
            bool customBg = !string.IsNullOrEmpty(_bgImagePath);

            var white = customBg ? CustomWhiteBrush : WhiteBrush;
            var gray = customBg ? CustomGrayBrush : FaintGrayBrush;

            if (NlTitle != null) NlTitle.Foreground = white;
            if (NickText != null) NickText.Foreground = white;
            if (SubscriptionTitle != null) SubscriptionTitle.Foreground = white;
            if (CsgoTitle != null) CsgoTitle.Foreground = white;
            if (Cs2Title != null) Cs2Title.Foreground = white;

            if (SubLabel != null) SubLabel.Foreground = gray;
            if (CsgoExpires != null) CsgoExpires.Foreground = gray;
            if (Cs2Expires != null) Cs2Expires.Foreground = gray;
            if (ClockText != null) ClockText.Foreground = gray;

            if (NavPanel != null)
            {
                foreach (var c in NavPanel.Children)
                {
                    if (c is Button btn)
                    {
                        // 替换 Style 的默认 Foreground，保留 IsMouseOver → White 的 Trigger
                        var baseStyle = btn.Style;
                        if (baseStyle != null)
                        {
                            var newStyle = new Style(typeof(Button), baseStyle);
                            newStyle.Setters.Add(new Setter(Button.ForegroundProperty, gray));
                            newStyle.Seal();
                            btn.Style = newStyle;
                            btn.ClearValue(Button.ForegroundProperty);  // 清掉可能残留的 Local value
                        }
                        else
                        {
                            btn.Foreground = gray;
                        }
                    }
                }
            }
        }

        public void ApplyThemeAndOpacity()
        {
            byte alpha = (byte)Math.Round(_bgOpacity * 255);

            var bgSource = string.IsNullOrEmpty(_bgImagePath) ? null : Photos.Load(_bgImagePath);
            if (bgSource != null)
            {
                // 自定义背景图
                if (RaindropsLayer != null) RaindropsLayer.Visibility = Visibility.Collapsed;
                if (GlassSheen != null) GlassSheen.Visibility = Visibility.Visible;

                var brush = new ImageBrush(bgSource)
                {
                    Stretch = Stretch.UniformToFill,
                    Opacity = _bgOpacity,
                };
                brush.Freeze();
                Card.Background = brush;
                Card.BorderBrush = new LinearGradientBrush(
                    Color.FromArgb((byte)Math.Min(255, alpha * 0.4 + 40), 117, 174, 229),
                    Color.FromArgb(alpha, 24, 42, 64),
                    new Point(0, 0), new Point(0.2, 1));

                ApplyTextColors();
                ApplyCardsBgOpacity();
                return;
            }

            if (_isRainTheme)
            {
                if (RaindropsLayer != null) RaindropsLayer.Visibility = Visibility.Visible;
                if (GlassSheen != null) GlassSheen.Visibility = Visibility.Visible;
                Card.Background = new SolidColorBrush(Color.FromArgb(alpha, 7, 16, 28));
                Card.BorderBrush = new LinearGradientBrush(
                    Color.FromArgb((byte)Math.Min(255, alpha * 0.4 + 40), 117, 174, 229),
                    Color.FromArgb(alpha, 24, 42, 64),
                    new Point(0, 0), new Point(0.2, 1));
            }
            else
            {
                // Старая / классическая тема (без дождя и без блика)
                if (RaindropsLayer != null) RaindropsLayer.Visibility = Visibility.Collapsed;
                if (GlassSheen != null) GlassSheen.Visibility = Visibility.Collapsed;
                Card.Background = new SolidColorBrush(Color.FromArgb(alpha, 5, 11, 20));
                Card.BorderBrush = new SolidColorBrush(Color.FromArgb(alpha, 20, 38, 58));
            }

            ApplyTextColors();
            ApplyCardsBgOpacity();
        }

        private void Profile_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_introPlaying || _injecting || _closing) return;

            if (_profileWin != null)
            {
                _profileWin.Activate();
                return;
            }

            _profileWin = new ProfileWindow(
                _isRainTheme,
                _bgOpacity,
                _hippoOpacity,
                _cardsBgOpacity,
                _bgImagePath,
                _avatarPath,
                _nick,
                isRain => SetTheme(isRain),
                op => SetBgOpacity(op),
                hipOp => SetHippoOpacity(hipOp),
                cardsOp => SetCardsBgOpacity(cardsOp),
                () => OpenBackgroundPicker(),
                ResetBackground,
                () => OpenAvatarPicker(),
                ResetProfile
            )
            {
                Owner = this
            };
            _profileWin.Closed += (s, a) => _profileWin = null;
            _profileWin.Show();
        }

        private void Hippo_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_introPlaying || _injecting || _closing) return;
            OpenPicker("选择挂件", Photos.Hippos, _currentHippo, idx =>
            {
                ApplyHippo(idx);
                ProfileWindow.SaveHippo(idx);
            });
        }

        private void OpenBackgroundPicker()
        {
            int current = Array.FindIndex(Photos.Backgrounds, x => x.Path == _bgImagePath);
            OpenPicker("选择背景图", Photos.Backgrounds, current, idx =>
            {
                SetBgImage(Photos.Backgrounds[idx].Path);
                SaveProfileSettings();
            });
        }

        private void ResetBackground()
        {
            SetBgImage(null);
            SaveProfileSettings();
        }

        private void OpenAvatarPicker()
        {
            int current = Array.FindIndex(Photos.Avatars, x => x.Path == _avatarPath);
            OpenPicker("选择头像", Photos.Avatars, current, idx =>
            {
                _avatarPath = Photos.Avatars[idx].Path;
                _nick = Photos.Avatars[idx].Name;
                ApplyProfile();
                SaveProfileSettings();
            });
        }

        private void ResetProfile()
        {
            _avatarPath = Photos.DefaultAvatarPath;
            _nick = Photos.DefaultNick;
            ApplyProfile();
            SaveProfileSettings();
        }

        private void SaveProfileSettings()
        {
            ProfileWindow.SaveProfile(_avatarPath, _nick, _bgImagePath);
        }

        private void OpenPicker(string title, (string Name, string Path)[] items, int current, Action<int> onSelect)
        {
            if (_pickerWin != null)
            {
                _pickerWin.Close();
                _pickerWin = null;
            }

            _pickerWin = new PickerWindow(title, items, current, onSelect)
            {
                Owner = this
            };
            _pickerWin.Closed += (s, a) => _pickerWin = null;
            _pickerWin.Show();
        }

        // Перетаскивание основного окна мышью (окно выбора игры следует за ним)
        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_introPlaying || _injecting || _closing) return;
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }

    // 图片资源目录：路径均相对于程序集中嵌入的资源
    public static class Photos
    {
        public const string DefaultAvatarPath = "Assets/avatar.png";
        public const string DefaultNick = "LKY0112";

        public static readonly (string Name, string Path)[] Hippos =
        {
            ("16岁离异少妇", "hippo photo/16岁离异少妇.png"),
            ("灯", "hippo photo/灯.png"),
            ("粉色无奶龙", "hippo photo/粉色无奶龙.png"),
            ("竟敢无视灯(☆-ｖ-)", "hippo photo/竟敢无视灯(☆-ｖ-).png"),
        };

        public static readonly (string Name, string Path)[] Backgrounds =
        {
            ("Anon", "backgroundphoto/Anon.jpg"),
            ("Rāna", "backgroundphoto/Rāna.jpg"),
            ("Rikki", "backgroundphoto/Rikki.jpg"),
            ("Soyo", "backgroundphoto/Soyo.jpg"),
        };

        public static readonly (string Name, string Path)[] Avatars =
        {
            ("Amoris", "profile photo/Amoris.webp"),
            ("Awa Subaru", "profile photo/Awa Subaru.jpg"),
            ("Boki", "profile photo/Boki.png"),
            ("Doloris", "profile photo/Doloris.webp"),
            ("Kumiko", "profile photo/Kumiko.png"),
            ("Mortis", "profile photo/Mortis.jpg"),
            ("Nina", "profile photo/Nina.png"),
            ("Oblivionis", "profile photo/Oblivionis.png"),
            ("Timoris", "profile photo/Timoris.webp"),
            ("Yui", "profile photo/Yui.png"),
        };

        private static readonly Dictionary<string, System.Windows.Media.ImageSource> ImageCache =
            new Dictionary<string, System.Windows.Media.ImageSource>();

        public static System.Windows.Media.ImageSource Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            lock (ImageCache)
            {
                if (ImageCache.TryGetValue(path, out var cached))
                {
                    return cached;
                }
            }

            var image = Decode(path);
            if (image != null)
            {
                lock (ImageCache)
                {
                    ImageCache[path] = image;
                }
            }
            return image;
        }

        private static System.Windows.Media.ImageSource Decode(string path)
        {
            try
            {
                object obj = GetResource(path);
                if (obj != null)
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;

                    if (obj is Stream stream)
                    {
                        if (stream.CanSeek && stream.Position != 0) stream.Position = 0;
                        bmp.StreamSource = stream;
                    }
                    else if (obj is byte[] bytes)
                    {
                        bmp.StreamSource = new MemoryStream(bytes);
                    }
                    else
                    {
                        return null;
                    }

                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { }

            try
            {
                string escaped = path.Replace(" ", "%20");
                return new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/" + escaped, UriKind.Absolute));
            }
            catch { }

            return null;
        }

        private static readonly Lazy<System.Resources.ResourceSet> ResourceSetHolder =
            new Lazy<System.Resources.ResourceSet>(() =>
            {
                try
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    var s = asm.GetManifestResourceStream("Loader.g.resources");
                    return s == null ? null : new System.Resources.ResourceSet(s);
                }
                catch
                {
                    return null;
                }
            });

        private static object GetResource(string path)
        {
            var set = ResourceSetHolder.Value;
            if (set == null) return null;

            var parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = EscapeSegment(parts[i]);
            }
            string escapedKey = string.Join("/", parts);

            object obj = set.GetObject(escapedKey, true);
            if (obj != null) return obj;

            return set.GetObject(path, true);
        }

        // 与 WPF 资源键一致的转义规则：保留非保留字符，其余按 UTF-8 百分号编码并小写
        private static string EscapeSegment(string segment)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in segment.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                    c == '-' || c == '.' || c == '_' || c == '~' ||
                    c == '!' || c == '(' || c == ')' || c == '*' || c == '\'')
                {
                    sb.Append(c);
                }
                else
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(c.ToString());
                    foreach (byte b in bytes)
                    {
                        sb.Append('%').Append(b.ToString("x2"));
                    }
                }
            }
            return sb.ToString();
        }
    }
}
