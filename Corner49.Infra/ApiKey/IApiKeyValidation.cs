namespace Corner49.Infra.ApiKey {
	public interface IApiKeyValidation {
		bool IsValidApiKey(string userApiKey);
	}
}
