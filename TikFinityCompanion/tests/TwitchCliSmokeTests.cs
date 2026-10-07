using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.Twitch;

namespace LaPichiRuleta.TikFinity.Tests;

internal static partial class Program
{
    // Opt-in integration smoke: requires the official CLI server on loopback.
    // No game process, Twitch authorization or credential store is involved.
    private static async Task<int> TwitchCliSmokeAsync()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        using var pipe = new AnonymousPipeServerStream(PipeDirection.Out);
        using var input = new AnonymousPipeClientStream(PipeDirection.In, pipe.GetClientHandleAsString());
        using var reader = new StreamReader(input);
        await using var output = new NdjsonWriter(pipe);
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = new TwitchEventSub(new TwitchLocalTestApi(http), output, true);
        var running = receiver.RunAsync(_ => Task.FromResult(TwitchLocalTestApi.Identity), _ => {
            connected.TrySetResult(); return Task.CompletedTask;
        }, cancellation.Token);
        try
        {
            var ready = await Task.WhenAny(connected.Task, running).WaitAsync(cancellation.Token);
            if (ready == running) await running;
            await connected.Task.WaitAsync(cancellation.Token);
            var events = new[] { ("Follow", "follow", "channel.follow"), ("Bits", "currency", "channel.cheer"),
                ("Subscription", "subscription", "channel.subscribe"), ("Gifted", "subscription", "channel.subscription.gift"),
                ("Resubscription", "subscription", "channel.subscription.message"),
                ("Reward", "redemption", "channel.channel_points_custom_reward_redemption.add") };
            foreach (var (kind, normalized, raw) in events)
            {
                var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { "-NoProfile", "-File", Path.GetFullPath("tools/test-twitch-eventsub.ps1"),
                    "-Event", kind, "-Amount", "150", "-Quantity", "5", "-RewardName", "Mini jefe" }) start.ArgumentList.Add(arg);
                using var cli = Process.Start(start)!;
                var cliOutput = cli.StandardOutput.ReadToEndAsync(); var cliError = cli.StandardError.ReadToEndAsync();
                await cli.WaitForExitAsync(cancellation.Token);
                if (cli.ExitCode != 0) throw new Exception(await cliError + await cliOutput);
                using var entry = JsonDocument.Parse(await reader.ReadLineAsync(cancellation.Token) ?? throw new IOException("Missing event"));
                Equal(normalized, entry.RootElement.GetProperty("type").GetString());
                Equal(raw, entry.RootElement.GetProperty("rawEventType").GetString());
                Equal(true, entry.RootElement.GetProperty("simulated").GetBoolean());
                Equal("twitch-cli", entry.RootElement.GetProperty("connector").GetString());
                if (kind == "Bits") Equal(150m, entry.RootElement.GetProperty("totalValue").GetDecimal());
                if (kind == "Gifted") Equal(5, entry.RootElement.GetProperty("count").GetInt32());
                if (kind == "Reward") Equal("Mini jefe", entry.RootElement.GetProperty("itemName").GetString());
                Console.WriteLine("PASS official Twitch CLI -> companion -> NDJSON: " + kind);
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { cancellation.Cancel(); try { await running; } catch (Exception) { } }
    }
}
