using System.Net.Http.Json;
using System.Text;
using CloudFlare.Client;
using CloudFlare.Client.Api.Result;
using CloudFlare.Client.Api.Zones.DnsRecord;
using CloudFlare.Client.Enumerators;

namespace MoonlightToCloudflareDNS;

public static class CloudflareService
{
	private const int DnsAlreadyExists = 81058;

	private static string? _cfApiKey;
	private static string? _zoneId;
	private static string? _panelUrl;
	private static string? _mlApiKey;

	private static CloudFlareClient? _cloudFlareClient;

	private static readonly List<Server> Servers = [];

	public static async Task Run(Dictionary<string, string?> configuration)
	{
		configuration.TryGetValue("CLOUDFLAREAPIKEY", out _cfApiKey);
		configuration.TryGetValue("CLOUDFLAREZONEID", out _zoneId);
		configuration.TryGetValue("MOONLIGHTAPIURL", out _panelUrl);
		configuration.TryGetValue("MOONLIGHTAPIKEY", out _mlApiKey);

		_cloudFlareClient = new CloudFlareClient(_cfApiKey);

		await Logging.Log(LogSeverity.Info, "Cloudflare", "Initialize");
		var isValid = await CheckValidity();
		if (!isValid)
		{
			await Logging.Log(LogSeverity.Error, "Cloudflare",
				"Could not verify Cloudflare configuration. Terminating.");
			return;
		}

		await Logging.Log(LogSeverity.Info, "Cloudflare", "Configuration is valid. Monitoring...");
		await MonitorForChanges();
	}

	private static async Task<bool> CheckValidity()
	{
		if (_cfApiKey == null || _zoneId == null || _cloudFlareClient == null)
			return false;

		var dnsRecord = new NewDnsRecord()
		{
			Type = DnsRecordType.A,
			Name = "mtcf",
			Content = "127.0.0.1",
			Ttl = 1,
			Proxied = false,
			Comment = "API Key Verification Record || Generated by MTCF",
		};

		var result = await _cloudFlareClient.Zones.DnsRecords.AddAsync(_zoneId, dnsRecord);
		return result.Success || (result.Errors.Count == 1 && result.Errors[0].Code == DnsAlreadyExists);
	}

	private static async Task MonitorForChanges()
	{
		var serverCount = 0;
		while (_cloudFlareClient == null)
		{
			await Task.Delay(TimeSpan.FromSeconds(30));
			await UpdateServerList();

			if (serverCount == Servers.Count)
				continue;

			foreach (var server in Servers)
				await UpdateServer(server);

			serverCount = Servers.Count;
		}

		await Logging.Log(LogSeverity.Error, "CF/Monitor", "CloudFlareClient is null. Terminating.");
	}

	private static async Task UpdateServer(Server server)
	{
		var aRecordResult = await CreateARecord(server);
		var srvRecordResult = await CreateSrvRecord(server);

		if (!(aRecordResult?.Success ?? false) &&
		    !(aRecordResult?.Errors.Count == 1 && aRecordResult.Errors[0].Code == DnsAlreadyExists))
		{
			await Logging.Log(LogSeverity.Warning, "CF/Monitor",
				$"Could not create A Record for: {server.Subdomain}.{server.Domain} [{aRecordResult?.Errors.Aggregate("", (s, error) => $"{s}, {error.Code}")[2..]}]");
			return;
		}

		var errorResponse = !srvRecordResult.IsSuccessStatusCode
			? await srvRecordResult.Content.ReadFromJsonAsync<CreateSrvResponse>()
			: null;

		if (!srvRecordResult.IsSuccessStatusCode &&
		    errorResponse is not { Errors: [{ Code: DnsAlreadyExists }] })
		{
			await Logging.Log(LogSeverity.Error, "CF/Monitor",
				$"Could not create SRV Record for: {server.Subdomain}.{server.Domain} [{errorResponse?.Errors?.Aggregate("", (s, error) => $"{s}, {error.Code}")[2..]}]");
			return;
		}

		await Logging.Log(LogSeverity.Info, "CF/Monitor",
			$"Updated records for {server.Subdomain}.{server.Domain}");
	}

	private static async Task UpdateServerList()
	{
		var response = await Api.Get(_panelUrl + "/servers", _mlApiKey);
		if (response.IsSuccessStatusCode)
		{
			Servers.Clear();
			var newServerList = await response.Content.ReadFromJsonAsync<List<Server>>();
			Servers.AddRange(newServerList ?? []);
		}
	}

	private static async Task<HttpResponseMessage> CreateSrvRecord(Server server)
	{
		var srvRecord = new
		{
			type = "SRV",
			name = $"_minecraft._tcp.{server.Subdomain}",
			data = new
			{
				priority = 0,
				weight = 5,
				port = server.Port.ToString(),
				target = $"{server.Subdomain}.{server.Domain}"
			},
			ttl = 1,
			proxied = false,
			comment = "Generated by MTCF",
		};

		var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(srvRecord), Encoding.UTF8,
			"application/json");
		return await Api.Post($"https://api.cloudflare.com/client/v4/zones/{_zoneId}/dns_records", content, _cfApiKey);
	}

	private static async Task<CloudFlareResult<DnsRecord>?> CreateARecord(Server server)
	{
		if (_cloudFlareClient == null)
			return null;

		var aRecord = new NewDnsRecord()
		{
			Type = DnsRecordType.A,
			Name = server.Subdomain,
			Content = server.IpAddress,
			Ttl = 1,
			Proxied = false,
			Comment = "Generated by MTCF",
		};

		return await _cloudFlareClient.Zones.DnsRecords.AddAsync(_zoneId, aRecord);
	}

	private abstract record CreateSrvResponse(
		bool Success,
		CloudFlareError[]? Errors = null,
		object? Messages = null,
		object? Result = null
	);

	private abstract record CloudFlareError(
		int Code = 0,
		string? Message = null
	);
}