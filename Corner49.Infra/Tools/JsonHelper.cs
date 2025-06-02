using System.Text.Json;
using System.Text.Json.Serialization;

namespace Corner49.Infra.Tools {
	public static class JsonHelper {


		private static JsonSerializerOptions _options = null;

		public static JsonSerializerOptions Options {
			get {
				if (_options == null) {
					_options = new JsonSerializerOptions();
					_options.SetDefault();
				}
				return _options;
			}
		}

		public static JsonSerializerOptions SetDefault(this JsonSerializerOptions options) {
			options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
			options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
			options.WriteIndented = true;
			options.NumberHandling = JsonNumberHandling.AllowReadingFromString;			
			options.Converters.Add(new JsonStringEnumConverter());
			options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;	
			return options;
		}

	}
}
