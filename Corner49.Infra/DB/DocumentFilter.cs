namespace Corner49.DB.Tools {
	public class DocumentFilter<T> where T : class {

		public string? Search { get; set; }

		public int? Take { get; set; }

		public string? ContinuationToken { get; set; }

		public virtual IQueryable<T> Build(IQueryable<T> qry) {
			return qry;
		}

	}
}
