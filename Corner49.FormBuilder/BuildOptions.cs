namespace Corner49.FormBuilder {
	public class BuildOptions {

		/// <summary>
		/// Show the label and controll on a single row
		/// </summary>
		public bool SingleRow { get; set; }

		/// <summary>
		/// Don't show the label as control, but as placeholder
		/// </summary>
		public bool LabelAsPlaceholder { get; set; }

		/// <summary>
		/// Render form in a horizontal view, set the column size (1 - 12)
		/// </summary>
		public int ColumnWidth { get; set; }

		public int[]? ColumnsSizes { get; set; }

	}
}
