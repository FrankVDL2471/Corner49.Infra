using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Corner49.SampleAPI {
	public class WeatherForecast {

		[Category("General")]
		[Description("Random Date")]
		public DateOnly Date { get; set; }

		[Description("Temperate in degrees celcius")]
		[Range(-72, 100)]
		public int TemperatureC { get; set; }

		public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

		
		public string? Summary { get; set; }
	}
}
