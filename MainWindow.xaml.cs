using System;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using Point = System.Windows.Point;

namespace Group_Project
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private bool _isDraggingHourly;
        private Point _hourlyDragStart;
        private double _hourlyStartOffset;

        private void toggleUnit_Checked(object sender, RoutedEventArgs e)
        {
            toggleUnit.Content = "°F";
        }

        private void toggleUnit_Unchecked(object sender, RoutedEventArgs e)
        {
            toggleUnit.Content = "°C";
        }

        private async void btnGetWeather_Click(object sender, RoutedEventArgs e)
        {
            string cityName = txtCity.Text.Trim();
            if (string.IsNullOrEmpty(cityName))
            {
                MessageBox.Show("Please enter a city name.");
                return;
            }

            // Determine unit
            bool isFahrenheit = toggleUnit.IsChecked == true;
            string unit = isFahrenheit ? "fahrenheit" : "celsius";
            string unitSymbol = isFahrenheit ? "°F" : "°C";

            // 1) Geocode
            string geoUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(cityName)}&format=json&limit=1";
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Group_Project_App");

                var geoResp = await client.GetAsync(geoUrl);
                geoResp.EnsureSuccessStatusCode();
                var geoJson = JArray.Parse(await geoResp.Content.ReadAsStringAsync());
                if (geoJson.Count == 0)
                {
                    MessageBox.Show("City not found.");
                    return;
                }

                string lat = geoJson[0]["lat"].ToString();
                string lon = geoJson[0]["lon"].ToString();

                // 2) Weather API
                string weatherUrl =
                    $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                    $"&hourly=temperature_2m,precipitation_probability" +
                    $"&daily=temperature_2m_max,temperature_2m_min" +
                    $"&current_weather=true" +
                    $"&temperature_unit={unit}" +
                    $"&forecast_days=7" +
                    $"&timezone=auto";

                var weatherResp = await client.GetAsync(weatherUrl);
                weatherResp.EnsureSuccessStatusCode();
                var weatherJson = JObject.Parse(await weatherResp.Content.ReadAsStringAsync());

                // --- Populate 7‑day cards ---
                DailyPanel.Children.Clear();
                var dailyTimes = (JArray)weatherJson["daily"]["time"];
                var dailyMaxTemps = (JArray)weatherJson["daily"]["temperature_2m_max"];
                var dailyMinTemps = (JArray)weatherJson["daily"]["temperature_2m_min"];
                for (int i = 0; i < dailyTimes.Count; i++)
                {
                    string date = dailyTimes[i].ToString();
                    double hi = dailyMaxTemps[i].ToObject<double>();
                    double lo = dailyMinTemps[i].ToObject<double>();
                    AddDailyCard(date, hi, lo, unitSymbol);
                }

                // --- Populate hourly strip ---
                HourlyPanel.Children.Clear();
                string curTimeStr = weatherJson["current_weather"]["time"].ToString();
                DateTime curTime = DateTime.Parse(curTimeStr);
                // show current hour first
                double curTemp = weatherJson["current_weather"]["temperature"].ToObject<double>();
                double curPrecip = 0;
                var hoursTimes = (JArray)weatherJson["hourly"]["time"];
                var hoursTemps = (JArray)weatherJson["hourly"]["temperature_2m"];
                var hoursPrecip = (JArray)weatherJson["hourly"]["precipitation_probability"];
                for (int j = 0; j < hoursTimes.Count; j++)
                {
                    if (DateTime.Parse(hoursTimes[j].ToString()) == curTime)
                    {
                        curPrecip = hoursPrecip[j].ToObject<double>();
                        break;
                    }
                }
                AddHourlyCard(curTime, curTemp, curPrecip, unitSymbol);

                // then next 24 hours
                DateTime endTime = curTime.AddHours(24);
                int count = 0;
                for (int i = 0; i < hoursTimes.Count && count < 24; i++)
                {
                    DateTime t = DateTime.Parse(hoursTimes[i].ToString());
                    if (t > curTime && t <= endTime)
                    {
                        double temp = hoursTemps[i].ToObject<double>();
                        double precip = hoursPrecip[i].ToObject<double>();
                        AddHourlyCard(t, temp, precip, unitSymbol);
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void AddDailyCard(string date, double hi, double lo, string unitSymbol)
        {
            // Outer card border spans full width
            var card = new Border
            {
                Background = Brushes.LightBlue,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Grid with two columns: date on left, temps on right
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: the date
            var dateText = new TextBlock
            {
                Text = date,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(dateText, 0);

            // Right: high/low
            var tempText = new TextBlock
            {
                Text = $"High {hi}{unitSymbol}  /  Low {lo}{unitSymbol}",
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(tempText, 1);

            grid.Children.Add(dateText);
            grid.Children.Add(tempText);

            card.Child = grid;
            DailyPanel.Children.Add(card);
        }

        private void AddHourlyCard(DateTime time, double temp, double precip, string unitSymbol)
        {
            var card = new Border
            {
                Background = Brushes.LightSkyBlue,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var text = new System.Windows.Controls.TextBlock
            {
                Text = $"{time:hh:mm tt}\n{temp}{unitSymbol}\nPrecip: {precip}%",
                TextAlignment = TextAlignment.Center
            };
            card.Child = text;
            HourlyPanel.Children.Add(card);
        }

        // Click-&-drag panning for the hourly strip:
        private void HourlyScrollViewer_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingHourly = true;
            _hourlyDragStart = e.GetPosition(HourlyScrollViewer);
            _hourlyStartOffset = HourlyScrollViewer.HorizontalOffset;
            HourlyScrollViewer.CaptureMouse();
            e.Handled = true;
        }

        private void HourlyScrollViewer_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDraggingHourly) return;
            var current = e.GetPosition(HourlyScrollViewer);
            double delta = _hourlyDragStart.X - current.X;
            HourlyScrollViewer.ScrollToHorizontalOffset(_hourlyStartOffset + delta);
        }

        private void HourlyScrollViewer_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDraggingHourly) return;
            _isDraggingHourly = false;
            HourlyScrollViewer.ReleaseMouseCapture();
        }
    }
}
