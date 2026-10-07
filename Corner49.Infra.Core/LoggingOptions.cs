using System.ComponentModel;

namespace Corner49.Infra {

	public class LoggingOptions {


		public LoggingOptions() {
			this.TrackActivity = true;
			this.TrackDependencies = false;
			this.TrackContent = false;
		}

		[DefaultValue(true)]
		public bool TrackActivity { get; set; }

		[DefaultValue(false)]
		public bool TrackDependencies { get; set; }

		[DefaultValue(false)]
		public bool TrackContent { get; set; }


		[DefaultValue(false)]
		public bool WriteToConsoleAsJson { get; set; }


		/// <summary>
		/// Filter logging bases on category prefix
		/// </summary>
		public string[]? FilterCategoryPrefix { get; set; }


	}
}
