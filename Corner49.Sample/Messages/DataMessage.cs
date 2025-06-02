using Corner49.Infra.Messages;
using Corner49.Sample.Models;

namespace Corner49.Sample.Messages {
	public class DataMessage : MessageBase {

		public DataMessage() : base("data") {
		}

		public DataModel? Booking { get; set; }


	}


	public interface IDataMessageService {

		Task Created(DataModel booking);
		Task Updated(DataModel booking);
	}


	public class DataMessageService : MessageService<DataMessage>, IDataMessageService {
		public DataMessageService(ILogger<DataMessageService> logger, IServiceProvider serviceProvider) : base(logger, serviceProvider) {
		}

		public Task Created(DataModel booking) {
			DataMessage msg = new DataMessage();
			msg.Action = nameof(Created);
			msg.Booking = booking;
			return base.Send(msg);
		}
		public Task Updated(DataModel booking) {
			DataMessage msg = new DataMessage();
			msg.Action = nameof(Updated);
			msg.Booking = booking;
			return base.Send(msg);
		}


	}

}
