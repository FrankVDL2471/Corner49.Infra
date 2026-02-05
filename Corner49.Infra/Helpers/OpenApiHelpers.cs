using Microsoft.OpenApi.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.OpenApi;

namespace Corner49.Infra.Helpers {
	public static class OpenApiHelpers {



		public static void BuildSchemas(this OpenApiOptions options) {
			options.AddSchemaTransformer((schema, context, cancelToken) => {
				if (context.JsonPropertyInfo?.PropertyType == null) return Task.CompletedTask;

				schema.CompleteSchema(context.JsonPropertyInfo.PropertyType, context.JsonPropertyInfo.Name);


				return Task.CompletedTask;
			});
		}
		public static void BuildOperations(this OpenApiOptions options) {
			options.AddOperationTransformer((operation, context, cancellationToken) => {
				if ((context.Description.HttpMethod == "GET") || (context.Description.HttpMethod == "DELETE")) {
					foreach (var parameter in context.Description.ActionDescriptor.Parameters) {
						if (parameter.ParameterType.IsValueType) continue;
						if (parameter.ParameterType == typeof(string)) continue;


						var props = parameter.ParameterType.GetProperties();
						foreach (var prp in props) {

							var arg = operation.Parameters?.FirstOrDefault(c => c.Name == prp.Name);
							if (arg == null) continue;

							arg.Description = prp.GetCustomAttribute<DescriptionAttribute>()?.Description ?? prp.GetCustomAttribute<DisplayAttribute>()?.Description ?? arg.Description;
							arg.Required = prp.GetCustomAttribute<RequiredAttribute>() != null;
							arg.Deprecated = prp.GetCustomAttribute<ObsoleteAttribute>() != null;

							arg.Schema.CompleteSchema(prp.PropertyType, prp.Name);
							arg.Schema.ReadOnly = prp.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly == true;
							//arg.Schema.LinkReference(prp.PropertyType);


							var def = prp.GetCustomAttribute<DefaultValueAttribute>()?.Value;
							if ((arg.Schema != null) && (def != null)) {
								if (def is string txt) {
									arg.Schema.Default = new Microsoft.OpenApi.Any.OpenApiString(txt);
								} else if (def is int nr) {
									arg.Schema.Default = new Microsoft.OpenApi.Any.OpenApiInteger(nr);
								} else if (def is double dbl) {
									arg.Schema.Default = new Microsoft.OpenApi.Any.OpenApiDouble(dbl);
								} else if (def is bool flag) {
									arg.Schema.Default = new Microsoft.OpenApi.Any.OpenApiBoolean(flag);
								} else if (def is DateTime dt) {
									arg.Schema.Default = new Microsoft.OpenApi.Any.OpenApiDateTime(dt);
								}
							}
						}
					}

				} else if ((context.Description.HttpMethod == "POST") || (context.Description.HttpMethod == "PUT")) {

					//foreach (var parameter in context.Description.ActionDescriptor.Parameters) {
					//	if (parameter.ParameterType.IsValueType) continue;
					//	if (parameter.ParameterType == typeof(string)) continue;



					//	foreach (var body in operation.RequestBody.Content.Values) {
					//		body.Schema.Type = parameter.ParameterType.Name;
					//		body.Schema.CompleteSchema(parameter.ParameterType);
					//	}

					//}

				}

				//if (operation.Responses != null) {
				//	foreach (var response in operation.Responses) {
				//		foreach (var body in response.Value.Content.Values) {
				//			var tp = context.Description.SupportedResponseTypes?.FirstOrDefault()?.Type;
				//			if (tp != null) {
				//				body.Schema.CompleteSchema(tp);
				//				//body.Schema.LinkReference(tp);
				//				//body.Schema.Type = tp.Name;
				//			}
				//		}
				//	}
				//}

				return Task.CompletedTask;
			});
		}



		private static Dictionary<Type, string> _knownSchemas = new Dictionary<Type, string>();

		public static OpenApiSchema LinkReference(this OpenApiSchema doc, Type prpType) {
			var baseType = Nullable.GetUnderlyingType(prpType) ?? prpType;

			if (baseType.Namespace == "System.Collections.Generic") {
				var tp = baseType.GenericTypeArguments[0];
				doc.Type = null;
				doc.Items.Type = null;
				doc.Items.Reference = new OpenApiReference {
					Type = ReferenceType.Schema,
					Id = tp.Name
				};
			} else {
				doc.Type = null;
				doc.Reference = new Microsoft.OpenApi.Models.OpenApiReference {
					Type = Microsoft.OpenApi.Models.ReferenceType.Schema,
					Id = baseType.Name
				};
			}
			return doc;
		}

		public static OpenApiSchema CompleteSchema(this OpenApiSchema doc, Type prpType, string? prpName = null) {
			var baseType = Nullable.GetUnderlyingType(prpType) ?? prpType;


			if (prpType.IsEnum == true) {
				if (doc.Enum?.Any() != true) {
					foreach (var nm in Enum.GetNames(prpType)) {
						doc.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(nm));
					}
					doc.Type = null;
					doc.Nullable = Nullable.GetUnderlyingType(prpType) != null;
				}
			} else if (baseType == typeof(decimal)) {
				doc.Format = "decimal";
			} else if (baseType.Namespace == "System.Collections.Generic") {
				var tp = baseType.GenericTypeArguments[0];
				if (doc.Items != null) doc.Items.CompleteSchema(tp);
			} else if ((!prpType.IsValueType) && (prpType != typeof(string))) {
				//if (_knownSchemas.ContainsKey(baseType)) {
				//	doc.Type = baseType.Name;
				//	doc.Reference = new Microsoft.OpenApi.Models.OpenApiReference {
				//		Type = Microsoft.OpenApi.Models.ReferenceType.Schema,
				//		Id = _knownSchemas[baseType]
				//	};
				//	return doc;
				//}
				//_knownSchemas.Add(baseType, baseType.Name);


				doc.Type = baseType.Name;
				foreach (var prp in prpType.GetProperties()) {
					if (prp.Name == "experiences") {
						Console.WriteLine($"VoucherExperience");
					}


					var key = doc.Properties.Keys.FirstOrDefault(c => c.Equals(prp.Name, StringComparison.OrdinalIgnoreCase));
					if (key == null) continue;
					var arg = doc.Properties[key];

					arg.Description = prp.GetCustomAttribute<DescriptionAttribute>()?.Description ?? prp.GetCustomAttribute<DisplayAttribute>()?.Description ?? arg.Description;
					arg.Deprecated = prp.GetCustomAttribute<ObsoleteAttribute>() != null;
					arg.ReadOnly = prp.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly ?? false;


					arg.CompleteSchema(prp.PropertyType, prp.Name);


					var def = prp.GetCustomAttribute<DefaultValueAttribute>()?.Value;
					if ((arg != null) && (def != null)) {
						if (def is string txt) {
							arg.Default = new Microsoft.OpenApi.Any.OpenApiString(txt);
						} else if (def is int nr) {
							arg.Default = new Microsoft.OpenApi.Any.OpenApiInteger(nr);
						} else if (def is double dbl) {
							arg.Default = new Microsoft.OpenApi.Any.OpenApiDouble(dbl);
						} else if (def is bool flag) {
							arg.Default = new Microsoft.OpenApi.Any.OpenApiBoolean(flag);
						} else if (def is DateTime dt) {
							arg.Default = new Microsoft.OpenApi.Any.OpenApiDateTime(dt);
						}
					}
				}
			} else if (baseType == typeof(int)) {
			} else if (baseType == typeof(Int64)) {
			} else if (baseType == typeof(string)) {
			} else if (baseType == typeof(bool)) {
			} else if (baseType == typeof(DateTime)) {
			} else if (baseType == typeof(DateTimeOffset)) {

			} else if (baseType.IsEnum == false) {
				Console.WriteLine("unkown type");
			}



			return doc;
		}

	}
}
