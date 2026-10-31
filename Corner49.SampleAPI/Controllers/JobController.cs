using Corner49.Infra.Jobs;
using Corner49.SampleAPI.Jobs;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace Corner49.SampleAPI.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class JobController : ControllerBase {


		public readonly IJobManager _jobManager;

		public JobController(IJobManager jobManager) {
			_jobManager = jobManager;
		}


		[HttpGet]
		public ActionResult RunDemo() {

			_jobManager.StartJob<DemoJob>(new Dictionary<string, string>() { { "arg1", "value1" } });

			return Ok();
		}

	}


}
