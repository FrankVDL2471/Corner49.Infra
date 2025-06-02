namespace Corner49.Infra.DB {
	public class QueryResult<T> where T : class {


		public QueryResult() { 
			this.Data = new List<T>();	
		}

		/// <summary>
		/// Total number of records in the query
		/// </summary>
		public int? TotalCount { get; set; }

		public string? ContinuationToken { get; set; }	

		public IList<T> Data { get;init; }

	}
}
