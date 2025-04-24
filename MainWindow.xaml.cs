using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
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

                //if else for past/ current

                string weatherUrl;

                if (historyCB.IsChecked == true)
                {
                    DateTime startDt = DateTime.TryParse(txtDate.Text, out var tmp)
                        ? tmp
                        : DateTime.Today;
                    // format YYYY-MM-DD
                    string start = startDt.ToString("yyyy-MM-dd");
                    string end = startDt.AddDays(6).ToString("yyyy-MM-dd");
                    weatherUrl =
                    $"https://historical-forecast-api.open-meteo.com/v1/forecast?" +
                    $"latitude={lat}&longitude={lon}" +
                    $"&start_date={start}&end_date={end}" +
                    "&hourly=temperature_2m,precipitation_probability,weathercode,uv_index" +
                    "&daily=visibility_mean,uv_index_max,relative_humidity_2m_mean,sunset,sunrise,temperature_2m_max,temperature_2m_min,wind_speed_10m_max,wind_gusts_10m_max," +
                        "precipitation_probability_max,weathercode" +
                    "&current_weather=true" +
                    $"&temperature_unit={unit}" +
                    "&timezone=auto" +
                    "&wind_speed_unit=ms";
                }
                else
                {
                    weatherUrl =
                       $"https://api.open-meteo.com/v1/forecast?" +
                       $"latitude={lat}&longitude={lon}" +
                       "&hourly=temperature_2m,precipitation_probability,uv_index,weathercode" +
                       "&daily=visibility_mean,uv_index_max,relative_humidity_2m_mean,sunset,sunrise,temperature_2m_max,temperature_2m_min,wind_speed_10m_max,wind_gusts_10m_max," +
                           "precipitation_probability_max,weathercode" +
                       "&current_weather=true" +
                       $"&temperature_unit={unit}" +
                       "&forecast_days=7" +
                       "&timezone=auto" +
                       "&wind_speed_unit=ms";
                }

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
                var dailysunset = (JArray)weatherJson["daily"]["sunset"];
                var dailysunrise = (JArray)weatherJson["daily"]["sunrise"];
                var dailywind = (JArray)weatherJson["daily"]["wind_speed_10m_max"];
                var dailygust = (JArray)weatherJson["daily"]["wind_gusts_10m_max"];
                var dailyUvMax = (JArray)weatherJson["daily"]["uv_index_max"];
                var dailyHumidity = (JArray)weatherJson["daily"]["relative_humidity_2m_mean"];
                var dailyVisibility = (JArray)weatherJson["daily"]["visibility_mean"];

                for (int i = 0; i < dailyTimes.Count; i++)
                {
                    string date = dailyTimes[i].ToString();
                    int hi = dailyMaxTemps[i].ToObject<int>();
                    int lo = dailyMinTemps[i].ToObject<int>();
                    int precipPct = dailyPrecip[i].ToObject<int?>() ?? 0;
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

                //Change time on current time panel and today's highligh
                currentCity.Text = cityName;
                numMod1.Text = curTemp.ToString() + unitSymbol;
                sourceMod1.Source = new BitmapImage(new Uri($"pack://application:,,,/Images/{iconFilename(curCode)}"));

                //Widgets
                int curwind = dailywind[0].ToObject<int>();
                int curgust = dailygust[0].ToObject<int>();
                wind_speed.Text = curwind + "m/s";
                wind_gust.Text = curgust + "m/s";

                string todaySunset = dailysunset[0].ToString();
                DateTime Sunset = DateTime.Parse(todaySunset);
                sunset.Text = $"{Sunset:hh:mm tt}";
                string todaySunrise = dailysunrise[0].ToString();
                DateTime Sunrise = DateTime.Parse(todaySunrise);
                sunrise.Text = $"{Sunrise:hh:mm tt}";

                int high = dailyMaxTemps[0].ToObject<int>();
                int low = dailyMinTemps[0].ToObject<int>();
                highMod.Text = high + unitSymbol;
                lowMod.Text = low + unitSymbol;

                //todays max uv
                string todayUV = dailyUvMax[0].ToString();
                UV.Text = todayUV;

                //todays humidity mean
                string todayHumidity = dailyHumidity[0].ToString();
                Humidity.Text = $"{todayHumidity}%";

                //todays mean visibility
                string todayVisibilityStr = dailyVisibility[0].ToString();
                double todayVisibility = double.Parse(todayVisibilityStr);
                Vis.Text = $"{todayVisibility:N0}m";

                // Match the current hour to get precip & code
                if (historyCB.IsChecked == true)
                {
                    for (int j = 0; j <= 24; j++)
                    {
                        DateTime t = DateTime.Parse(hoursTimes[j].ToString());
                        int temp = hoursTemps[j]?.ToObject<int?>() ?? 0;
                        curPrecip = hoursPrecip[j].ToObject<int?>() ?? 0;
                        curCode = hoursCodes[j].ToObject<int>();
                        AddHourlyCard(t, temp, curPrecip, curCode, unitSymbol);                               
                    }
                }
                else
                {

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
            }
            catch (Exception ex)
            {
                // Display any unexpected errors
                MessageBox.Show($"Error: {ex.Message}");
            }
        }


        //-----------------Parsing and Formatting---------------------
        // Parse & format date (e.g. April 17)
        private string dateFormat(string date)
        {
            if (!DateTime.TryParse(date, out DateTime dt))
                dt = DateTime.Today;
            string formattedDate = dt.ToString("MMMM d");
            return formattedDate;
        }

        // Find the filename for the icon related to the weatherCode
        private string iconFilename(int weatherCode)
        {
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
            return iconFile;
        }
        //-----------------Parsing and Formatting---------------------

        //--------------Populate day and hour cards-------------------
        // Function: Builds a single day card in the 7‑day forecast area.
        // Creates an icon + date + high/low/precip chances grid.
        private void AddDailyCard(
            string date,
            int hi,
            int lo,
            int precipPct,
            int weatherCode,
            string unitSymbol)
        {

            //Formatting
            string formattedDate = dateFormat(date);
            string iconFile = iconFilename(weatherCode);

            // Create the visual card container (uses CardDay.xaml)
            CardDay cardDay = new CardDay()
            {
                Source = new BitmapImage(new Uri($"pack://application:,,,/Images/{iconFile}")),
                Day = formattedDate,
                Maxtemp = $"{hi}{unitSymbol}",
                Mintemp = $"{lo}{unitSymbol}",
                Prcpt = $"{precipPct}%"
            };

            //Populate the today panel
            DailyPanel.Children.Add(cardDay);
        }

        // Function: Builds a single hourly forecast card
        // Creates an Icon stacked above time/temp/precip text.
        private void AddHourlyCard(
            DateTime time,
            int temp,
            int precip,
            int weatherCode,
            string unitSymbol)
        {
            // Format
            string iconFile = iconFilename(weatherCode);

            // Create the visual card container (user CardHour.xaml)
            CardHour cardHour = new CardHour()
            {
                Source = new BitmapImage(new Uri($"pack://application:,,,/Images/{iconFile}")),
                Hour= $"{time:hh:mm tt}",
                Temp= $"{temp}{unitSymbol}",
                Prcpt= $"{precip}%"
            };
            // Populate the week panel 
            HourlyPanel.Children.Add( cardHour );
        }
        //--------------Populate day and hour cards-------------------

        //---------------Today's panel drag-scroll--------------------
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
        //---------------Today's panel drag-scroll--------------------

        //-------------------Link to resources------------------------
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
        //-------------------Link to resources------------------------

        //------------------------------------------------------------
        //Exit Button
        private void exitButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        //Drag the app around the screen (using the right side menu)
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
        //------------------------------------------------------------

        //-----------------------Search Bar---------------------------
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
        //Instead of pressing the search button can press enter to load the city
        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnGetWeather_Click(sender, e);
            }
        }

        //Styling choice when pressing between week and today label 1
        private void todayLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HourlyPanel.Visibility = Visibility.Visible;
            DailyPanel.Visibility = Visibility.Collapsed;

            todayLabel.Style = (Style)Application.Current.Resources["activeTextButton"];
            weekLabel.Style = (Style)Application.Current.Resources["textButton"];
        }
        //Styling choice when pressing between week and today label 2
        private void weekLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HourlyPanel.Visibility= Visibility.Collapsed;
            DailyPanel.Visibility = Visibility.Visible;

            todayLabel.Style = (Style)Application.Current.Resources["textButton"];
            weekLabel.Style = (Style)Application.Current.Resources["activeTextButton"];
        }

        //past dates visibility
        private void historyCB_Unchecked(object sender, RoutedEventArgs e)
        {
            txtDate.Visibility = Visibility.Hidden;
            txtDateRules.Visibility = Visibility.Hidden;
            
        }

        private void historyCB_Checked(object sender, RoutedEventArgs e)
        {
            txtDate.Visibility = Visibility.Visible;
            txtDateRules.Visibility = Visibility.Visible;
        }
    }
}
