namespace Corner49.Storage {
	public class BlobInfo {

		public string? ContentType { get; set; }

		public string? ETag { get; set; }

		public long? ContentLength { get; set; }

		public DateTimeOffset? Date { get; set; }
	}
}
