using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace AresWin
{
    public partial class SettingsWindow : Window
    {
        private MainWindow _mainWindow;
        private bool _isInitializing = true;

        public SettingsWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            Owner = mainWindow;
            InitializeComponent();
            
            // Sync initial states from MainWindow
            SyncFromMainWindow();
            _isInitializing = false;
        }

        private void SyncFromMainWindow()
        {
            if (_mainWindow == null) return;

            // Sync comboboxes by finding them in MainWindow
            var themeSelector = _mainWindow.FindName("ThemeSelector") as ComboBox;
            var accentSelector = _mainWindow.FindName("AccentSelector") as ComboBox;
            
            if (themeSelector != null && ThemeSelector != null) ThemeSelector.SelectedIndex = themeSelector.SelectedIndex;
            if (accentSelector != null && AccentSelector != null) AccentSelector.SelectedIndex = accentSelector.SelectedIndex;

            // Sync toggles
            var btnAutoScan = _mainWindow.FindName("btnAutoScan") as ToggleButton;
            var btnMatrixVisible = _mainWindow.FindName("btnMatrixVisible") as ToggleButton;
            var btnMatrixAnim = _mainWindow.FindName("btnMatrixAnim") as ToggleButton;
            var sliderMatrixSpeed = _mainWindow.FindName("sliderMatrixSpeed") as Slider;
            var txtMatrixSpeed = _mainWindow.FindName("txtMatrixSpeed") as TextBlock;

            if (btnAutoScan != null && this.btnAutoScan != null)
            {
                this.btnAutoScan.IsChecked = btnAutoScan.IsChecked;
                this.btnAutoScan.Content = btnAutoScan.IsChecked == true ? "AUTO SCAN ENABLED" : "AUTO SCAN DISABLED";
            }

            if (btnMatrixVisible != null && this.btnMatrixVisible != null)
            {
                this.btnMatrixVisible.IsChecked = btnMatrixVisible.IsChecked;
                this.btnMatrixVisible.Content = btnMatrixVisible.IsChecked == true ? "MATRIX VISIBLE" : "MATRIX HIDDEN";
            }

            if (btnMatrixAnim != null && this.btnMatrixAnim != null)
            {
                this.btnMatrixAnim.IsChecked = btnMatrixAnim.IsChecked;
                this.btnMatrixAnim.Content = btnMatrixAnim.IsChecked == true ? "MATRIX ANIMATION ON" : "MATRIX ANIMATION OFF";
            }

            if (sliderMatrixSpeed != null && this.sliderMatrixSpeed != null) this.sliderMatrixSpeed.Value = sliderMatrixSpeed.Value;
            if (txtMatrixSpeed != null && this.txtMatrixSpeed != null) this.txtMatrixSpeed.Text = txtMatrixSpeed.Text;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void btnMin_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void btnMax_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ThemeSelector == null) return;
            if (ThemeSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _mainWindow?.ApplyThemePublic(tag);
                
                var themeSelector = _mainWindow?.FindName("ThemeSelector") as ComboBox;
                if (themeSelector != null) themeSelector.SelectedIndex = ThemeSelector.SelectedIndex;
            }
        }

        private void AccentSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (AccentSelector == null) return;
            if (AccentSelector.SelectedItem is ComboBoxItem item && item.Tag is string hexColor)
            {
                _mainWindow?.SetAccentColorPublic(hexColor);
                
                var accentSelector = _mainWindow?.FindName("AccentSelector") as ComboBox;
                if (accentSelector != null) accentSelector.SelectedIndex = AccentSelector.SelectedIndex;
            }
        }

        private void btnAutoScan_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = btnAutoScan?.IsChecked == true;
            // Update settings window label immediately
            if (btnAutoScan != null) btnAutoScan.Content = enabled ? "AUTO SCAN ENABLED" : "AUTO SCAN DISABLED";

            // Update main window
            _mainWindow?.SetAutoScanState(enabled);
            
            var mainBtnAutoScan = _mainWindow?.FindName("btnAutoScan") as ToggleButton;
            if (mainBtnAutoScan != null) mainBtnAutoScan.IsChecked = enabled;
        }

        private void btnMatrixVisible_Click(object sender, RoutedEventArgs e)
        {
            bool visible = btnMatrixVisible?.IsChecked == true;
            if (btnMatrixVisible != null) btnMatrixVisible.Content = visible ? "MATRIX VISIBLE" : "MATRIX HIDDEN";

            _mainWindow?.SetMatrixVisible(visible);
            
            var mainBtnMatrixVisible = _mainWindow?.FindName("btnMatrixVisible") as ToggleButton;
            if (mainBtnMatrixVisible != null) mainBtnMatrixVisible.IsChecked = visible;
        }

        private void btnMatrixAnim_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = btnMatrixAnim?.IsChecked == true;
            if (btnMatrixAnim != null) btnMatrixAnim.Content = enabled ? "MATRIX ANIMATION ON" : "MATRIX ANIMATION OFF";

            _mainWindow?.SetMatrixAnimation(enabled);
            
            var mainBtnMatrixAnim = _mainWindow?.FindName("btnMatrixAnim") as ToggleButton;
            if (mainBtnMatrixAnim != null) mainBtnMatrixAnim.IsChecked = enabled;
        }

        private void sliderMatrixSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return; // skip events fired during InitializeComponent()

            _mainWindow?.SetMatrixSpeed(e.NewValue);

            var mainSlider = _mainWindow?.FindName("sliderMatrixSpeed") as Slider;
            var mainTxtMatrixSpeed = _mainWindow?.FindName("txtMatrixSpeed") as TextBlock;

            if (mainSlider != null) mainSlider.Value = e.NewValue;
            if (mainTxtMatrixSpeed != null) mainTxtMatrixSpeed.Text = $"{e.NewValue:0.0}x";

            if (txtMatrixSpeed != null)
                txtMatrixSpeed.Text = $"{e.NewValue:0.0}x";
        }
    }
}