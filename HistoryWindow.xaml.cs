using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;

namespace HeartRateMonitor
{
    public partial class HistoryWindow : FluentWindow
    {
        private UserProfile? _currentProfile;
        private System.Collections.ObjectModel.ObservableCollection<Session> _historicalSessions;
        private DispatcherTimer _resizeTimer;

        public HistoryWindow(UserProfile profile, System.Collections.ObjectModel.ObservableCollection<Session> historicalSessions)
        {
            InitializeComponent();
            _currentProfile = profile;
            _historicalSessions = historicalSessions;

            if (_currentProfile != null)
            {
                TxtSelectedProfile.Text = $"Profil: {_currentProfile.Name}";
                LoadHistoricalSessions();
            }

            _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _resizeTimer.Tick += ResizeTimer_Tick;
        }

        private void LoadHistoricalSessions()
        {
            ListHistoricalSessions.ItemsSource = null;
            if (_currentProfile != null)
            {
                // Sort by StartTime descending
                var sortedSessions = _historicalSessions.OrderByDescending(s => s.StartTime).ToList();
                ListHistoricalSessions.ItemsSource = sortedSessions;
            }
        }

        private void ListHistoricalSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListHistoricalSessions.SelectedItem is Session selectedSession)
            {
                GridHistoryDetails.Visibility = Visibility.Visible;
                GridHistoryDetails.DataContext = new { SelectedSession = selectedSession };
                
                // Draw chart for selected session
                DrawHistoryChart(selectedSession);
            }
            else
            {
                GridHistoryDetails.Visibility = Visibility.Collapsed;
                GridHistoryDetails.DataContext = null;
                ChartCanvasHistory.Children.Clear();
            }
        }

        private void DrawHistoryChart(Session session)
        {
            // Assuming global settings are available via AppSettings.Instance or similar.
            // For now we just call DrawChart with some default display preferences.
                        ChartRenderer.DrawChart(
                ChartCanvasHistory, 
                session.History, 
                0, // 0 duration means show all
                true, // autoYScale
                180, // manualMaxY
                true, // showGrid
                _currentProfile?.Age ?? 30, // age
                false // isActiveSession
            );
        }

        private void BtnDeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (ListHistoricalSessions.SelectedItem is Session selectedSession && _currentProfile != null)
            {
                var result = System.Windows.MessageBox.Show($"Czy na pewno chcesz usunąć trening '{selectedSession.Name}'?", "Usuń Trening", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _historicalSessions.Remove(selectedSession);
                    // TODO: remove from disk if needed
                    LoadHistoricalSessions();
                }
            }
        }

        private void BtnExportCsvHistory_Click(object sender, RoutedEventArgs e)
        {
            if (ListHistoricalSessions.SelectedItem is Session selectedSession)
            {
                try
                {
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = $"trening_{selectedSession.StartTime:yyyyMMdd_HHmm}.csv",
                        DefaultExt = ".csv",
                        Filter = "Pliki CSV (.csv)|*.csv"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Czas (s),BPM,Strefa");
                        foreach (var point in selectedSession.History)
                        {
                            sb.AppendLine($"{point.Time},{point.Bpm},{0}");
                        }
                        File.WriteAllText(dialog.FileName, sb.ToString());
                        System.Windows.MessageBox.Show("Dane zostały poprawnie wyeksportowane.", "Sukces", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Export", "Failed to export historical session", ex);
                    System.Windows.MessageBox.Show($"Błąd podczas eksportu: {ex.Message}", "Błąd", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ChartCanvasHistory_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }

        private void ResizeTimer_Tick(object sender, EventArgs e)
        {
            _resizeTimer.Stop();
            if (ListHistoricalSessions.SelectedItem is Session selectedSession && selectedSession.History.Count > 0)
            {
                DrawHistoryChart(selectedSession);
            }
        }
    }
}
