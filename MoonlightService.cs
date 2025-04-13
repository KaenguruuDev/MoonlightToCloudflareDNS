using Docker.DotNet;
using Docker.DotNet.Models;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace MoonlightToCloudflareDNS;

public static class MoonlightService
{
	private static readonly DockerClient Client =
		new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();

	private static string? _apiKey;
	private static readonly List<Server> Servers = [];

	public static async Task Run(Dictionary<string, string?> configuration)
	{
		configuration.TryGetValue("MOONLIGHTAPIKEY", out _apiKey);

		var containers = await Client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
		if (containers.Count <= 0)
		{
			await Logging.Log(LogSeverity.Error, "Moonlight", "Unable to list containers. Terminating.");
			return;
		}

		var builder = WebApplication.CreateBuilder();
		var app = builder.Build();
		app.Urls.Add("http://localhost:5000");

		app.MapGet("/servers", (HttpContext context) =>
		{
			var authHeader = context.Request.Headers.Authorization.ToString();
			return authHeader != $"Bearer {_apiKey}" ? Results.Unauthorized() : Results.Ok(Servers);
		});

		await Logging.Log(LogSeverity.Info, "Moonlight", "Configuration is valid. Monitoring...");
		_ = app.RunAsync();
		await MonitorForChanges();
	}

	private static async Task MonitorForChanges()
	{
		while (true)
		{
			var containers = await Client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
			if (containers.Count <= 0)
				continue;

			Servers.Clear();
			foreach (var container in containers)
			{
				var name = container.Names.FirstOrDefault(n => n.Contains("/"))?.Replace("/", "");

				if (name == null || !name.Contains("moonlight-runtime-"))
					continue;

				if (container.Mounts.FirstOrDefault()?.Source == null)
					continue;

				const string relativeConfigFilePath = "/dns.json";
				if (!File.Exists(container.Mounts.FirstOrDefault()?.Source + relativeConfigFilePath))
					continue;

				try
				{
					var json = await File.ReadAllTextAsync(container.Mounts.FirstOrDefault()?.Source +
					                                       relativeConfigFilePath);
					var server = JsonConvert.DeserializeObject<Server>(json);
					if (server == null)
						continue;

					Servers.Add(server);
				}
				catch (Exception ex)
				{
					await Logging.Log(LogSeverity.Error, "ML/Monitor",
						$"Invalid DNS config for: {container.Names.FirstOrDefault()} [{ex.Message}]");
				}
			}

			await Task.Delay(30_000);
		}
	}
}