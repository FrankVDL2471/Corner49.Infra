using Corner49.FormBuilder.Builder;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Corner49.FormBuilder {
	public static class HelperExtensions {


		public static FormFieldBuilder BuildForm(this IHtmlHelper htmlHelper, HttpContext context, object data) {

			//var localize = context.RequestServices.GetService<IStringLocalizer<SharedResources>>();
			//var models = context.RequestServices.GetService<IStringLocalizer<ModelResources>>();

			var builder = new FormFieldBuilder(htmlHelper);
			builder.LoadData(data);
			return builder;
		}

		public static FormFieldBuilder BuildForm(this IHtmlHelper htmlHelper, object data) {

			var builder = new FormFieldBuilder(htmlHelper);
			builder.LoadData(data);
			return builder;
		}


		public static FormFieldBuilder BuildForm(this RenderTreeBuilder renderTree, object data) {
			var builder = new FormFieldBuilder(renderTree);
			builder.LoadData(data);
			return builder;
		}





	}
}
