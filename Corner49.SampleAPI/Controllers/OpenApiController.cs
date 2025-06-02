using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Corner49.SampleAPI.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class OpenApiController : ControllerBase {



		[EndpointName("Get Model")]
		[EndpointDescription("Test OpenAPI model")]
		[ProducesResponseType<TestModel>(StatusCodes.Status200OK, "application/json")]
		[HttpGet]
		public ActionResult<TestModel> Get(string id) {
			return new TestModel();
		}


		[EndpointName("Query Model")]
		[EndpointDescription("Test OpenAPI model")]
		[ProducesResponseType<TestModel>(StatusCodes.Status200OK, "application/json")]
		[HttpGet("Query")]
		public ActionResult<TestModel> Query([FromQuery] TestModel filter) {
			return new TestModel();
		}

		[EndpointName("Post Model")]
		[EndpointDescription("Post OpenAPI model")]
		[ProducesResponseType<TestModel>(StatusCodes.Status200OK, "application/json")]
		[HttpPost]
		public ActionResult<TestModel> Post([FromBody] TestModel body) {
			return new TestModel();
		}


	}


	public enum TestEnum {
		Val1,
		Val2,
		Val3
	}

	public class TestModel {

		public int IntValue { get; set; }	

		public decimal DecimalValue { get; set; }	

		public string? StringValue { get; set; }

		public TestEnum EnumValue { get; set; }

		public TestEnum? NullableEnum { get; set; }

		public NestedModel? NestedItem { get; set; }

		public List<NestedModel>? NestedItems { get; set; }

	}

	public class NestedModel {

		public string NestedId { get; set; }

		[ReadOnly(true)]
		public string? NestedName { get; set; }

	}
}
