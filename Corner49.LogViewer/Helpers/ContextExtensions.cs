namespace Corner49.LogViewer.Helpers {
	public static class ContextExtensions {
		public static string GetRedirect(this HttpContext context, string path) {
			if (context.Request.QueryString.HasValue) {
				return path + context.Request.QueryString.Value;
			}
			return path;
		}
		
	}
}
