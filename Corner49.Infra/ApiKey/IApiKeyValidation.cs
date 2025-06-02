using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Corner49.Infra.ApiKey {
	public interface IApiKeyValidation {
		bool IsValidApiKey(string userApiKey);
	}
}
