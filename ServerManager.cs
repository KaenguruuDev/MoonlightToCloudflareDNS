namespace MoonlightToCloudflareDNS;

public record Server(string IpAddress, int Port, string Domain, string Subdomain);

public static class ServerManager
{
	public static readonly List<Server> Servers = [new("127.0.0.1", 2025, "kaenguruu.dev", "test")];
}