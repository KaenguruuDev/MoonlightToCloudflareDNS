namespace MoonlightToCloudflareDNS;

internal abstract class Program
{
	private static bool _isMoonlight;

	// Usage: ./MTCF path/to/.env moonlight/cloudflare
	private static async Task Main(string[] args)
	{
		if (!await CheckParameters(args))
			return;

		var configuration = await CheckConfiguration(args);
		if (configuration is null)
			return;

		await Logging.Log(LogSeverity.Info, "Init", $"Starting {(_isMoonlight ? "Moonlight" : "Cloudflare")} Service");

#pragma warning disable
		if (_isMoonlight)
			MoonlightService.Run(configuration);
		else
			CloudflareService.Run(configuration);
#pragma warning restore

		await Task.Delay(-1);
	}

	private static async Task<Dictionary<string, string?>?> CheckConfiguration(string[] args)
	{
		var configuration = new Dictionary<string, string?>();

		if (args[1] == "cloudflare")
		{
			configuration["CLOUDFLAREAPIKEY"] = null;
			configuration["CLOUDFLAREZONEID"] = null;
			configuration["MOONLIGHTAPIKEY"] = null;
			configuration["MOONLIGHTAPIURL"] = null;
			_isMoonlight = false;
		}
		else
		{
			configuration["MOONLIGHTAPIKEY"] = null;
			_isMoonlight = true;
		}

		var lines = await File.ReadAllLinesAsync(args[0]);
		foreach (var line in lines)
		{
			var identifier = line.Split("=").FirstOrDefault();
			var value = line.Split("=").LastOrDefault();

			if (identifier == null || value == null)
				continue;

			if (!configuration.ContainsKey(identifier))
				continue;

			configuration[identifier] = value;
		}

		if (configuration.All(c => c.Value != null)) return configuration;

		await Logging.Log(LogSeverity.Error, "Init",
			$"Missing configuration values for: {configuration.Aggregate("", (s, pair) => $"{s}, {pair.Key}")[2..]}");
		return null;
	}

	private static async Task<bool> CheckParameters(string[] args)
	{
		if (args.Length < 2)
		{
			await Logging.Log(LogSeverity.Error, "Init", "Missing configuration file or application mode.");
			return false;
		}

		if (!File.Exists(args[0]))
		{
			await Logging.Log(LogSeverity.Error, "Init", "File not found: " + args[0]);
			return false;
		}

		if (args[1] != "cloudflare" && args[1] != "moonlight")
		{
			await Logging.Log(LogSeverity.Error, "Init",
				$"Mode '{args[1]}' not supported. Use 'moonlight' or 'cloudflare'");
			return false;
		}

		return true;
	}
}