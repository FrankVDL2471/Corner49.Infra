using Corner49.FormBuilder;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Corner49.LogViewer.Models {
	public class LogFilter {

		public LogFilter() {
			this.Sorting = "DESC";
		}

		[Category("Filter")]
		public string? App { get; set; }


		[Category("Filter")]
		[DataType(DataType.Date)]
		public DateTime? Date { get; set; }

		[Category("Filter")]
		[Value("", null)]
		[Value("00", 00)]
		[Value("01", 01)]
		[Value("02", 02)]
		[Value("03", 03)]
		[Value("04", 04)]
		[Value("05", 05)]
		[Value("06", 06)]
		[Value("07", 07)]
		[Value("08", 08)]
		[Value("09", 09)]
		[Value("10", 10)]
		[Value("11", 11)]
		[Value("12", 12)]
		[Value("13", 13)]
		[Value("14", 14)]
		[Value("15", 15)]
		[Value("16", 16)]
		[Value("17", 17)]
		[Value("18", 18)]
		[Value("19", 19)]
		[Value("20", 20)]
		[Value("21", 21)]
		[Value("22", 22)]
		[Value("23", 23)]
		public int? Hour { get; set; }


		[Category("Filter")]
		[Value("", "")]
		[Value("Information", "Information")]
		[Value("Warning", "Warning")]
		[Value("Error", "Error")]
		public string? Level { get; set; }


		[Category("Filter")]
		[Value("Oldest First", "ASC")]
		[Value("Newest First", "DESC")]
		public string Sorting { get; set; }

	}
}
