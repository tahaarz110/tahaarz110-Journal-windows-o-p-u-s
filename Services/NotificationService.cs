// ابتدای فایل: Services/NotificationService.cs
// مسیر: /Services/NotificationService.cs

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace TradingJournal.Services
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        Trade,
        System
    }

    public class Notification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }
        public bool IsPersistent { get; set; }
        public string? ActionText { get; set; }
        public Action? Action { get; set; }
        public object? Data { get; set; }
    }

    public interface INotificationService
    {
        void Show(string message, NotificationType type = NotificationType.Info);
        void Show(string title, string message, NotificationType type = NotificationType.Info);
        void Show(Notification notification);
        Task ShowAsync(Notification notification);
        void ShowSnackbar(string message, string? actionText = null, Action? action = null);
        void ShowDialog(string title, string message, NotificationType type = NotificationType.Info);
        Task<bool> ShowConfirmationAsync(string title, string message);
        void ClearAll();
        List<Notification> GetHistory();
        event EventHandler<Notification>? NotificationReceived;
    }

    public class NotificationService : INotificationService, INotifyPropertyChanged
    {
        private static NotificationService? _instance;
        private readonly List<Notification> _notifications;
        private readonly Queue<Notification> _notificationQueue;
        private readonly DispatcherTimer _displayTimer;
        private Window? _notificationWindow;
        private StackPanel? _notificationPanel;
        private readonly object _lock = new object();
        private bool _isProcessing;

        public static NotificationService Instance => _instance ??= new NotificationService();
        public event EventHandler<Notification>? NotificationReceived;
        public event PropertyChangedEventHandler? PropertyChanged;

        private NotificationService()
        {
            _notifications = new List<Notification>();
            _notificationQueue = new Queue<Notification>();
            
            _displayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _displayTimer.Tick += ProcessQueue;
            _displayTimer.Start();

            InitializeNotificationWindow();
        }

        private void InitializeNotificationWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _notificationWindow = new Window
                {
                    Title = "Notifications",
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    Topmost = true,
                    ShowInTaskbar = false,
                    Width = 400,
                    Height = 600,
                    Left = SystemParameters.WorkArea.Width - 420,
                    Top = 20,
                    IsHitTestVisible = false,
                    ShowActivated = false
                };

                _notificationPanel = new StackPanel
                {
                    Margin = new Thickness(10)
                };

                _notificationWindow.Content = _notificationPanel;
            });
        }

        public void Show(string message, NotificationType type = NotificationType.Info)
        {
            Show("اعلان", message, type);
        }

        public void Show(string title, string message, NotificationType type = NotificationType.Info)
        {
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Type = type,
                IsPersistent = type == NotificationType.Error
            };

            Show(notification);
        }

        public void Show(Notification notification)
        {
            lock (_lock)
            {
                _notifications.Add(notification);
                _notificationQueue.Enqueue(notification);
            }

            NotificationReceived?.Invoke(this, notification);
            
            // Log notification
            LogNotification(notification);
        }

        public async Task ShowAsync(Notification notification)
        {
            await Task.Run(() => Show(notification));
        }

        private void ProcessQueue(object? sender, EventArgs e)
        {
            if (_isProcessing || _notificationQueue.Count == 0)
                return;

            _isProcessing = true;

            lock (_lock)
            {
                while (_notificationQueue.Count > 0 && _notificationPanel?.Children.Count < 5)
                {
                    var notification = _notificationQueue.Dequeue();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DisplayNotification(notification);
                    });
                }
            }

            _isProcessing = false;
        }

        private void DisplayNotification(Notification notification)
        {
            if (_notificationPanel == null || _notificationWindow == null)
                return;

            // Create notification card
            var card = new Card
            {
                Margin = new Thickness(0, 0, 0, 10),
                Background = GetBackgroundColor(notification.Type),
                Foreground = Brushes.White,
                UniformCornerRadius = 4,
                Padding = new Thickness(16),
                Width = 380,
                Opacity = 0,
                RenderTransform = new TranslateTransform(400, 0)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Icon and Title
            var headerPanel = new DockPanel();
            
            var icon = new PackIcon
            {
                Kind = GetIcon(notification.Type),
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            DockPanel.SetDock(icon, Dock.Left);
            headerPanel.Children.Add(icon);

            var titleText = new TextBlock
            {
                Text = notification.Title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(titleText);

            Grid.SetRow(headerPanel, 0);
            grid.Children.Add(headerPanel);

            // Message
            var messageText = new TextBlock
            {
                Text = notification.Message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                Opacity = 0.9
            };
            Grid.SetRow(messageText, 1);
            grid.Children.Add(messageText);

            // Action button if provided
            if (!string.IsNullOrEmpty(notification.ActionText) && notification.Action != null)
            {
                var actionButton = new Button
                {
                    Content = notification.ActionText,
                    Style = Application.Current.FindResource("MaterialDesignFlatButton") as Style,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                actionButton.Click += (s, e) => notification.Action.Invoke();
                
                Grid.SetRow(actionButton, 2);
                grid.Children.Add(actionButton);
            }

            card.Content = grid;

            // Add close button
            var closeButton = new Button
            {
                Content = new PackIcon { Kind = PackIconKind.Close, Width = 16, Height = 16 },
                Style = Application.Current.FindResource("MaterialDesignToolButton") as Style,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -8, -8, 0),
                Width = 24,
                Height = 24
            };
            closeButton.Click += (s, e) => RemoveNotification(card);

            var container = new Grid();
            container.Children.Add(card);
            container.Children.Add(closeButton);

            _notificationPanel.Children.Add(container);

            // Animate in
            var slideIn = new DoubleAnimation
            {
                From = 400,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            card.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            card.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Show window if hidden
            if (!_notificationWindow.IsVisible)
            {
                _notificationWindow.Show();
            }

            // Auto-remove after delay (unless persistent)
            if (!notification.IsPersistent)
            {
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    RemoveNotification(container);
                };
                timer.Start();
            }
        }

        private void RemoveNotification(FrameworkElement element)
        {
            if (_notificationPanel == null || !_notificationPanel.Children.Contains(element))
                return;

            // Animate out
            var slideOut = new DoubleAnimation
            {
                To = 400,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };

            slideOut.Completed += (s, e) =>
            {
                _notificationPanel.Children.Remove(element);
                
                // Hide window if no more notifications
                if (_notificationPanel.Children.Count == 0 && _notificationWindow != null)
                {
                    _notificationWindow.Hide();
                }
            };

            if (element is Grid grid && grid.Children[0] is Card card)
            {
                card.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
                card.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        public void ShowSnackbar(string message, string? actionText = null, Action? action = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow?.Content is Grid mainGrid)
                {
                    var snackbar = new Snackbar
                    {
                        Message = { Content = message },
                        IsActive = true
                    };

                    if (!string.IsNullOrEmpty(actionText) && action != null)
                    {
                        snackbar.ActionButtonStyle = Application.Current.FindResource("MaterialDesignSnackbarActionButton") as Style;
                        snackbar.ActionContent = actionText;
                        snackbar.ActionCommand = new RelayCommand(_ => action());
                    }

                    SnackbarMessageQueue queue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
                    snackbar.MessageQueue = queue;
                    
                    Grid.SetRow(snackbar, mainGrid.RowDefinitions.Count - 1);
                    mainGrid.Children.Add(snackbar);

                    queue.Enqueue(message);
                }
            });
        }

        public void ShowDialog(string title, string message, NotificationType type = NotificationType.Info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var icon = type switch
                {
                    NotificationType.Success => MessageBoxImage.Information,
                    NotificationType.Warning => MessageBoxImage.Warning,
                    NotificationType.Error => MessageBoxImage.Error,
                    _ => MessageBoxImage.Information
                };

                MessageBox.Show(message, title, MessageBoxButton.OK, icon);
            });
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                return result == MessageBoxResult.Yes;
            });
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _notifications.Clear();
                _notificationQueue.Clear();
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                _notificationPanel?.Children.Clear();
                _notificationWindow?.Hide();
            });
        }

        public List<Notification> GetHistory()
        {
            lock (_lock)
            {
                return _notifications.ToList();
            }
        }

        private Brush GetBackgroundColor(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                NotificationType.Warning => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                NotificationType.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                NotificationType.Trade => new SolidColorBrush(Color.FromRgb(103, 58, 183)),
                NotificationType.System => new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                _ => new SolidColorBrush(Color.FromRgb(33, 150, 243))
            };
        }

        private PackIconKind GetIcon(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => PackIconKind.CheckCircle,
                NotificationType.Warning => PackIconKind.AlertCircle,
                NotificationType.Error => PackIconKind.CloseCircle,
                NotificationType.Trade => PackIconKind.TrendingUp,
                NotificationType.System => PackIconKind.Cog,
                _ => PackIconKind.Information
            };
        }

        private void LogNotification(Notification notification)
        {
            var logMessage = $"{notification.Title}: {notification.Message}";
            
            switch (notification.Type)
            {
                case NotificationType.Error:
                    Log.Error(logMessage);
                    break;
                case NotificationType.Warning:
                    Log.Warning(logMessage);
                    break;
                default:
                    Log.Information(logMessage);
                    break;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

// پایان فایل: Services/NotificationService.cs