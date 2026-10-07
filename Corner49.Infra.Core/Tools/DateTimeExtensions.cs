using System.Globalization;
using TimeZoneConverter;

namespace Corner49.Infra.Tools {
	/// <summary>
	/// Exception that occurs when the booking does not exist.
	/// </summary>
	public static class DateTimeExtensions {
		public const string BrusselsTimezone = "Europe/Brussels";

		//public static DateTime ConvertServerTimeToUtc(this DateTime datetime) {
		//	if (datetime.Kind == DateTimeKind.Utc) return datetime;

		//	var tz = TZConvert.GetTimeZoneInfo(BrusselsTimezone);
		//	if (tz.IsInvalidTime(datetime)) {
		//		//timestamp is in a 'hole of time' the clock has been reverted by one hour due to daylightsavings
		//		datetime = datetime.AddHours(1);
		//	}
		//	return TimeZoneInfo.ConvertTimeToUtc(datetime, tz);
		//}

		//public static DateTime? ConvertServerTimeToUtc(this DateTime? datetime) {
		//	if (!datetime.HasValue) {
		//		return datetime;
		//	}

		//	return datetime.Value.ConvertServerTimeToUtc();
		//}

		//public static DateTime ConvertServerTimeFromUtc(this DateTime datetime) {
		//	return TimeZoneInfo.ConvertTimeFromUtc(datetime, TZConvert.GetTimeZoneInfo(BrusselsTimezone));
		//}

		//public static DateTime? ConvertServerTimeFromUtc(this DateTime? datetime) {
		//	if (!datetime.HasValue) {
		//		return datetime;
		//	}

		//	return datetime.Value.ConvertServerTimeFromUtc();
		//}

		//public static DateTime ConvertLocalTimeToUtc(this DateTime dateTime, string timeZone) {
		//	if (dateTime.Kind != DateTimeKind.Unspecified) {
		//		throw new ArgumentException("Datetime kind needs to be DateTimeKind.Unspecified");
		//	}

		//	return TimeZoneInfo.ConvertTimeToUtc(dateTime, TZConvert.GetTimeZoneInfo(timeZone));
		//}



		//public static DateTimeOffset ConvertDateTimeOffsetFromDateTimeOffset(this DateTimeOffset dateTime, string timeZone) {
		//	var offset = TZConvert.GetTimeZoneInfo(timeZone).GetUtcOffset(dateTime.UtcDateTime);
		//	var dateTimeOffset = new DateTimeOffset(dateTime.UtcDateTime, new TimeSpan(0)).ToOffset(offset);
		//	return dateTimeOffset;
		//}


		//public static DateTimeOffset ConvertServerTimeToDateTimeOffset(this DateTime dateTime) {
		//	return dateTime.ConvertLocalTimeToDateTimeOffset(BrusselsTimezone);
		//}

		//public static DateTimeOffset ConvertLocalTimeToDateTimeOffset(this DateTime dateTime, string timeZone) {
		//	var utcDateTime = dateTime.ConvertLocalTimeToUtc(timeZone);
		//	return utcDateTime.ConvertUtcTimeToDateTimeOffset(timeZone);
		//}

		//public static DateTimeOffset ConvertUtcTimeToDateTimeOffset(this DateTime utcDateTime, string timeZone) {
		//	var offset = TZConvert.GetTimeZoneInfo(timeZone).GetUtcOffset(utcDateTime);
		//	var dateTimeOffset = new DateTimeOffset(utcDateTime, new TimeSpan(0)).ToOffset(offset);
		//	return dateTimeOffset;
		//}


		public static string ToLocalString(this DateTimeOffset dateTime, string format = "dd/MM/yyyy HH:mm:ss", string? timeZone = null, string language = null) {
			var dt = dateTime.UtcDateTime.ConvertToLocalTime(timeZone);
			if (language != null) return dt.FormatToString(format, language);
			return dt.ToString(format);
		}


		public static string ToLocalString(this DateTime dateTime, string format = "dd/MM/yyyy HH:mm:ss", string? timeZone = null, string language = null) {

			var dt = dateTime.ConvertToLocalTime(timeZone);
			if (language != null) return dt.FormatToString(format, language);
			return dt.ToString(format);
		}



		public static DateTime ConvertToUtc(this DateTime dateTime, string? timeZone = null) {
			if (dateTime.Kind == DateTimeKind.Utc) return dateTime;


			DateTime dt = dateTime.Kind == DateTimeKind.Unspecified ? dateTime : new DateTime(dateTime.Ticks, DateTimeKind.Unspecified);
			return TimeZoneInfo.ConvertTimeToUtc(dt, TZConvert.GetTimeZoneInfo(timeZone ?? BrusselsTimezone));
		}


		public static DateTime ConvertToLocalTime(this DateTime dateTime, string? timeZone = null) {
			if (dateTime.Kind == DateTimeKind.Local) return dateTime;
			return TimeZoneInfo.ConvertTimeFromUtc(dateTime, TZConvert.GetTimeZoneInfo(timeZone ?? BrusselsTimezone));
		}




		public static string FormatToString(this DateTime dateTime, string format, string language) {
			var culture = CultureInfo.GetCultures(CultureTypes.AllCultures).FirstOrDefault(x => x.Name == language.ToLower());
			if (culture == null) {
				culture = CultureInfo.CurrentCulture;
			}
			return dateTime.ToString(format, culture);
		}


		public static DateTime FirstOfMonth(this DateTime dateTime) {
			return new DateTime(dateTime.Year, dateTime.Month, 1);
		}





	}
}
