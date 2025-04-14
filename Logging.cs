using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MoonlightToCloudflareDNS;

public enum LogSeverity
{
	Debug,
	Info,
	Warning,
	Error,
	Critical
}

public static class Logging
{
	private static readonly FileStream FileStream;

	static Logging()
	{
		if (!Directory.Exists("logs"))
			Directory.CreateDirectory("logs");

		if (File.Exists("logs/latest.log"))
			File.Move("logs/latest.log", $"logs/{GetLogFileName(new FileInfo("logs/latest.log").CreationTime)}.log");

		var maxLogCount = 5;
		foreach (var file in Directory.GetFiles("logs").Reverse())
		{
			if (maxLogCount-- <= 0)
				File.Delete(file);
		}

		FileStream = new FileStream("logs/latest.log", FileMode.Append, FileAccess.Write);

		AppDomain.CurrentDomain.ProcessExit += (sender, e) => OnExit();

#pragma warning disable
		Log(LogSeverity.Info, "Logging", $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}");
		Log(LogSeverity.Info, "Logging", "Initialize");
#pragma warning restore
	}

	public static async Task Log(LogSeverity severity, string source, string message, Exception? exception = null)
	{
		Console.ForegroundColor = severity switch
		{
			LogSeverity.Critical or LogSeverity.Error => ConsoleColor.Red,
			LogSeverity.Warning => ConsoleColor.Yellow,
			_ => ConsoleColor.White
		};

		Console.WriteLine($"{GetLogTimeStamp() + " [" + severity + "] " + source,-30}{message}");
		if (exception != null)
		{
			var trace = new StackTrace(exception, true);
			var frame = trace.GetFrame(0);
			var method = frame?.GetMethod();

			var methodName = method?.Name ?? "??";
			var typeName = method?.DeclaringType?.FullName ?? "unknown";

			// modules.Onboarding+<Handle>d__6.MoveNext -> modules.Onboarding::Handle
			var asyncMatch = Regex.Match(typeName, @"(?<ns>.*)\+\<(?<method>.*)\>d__\d+");
			if (asyncMatch.Success)
			{
				var cleanedType = asyncMatch.Groups["ns"].Value.Replace("RTOSharp.", "");
				var cleanedMethod = asyncMatch.Groups["method"].Value;
				Console.WriteLine(
					$"{GetLogTimeStamp() + " [" + severity + "] " + source,-30}{exception.Message} [{cleanedType}::{cleanedMethod}]");
			}
			else
				Console.WriteLine(
					$"{GetLogTimeStamp() + " [" + severity + "] " + source,-30}{exception.Message} [UNKNOWN]");
		}

		Console.ResetColor();

		await using StreamWriter writer = new(FileStream, leaveOpen: true);
		await writer.WriteLineAsync($"{GetLogTimeStamp() + " [" + severity + "] " + source,-30}{message}");
	}

	private static string GetLogTimeStamp() =>
		$"[{DateTime.Now.Hour:D2}:{DateTime.Now.Minute:D2}:{DateTime.Now.Second:D2}]";

	private static string GetLogFileName(DateTime time) =>
		$"{time.Year:D4}-{time.Month:D2}-{time.Day:D2}T{time.AddHours(1).Hour:D2}-{time.Minute:D2}-{time.Second:D2}";

	private static void OnExit()
	{
		FileStream.Close();
	}
}