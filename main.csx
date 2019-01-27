#! "netcoreapp2.0"

#r "nuget:NetStandard.Library,2.0.0"
#r "nuget: Newtonsoft.Json, 11.0.2"
#r "nuget: System.Net.Http, 4.3.3"
#r "lib/Netatmo.Net.dll"

using System.Net.Http; 
using Newtonsoft.Json; 
using Newtonsoft.Json.Converters;
using System.Linq; 
using System.Threading.Tasks;
using Netatmo.Net;
using Netatmo.Net.Model;
using System.Collections.Generic;

private string _clientId => Environment.GetEnvironmentVariable("ClientId");
private string _clientSecret => Environment.GetEnvironmentVariable("ClientSecret");
private string _mainModuleMacAddress => Environment.GetEnvironmentVariable("MacAddress");
private string _email => Environment.GetEnvironmentVariable("Email");
private string _password => Environment.GetEnvironmentVariable("Password");

private string _weatherMapStationId => Environment.GetEnvironmentVariable("stationId");
private readonly string _apiBaseAddress = @"http://api.openweathermap.org/data/3.0/measurements";
private  string _weatherServiceAPIKey => Environment.GetEnvironmentVariable("appId");
private int _limit = 2048;
private int _defaultNumberOfHours = 24;
private readonly Dictionary<string, MeasurementType[]> Modules = new Dictionary<string, MeasurementType[]>() 
{
	{ "02:00:00:1c:59:b6", new MeasurementType[] {MeasurementType.Temperature, MeasurementType.Humidity} },
	{ "06:00:00:00:c3:48", new MeasurementType[] {MeasurementType.WindStrength}}
};

await PublishNetatmoModuleMeasurements(await GetNetatmoMeasurements());

private async Task<IEnumerable<Measurement>> GetNetatmoMeasurements()
{
	var _api = new NetatmoApi($"{_clientId}", $"{_clientSecret}");
	var token = await _api.Login(_email, _password, new[] { NetatmoScope.read_station });
	var moduleMeasurements = new List<Measurement>();
	foreach(var module in Modules)
	{
		var measurement = await _api.GetMeasure(
			deviceId: _mainModuleMacAddress, 
			moduleId: module.Key,
			scale: Scale.ThirtyMinutes, 
			limit: _limit, 
			begin: GetFromDate(), 
			end: DateTimeOffset.UtcNow.DateTime, 
			measurementTypes: module.Value
		);
		if(measurement.Success)
		{
			moduleMeasurements.AddRange(measurement.Result.Measurements);
		}
	}
	return moduleMeasurements;
}

private async Task PublishNetatmoModuleMeasurements(IEnumerable<Measurement> measurements)
{
	List<WeatherSensorMeasurement> weatherSensorMeasurements = MapMeasurementsToWeatherSensorMeasurements(measurements).ToList();
	using(var client = new HttpClient())
	{
		var content = new StringContent(JsonConvert.SerializeObject(weatherSensorMeasurements).ToString(), Encoding.UTF8, "application/json");
		var response = await client.PostAsync($"{_apiBaseAddress}?appId={_weatherServiceAPIKey}", content);
		Console.WriteLine(await response.Content.ReadAsStringAsync());
	}

}

private IEnumerable<WeatherSensorMeasurement> MapMeasurementsToWeatherSensorMeasurements(IEnumerable<Measurement> measurements)
{
	List<WeatherSensorMeasurement> weatherSensorMeasurements = new List<WeatherSensorMeasurement>();
	foreach(var measurement in measurements)
	{
		var sensorMeasurements = new WeatherSensorMeasurement 
		{
			StationId = _weatherMapStationId,
			TimeStamp = measurement.TimeStamp,
			Temp = measurement.MeasurementValues.Where(t => t.Type.Equals(MeasurementType.Temperature)).SingleOrDefault()?.Value,
			Humidity = measurement.MeasurementValues.Where(t => t.Type.Equals(MeasurementType.Humidity)).SingleOrDefault()?.Value,
			WindSpeed = measurement.MeasurementValues.Where(t => t.Type.Equals(MeasurementType.WindStrength)).SingleOrDefault()?.Value,
		};
		weatherSensorMeasurements.Add(sensorMeasurements);
	}
	return weatherSensorMeasurements;
}

Console.WriteLine();

private DateTime GetFromDate()
{
    if (Args.Any())
    {
        _defaultNumberOfHours = int.Parse(Args[0]);
    }
    return DateTimeOffset.UtcNow.AddHours(- _defaultNumberOfHours).DateTime;
}

public class WeatherSensorMeasurement
{
    [JsonProperty("station_id")]
    public string StationId { get; set; }
    
	[JsonProperty("dt")]
    public long TimeStamp { get; set; }
    
	[JsonProperty("temperature")]
    public double? Temp { get; set; }
    
	[JsonProperty("humidity")]
    public double? Humidity { get; set; }
	
	[JsonProperty("wind_speed")]
    public double? WindSpeed {get; set; } 
}