using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;

namespace Corner49.FormBuilder.Builder {
	public class FormLocalizer {

		private IStringLocalizer _model;
		private IStringLocalizer _shared;
		private IViewLocalizer _view;

		public FormLocalizer(IStringLocalizer modelLocalizer, IStringLocalizer sharedLocalizer, IViewLocalizer viewLocalizer) {
			_model = modelLocalizer;
			_shared = sharedLocalizer;
			_view = viewLocalizer;
		}

		public string GetTranslation(string key, string defaultValue) {
			if (_view != null) {
				var trans = _view.GetString(key);
				if (trans != key) return trans;
			}
			if (_model != null) {
				var trans = _model.GetString(key);
				if (trans != key) return trans;
			}
			if (_shared != null) {
				var trans = _shared.GetString(key);
				if (trans != key) return trans;
			}

			return defaultValue;

		}

	}
}
