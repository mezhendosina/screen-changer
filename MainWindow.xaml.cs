using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ScreenChanger.Models;
using ScreenChanger.Services;
using Wpf.Ui.Controls;
using WpfApplication  = System.Windows.Application;
using MediaColor      = System.Windows.Media.Color;
using WpfBrush       = System.Windows.Media.Brush;
using WpfButton      = System.Windows.Controls.Button;
using WpfTextBlock   = System.Windows.Controls.TextBlock;
using WpfFontFamily  = System.Windows.Media.FontFamily;
using WpfRectangle   = System.Windows.Shapes.Rectangle;
using WpfCanvas      = System.Windows.Controls.Canvas;

namespace ScreenChanger;

public partial class MainWindow : FluentWindow
{
    private readonly HotkeyService _hotkeyService = new();
    private System.Windows.Forms.NotifyIcon? _tray;
    private MonitorInfo[] _monitors = [];
    private readonly List<WpfButton> _cardButtons = new();

    private static readonly uint[] HotkeyVirtualKeys =
        [0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39]; // 1–9

    public MainWindow()
    {
        InitializeComponent();
        _monitors = DisplayConfigService.GetConnectedMonitors();
        BuildCards();
        SetupTray();
        HighlightCard(DisplayConfigService.ActiveIndex);
    }

    private void BuildCards()
    {
        Width = Math.Max(480, _monitors.Length * 214);
        CardsPanel.ColumnDefinitions.Clear();
        CardsPanel.Children.Clear();
        _cardButtons.Clear();

        for (int i = 0; i < _monitors.Length; i++)
        {
            if (i > 0)
                CardsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            CardsPanel.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 180 });

            var btn = CreateCard(_monitors[i]);
            Grid.SetColumn(btn, i == 0 ? 0 : i * 2);
            CardsPanel.Children.Add(btn);
            _cardButtons.Add(btn);
        }
    }

    private WpfButton CreateCard(MonitorInfo monitor)
    {
        var icon = BuildMonitorIcon();
        var title = new WpfTextBlock
        {
            Text = monitor.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = (WpfBrush)FindResource("TextFillColorPrimaryBrush")
        };

        string hotkeyText = monitor.Index < HotkeyVirtualKeys.Length
            ? $"Ctrl + Alt + {monitor.Index + 1}"
            : "";
        var badge = new Border
        {
            Style = (Style)FindResource("KeyBadgeStyle"),
            Child = new WpfTextBlock
            {
                Text = hotkeyText,
                FontSize = 12,
                FontFamily = new WpfFontFamily("Cascadia Code, Consolas, Courier New"),
                Foreground = (WpfBrush)FindResource("TextFillColorSecondaryBrush")
            }
        };

        var stack = new StackPanel();
        stack.Children.Add(icon);
        stack.Children.Add(title);
        if (!string.IsNullOrEmpty(hotkeyText)) stack.Children.Add(badge);

        var btn = new WpfButton
        {
            Style = (Style)FindResource("MonitorCardStyle"),
            Content = stack,
            Tag = monitor.Index
        };
        btn.Click += Card_Click;
        return btn;
    }

    private static Viewbox BuildMonitorIcon()
    {
        var canvas = new Canvas { Width = 100, Height = 84 };

        var body = new WpfRectangle
        {
            Width = 100, Height = 66, RadiusX = 6, RadiusY = 6,
            Fill = new SolidColorBrush(MediaColor.FromRgb(0x3a, 0x3a, 0x3a)),
            Stroke = new SolidColorBrush(MediaColor.FromRgb(0x58, 0x58, 0x58)),
            StrokeThickness = 2
        };
        canvas.Children.Add(body);

        var screen = new WpfRectangle
        {
            RadiusX = 3, RadiusY = 3,
            Width = 84, Height = 50,
            Fill = new SolidColorBrush(MediaColor.FromRgb(0x0d, 0x11, 0x17))
        };
        Canvas.SetLeft(screen, 8); Canvas.SetTop(screen, 7);
        canvas.Children.Add(screen);

        AddLine(canvas, 14, 17, 42, "#ffffff20");
        AddLine(canvas, 14, 24, 30, "#ffffff15");
        AddLine(canvas, 14, 31, 54, "#ffffff15");
        AddLine(canvas, 14, 38, 24, "#ffffff0a");

        var neck = new WpfRectangle { Width = 8, Height = 10, Fill = new SolidColorBrush(MediaColor.FromRgb(0x3a, 0x3a, 0x3a)) };
        Canvas.SetLeft(neck, 46); Canvas.SetTop(neck, 66);
        canvas.Children.Add(neck);

        var stand = new WpfRectangle
        {
            Width = 40, Height = 6, RadiusX = 3, RadiusY = 3,
            Fill = new SolidColorBrush(MediaColor.FromRgb(0x3a, 0x3a, 0x3a)),
            Stroke = new SolidColorBrush(MediaColor.FromRgb(0x58, 0x58, 0x58)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(stand, 30); Canvas.SetTop(stand, 76);
        canvas.Children.Add(stand);

        return new Viewbox { Width = 74, Height = 62, Child = canvas, Margin = new Thickness(0, 0, 0, 14) };
    }

    private static void AddLine(Canvas canvas, double left, double top, double width, string colorHex)
    {
        var c = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
        var rect = new WpfRectangle
        {
            Width = width, Height = 3, RadiusX = 2, RadiusY = 2,
            Fill = new SolidColorBrush(c)
        };
        Canvas.SetLeft(rect, left); Canvas.SetTop(rect, top);
        canvas.Children.Add(rect);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        _hotkeyService.Initialize(helper.Handle);

        uint mod = HotkeyService.MOD_CONTROL | HotkeyService.MOD_ALT;
        for (int i = 0; i < Math.Min(_monitors.Length, HotkeyVirtualKeys.Length); i++)
        {
            int idx = i;
            _hotkeyService.Register(i + 1, mod, HotkeyVirtualKeys[i], () => DoSwitch(_monitors[idx]));
        }
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/icon.ico");
        using var stream = WpfApplication.GetResourceStream(uri)!.Stream;
        return new System.Drawing.Icon(stream, new System.Drawing.Size(16, 16));
    }

    private void SetupTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "Screen Changer",
            Visible = true,
            Icon = LoadTrayIcon()
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => Dispatcher.Invoke(ShowApp));
        menu.Items.Add("-");

        for (int i = 0; i < _monitors.Length; i++)
        {
            int idx = i;
            string hotkey = idx < HotkeyVirtualKeys.Length ? $"  (Ctrl+Alt+{idx + 1})" : "";
            menu.Items.Add(_monitors[i].Name + hotkey, null,
                (_, _) => DoSwitch(_monitors[idx]));
        }

        menu.Items.Add("-");

        var startupItem = new System.Windows.Forms.ToolStripMenuItem("Автозапуск")
        {
            CheckOnClick = true,
            Checked = StartupService.IsEnabled()
        };
        startupItem.CheckedChanged += (_, _) =>
        {
            if (startupItem.Checked) StartupService.Enable();
            else StartupService.Disable();
        };
        menu.Items.Add(startupItem);

        menu.Items.Add("-");
        menu.Items.Add("Выход", null, (_, _) => Dispatcher.Invoke(() => WpfApplication.Current.Shutdown()));

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowApp);
    }

    private void ShowApp()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton btn && btn.Tag is int idx && idx < _monitors.Length)
            DoSwitch(_monitors[idx]);
    }

    private void DoSwitch(MonitorInfo monitor)
    {
        DisplayConfigService.ActivateOnly(monitor);
        Dispatcher.Invoke(() =>
        {
            HighlightCard(DisplayConfigService.ActiveIndex);
            NotifyTray(monitor.Name);
        });
    }

    private void HighlightCard(int index)
    {
        for (int i = 0; i < _cardButtons.Count; i++)
            SetSelected(_cardButtons[i], i == index);

        StatusText.Text = index >= 0 && index < _monitors.Length
            ? $"Активен: {_monitors[index].Name}"
            : "Активен: —";
    }

    private static void SetSelected(WpfButton btn, bool selected)
    {
        if (selected)
        {
            btn.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0, 120, 212));
            btn.BorderThickness = new Thickness(2);
            btn.Effect = new DropShadowEffect
            {
                Color = MediaColor.FromRgb(0, 120, 212),
                BlurRadius = 16,
                ShadowDepth = 0,
                Opacity = 0.5
            };
        }
        else
        {
            btn.SetResourceReference(BorderBrushProperty, "CardStrokeColorDefaultBrush");
            btn.BorderThickness = new Thickness(1.5);
            btn.Effect = null;
        }
    }

    private void NotifyTray(string name)
    {
        _tray?.ShowBalloonTip(1800, "Screen Changer", $"Переключено: {name}",
            System.Windows.Forms.ToolTipIcon.None);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkeyService.Dispose();
        _tray?.Dispose();
        base.OnClosed(e);
    }
}
