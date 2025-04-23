using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Group_Project.UserControls;
using Newtonsoft.Json.Linq;
using Point = System.Windows.Point;

namespace Group_Project
{
    public partial class MainWindow : Window
    {
        // Flags and state for click‑and‑drag scrolling of hourly forecast strip
        private bool _isDraggingHourly;
        private Point _hourlyDragStart;
        private double _hourlyStartOffset;

        public MainWindow()
        {
            InitializeComponent();
            string defautlCity = "Nashville";
            City.Text = defautlCity;
            getWeather(defautlCity, "fahrenheit", "°F");
            HourlyPanel.Visibility = Visibility.Collapsed;
        }

        // Responsible for toggling the temperature unit between Celsius and Fahrenheit.
        private void toggleUnit_Checked(object sender, RoutedEventArgs e)
        {
            toggleUnit.Content = "°F";
        }

        private void toggleUnit_Unchecked(object sender, RoutedEventArgs e)
        {
            toggleUnit.Content = "°C";
        }

        // Main handler for the Get Weather button.
        // 1) Validates city input
        // 2) Geocodes city to lat/lon via Nominatim
        // 3) Fetches weather data from Open‑Meteo
        // 4) Fetches active alerts from NWS
        // 5) Populates UI panels (alerts banner, 7‑day cards, hourly strip)
        private void btnGetWeather_Click(object sender, RoutedEventArgs e)
        {
            // Read and trim city name
            string cityName = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(cityName))
            {
                MessageBox.Show("Please enter a city name.");
                return;
            }
            // Decide unit and symbol based on toggle state
            bool isFahrenheit = toggleUnit.IsChecked == true;
            string unit = isFahrenheit ? "fahrenheit" : "celsius";
            string unitSymbol = isFahrenheit ? "°F" : "°C";
            getWeather(cityName, unit, unitSymbol);
        }

        private async void getWeather(string cityName, string unit, string unitSymbol) {
            // 1) Build Nominatim geocode URL
            string geoUrl = $"https://nominatim.openstreetmap.org/search?" +
                            $"q={Uri.EscapeDataString(cityName)}&format=json&limit=1";

            try
            {
                using var client = new HttpClient();
                // Required header per Nominatim usage policy
                client.DefaultRequestHeaders.Add("User-Agent", "Group_Project_App");

                // Send geocoding request
                var geoResp = await client.GetAsync(geoUrl);
                geoResp.EnsureSuccessStatusCode();
                var geoJson = JArray.Parse(await geoResp.Content.ReadAsStringAsync());

                // If city not found, inform user
                if (geoJson.Count == 0)
                {
                    MessageBox.Show("City not found.");
                    return;
                }
                City.Text = cityName;

                // Extract latitude and longitude from response
                string lat = geoJson[0]["lat"].ToString();
                string lon = geoJson[0]["lon"].ToString();

                // 2) Build Open‑Meteo weather API URL
                string weatherUrl =
                    $"https://api.open-meteo.com/v1/forecast?" +
                    $"latitude={lat}&longitude={lon}" +
                    "&hourly=temperature_2m,precipitation_probability,weathercode" +
                    "&daily=temperature_2m_max,temperature_2m_min," +
                        "precipitation_probability_max,weathercode" +
                    "&current_weather=true" +
                    $"&temperature_unit={unit}" +
                    "&forecast_days=7" +
                    "&timezone=auto";

                // Send weather data request
                var weatherResp = await client.GetAsync(weatherUrl);
                weatherResp.EnsureSuccessStatusCode();
                var weatherJson = JObject.Parse(await weatherResp.Content.ReadAsStringAsync());

                // --- Fetch alerts from NWS API ---
                string alertsUrl = $"https://api.weather.gov/alerts/active?point={lat},{lon}";
                var alertsResp = await client.GetAsync(alertsUrl);

                if (alertsResp.IsSuccessStatusCode)
                {
                    // Parse GeoJSON features array
                    var alertsJson = JObject.Parse(await alertsResp.Content.ReadAsStringAsync());
                    var features = (JArray)alertsJson["features"];

                    if (features.Count > 0)
                    {
                        // Collate unique 'event' strings (e.g. "Tornado Warning")
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
                        // No active alerts
                        AlertsBanner.Text = "No active alerts.";
                        AlertsBorder.Background = Brushes.LightGreen;
                        AlertsBanner.Foreground = Brushes.Black;
                    }
                }
                else
                {
                    // Alert API down or unreachable
                    AlertsBanner.Text = "Alerts unavailable.";
                    AlertsBorder.Background = Brushes.Gray;
                    AlertsBanner.Foreground = Brushes.White;
                }

                // --- Populate 7‑day forecast cards ---
                DailyPanel.Children.Clear();
                var dailyTimes = (JArray)weatherJson["daily"]["time"];
                var dailyMaxTemps = (JArray)weatherJson["daily"]["temperature_2m_max"];
                var dailyMinTemps = (JArray)weatherJson["daily"]["temperature_2m_min"];
                var dailyPrecip = (JArray)weatherJson["daily"]["precipitation_probability_max"];
                var dailyCodes = (JArray)weatherJson["daily"]["weathercode"];

                for (int i = 0; i < dailyTimes.Count; i++)
                {
                    string date = dailyTimes[i].ToString();
                    int hi = dailyMaxTemps[i].ToObject<int>();
                    int lo = dailyMinTemps[i].ToObject<int>();
                    int precipPct = dailyPrecip[i].ToObject<int>();
                    int code = dailyCodes[i].ToObject<int>();

                    AddDailyCard(date, hi, lo, precipPct, code, unitSymbol);
                }

                // --- Populate hourly forecast strip ---
                HourlyPanel.Children.Clear();

                // Get current weather snapshot
                string curTimeStr = weatherJson["current_weather"]["time"].ToString();
                DateTime curTime = DateTime.Parse(curTimeStr);
                int curTemp = weatherJson["current_weather"]["temperature"].ToObject<int>();

                // Default if not found in hourly array
                int curPrecip = 0, curCode = 0;
                var hoursTimes = (JArray)weatherJson["hourly"]["time"];
                var hoursTemps = (JArray)weatherJson["hourly"]["temperature_2m"];
                var hoursPrecip = (JArray)weatherJson["hourly"]["precipitation_probability"];
                var hoursCodes = (JArray)weatherJson["hourly"]["weathercode"];

                // Match the current hour to get precip & code
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

                // Then show the next 24 hours
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
                // Display any unexpected errors
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        // Builds a single day card in the 7‑day forecast area.
        // Creates an icon + date + high/low/precip chances grid.
        private void AddDailyCard(
            string date,
            int hi,
            int lo,
            int precipPct,
            int weatherCode,
            string unitSymbol)
        {
            // Parse & format date (e.g. April 17)
            if (!DateTime.TryParse(date, out DateTime dt))
                dt = DateTime.Today;
            string formattedDate = dt.ToString("MMMM d");

            // Map weatherCode to an icon filename
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
                56 => "lightfreezingdrizzle.png",
                57 => "densefreezingdrizzle.png",
                61 or 63 or 80 or 81 => "moderaterain.png",
                65 or 82 => "heavyrain.png",
                66 => "lightfreezingrain.png",
                67 => "heavyfreezingrain.png",
                71 => "slightsnowfall.png",
                73 => "moderatesnowfall.png",
                75 or 86 => "heavysnowfall.png",
                77 => "snowflake.png",
                95 => "thunderstorm.png",
                96 or 99 => "thunderstormwithhail.png",
                _ => "unknown.png",
            };

            // Create the visual card container
            CardDay cardDay = new CardDay()
            {
                Source = new BitmapImage(new Uri($"pack://application:,,,/Images/{iconFile}")),
                Day = formattedDate,
                Maxtemp = $"{hi}{unitSymbol}",
                Mintemp = $"{lo}{unitSymbol}",
                Prcpt = $"{precipPct}%"
            };
            DailyPanel.Children.Add(cardDay);
        }

        // Builds a single hourly forecast card with an icon stacked above time/temp/precip text.
        private void AddHourlyCard(
            DateTime time,
            int temp,
            int precip,
            int weatherCode,
            string unitSymbol)
        {
            // Map code → icon (same as daily)
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
                56 => "lightfreezingdrizzle.png",
                57 => "densefreezingdrizzle.png",
                61 or 63 or 80 or 81 => "moderaterain.png",
                65 or 82 => "heavyrain.png",
                66 => "lightfreezingrain.png",
                67 => "heavyfreezingrain.png",
                71 => "slightsnowfall.png",
                73 => "moderatesnowfall.png",
                75 or 86 => "heavysnowfall.png",
                77 => "snowflake.png",
                95 => "thunderstorm.png",
                96 or 99 => "thunderstormwithhail.png",
                _ => "unknown.png",
            };

            // Create the visual card container
            CardHour cardHour = new CardHour()
            {
                Source = new BitmapImage(new Uri($"pack://application:,,,/Images/{iconFile}")),
                Hour= $"{time:hh:mm tt}",
                Temp= $"{temp}{unitSymbol}",
                Prcpt= $"{precip}%"
            };
            HourlyPanel.Children.Add( cardHour );
        }

        // Begin drag‑scroll when mouse button is pressed.
        private void HourlyScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingHourly = true;
            _hourlyDragStart = e.GetPosition(HourlyScrollViewer);
            _hourlyStartOffset = HourlyScrollViewer.HorizontalOffset;
            HourlyScrollViewer.CaptureMouse();
            e.Handled = true;
        }

        // Scroll horizontally as the mouse moves.
        private void HourlyScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingHourly) return;
            var current = e.GetPosition(HourlyScrollViewer);
            double delta = _hourlyDragStart.X - current.X;
            HourlyScrollViewer.ScrollToHorizontalOffset(_hourlyStartOffset + delta);
        }

        // End drag‑scroll when mouse button is released.
        private void HourlyScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingHourly) return;
            _isDraggingHourly = false;
            HourlyScrollViewer.ReleaseMouseCapture();
        }

        // Opens the Nominatim attribution link in the default browser.
        private void Nominatim_LinkClicked(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://nominatim.openstreetmap.org/",
                UseShellExecute = true
            });
        }

        // Opens the Open‑Meteo attribution link in the default browser.
        private void OpenMeteo_LinkClicked(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://open-meteo.com/",
                UseShellExecute = true
            });
        }

        private void exitButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void textSearch_MouseDown(object sender, MouseButtonEventArgs e)
        {
            txtSearch.Focus();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtSearch.Text) && txtSearch.Text.Length > 0)
            {
                textSearch.Visibility = Visibility.Collapsed;
            }
            else
            {
                textSearch.Visibility = Visibility.Visible;
            }
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnGetWeather_Click(sender, e);
            }
        }

        private void todayLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HourlyPanel.Visibility = Visibility.Visible;
            DailyPanel.Visibility = Visibility.Collapsed;

            todayLabel.Style = (Style)Application.Current.Resources["activeTextButton"];
            weekLabel.Style = (Style)Application.Current.Resources["textButton"];
        }

        private void weekLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HourlyPanel.Visibility= Visibility.Collapsed;
            DailyPanel.Visibility = Visibility.Visible;

            todayLabel.Style = (Style)Application.Current.Resources["textButton"];
            weekLabel.Style = (Style)Application.Current.Resources["activeTextButton"];
        }
    }
}
