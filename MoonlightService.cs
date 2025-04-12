namespace MoonlightToCloudflareDNS;

public static class MoonlightService
{
	private static string? _apiKey;
	private static string? _panelUrl;

	public static async Task Run(Dictionary<string, string?> configuration)
	{
		configuration.TryGetValue("MOONLIGHTAPIKEY", out _apiKey);
		configuration.TryGetValue("MOONLIGHTPANELURL", out _panelUrl);

		await Api.Get(_panelUrl ?? "");
		await Task.Delay(1000);
	}
}