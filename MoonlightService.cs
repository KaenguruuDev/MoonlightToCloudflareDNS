using Docker.DotNet;
using Docker.DotNet.Models;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MoonlightToCloudflareDNS;

public static class MoonlightService
{
	private static DockerClient? _client;

	private static string? _apiKey;
	private static readonly List<Server> Servers = [];

	public static async Task Run(Dictionary<string, string?> configuration)
	{
		configuration.TryGetValue("MOONLIGHTAPIKEY", out _apiKey);

		await Logging.Log(LogSeverity.Info, "Moonlight", "Initialize");
		_client = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();

		var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
		if (containers.Count <= 0)
		{
			await Logging.Log(LogSeverity.Error, "Moonlight", "Unable to query containers. Terminating.");
			return;
		}

		await Logging.Log(LogSeverity.Info, "Moonlight", "Configuration is valid. Monitoring...");

		var app = ConfigureApi();
		_ = app.RunAsync();
		await MonitorForChanges();
	}

	private static async Task MonitorForChanges()
	{
		while (_client != null)
		{
			await UpdateServersViaDocker();
			await Task.Delay(TimeSpan.FromSeconds(30));
		}
	}

	private static async Task UpdateServersViaDocker()
	{
		if (_client == null)
			return;

		var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
		if (containers.Count <= 0)
			return;

		Servers.Clear();
		foreach (var container in containers)
		{
			var name = container.Names.FirstOrDefault(n => n.Contains('/'))?.Replace("/", "");

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
	}

	private static WebApplication ConfigureApi()
	{
		var builder = WebApplication.CreateBuilder();
		builder.Logging.ClearProviders();

		var app = builder.Build();
		app.Urls.Add("http://localhost:5000");

		app.MapGet("/servers", (HttpContext context) =>
		{
			var authHeader = context.Request.Headers.Authorization.ToString();
			return authHeader != $"Bearer {_apiKey}" ? Results.Unauthorized() : Results.Ok(Servers);
		});

		return app;
	}
}