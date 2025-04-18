using System;
using System.Text;
using System.Windows;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json.Linq;

namespace Group_Project
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void btnGetWeather_Click(object sender, RoutedEventArgs e)
        {
            string cityName = txtCity.Text;
            if (string.IsNullOrWhiteSpace(cityName))
            {
                MessageBox.Show("Please enter a city name.");
                return;
            }

            // Get latitude and longitude using Nominatim geocoding
            string geocodeUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(cityName)}&format=json&limit=1";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Nominatim requires a valid User-Agent header
                    client.DefaultRequestHeaders.Add("User-Agent", "YourAppNameHere");

                    HttpResponseMessage geocodeResponse = await client.GetAsync(geocodeUrl);
                    geocodeResponse.EnsureSuccessStatusCode();
                    string geocodeResult = await geocodeResponse.Content.ReadAsStringAsync();

                    JArray locationArray = JArray.Parse(geocodeResult);
                    if (locationArray.Count == 0)
                    {
                        MessageBox.Show("City not found. Please try another city.");
                        return;
                    }

                    var location = locationArray[0];
                    string lat = location["lat"].ToString();
                    string lon = location["lon"].ToString();

                    // Call the weather API with hourly and daily forecast parameters,
                    // setting timezone=auto to have times in the local timezone.
                    string weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                        $"&hourly=temperature_2m,precipitation_probability" +
                        $"&daily=temperature_2m_max,temperature_2m_min" +
                        $"&current_weather=true" +
                        $"&temperature_unit=fahrenheit" +
                        $"&forecast_days=7" +
                        $"&timezone=auto";

                    HttpResponseMessage weatherResponse = await client.GetAsync(weatherUrl);
                    weatherResponse.EnsureSuccessStatusCode();
                    string weatherResult = await weatherResponse.Content.ReadAsStringAsync();

                    JObject weatherJson = JObject.Parse(weatherResult);

                    // Get current weather and daily forecast
                    double currentTemp = weatherJson["current_weather"]["temperature"].ToObject<double>();
                    JArray dailyTimes = (JArray)weatherJson["daily"]["time"];
                    JArray dailyMaxTemps = (JArray)weatherJson["daily"]["temperature_2m_max"];
                    JArray dailyMinTemps = (JArray)weatherJson["daily"]["temperature_2m_min"];

                    string dailyReport = $"Current Temperature in {cityName}: {currentTemp}°F\n\n7-Day Forecast:\n";
                    for (int i = 0; i < dailyTimes.Count; i++)
                    {
                        string date = dailyTimes[i].ToString();
                        double maxTemp = dailyMaxTemps[i].ToObject<double>();
                        double minTemp = dailyMinTemps[i].ToObject<double>();
                        dailyReport += $"{date}: High {maxTemp}°F, Low {minTemp}°F\n";
                    }
                    txtWeather.Text = dailyReport;

                    // Build the hourly report: show forecast from the current hour to the next 24 hours.
                    // Use the current_weather time as the starting point.
                    string currentTimeStr = weatherJson["current_weather"]["time"].ToString();
                    DateTime currentTime = DateTime.Parse(currentTimeStr);
                    DateTime endTime = currentTime.AddHours(24);

                    JArray hourlyTimes = (JArray)weatherJson["hourly"]["time"];
                    JArray hourlyTemps = (JArray)weatherJson["hourly"]["temperature_2m"];
                    JArray hourlyPrecip = (JArray)weatherJson["hourly"]["precipitation_probability"];

                    string hourlyReport = "24 Hour Forecast:\n";
                    for (int i = 0; i < hourlyTimes.Count; i++)
                    {
                        DateTime hourTime = DateTime.Parse(hourlyTimes[i].ToString());
                        if (hourTime >= currentTime && hourTime <= endTime)
                        {
                            double temp = hourlyTemps[i].ToObject<double>();
                            // Ensure the precipitation value exists for this hour.
                            double precip = hourlyPrecip.Count > i ? hourlyPrecip[i].ToObject<double>() : 0;
                            hourlyReport += $"{hourTime.ToString("hh:mm tt")}: {temp}°F, Precipitation: {precip}%\n";
                        }
                    }
                    txtHourlyWeather.Text = hourlyReport;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

    }
}