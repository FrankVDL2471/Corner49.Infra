namespace Corner49.Infra.Messages {
	public abstract class MessageBase {

		public MessageBase(string name, bool useQueue= false) {
			this.Name = name;
			this.UseQueue = useQueue;
		}

		/// <summary>
		/// Topic or Queue Name
		/// </summary>
		public string Name { get; init; }

		public bool UseQueue { get; init; }

		public string Action { get; set; }


		public virtual string GetMessageId() {
			return null;
		}
	}
}
