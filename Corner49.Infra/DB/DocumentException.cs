namespace Corner49.Infra.DB {
	public class DocumentException : System.Exception {

		public DocumentException() : base() { }

		public DocumentException(string message, Exception? innerException = null) : base(message, innerException) { }


	}

	public class DocumentContainerNotFoundException : DocumentException {

		public DocumentContainerNotFoundException(string name) : base($"Container '{name}' not found or initialized") { }

	}

	




}
