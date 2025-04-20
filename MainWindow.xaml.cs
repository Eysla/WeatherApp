using System;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
                    $"&hourly=temperature_2m,precipitation_probability,weathercode" +
                    $"&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max,weathercode" +
                    $"&current_weather=true" +
                    $"&temperature_unit={unit}" +
                    $"&forecast_days=7" +
                    $"&timezone=auto";

                var weatherResp = await client.GetAsync(weatherUrl);
                weatherResp.EnsureSuccessStatusCode();
                var weatherJson = JObject.Parse(await weatherResp.Content.ReadAsStringAsync());

                // --- Fetch active alerts from NWS API ---
                string alertsUrl = $"https://api.weather.gov/alerts/active?point={lat},{lon}";
                var alertsResp = await client.GetAsync(alertsUrl);
                if (alertsResp.IsSuccessStatusCode)
                {
                    var alertsJson = JObject.Parse(await alertsResp.Content.ReadAsStringAsync());
                    var features = (JArray)alertsJson["features"];

                    if (features.Count > 0)
                    {
                        // Collect distinct alert events
                        var events = features
                            .Select(f => f["properties"]?["event"]?.ToString())
                            .Where(ev => !string.IsNullOrEmpty(ev))
                            .Distinct();

                        AlertsBanner.Text = string.Join(" · ", events);
                        AlertsBorder.Background = Brushes.OrangeRed;
                        AlertsBanner.Foreground = Brushes.White;
                    }
                    else
                    {
                        AlertsBanner.Text = "No active alerts.";
                        AlertsBorder.Background = Brushes.LightGreen;
                        AlertsBanner.Foreground = Brushes.Black;
                    }
                }
                else
                {
                    AlertsBanner.Text = "Alerts unavailable.";
                    AlertsBorder.Background = Brushes.Gray;
                    AlertsBanner.Foreground = Brushes.White;
                }

                // --- Populate 7‑day cards ---
                DailyPanel.Children.Clear();
                var dailyTimes = (JArray)weatherJson["daily"]["time"];
                var dailyMaxTemps = (JArray)weatherJson["daily"]["temperature_2m_max"];
                var dailyMinTemps = (JArray)weatherJson["daily"]["temperature_2m_min"];
                var dailyPrecip = (JArray)weatherJson["daily"]["precipitation_probability_max"];
                var dailyWeatherCodes = (JArray)weatherJson["daily"]["weathercode"];
                for (int i = 0; i < dailyTimes.Count; i++)
                {
                    string date = dailyTimes[i].ToString();
                    int hi = dailyMaxTemps[i].ToObject<int>();
                    int lo = dailyMinTemps[i].ToObject<int>();
                    int precipPct = dailyPrecip[i].ToObject<int>();
                    int code = dailyWeatherCodes[i].ToObject<int>();
                    AddDailyCard(date, hi, lo, precipPct, code, unitSymbol);
                }

                // --- Populate hourly strip ---
                HourlyPanel.Children.Clear();
                string curTimeStr = weatherJson["current_weather"]["time"].ToString();
                DateTime curTime = DateTime.Parse(curTimeStr);
                // show current hour first
                int curTemp = weatherJson["current_weather"]["temperature"].ToObject<int>();
                int curPrecip = 0;
                int curCode = 0;
                var hoursTimes = (JArray)weatherJson["hourly"]["time"];
                var hoursTemps = (JArray)weatherJson["hourly"]["temperature_2m"];
                var hoursPrecip = (JArray)weatherJson["hourly"]["precipitation_probability"];
                var hoursCodes = (JArray)weatherJson["hourly"]["weathercode"];
                for (int j = 0; j < hoursTimes.Count; j++)
                {
                    if (DateTime.Parse(hoursTimes[j].ToString()) == curTime)
                    {
                        curPrecip = hoursPrecip[j].ToObject<int>();
                        curCode = hoursCodes[j].ToObject<int>();
                        break;
                    }
                }
                AddHourlyCard(curTime, curTemp, curPrecip, curCode, unitSymbol);

                // then next 24 hours
                DateTime endTime = curTime.AddHours(24);
                int count = 0;
                for (int i = 0; i < hoursTimes.Count && count < 24; i++)
                {
                    DateTime t = DateTime.Parse(hoursTimes[i].ToString());
                    if (t > curTime && t <= endTime)
                    {
                        int temp = hoursTemps[i].ToObject<int>();
                        int precip = hoursPrecip[i].ToObject<int>();
                        int code = hoursCodes[i].ToObject<int>();
                        AddHourlyCard(t, temp, precip, code, unitSymbol);
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void AddDailyCard(
            string date,
            int hi,
            int lo,
            int precipPct,
            int weatherCode,
            string unitSymbol)
        {
            // 1) parse date
            if (!DateTime.TryParse(date, out DateTime dt)) dt = DateTime.Today;
            string formattedDate = dt.ToString("MMMM d");

            // 2) map code → icon filename
            string iconFile = weatherCode switch
            {
                0 => "clear.png",
                1 => "mostlyclear.png",
                2 => "partlycloudy.png",
                3 => "overcast.png",
                45 => "fog.png",
                48 => "rimefog.png",
                51 => "lightdrizzle.png",
                53 => "moderatedrizzle.png",
                55 => "densedrizzle.png",
                56 => "lightfreezing-drizzle.png",
                57 => "densefreezing-drizzle.png",
                61 or 63 or 80 or 81 => "moderaterain.png",
                65 or 82 => "heavyrain.png",
                66 => "lightfreezing-rain.png",
                67 => "heavyfreezing-rain.png",
                71 => "slightsnowfall.png",
                73 => "moderatesnowfall.png",
                75 or 86 => "heavysnowfall.png",
                77 => "snowflake.png",
                95 => "thunderstorm.png",
                96 or 99 => "thunderstormwithhail.png"
            };

            var card = new Border
            {
                Background = Brushes.LightBlue,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // 3) grid: [Icon][Date *][High Auto][Low Auto][Precip Auto]
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // date
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // high
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // low
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // precip

            // 4) icon
            var img = new Image
            {
                Source = new BitmapImage(new Uri($"pack://application:,,,/Images/{iconFile}")),
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(img, 0);

            // 5) date
            var dateText = new TextBlock
            {
                Text = formattedDate,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left
            };
            Grid.SetColumn(dateText, 1);

            // 6) high
            var highText = new TextBlock
            {
                Text = $"High: {hi}{unitSymbol} ",
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(8, 0, 8, 0)
            };
            Grid.SetColumn(highText, 2);

            // 7) low
            var lowText = new TextBlock
            {
                Text = $"Low:  {lo}{unitSymbol}   ",
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(8, 0, 8, 0)
            };
            Grid.SetColumn(lowText, 3);

            // 8) precip
            var precipText = new TextBlock
            {
                Text = $"Precip: {precipPct}%",
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(precipText, 4);

            // 9) assemble
            grid.Children.Add(img);
            grid.Children.Add(dateText);
            grid.Children.Add(highText);
            grid.Children.Add(lowText);
            grid.Children.Add(precipText);
            card.Child = grid;
            DailyPanel.Children.Add(card);
        }

        private void AddHourlyCard(
            DateTime time, 
            int temp, 
            int precip, 
            int weatherCode, 
            string unitSymbol)
        {
            // 1) map code → icon filename (same mapping as your daily cards)
            string iconFile = weatherCode switch
            {
                0 => "clear.png",
                1 => "mostlyclear.png",
                2 => "partlycloudy.png",
                3 => "overcast.png",
                45 => "fog.png",
                48 => "rimefog.png",
                51 => "lightdrizzle.png",
                53 => "moderatedrizzle.png",
                55 => "densedrizzle.png",
                56 => "lightfreezing-drizzle.png",
                57 => "densefreezing-drizzle.png",
                61 or 63 or 80 or 81 => "moderaterain.png",
                65 or 82 => "heavyrain.png",
                66 => "lightfreezing-rain.png",
                67 => "heavyfreezing-rain.png",
                71 => "slightsnowfall.png",
                73 => "moderatesnowfall.png",
                75 or 86 => "heavysnowfall.png",
                77 => "snowflake.png",
                95 => "thunderstorm.png",
                96 or 99 => "thunderstormwithhail.png",
                _ => "unknown.png",
            };

            // 2) build the card
            var card = new Border
            {
                Background = Brushes.LightSkyBlue,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 3) stack icon over text
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // icon
            var img = new Image
            {
                Source = new BitmapImage(new Uri($"pack://application:,,,/Images/{iconFile}")),
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 0, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(img);

            // time + temp + precip lines
            var text = new TextBlock
            {
                Text = $"{time:hh:mm tt}\n{temp}{unitSymbol}\nPrecip: {precip}%",
                TextAlignment = TextAlignment.Center
            };
            stack.Children.Add(text);

            card.Child = stack;
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
