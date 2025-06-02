namespace Corner49.Infra.Http {

	public class ApiClientException : Exception {
		/// <summary>
		/// An exception that gets thrown from the platforms side.
		/// </summary>
		/// <param name="message">The message containing the reason why the call failed.</param>
		/// <param name="inner">The inner <see cref="Exception"/> object.</param>
		public ApiClientException(string message, Exception inner) : base(message, inner) {
		}

		public ApiClientException(string message) : base(message) {
		}

	}
}
