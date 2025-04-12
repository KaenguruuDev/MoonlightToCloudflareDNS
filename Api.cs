using System.Net.Http.Headers;

namespace MoonlightToCloudflareDNS;

public static class Api
{
	private static readonly HttpClient Client = new HttpClient();

	public static async Task<HttpResponseMessage> Get(string url)
	{
		return await Client.GetAsync(url);
	}

	public static async Task<HttpResponseMessage> Post(string url, StringContent json, string? apiKey = null)
	{
		if (string.IsNullOrWhiteSpace(url))
			throw new ArgumentNullException(nameof(url));
		if (apiKey != null)
			Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

		return await Client.PostAsync(url, json);
	}
}