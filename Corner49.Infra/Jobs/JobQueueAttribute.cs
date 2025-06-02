using Hangfire.Common;
using Hangfire.States;
using System.Globalization;

namespace Corner49.Infra.Jobs {

	public class JobQueueAttribute : JobFilterAttribute, IElectStateFilter {
		public static string Queue => System.Environment.MachineName.ToLower();

		public JobQueueAttribute() {
			Order = int.MaxValue;			
		}

		public void OnStateElection(ElectStateContext context) {
			if (context.CandidateState is EnqueuedState enqueuedState) {
				enqueuedState.Queue = string.Format(CultureInfo.InvariantCulture, Queue, context.BackgroundJob.Job.Args.ToArray());
			}
		}
	}

}
