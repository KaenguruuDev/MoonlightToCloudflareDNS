using System.Reflection;

namespace MoonlightToCloudflareDNS;

class Program
{
	private static readonly Dictionary<string, string?> Configuration = new()
	{
		{ "CLOUDFLAREAPIKEY", null },
		{ "CLOUDFLAREZONEID", null },
		{ "MOONLIGHTAPIKEY", null },
		{ "MOONLIGHTPANELURL", null },
	};

	// Usage: ./MlCloudDNS path/to/.env
	static async Task Main(string[] args)
	{
		if (args.Length < 1)
		{
			await Logging.Log(LogSeverity.Error, "Init", "No environment file specified.");
			return;
		}

		if (!File.Exists(args[0]))
		{
			await Logging.Log(LogSeverity.Error, "Init", "File not found: " + args[0]);
			return;
		}

		var lines = File.ReadAllLines(args[0]);
		foreach (var line in lines)
		{
			var identifier = line.Split("=").FirstOrDefault();
			var value = line.Split("=").LastOrDefault();

			if (identifier == null || value == null)
				continue;

			if (!Configuration.ContainsKey(identifier))
				continue;

			Configuration[identifier] = value;
		}

		if (Configuration.Any(c => c.Value == null))
		{
			await Logging.Log(LogSeverity.Error, "Init",
				$"Missing configuration values for: {Configuration.Aggregate("", (s, pair) => $"{s}, {pair.Key}")[2..]}");
			return;
		}

		await Logging.Log(LogSeverity.Info, "Init", $"Starting CloudflareDNS and Moonlight Service");

#pragma warning disable
		CloudflareService.Run(Configuration);
		MoonlightService.Run(Configuration);
#pragma warning restore

		await Task.Delay(-1);
	}
}