namespace Corner49.FormBuilder {
	public class ValuesAttribute : Attribute {

		private readonly object[] _values;

		public ValuesAttribute(params object[] values) {
			_values = values;
		}

		public object[] Values => _values;


	}

	[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
	public class ValueAttribute : Attribute {

		private readonly string _name;
		private readonly object _value;

		public ValueAttribute(string name, object value) {
			_name = name;
			_value = value;
		}

		public string Name => _name;

		public object Value => _value;

	}
}
