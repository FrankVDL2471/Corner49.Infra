namespace Corner49.Infra.Jobs {
	public class JobInfo {

		public string? Id { get; set; }

		public string? Type { get; set; }

		public string? Name { get; set; }
		public Dictionary<string, string>? Args { get; set; }
		public string? Status { get; set; }

	}
}
