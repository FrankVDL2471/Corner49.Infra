namespace Corner49.Infra.Jobs {

	public interface IJobConfig {
		string ConnectString { get; set; }
		string DbName { get; set; }
		string ContainerName { get; set; }	
		bool UseLocalQueue { get; set; }

		bool UseSqlServer { get; set; }

		bool DisableAutomaticRestart { get; set; }

		bool EnableDashboard { get; set; }	
	}

	public class JobConfig : IJobConfig {

		public JobConfig() {
			this.EnableDashboard = true;
		}

		public string ConnectString { get; set; }

		public string DbName { get; set; }
		public string ContainerName { get; set; }

		public bool UseLocalQueue { get; set; }

		public bool UseSqlServer { get; set; }

		public bool DisableAutomaticRestart { get; set; }	

		public bool EnableDashboard { get; set; }
	}
}
