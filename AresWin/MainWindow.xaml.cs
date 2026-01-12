using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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
        private DispatcherTimer? _scanTimer;
        private DispatcherTimer? _animTimer;
        private List<MatrixStream> _matrixStreams = new List<MatrixStream>();
        private Random _rng = new Random();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitMatrixAnimation();

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
            s.Y += s.Speed;
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
            }
            catch (Exception ex)
            {
                // Handle errors silently or log
                Debug.WriteLine(ex.Message);
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

        private void gridConnections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gridConnections.SelectedItem is ConnectionInfo item)
            {
                string colorRes = item.Risk == "HIGH" ? "TronRed" : "TronCyan";
                txtTarget.Foreground = (Brush)FindResource(colorRes);
                txtTarget.Text = $"TARGET LOCKED: {item.ProcessName} (PID:{item.PID}) :: {item.RemoteAddress} [{item.Location}] :: THREAT [{item.Risk}]";
            }
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
    }
}