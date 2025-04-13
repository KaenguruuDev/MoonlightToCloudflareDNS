using Docker.DotNet;
using Docker.DotNet.Models;

namespace MoonlightToCloudflareDNS;

public static class MoonlightService
{
	private static readonly DockerClient Client =
		new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();

	private static string? _apiKey;

	public static async Task Run(Dictionary<string, string?> configuration)
	{
		configuration.TryGetValue("MOONLIGHTAPIKEY", out _apiKey);

		var containers = await Client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
		if (containers.Count <= 0)
		{
			await Logging.Log(LogSeverity.Error, "Moonlight", "Unable to list containers. Terminating.");
			return;
		}

		await Logging.Log(LogSeverity.Info, "Moonlight", "Configuration is valid. Monitoring...");
		await MonitorForChanges();
	}

	private static async Task MonitorForChanges()
	{
		while (true)
		{
			var containers = await Client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
			if (containers.Count <= 0)
				continue;

			foreach (var container in containers)
			{
				var name = container.Names.FirstOrDefault(n => n.Contains("/"))?.Replace("/", "");
				
				
				if (name == null || !name.Contains("moonlight-runtime-"))
					continue;

				Console.WriteLine(container.Mounts.FirstOrDefault()?.Source);
			}

			await Task.Delay(30_000);
		}
	}
}