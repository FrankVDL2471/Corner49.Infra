namespace Corner49.Infra.Tools {
	public static class DateTimeHelper {

		public static int DateToInt(this DateTime dt) {
			return dt.Year * 10000 + dt.Month * 100 + dt.Day;
		}
		

		public static DateTime DateFromInt(int dt) {
			int year = dt / 10000;
			int month = (dt - year * 10000) / 100;
			int day = dt % 100;

			return new DateTime(year, month, day);

		}
	}
}
