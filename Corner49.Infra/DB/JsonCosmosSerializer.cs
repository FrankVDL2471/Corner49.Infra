using Corner49.Infra.Tools;
using Microsoft.Azure.Cosmos;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Corner49.Infra.DB {
	public class JsonCosmosSerializer : CosmosLinqSerializer {

		private readonly JsonSerializerOptions _options;
		public JsonCosmosSerializer() {
			_options = new JsonSerializerOptions();
			_options.SetDefault();
		}

		public override T FromStream<T>(Stream stream) {
			using (StreamReader reader = new StreamReader(stream)) {
				string json = reader.ReadToEnd();
				return JsonSerializer.Deserialize<T>(json, _options);
			}
		}

		public override Stream ToStream<T>(T input) {
			MemoryStream mem = new MemoryStream();
			using (StreamWriter writer = new StreamWriter(mem, new System.Text.UTF8Encoding(false), -1, true)) {
				string json = JsonSerializer.Serialize(input, _options);
				writer.Write(json);
				writer.Flush();
			}
			mem.Position = 0;
			return mem;

		}


		public override string SerializeMemberName(MemberInfo memberInfo) {
			JsonPropertyNameAttribute jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);
			if (!string.IsNullOrEmpty(jsonPropertyNameAttribute?.Name)) {
				return jsonPropertyNameAttribute.Name;
			}

			if (_options.PropertyNamingPolicy != null) {
				return _options.PropertyNamingPolicy.ConvertName(memberInfo.Name);
			}

			// Do any additional handling of JsonSerializerOptions here.

			return memberInfo.Name;
		}
	}
}
