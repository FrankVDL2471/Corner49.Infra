using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Corner49.Infra.Helpers {
	public static class OpenApiHelpers {



		public static void BuildSchemas(this OpenApiOptions options) {
			options.AddSchemaTransformer((schema, context, cancelToken) => {
				if (context.JsonPropertyInfo?.PropertyType == null) return Task.CompletedTask;
				if (context.JsonPropertyInfo?.Name == null) return Task.CompletedTask;

				try {
					schema.CompleteSchema(context.JsonPropertyInfo.PropertyType, context.JsonPropertyInfo.Name);

				} catch (Exception err) {
					Console.Error.WriteLine($"OpenApi.BuildSchemas '{context.JsonPropertyInfo.Name}' failed : {err.Message}");
				}

				return Task.CompletedTask;
			});
		}
		public static void BuildOperations(this OpenApiOptions options) {
			// NOTE: BuildOperations has been temporarily disabled due to API changes in Microsoft.OpenApi v10
			// The IOpenApiParameter interface now has read-only properties that cannot be modified directly
			// This functionality needs to be rewritten using the new v10 patterns or removed if not critical

			// TODO: Reimplement using v10 parameter transformers if needed
		}



		private static Dictionary<Type, string> _knownSchemas = new Dictionary<Type, string>();

		// NOTE: LinkReference has been disabled due to API changes in Microsoft.OpenApi v10
		// The Reference property and related types have changed significantly
		public static OpenApiSchema LinkReference(this OpenApiSchema doc, Type prpType) {
			// TODO: Reimplement using v10 reference patterns
			return doc;
		}

		public static OpenApiSchema CompleteSchema(this OpenApiSchema doc, Type prpType, string? prpName = null) {
			var baseType = Nullable.GetUnderlyingType(prpType) ?? prpType;

			if (prpType.IsEnum == true) {
				if (doc.Enum?.Any() != true) {
					if (doc.Enum == null) doc.Enum = new List<JsonNode>();
					foreach (var nm in Enum.GetNames(prpType)) {
						doc.Enum.Add(JsonValue.Create(nm));
					}
					// In v10, Type is JsonSchemaType enum, not a string
					// and Nullable property may not be directly settable
				}
			} else if (baseType == typeof(decimal)) {
				doc.Format = "decimal";
			} else if (baseType.Namespace == "System.Collections.Generic") {
				var tp = baseType.GenericTypeArguments[0];

				// Items is IOpenApiSchema in v10, which may have different mutability
				// Recursive CompleteSchema call may not work on interface types
				// TODO: Investigate v10 pattern for modifying collection item schemas
			} else if ((!prpType.IsValueType) && (prpType != typeof(string))) {
				// Complex type handling
				foreach (var prp in prpType.GetProperties()) {
					var key = doc.Properties?.Keys?.FirstOrDefault(c => c.Equals(prp.Name, StringComparison.OrdinalIgnoreCase));
					if (key == null) continue;

					// Properties[key] returns IOpenApiSchema which has read-only properties in v10
					// We cannot directly modify Description, Deprecated, ReadOnly, or Default
					// These need to be set during schema creation, not after

					// TODO: Rewrite to use v10's immutable schema patterns
				}
			} else if (baseType == typeof(int)) {
			} else if (baseType == typeof(Int64)) {
			} else if (baseType == typeof(string)) {
			} else if (baseType == typeof(bool)) {
			} else if (baseType == typeof(DateTime)) {
			} else if (baseType == typeof(DateTimeOffset)) {
			} else if (baseType.IsEnum == false) {
				// Unknown type
			}

			return doc;
		}

	}
}
