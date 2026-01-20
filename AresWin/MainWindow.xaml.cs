using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json; // Requires .NET Core 3.1+ or .NET 5/6/8
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.ComponentModel;

namespace AresWin
{
    public partial class MainWindow : Window
    {
        // --- DATA MODEL ---
        public class ConnectionInfo
        {
            public int PID { get; set; }
            public string ProcessName { get; set; } = "";
            public string RemoteAddress { get; set; } = "";
            public int RemotePort { get; set; }
            public string Location { get; set; } = "";
            public string Network { get; set; } = "";
            public string Country { get; set; } = "";
            public string Risk { get; set; } = "LOW";
            public string State { get; set; } = "";
        }

        // --- STATE & CACHE ---
        private ObservableCollection<ConnectionInfo> _connections = new ObservableCollection<ConnectionInfo>();
        private Dictionary<string, ConnectionInfo> _geoCache = new Dictionary<string, ConnectionInfo>();

        private bool _isScanning = false;
        private string? _currentSortMember;
        private ListSortDirection _currentSortDirection = ListSortDirection.Ascending;
        private DispatcherTimer? _scanTimer;
        private DispatcherTimer? _animTimer;
        private List<MatrixStream> _matrixStreams = new List<MatrixStream>();
        private Random _rng = new Random();
        private double _matrixSpeedMultiplier = 1.0;
        private bool _isApplyingTheme;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitMatrixAnimation();
            InitializeSettings();

            // Set up auto-scan timer (10 seconds)
            _scanTimer = new DispatcherTimer();
            _scanTimer.Interval = TimeSpan.FromSeconds(10);
            _scanTimer.Tick += (s, ev) =>
            {
                if (btnPause.IsChecked == false) RefreshGrid();
            };
            _scanTimer.Start();

            // Initial Scan
            RefreshGrid();
        }

        // --- MATRIX ANIMATION ENGINE ---
        private class MatrixStream
        {
            public TextBlock Block { get; set; } = new TextBlock();
            public double Y { get; set; }
            public double Speed { get; set; }
            public TranslateTransform Transform { get; set; } = new TranslateTransform();
        }

        private void InitMatrixAnimation()
        {
            double screenHeight = 900;
            SolidColorBrush brush = (SolidColorBrush)FindResource("TronCyan");
            DropShadowEffect glow = (DropShadowEffect)FindResource("TextGlow");

            // Create columns across width (1550px)
            for (int x = 0; x < 1550; x += 20)
            {
                var block = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 14,
                    Foreground = brush,
                    Effect = glow,
                    TextAlignment = TextAlignment.Center,
                    IsHitTestVisible = false,
                    RenderTransform = new TranslateTransform()
                };

                Canvas.SetLeft(block, x);
                MatrixCanvas.Children.Add(block);

                var stream = new MatrixStream
                {
                    Block = block,
                    Transform = (TranslateTransform)block.RenderTransform
                };

                ResetStream(stream, true, screenHeight);
                _matrixStreams.Add(stream);
            }

            // Animation Loop (~30 FPS)
            _animTimer = new DispatcherTimer();
            _animTimer.Interval = TimeSpan.FromMilliseconds(33);
            _animTimer.Tick += (s, e) =>
            {
                if (btnPause.IsChecked == true) return;
                foreach (var stream in _matrixStreams) TickStream(stream, screenHeight);
            };
            _animTimer.Start();
        }

        private void ResetStream(MatrixStream s, bool startRandom, double height)
        {
            string chars = "01XY";
            int len = _rng.Next(4, 12);
            string text = "";
            for (int i = 0; i < len; i++) text += chars[_rng.Next(chars.Length)] + "\n";
            s.Block.Text = text;
            s.Block.Opacity = _rng.Next(2, 6) / 10.0;
            s.Speed = _rng.Next(4, 12);

            if (startRandom) s.Y = -_rng.Next(0, (int)height);
            else s.Y = -(s.Block.ActualHeight + _rng.Next(50, 300));

            s.Transform.Y = s.Y;
        }

        private void TickStream(MatrixStream s, double height)
        {
            s.Y += s.Speed * _matrixSpeedMultiplier;
            if (s.Y > height) ResetStream(s, false, height);
            else s.Transform.Y = s.Y;
        }

        // --- SCANNING LOGIC ---
        private async void RefreshGrid()
        {
            if (_isScanning) return;
            _isScanning = true;

            txtTarget.Text = "SCANNING NETWORK...";
            txtTarget.Foreground = Brushes.Yellow;
            SetAgentStatus("CONNECTING", (Brush)FindResource("TronOrange"), (Effect)FindResource("GlowOrange"));

            try
            {
                // Run heavy netstat parsing in background task
                var connections = await Task.Run(() => GetNetstatConnections());

                _connections.Clear();
                bool showLan = btnLan.IsChecked == true;

                foreach (var conn in connections)
                {
                    bool isLan = IsLanIp(conn.RemoteAddress);
                    if (isLan && !showLan) continue;

                    // Enrich with Geolocation
                    await EnrichGeoInfo(conn);

                    _connections.Add(conn);
                }

                // Update UI Data
                GroupData();

                txtStatus.Text = "ONLINE";
                txtCount.Text = $" | NODES ACTIVE: {_connections.Count}";
                txtTarget.Text = "SYSTEM READY";
                txtTarget.Foreground = (SolidColorBrush)FindResource("TronCyan");
                SetAgentStatus("CONNECTED", (Brush)FindResource("TronGreen"), (Effect)FindResource("GlowGreen"));
            }
            catch (Exception ex)
            {
                // Handle errors silently or log
                Debug.WriteLine(ex.Message);
                SetAgentStatus("DISCONNECTED", (Brush)FindResource("TronRed"), (Effect)FindResource("GlowRed"));
            }
            finally
            {
                _isScanning = false;
            }
        }

        // Parse 'netstat -ano' to get Process IDs and IPs
        private List<ConnectionInfo> GetNetstatConnections()
        {
            var list = new List<ConnectionInfo>();
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-ano",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // Split by whitespace
                    var parts = Regex.Split(line.Trim(), "\\s+");
                    if (parts.Length < 5) continue;
                    if (parts[0] != "TCP") continue;

                    string remote = parts[2];
                    string state = parts[3];
                    int pid = int.Parse(parts[4]);

                    if (state != "ESTABLISHED") continue;

                    // Parse IP:Port (handle IPv6 brackets if needed, basic logic here)
                    int lastColon = remote.LastIndexOf(':');
                    if (lastColon == -1) continue;
                    string ip = remote.Substring(0, lastColon);
                    string portStr = remote.Substring(lastColon + 1);
                    int port = int.Parse(portStr);

                    if (ip == "127.0.0.1" || ip == "[::1]" || ip == "0.0.0.0") continue;

                    // Get Process Name
                    string pName = "SYSTEM";
                    try
                    {
                        var p = Process.GetProcessById(pid);
                        pName = p.ProcessName.ToUpper();
                    }
                    catch { }

                    // Determine Risk
                    string risk = "LOW";
                    if (port == 4444 || port == 3389 || port == 1337) risk = "HIGH";
                    else if (port == 445 || port == 8080) risk = "MODERATE";

                    list.Add(new ConnectionInfo
                    {
                        PID = pid,
                        ProcessName = pName,
                        RemoteAddress = ip,
                        RemotePort = port,
                        State = state,
                        Risk = risk
                    });
                }
            }
            catch { }
            return list;
        }

        private bool IsLanIp(string ip)
        {
            return ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172.");
        }

        // Fill in Location/Network/Country
        private async Task EnrichGeoInfo(ConnectionInfo conn)
        {
            string ip = conn.RemoteAddress;

            // Check Cache first
            if (_geoCache.ContainsKey(ip))
            {
                var cached = _geoCache[ip];
                conn.Location = cached.Location;
                conn.Network = cached.Network;
                conn.Country = cached.Country;
                return;
            }

            // Local Check
            if (IsLanIp(ip))
            {
                conn.Location = "LOCAL NETWORK";
                conn.Network = "LAN";
                conn.Country = "LAN";
                return;
            }

            // API Call
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(800);
                    // Standard API call without params
                    var json = await client.GetStringAsync($"http://ip-api.com/json/{ip}");

                    // Parse JSON using System.Text.Json
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                        {
                            string city = root.GetProperty("city").GetString();
                            string countryCode = root.GetProperty("countryCode").GetString();
                            string isp = root.GetProperty("isp").GetString();

                            conn.Location = $"{city}, {countryCode}";
                            conn.Network = isp;
                            conn.Country = countryCode;

                            // Cache logic: we store a dummy ConnectionInfo holding the geo data
                            _geoCache[ip] = new ConnectionInfo
                            {
                                Location = conn.Location,
                                Network = conn.Network,
                                Country = conn.Country
                            };
                            return;
                        }
                    }
                }
            }
            catch { }

            // Failover
            conn.Location = "UNKNOWN NET";
            conn.Network = "UNKNOWN";
            conn.Country = "UNK";
            _geoCache[ip] = new ConnectionInfo { Location = "UNKNOWN NET", Network = "UNKNOWN", Country = "UNK" };
        }

        private void GroupData()
        {
            // Bind data
            gridConnections.ItemsSource = _connections;

            // Set up Grouping
            ICollectionView view = CollectionViewSource.GetDefaultView(gridConnections.ItemsSource);
            view.GroupDescriptions.Clear();

            if (btnGroup.IsChecked == true)
            {
                // Group by ProcessName
                view.GroupDescriptions.Add(new PropertyGroupDescription("ProcessName"));
                btnGroup.Content = "UNGROUP APPS";
            }
            else
            {
                btnGroup.Content = "GROUP APPS";
            }

            ApplySort();
        }

        // --- UI EVENT HANDLERS ---
        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void btnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void btnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void btnMax_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e) => RefreshGrid();
        private void btnGroup_Click(object sender, RoutedEventArgs e) => RefreshGrid();
        private void btnLan_Click(object sender, RoutedEventArgs e) => RefreshGrid();

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (btnPause.IsChecked == true)
            {
                txtTarget.Text = "SYSTEM PAUSED";
                txtTarget.Foreground = (Brush)FindResource("TronOrange");
            }
            else
            {
                RefreshGrid();
            }
        }

        private void InitializeSettings()
        {
            _isApplyingTheme = true;
            ThemeSelector.SelectedIndex = 0;
            AccentSelector.SelectedIndex = 0;
            btnAutoScan.IsChecked = true;
            btnMatrixVisible.IsChecked = true;
            btnMatrixAnim.IsChecked = true;
            sliderMatrixSpeed.Value = 1.0;
            txtMatrixSpeed.Text = "1.0x";
            _isApplyingTheme = false;

            ApplyTheme("Ares");
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingTheme) return;
            if (ThemeSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                ApplyTheme(tag);
            }
        }

        private void AccentSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingTheme) return;
            if (AccentSelector.SelectedItem is ComboBoxItem item && item.Tag is string hexColor)
            {
                SetAccentColor(hexColor);
            }
        }

        private void btnAutoScan_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = btnAutoScan.IsChecked == true;
            btnAutoScan.Content = enabled ? "AUTO SCAN ENABLED" : "AUTO SCAN DISABLED";
            if (_scanTimer != null)
            {
                if (enabled) _scanTimer.Start();
                else _scanTimer.Stop();
            }
        }

        private void btnMatrixVisible_Click(object sender, RoutedEventArgs e)
        {
            bool visible = btnMatrixVisible.IsChecked == true;
            btnMatrixVisible.Content = visible ? "MATRIX VISIBLE" : "MATRIX HIDDEN";
            MatrixCanvas.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void btnMatrixAnim_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = btnMatrixAnim.IsChecked == true;
            btnMatrixAnim.Content = enabled ? "MATRIX ANIMATION ON" : "MATRIX ANIMATION OFF";
            if (_animTimer != null)
            {
                if (enabled) _animTimer.Start();
                else _animTimer.Stop();
            }
        }

        private void sliderMatrixSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _matrixSpeedMultiplier = e.NewValue;
            if (txtMatrixSpeed != null)
            {
                txtMatrixSpeed.Text = $"{_matrixSpeedMultiplier:0.0}x";
            }
        }

        private void ApplyTheme(string themeTag)
        {
            _isApplyingTheme = true;

            switch (themeTag)
            {
                case "WindowsDark":
                    SetAccentColor("#0078D4", updateAccentSelector: true);
                    SetBrushColor("AppBackgroundBrush", "#1E1E1E");
                    SetBrushColor("CanvasBackgroundBrush", "#151515");
                    SetBrushColor("PanelBackgroundBrush", "#252526");
                    SetBrushColor("PanelBorderBrush", "#3C3C3C");
                    SetBrushColor("TextPrimaryBrush", "#FFFFFF");
                    SetBrushColor("TextSecondaryBrush", "#C8C8C8");
                    break;
                case "WindowsLight":
                    SetAccentColor("#0078D4", updateAccentSelector: true);
                    SetBrushColor("AppBackgroundBrush", "#F3F3F3");
                    SetBrushColor("CanvasBackgroundBrush", "#F8F8F8");
                    SetBrushColor("PanelBackgroundBrush", "#FFFFFF");
                    SetBrushColor("PanelBorderBrush", "#D0D0D0");
                    SetBrushColor("TextPrimaryBrush", "#111111");
                    SetBrushColor("TextSecondaryBrush", "#555555");
                    break;
                default:
                    SetAccentColor("#00EAFF", updateAccentSelector: true);
                    SetBrushColor("AppBackgroundBrush", "#000510");
                    SetBrushColor("CanvasBackgroundBrush", "#000205");
                    SetBrushColor("PanelBackgroundBrush", "#050B1A");
                    SetBrushColor("PanelBorderBrush", "#004488");
                    SetBrushColor("TextPrimaryBrush", "#FFFFFF");
                    SetBrushColor("TextSecondaryBrush", "#8899AA");
                    break;
            }

            _isApplyingTheme = false;
        }

        private void SetAccentColor(string hexColor, bool updateAccentSelector = false)
        {
            SetBrushColor("TronCyan", hexColor);
            SetBrushColor("TronBlue", hexColor);

            SetDropShadowColor("GlowCyan", hexColor);
            SetDropShadowColor("TextGlow", hexColor);

            if (updateAccentSelector)
            {
                SelectComboBoxItemByTag(AccentSelector, hexColor);
            }
        }

        private void SetBrushColor(string resourceKey, string hexColor)
        {
            if (FindResource(resourceKey) is SolidColorBrush brush)
            {
                brush.Color = (Color)ColorConverter.ConvertFromString(hexColor);
            }
        }

        private void SetDropShadowColor(string resourceKey, string hexColor)
        {
            if (FindResource(resourceKey) is DropShadowEffect effect)
            {
                effect.Color = (Color)ColorConverter.ConvertFromString(hexColor);
            }
        }

        private static void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            if (comboBox == null) return;
            foreach (var item in comboBox.Items)
            {
                if (item is ComboBoxItem comboItem && string.Equals(comboItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = comboItem;
                    return;
                }
            }
        }

        private void gridConnections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gridConnections.SelectedItem is ConnectionInfo item)
            {
                string colorRes = item.Risk == "HIGH" ? "TronRed" : "TronCyan";
                txtTarget.Foreground = (Brush)FindResource(colorRes);
                txtTarget.Text = $"TARGET LOCKED: {item.ProcessName} (PID:{item.PID}) :: {item.RemoteAddress} [{item.Location}] :: THREAT [{item.Risk}]";
            }
        }

        private void gridConnections_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string sortMember = e.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(sortMember)) return;

            _currentSortDirection = e.Column.SortDirection != ListSortDirection.Ascending
                ? ListSortDirection.Ascending
                : ListSortDirection.Descending;
            _currentSortMember = sortMember;

            ApplySort();
        }

        private void btnKill_Click(object sender, RoutedEventArgs e)
        {
            if (gridConnections.SelectedItem is ConnectionInfo item)
            {
                try
                {
                    Process.GetProcessById(item.PID).Kill();
                    MessageBox.Show($"Terminated {item.ProcessName}", "DEFENSE", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshGrid();
                }
                catch { MessageBox.Show("Failed to terminate. Admin rights required?", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void btnBlock_Click(object sender, RoutedEventArgs e)
        {
            if (gridConnections.SelectedItem is ConnectionInfo item)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"advfirewall firewall add rule name=\"ARES_BLOCK_{item.RemoteAddress}\" dir=out action=block remoteip={item.RemoteAddress}",
                        CreateNoWindow = true,
                        UseShellExecute = true,
                        Verb = "runas" // Request Admin
                    };
                    Process.Start(psi);
                    MessageBox.Show($"Blocked IP {item.RemoteAddress}", "FIREWALL", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { MessageBox.Show("Failed to block IP.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void btnAI_Click(object sender, RoutedEventArgs e)
        {
            if (gridConnections.SelectedItem is ConnectionInfo item)
            {
                string prompt = $"Analyze Network Connection: Process '{item.ProcessName}', IP {item.RemoteAddress}, Network '{item.Network}', Port {item.RemotePort}. Risk Assessment?";
                Clipboard.SetText(prompt);
                MessageBox.Show("Prompt copied to clipboard.\nOpening AI...", "NEURAL LINK", MessageBoxButton.OK, MessageBoxImage.Information);

                try
                {
                    Process.Start(new ProcessStartInfo { FileName = "https://gemini.google.com/app", UseShellExecute = true });
                }
                catch { }
            }
        }

        private void ApplySort()
        {
            var view = CollectionViewSource.GetDefaultView(gridConnections.ItemsSource) as ListCollectionView;
            if (view == null)
            {
                return;
            }

            foreach (var column in gridConnections.Columns)
            {
                column.SortDirection = column.SortMemberPath == _currentSortMember ? _currentSortDirection : null;
            }

            if (string.IsNullOrWhiteSpace(_currentSortMember))
            {
                view.CustomSort = null;
                return;
            }

            view.CustomSort = new ConnectionInfoComparer(_currentSortMember, _currentSortDirection);
        }

        private void SetAgentStatus(string status, Brush color, Effect glow)
        {
            txtAgentStatus.Text = status;
            txtAgentStatus.Foreground = color;
            agentStatusDot.Fill = color;
            agentStatusDot.Effect = glow;
        }

        private sealed class ConnectionInfoComparer : IComparer
        {
            private readonly string _member;
            private readonly ListSortDirection _direction;

            public ConnectionInfoComparer(string member, ListSortDirection direction)
            {
                _member = member;
                _direction = direction;
            }

            public int Compare(object? x, object? y)
            {
                var left = x as ConnectionInfo;
                var right = y as ConnectionInfo;
                int result = CompareMember(left, right);
                return _direction == ListSortDirection.Ascending ? result : -result;
            }

            private int CompareMember(ConnectionInfo? left, ConnectionInfo? right)
            {
                switch (_member)
                {
                    case nameof(ConnectionInfo.RemoteAddress):
                        return CompareIp(left?.RemoteAddress, right?.RemoteAddress);
                    case nameof(ConnectionInfo.PID):
                        return (left?.PID ?? 0).CompareTo(right?.PID ?? 0);
                    case nameof(ConnectionInfo.RemotePort):
                        return (left?.RemotePort ?? 0).CompareTo(right?.RemotePort ?? 0);
                    case nameof(ConnectionInfo.Location):
                        return CompareText(left?.Location, right?.Location);
                    case nameof(ConnectionInfo.Network):
                        return CompareText(left?.Network, right?.Network);
                    case nameof(ConnectionInfo.Risk):
                        return CompareRisk(left?.Risk, right?.Risk);
                    case nameof(ConnectionInfo.State):
                        return CompareText(left?.State, right?.State);
                    case nameof(ConnectionInfo.ProcessName):
                        return CompareText(left?.ProcessName, right?.ProcessName);
                    default:
                        return CompareText(left?.ToString(), right?.ToString());
                }
            }

            private static int CompareText(string? left, string? right)
            {
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            }

            private static int CompareRisk(string? left, string? right)
            {
                return RiskScore(left).CompareTo(RiskScore(right));
            }

            private static int RiskScore(string? risk)
            {
                return risk switch
                {
                    "HIGH" => 3,
                    "MODERATE" => 2,
                    "LOW" => 1,
                    _ => 0
                };
            }

            private static int CompareIp(string? left, string? right)
            {
                if (left == right) return 0;
                if (left == null) return -1;
                if (right == null) return 1;

                left = left.Trim('[', ']');
                right = right.Trim('[', ']');

                if (IPAddress.TryParse(left, out var leftIp) && IPAddress.TryParse(right, out var rightIp))
                {
                    byte[] leftBytes = leftIp.GetAddressBytes();
                    byte[] rightBytes = rightIp.GetAddressBytes();
                    int lengthCompare = leftBytes.Length.CompareTo(rightBytes.Length);
                    if (lengthCompare != 0) return lengthCompare;

                    for (int i = 0; i < leftBytes.Length; i++)
                    {
                        int byteCompare = leftBytes[i].CompareTo(rightBytes[i]);
                        if (byteCompare != 0) return byteCompare;
                    }
                    return 0;
                }

                return CompareText(left, right);
            }
        }
    }
}
