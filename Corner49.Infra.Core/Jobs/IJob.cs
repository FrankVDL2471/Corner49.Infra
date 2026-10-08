namespace Corner49.Infra.Jobs {
	public interface IJob {

		public Task Execute(string[]? args = null, CancellationToken cancellationToken = default);
	}
}
