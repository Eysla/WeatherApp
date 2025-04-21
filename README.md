# WeatherApp

**WeatherApp** is a WPF desktop application built with .NET 8 that provides:

- **Geocoding** via Nominatim (OpenStreetMap)
- **Current weather**, **7-day forecast**, and **24-hour hourly forecast** using the Open‑Meteo API
- **Severe weather alerts** from the National Weather Service
- **Unit toggle** between Fahrenheit and Celsius
- **Interactive UI** with daily cards, hourly click‑and‑drag strip, and weather icons

---

## Features

1. **City Lookup**: Enter any city name to fetch its latitude/longitude via Nominatim.
2. **7-Day Forecast**: Displays cards for each day with:
   - Date (Month Day)
   - Weather icon (clear, rain, snow, etc.)
   - High/Low temperatures
   - Daily precipitation chance
3. **Hourly Forecast**: Displays a horizontally scrollable strip of the current hour + next 24 hours, each card showing:
   - Time (12-hour AM/PM)
   - Weather icon
   - Temperature
   - Precipitation chance
4. **Alerts Banner**: Shows active alerts (e.g. Severe Thunderstorm Warning, Flash Flood Warning) or indicates none.
5. **Unit Toggle**: Switch between Fahrenheit and Celsius at runtime.
6. **Click‑and‑Drag Scrolling**: Drag the hourly strip to pan left/right without visible scrollbars.

---

## Prerequisites

- **Windows 10/11**
- **Visual Studio 2022** (or later) with **.NET 8 Desktop Development** workload
- **Newtonsoft.Json** NuGet package

---

## Getting Started

1. **Clone the repository**:
   ```bash
   git clone https://github.com/yourusername/WeatherApp.git
   cd WeatherApp
   ```

2. **Open the solution** in Visual Studio:
   - `WeatherApp.sln`

3. **Restore NuGet packages**:
   - Right-click the solution ▶ **Restore NuGet Packages**

4. **Build & Run**:
   - Set **MainWindow** project as startup
   - Press **F5** or **Run**

5. **Use the app**:
   - Enter a city and click **Get Weather**
   - Toggle units between °F/°C
   - Drag the hourly strip to pan

---

## Project Structure

```
WeatherApp/               # Solution root
├─ Images/                # Weather icon resources
├─ MainWindow.xaml        # WPF UI layout
├─ MainWindow.xaml.cs     # Code‑behind with API calls & UI logic
├─ WeatherApp.csproj      # .NET project file
└─ README.md              # This file
```

---

## Attribution

- **Geocoding**: Nominatim — https://nominatim.openstreetmap.org/  
- **Weather data**: Open‑Meteo — https://open-meteo.com/  
- **Alerts**: National Weather Service — https://api.weather.gov/

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

---

## Contributing

Contributions are welcome! Please open issues or pull requests on GitHub.
