namespace Corner49.Infra.Health {
	public interface IHealthStatus {

		public string Name { get; }

		public bool IsRunning { get; }
	}
}
