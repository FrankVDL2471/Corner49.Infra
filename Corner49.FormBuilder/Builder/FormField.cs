using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Corner49.FormBuilder.Builder {


	public class FormField {


		public string FullName { get; set; }
		public string Name { get; set; }
		public string Label { get; set; }
		public string Description { get; set; }

		public string? Placeholder { get; set; }

		public string Type { get; set; }

		public string Step { get; set; }

		public string Category { get; set; }

		public int Order { get; set; } = -1;

		public bool Visible { get; set; } = true;

		public bool ReadOnly { get; set; } = false;

		public bool Required { get; set; } = false;

		public IList<KeyValuePair<object, string>> SelectOptions { get; set; }

		public int Onboarding { get; set; } = -1;

		private PropertyInfo _prop;

		public void Load(PropertyInfo prp) {
			_prop = prp;
			Name = prp.Name;

			FullName = $"{prp.DeclaringType.Name}.{prp.Name}";


			//https://www.learnrazorpages.com/razor-pages/forms
			if (prp.PropertyType.IsAssignableTo(typeof(int))) {
				Type = "number";
			} else if (prp.PropertyType.IsAssignableTo(typeof(double))) {
				Type = "number";
				Step = "0.01";
			//} else if (prp.PropertyType.IsAssignableTo(typeof(decimal))) {
			//	Type = "number";
			//} else if (prp.PropertyType.IsAssignableTo(typeof(decimal?))) {
			//	Type = "number";
			} else if (prp.PropertyType.IsAssignableTo(typeof(DateTime))) {
				Type = "datetime";
			} else if (prp.PropertyType.IsAssignableTo(typeof(DateTime?))) {
				Type = "datetime";
			} else if (prp.PropertyType.IsAssignableTo(typeof(DateTimeOffset))) {
				Type = "datetime-local";
			} else if (prp.PropertyType.IsAssignableTo(typeof(DateTimeOffset?))) {
				Type = "datetime-local";
			} else if (prp.PropertyType.IsAssignableTo(typeof(bool))) {
				SelectOptions = new List<KeyValuePair<object, string>>();
				SelectOptions.Add(new KeyValuePair<object, string>(true, "Yes"));
				SelectOptions.Add(new KeyValuePair<object, string>(false, "No"));
			} else if (prp.PropertyType.IsAssignableTo(typeof(bool?))) {
				SelectOptions = new List<KeyValuePair<object, string>>();
				SelectOptions.Add(new KeyValuePair<object, string>(null, ""));
				SelectOptions.Add(new KeyValuePair<object, string>(true, "Yes"));
				SelectOptions.Add(new KeyValuePair<object, string>(false, "No"));
			} else if (prp.PropertyType.IsEnum) {
				Type = null;
				SelectOptions = new List<KeyValuePair<object, string>>();

				var values = Enum.GetValues(prp.PropertyType);
				var names = Enum.GetNames(prp.PropertyType);
				for (int i = 0; i < names.Length; i++) {
					SelectOptions.Add(new KeyValuePair<object, string>(values.GetValue(i), names[i]));
				}
			}



			var valueAttr = prp.GetCustomAttribute<ValuesAttribute>(false);
			if (valueAttr != null) {
				Type = null;
				SelectOptions = new List<KeyValuePair<object, string>>();
				foreach (var val in valueAttr.Values) {
					SelectOptions.Add(new KeyValuePair<object, string>(val, val.ToString()));
				}
			}
			var valueAttrs = prp.GetCustomAttributes<ValueAttribute>(false);
			if (valueAttrs != null && valueAttrs.Any()) {
				Type = null;
				SelectOptions = new List<KeyValuePair<object, string>>();
				foreach (var val in valueAttrs) {
					SelectOptions.Add(new KeyValuePair<object, string>(val.Value, val.Name));
				}
			}


			var dataTypeAttr = prp.GetCustomAttribute<DataTypeAttribute>(false);
			if (dataTypeAttr != null) {
				if (dataTypeAttr.DataType == DataType.Date) {
					Type = "date";
				} else if (dataTypeAttr.DataType == DataType.EmailAddress) {
					Type = "email";
				} else if (dataTypeAttr.DataType == DataType.PhoneNumber) {
					Type = "tel";
				} else if (dataTypeAttr.DataType == DataType.Password) {
					Type = "password";
				} else if (dataTypeAttr.DataType == DataType.MultilineText) {
					Type = "textarea";
				//} else if (dataTypeAttr.DataType == DataType.Currency) {
				//	Type = "number";
				} else if (dataTypeAttr.DataType == DataType.Upload) {
					Type = "image";
				}
			}

			var displayAttr = prp.GetCustomAttribute<DisplayAttribute>(false);
			if (displayAttr != null) {
				Label = displayAttr.Name;
				Description = displayAttr.Description;
				Category = displayAttr.GroupName;
				Order = displayAttr.GetOrder() ?? -1;
				Visible = displayAttr.GetAutoGenerateField() != false;
				Placeholder = displayAttr.Prompt;
			} else {
				var displayNameAttr = prp.GetCustomAttribute<DisplayNameAttribute>(false);
				if (displayNameAttr != null) {
					Label = displayNameAttr.DisplayName;
				}
				var discriptionAttr = prp.GetCustomAttribute<DescriptionAttribute>(false);
				if (discriptionAttr != null) {
					Description = discriptionAttr.Description;
				}


				var browsableAttr = prp.GetCustomAttribute<BrowsableAttribute>(false);
				if (browsableAttr != null) {
					if (!browsableAttr.Browsable) {
						Visible = false;
					}
				}

			}

			var catAttr = prp.GetCustomAttribute<CategoryAttribute>(false);
			if (catAttr?.Category != null) {
				Category = catAttr.Category;
			}


			var requiredAttr = prp.GetCustomAttribute<RequiredAttribute>(false);
			if (requiredAttr != null) {
				Required = true;
			}

			var readonlyAttr = prp.GetCustomAttribute<ReadOnlyAttribute>(false);
			if (readonlyAttr != null) {
				ReadOnly = readonlyAttr.IsReadOnly;
			}
		}


		public object GetValue(object data) {
			if (_prop == null) _prop = data.GetType().GetProperty(Name);
			return _prop.GetValue(data);
		}

		public IHtmlContent GenerateField(object data, ModelStateDictionary modelState) {
			object val = GetValue(data);
			return Generate(val, modelState);
		}

		public IHtmlContent Generate(object val, ModelStateDictionary modelState, FormLocalizer localizer = null, BuildOptions? options = null) {
			if (val?.GetType()?.IsEnum == true) {
				val = val.ToString();
			}

			ModelStateEntry state = modelState[Name];

			var div = new TagBuilder("div");
			if ((options?.ColumnWidth ?? 0) > 0) div.AddCssClass($"col-sm-{options.ColumnWidth}");

			div.AddCssClass("form-group mb-3");
			if (options?.SingleRow == true) div.AddCssClass("row");

			string lbl = localizer.GetTranslation(FullName, Label ?? Name);

			TagBuilder? label = null;
			if (options?.LabelAsPlaceholder != true) {
				label = new TagBuilder("label");
				label.AddCssClass("control-label");
				if (options?.SingleRow == true) label.AddCssClass("col-sm-3");
				label.Attributes.Add("for", Name);

				label.InnerHtml.SetContent(lbl);
			}

			string description = localizer.GetTranslation($"{FullName}.Info", Description);


			if (Required && val == null) {
				//<span class="text-danger">*</span>
				var req = new TagBuilder("span");
				req.AddCssClass("text-danger");
				req.InnerHtml.SetContent("*");
				if (label != null) label.InnerHtml.AppendHtml(req);
			}
			if (label != null) div.InnerHtml.AppendHtml(label);

			if (Type == "image") {
				var imgDiv = new TagBuilder("div");
				imgDiv.Attributes.Add("id", "preview");

				if (val != null) {
					var img = new TagBuilder("img");
					img.AddCssClass("m-auto");
					img.Attributes.Add("src", val.ToString());
					img.Attributes.Add("height", "150");
					imgDiv.InnerHtml.AppendHtml(img);
				}

				div.InnerHtml.AppendHtml(imgDiv);
			}


			TagBuilder input;
			if (SelectOptions != null) {
				input = new TagBuilder("select");
			} else if (Type == "textarea") {
				input = new TagBuilder("textarea");
				input.Attributes.Add("rows", "10");
			} else {
				input = new TagBuilder("input");
			}

			var row = div;
			if (options?.SingleRow == true) {
				row = new TagBuilder("div");
				row.AddCssClass("col-sm-9");
			}


			//new TagBuilder(this.SelectOptions == null ? "input" : "select");
			if (Type != "image") {
				input.AddCssClass("form-control");
				input.AddCssClass("changable");
			}
			if (state?.ValidationState == ModelValidationState.Invalid) {
				input.AddCssClass("input-validation-error");
			}
			if (ReadOnly) {
				input.Attributes.Add("readonly", null);
			}

			input.Attributes.Add("id", Name);
			input.Attributes.Add("name", Name);
			if (Type != null) {
				if (Type == "image") {
					input.Attributes.Add("type", "file");
					input.Attributes.Add("accept", "image/png, image/jpeg");
				} else {
					input.Attributes.Add("type", Type);
				}
			}
			if (Step != null) input.Attributes.Add("step", Step);

			input.Attributes.Add("data-val", "true");
			input.Attributes.Add($"data-val-{Name}", "Invalid value");
			if (Required) {
				input.Attributes.Add($"data-val-required", "This field is required");
			}
			if (SelectOptions != null) {
				foreach (var itm in SelectOptions) {
					var opt = new TagBuilder("option");
					opt.Attributes.Add("value", itm.Key == null ? string.Empty : itm.Key.ToString());
					if (itm.Key == null && val == null) {
						opt.Attributes.Add("selected", "");
					} else if (val != null && itm.Key != null && itm.Key.GetType() == val.GetType()) {
						if (itm.Key.Equals(val)) {
							opt.Attributes.Add("selected", "");
						}
					} else if (itm.Key?.ToString().Equals(val) == true) {
						opt.Attributes.Add("selected", "");
					}
					opt.InnerHtml.SetContent(localizer.GetTranslation($"{FullName}+{itm.Key}", itm.Value));
					input.InnerHtml.AppendHtml(opt);
				}
			} else if (val != null) {
				if (Type == "datetime-local") {
					if (val is DateTime dt) {
						input.Attributes.Add("value", dt.ToString("yyyy-MM-ddTHH:mm:ss"));
					} else if (val is DateTimeOffset dto) {
						input.Attributes.Add("value", dto.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
					} else if (val != null) {
						input.Attributes.Add("value", val.ToString());
					}
				} else if (Type == "textarea") {
					input.InnerHtml.Append(val as string);
				} else if (Type == "date") {
					if (val is DateTime dt) {
						input.Attributes.Add("value", dt.ToString("yyyy-MM-dd"));
					}
				} else if (val is decimal dec) {
					input.Attributes.Add("value", dec.ToString(System.Globalization.CultureInfo.InvariantCulture));
				} else if (val is List<string> lst) {
					input.Attributes.Add("value", string.Join(",", lst));
				} else {
					input.Attributes.Add("value", val.ToString());
				}
			}
			if (!string.IsNullOrEmpty(description)) {
				input.Attributes.Add("title", description);
			}
			if (options?.LabelAsPlaceholder == true) {
				input.Attributes.Add("placeholder", lbl);
			} else {
				string prompt = Placeholder ?? localizer.GetTranslation($"{FullName}.Prompt", null);
				if (!string.IsNullOrEmpty(prompt)) {
					input.Attributes.Add("placeholder", prompt);
				}
			}

			row.InnerHtml.AppendHtml(input);

			var span = new TagBuilder("span");
			span.AddCssClass("text-danger field-validation-valid");
			span.Attributes.Add("data-valmsg-replace", "true");
			span.Attributes.Add("data-valmsg-for", Name);

			if (state?.Errors != null) {
				string errMsg = string.Empty;
				foreach (var err in state.Errors) {
					errMsg += err.ErrorMessage;
				}
				if (!string.IsNullOrEmpty(errMsg)) {
					span.InnerHtml.SetContent(errMsg);
				}
			}

			row.InnerHtml.AppendHtml(span);

			if (!string.IsNullOrEmpty(description)) {
				var tooltip = new TagBuilder("small");
				tooltip.AddCssClass("form-text text-muted");
				tooltip.Attributes.Add("id", Name + "_tooltip");
				tooltip.InnerHtml.SetContent(description);

				row.InnerHtml.AppendHtml(tooltip);
			}

			if (options?.SingleRow == true) {
				div.InnerHtml.AppendHtml(row);
			}

			return div;
		}



		public void Render(RenderTreeBuilder builder, object val, ModelStateDictionary? modelState, FormLocalizer localizer = null, bool singleRow = false) {


			if (val?.GetType()?.IsEnum == true) {
				val = val.ToString();
			}

			ModelStateEntry state = null;
			if (modelState?.ContainsKey(Name) == true) {
				state = modelState[Name];
			}
			

			builder.OpenElement(0, "div");
			builder.AddAttribute(1, "class", "form-group mb-3");
			if (singleRow) builder.AddAttribute(2, "class", "row");	

			builder.OpenElement(3, "label");
			builder.AddAttribute(4, "class", "control-label");	
			builder.AddAttribute(5, "class", "col-sm-3");
			builder.AddAttribute(6, "for", Name);
			
			string lbl = localizer.GetTranslation(FullName, Label ?? Name);
			builder.AddContent(7, lbl);

			string description = localizer.GetTranslation($"{FullName}.Info", Description);

			if (Required && val == null) {
				//<span class="text-danger">*</span>
				builder.OpenElement(8, "span");
				builder.AddAttribute(9, "class", "text-danger");
				builder.AddContent(10, "*");
				builder.CloseElement();  //close span
			}
			builder.CloseElement(); //close label	


			if (Type == "image") {
				builder.OpenElement(11, "div");
				builder.AddAttribute(12, "id", "preview");

				if (val != null) {
					var img = new TagBuilder("img");
					builder.OpenElement(13, "img");
					builder.AddAttribute(14, "class", "m-auto");
					builder.AddAttribute(15, "src", val.ToString());
					builder.AddAttribute(16, "height", "150");
					builder.CloseElement(); //close img	
				}
				builder.CloseElement(); //close image div	
			}


			if (SelectOptions != null) {
				builder.OpenElement(17, "select");
			} else if (Type == "textarea") {
				builder.OpenElement(18, "textarea");
				builder.AddAttribute(19, "rows", "10");	
			} else {
				builder.OpenElement(20, "input");	
			}

			if (singleRow) {
				builder.OpenElement(21, "div");
				builder.AddAttribute(Order, "class", "col-sm-9");
			}


			//new TagBuilder(this.SelectOptions == null ? "input" : "select");
			if (Type != "image") {
				builder.AddAttribute(22, "class", "form-control");
			}
			if (state?.ValidationState == ModelValidationState.Invalid) {
				builder.AddAttribute(23, "class", "input-validation-error");	
			}
			if (ReadOnly) {
				builder.AddAttribute(24, "readonly");
			}

			builder.AddAttribute(25, "id", Name);
			builder.AddAttribute(25, "name", Name);

			if (Type != null) {
				if (Type == "image") {
					builder.AddAttribute(26, "type", "file");
					builder.AddAttribute(27, "accept", "image/png, image/jpeg");
				} else {
					builder.AddAttribute(28, "type", Type);					
				}
			}
			if (Step != null) builder.AddAttribute(29,"step", Step);

			builder.AddAttribute(30, "data-val", "true");
			builder.AddAttribute(31, $"data-val-{Name}", "Invalid value");
			if (Required) {
				builder.AddAttribute(32, $"data-val-required", "This field is required");
			}
			if (SelectOptions != null) {
				foreach (var itm in SelectOptions) {
					builder.OpenElement(33, "option");
					builder.AddAttribute(34, "value", itm.Key == null ? string.Empty : itm.Key.ToString());
					if (itm.Key == null && val == null) {
						builder.AddAttribute(35, "selected", "");	
					} else if (val != null && itm.Key != null && itm.Key.GetType() == val.GetType()) {
						if (itm.Key.Equals(val)) {
							builder.AddAttribute(35, "selected", "");
						}
					} else if (itm.Key?.ToString().Equals(val) == true) {
						builder.AddAttribute(35, "selected", "");
					}
					builder.AddContent(36, localizer.GetTranslation($"{FullName}+{itm.Key}", itm.Value));
					builder.CloseElement();
				}
			} else if (val != null) {
				if (Type == "datetime-local") {
					if (val is DateTime dt) {
						builder.AddAttribute(37, "value", dt.ToString("yyyy-MM-ddTHH:mm:ss"));
					} else if (val is DateTimeOffset dto) {
						builder.AddAttribute(37, "value", dto.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"));
					} else if (val != null) {
						builder.AddAttribute(37, "value",val.ToString());
					}
				} else if (Type == "textarea") {
					builder.AddContent(38, val as string);
				} else if (val is decimal dec) {
					builder.AddAttribute(39, "value", dec.ToString(System.Globalization.CultureInfo.InvariantCulture));

				} else {
					builder.AddAttribute(40, "value", val.ToString());
				}
			}
			if (!string.IsNullOrEmpty(description)) {
				builder.AddAttribute(41, "title", description);
			}
			string prompt = Placeholder ?? localizer.GetTranslation($"{FullName}.Prompt", null);
			if (!string.IsNullOrEmpty(prompt)) {
				builder.AddAttribute(42, "placeholder", prompt);	
			}
			builder.CloseElement();

			builder.OpenElement(43, "span");
			builder.AddAttribute(44, "class", "text-danger field-validation-valid");
			builder.AddAttribute(45, "data-valmsg-replace", "true");
			builder.AddAttribute(46, "data-valmsg-for", Name);	

			if (state?.Errors != null) {
				string errMsg = string.Empty;
				foreach (var err in state.Errors) {
					errMsg += err.ErrorMessage;
				}
				if (!string.IsNullOrEmpty(errMsg)) {
					builder.AddContent(47, errMsg);
				}
			}

			builder.CloseElement(); //close span
			

			if (!string.IsNullOrEmpty(description)) {
				builder.OpenElement(48, "small");
				builder.AddAttribute(49, "class", "form-text text-muted");	
				builder.AddAttribute(50, "id", Name + "_tooltip");
				builder.AddContent(51, description);
				builder.CloseElement();
			}

			if (singleRow) {
				builder.CloseElement(); //close row				
			}

			builder.CloseElement(); //div
		}


	}
}
