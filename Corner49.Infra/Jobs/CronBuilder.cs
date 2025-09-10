namespace Corner49.Infra.Jobs {
	public class CronBuilder {
		private string _min = "*";
		private string _hour = "*";
		private string _day = "*";
		private string _month = "*";
		private string _dayOfMonth = "*";

		public CronBuilder() {
		}

		public CronBuilder WithDayOfMonth(int dayOfMonth) {
			_dayOfMonth = dayOfMonth.ToString();
			return this;
		}

		public CronBuilder WithHour(int hour) {
			var dt = DateTime.Today.AddHours(hour).ToUniversalTime();  //convert to utc
			_hour = dt.Hour.ToString();
			return this;
		}
		public CronBuilder EveryHour(int stepHour) {
			_hour = "*/" + stepHour.ToString();
			return this;
		}

		public CronBuilder WithMinute(int minute) {
			_min = minute.ToString();
			return this;
		}

		public CronBuilder EveryMinute(int stepMinute) {
			_min = "*/" + stepMinute.ToString();
			return this;
		}

		public override string ToString() {
			return $"{_min} {_hour} {_day} {_month} {_dayOfMonth}";
		}
	}
}