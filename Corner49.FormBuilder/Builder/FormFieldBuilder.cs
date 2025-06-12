using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Corner49.FormBuilder.Builder {
	public class FormFieldBuilder {

		private readonly IHtmlHelper? _html;
		private readonly RenderTreeBuilder? _builder;

		private List<FormField> _fields = new List<FormField>();

		private readonly List<string> _categories = new List<string>();
		private string _prefix = null;
		private bool? _readOnly = null;
		private int _onboaring = -1;

		private IViewLocalizer _viewLocalizer;


		public FormFieldBuilder(IHtmlHelper htmlHelper) {
			_html = htmlHelper;
		}

		public FormFieldBuilder(RenderTreeBuilder renderTree) {
			_builder = renderTree;
		}

		private object _data;

		private Func<FormField, object> _getValue;

		public void LoadData(object data) {
			_data = data;
			_getValue = (fld) => {
				return fld.GetValue(data);
			};

			if (data == null) return;
			foreach (var prp in _data.GetType().GetProperties()) {
				var fld = new FormField();
				fld.Load(prp);
				if (!fld.Visible) continue;
				_fields.Add(fld);
			}
		}




		//public static FormFieldBuilder Create(IHtmlHelper html, object data) {
		//	var bld =  new FormFieldBuilder(html);
		//	bld.LoadData(data);
		//	return bld;
		//}



		public FormFieldBuilder WithOnboarding(int onboarding) {
			_onboaring = onboarding;
			return this;
		}

		public FormFieldBuilder WithCategory(string category) {
			_categories.Add(category);
			return this;
		}

		public FormFieldBuilder WithPrefix(string prefix) {
			_prefix = prefix;
			return this;
		}

		public FormFieldBuilder WithLocalizer(IViewLocalizer viewLocalizer) {
			_viewLocalizer = viewLocalizer;
			return this;
		}

		public FormFieldBuilder WithLookup(string fldName, IEnumerable<KeyValuePair<object, string>> values, bool addEmpty = false) {
			var fld = _fields.FirstOrDefault(f => f.Name.Equals(fldName, StringComparison.OrdinalIgnoreCase));
			if (fld == null) return this;
			fld.SelectOptions = values?.ToList() ?? new List<KeyValuePair<object, string>>();
			if (addEmpty) {
				if (fld.SelectOptions.Any()) {
					fld.SelectOptions.Insert(0, new KeyValuePair<object, string>(string.Empty, string.Empty));
				} else {
					fld.SelectOptions.Add(new KeyValuePair<object, string>(string.Empty, string.Empty));
				}

			}
			return this;
		}
		public FormFieldBuilder WithLookup(string fldName, Type enumType, bool addEmpty = false) {
			if (!enumType.IsEnum) return this;

			var fld = _fields.FirstOrDefault(f => f.Name.Equals(fldName, StringComparison.OrdinalIgnoreCase));
			if (fld == null) return this;


			fld.SelectOptions = new List<KeyValuePair<object, string>>();
			if (addEmpty) fld.SelectOptions.Add(new KeyValuePair<object, string>(null, string.Empty));

			foreach(var key in Enum.GetNames(enumType)) {
				var val = Enum.Parse(enumType, key);
				fld.SelectOptions.Add(new KeyValuePair<object, string>(val, key));
			}
			return this;
		}



		public FormFieldBuilder RemoveField(string fldName, Func<bool> check = null) {
			var fld = _fields.FirstOrDefault(f => f.Name.Equals(fldName, StringComparison.OrdinalIgnoreCase));
			if (fld == null) return this;

			if (check != null) {
				if (check()) _fields.Remove(fld);
			} else {
				_fields.Remove(fld);
			}

			return this;
		}


		public FormFieldBuilder AsReadonly(bool readOnly = true) {
			_readOnly = readOnly;
			return this;
		}

		public IHtmlContent Build(BuildOptions? options = null) {
			List<FormField> lst = new List<FormField>();

			foreach (var fld in _fields) {
				if (_categories.Any()) {
					if (fld.Category == null) continue;
					if (!_categories.Contains(fld.Category)) continue;
				}
				if (_onboaring > 0) {
					if (fld.Onboarding != _onboaring) continue;
				}
				if (!string.IsNullOrEmpty(_prefix)) {
					if (fld.Label == null) fld.Label = fld.Name;
					fld.Name = _prefix + fld.Name;
				}
				if (fld.Order < 0) fld.Order = lst.Count;
				if (_readOnly == true) fld.ReadOnly = true;
				lst.Add(fld);
			}

			var localizer = new FormLocalizer(null, null, _viewLocalizer);

			int idx = 0;
			int width = options?.ColumnWidth ?? (int)(12 / lst.Count);

			List<IHtmlContent> arr = new List<IHtmlContent>();
			foreach(var fld in lst.OrderBy(f => f.Order)) {
				if (options?.ColumnsSizes != null) {
					options.ColumnWidth = (idx < options?.ColumnsSizes?.Length) ? options.ColumnsSizes[idx] :width;
				}
				arr.Add(fld.Generate(_getValue.Invoke(fld), _html.ViewContext.ModelState, localizer, options));
				idx++;
			}

			string result = null;
			using (var writer = new StringWriter()) {
				foreach (var tag in arr) {
					tag.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
				}
				result = writer.ToString();
			}

			return new HtmlString(result);
		}

		public void Render(bool singleRow) {
			List<FormField> lst = new List<FormField>();

			foreach (var fld in _fields) {
				if (_categories.Any()) {
					if (fld.Category == null) continue;
					if (!_categories.Contains(fld.Category)) continue;
				}
				if (_onboaring > 0) {
					if (fld.Onboarding != _onboaring) continue;
				}
				if (!string.IsNullOrEmpty(_prefix)) {
					if (fld.Label == null) fld.Label = fld.Name;
					fld.Name = _prefix + fld.Name;
				}
				if (fld.Order < 0) fld.Order = lst.Count;
				if (_readOnly == true) fld.ReadOnly = true;
				lst.Add(fld);
			}

		

			var localizer = new FormLocalizer(null, null, _viewLocalizer);
			foreach(var fld in lst.OrderBy(f => f.Order)) {
				fld.Render(_builder, _getValue.Invoke(fld), null, localizer, singleRow);
			};

		}



	}
}
