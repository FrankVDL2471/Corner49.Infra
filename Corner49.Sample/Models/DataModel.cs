using Corner49.FormBuilder;

namespace Corner49.Sample.Models {
	public class DataModel {


		public string Id { get; set; }	

		public string Name { get; set; }

		public bool CheckBox { get; set; }	

		public TestEnum EnumDropdown { get; set; }


		[Value("Value 1", "1")]
		[Value("Value 2", "2")]
		[Value("Value 3", "3")]
		public string ValuesDropDown { get; set; }	

	}


	public enum TestEnum {
		Enum1,
		Enum2,
		Enum3,
	}
}
