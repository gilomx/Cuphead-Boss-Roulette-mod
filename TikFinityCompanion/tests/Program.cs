using System.Text.Json;
using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.Runtime;
using LaPichiRuleta.TikFinity.TikFinity;

namespace LaPichiRuleta.TikFinity.Tests;

internal static class Program
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 8, 26, 18, 30, 0, TimeSpan.Zero);

    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("flat gift progress", FlatGiftProgress),
            ("nested gift final", NestedGiftFinal),
            ("non-streak gift", NonStreakGift),
            ("unknown streak end is provisional", UnknownStreakEnd),
            ("streak idempotency", StreakIdempotency),
            ("total coin fallback", TotalCoinFallback),
            ("like normalization", LikeNormalization),
            ("array envelope", ArrayEnvelope),
            ("unsupported event is dropped", UnsupportedEventIsDropped),
            ("malformed input", MalformedInput),
            ("status JSON contract", StatusJsonContract),
            ("event JSON null contract", EventJsonNullContract),
            ("argument parsing", ArgumentParsing),
            ("reconnect backoff", Backoff),
            ("long image URL", LongImageUrl),
        };

        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine("PASS " + test.Name);
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine("FAIL " + test.Name + ": " + exception.Message);
            }
        }

        Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static void FlatGiftProgress()
    {
        var streamEvent = One(Fixture("gift-progress-flat.json"));
        Equal("gift", streamEvent.Type);
        Equal("9001", streamEvent.EventId);
        Equal("viewer_one", streamEvent.UserName);
        Equal("1234567890123456789", streamEvent.UserId);
        Equal("5655", streamEvent.ItemId);
        Equal("Rose", streamEvent.ItemName);
        Equal("https://example.invalid/rose.png", streamEvent.ItemImageUrl);
        Equal(3, streamEvent.Count);
        Equal(1m, streamEvent.UnitValue);
        Equal(3m, streamEvent.TotalValue);
        Equal("coin", streamEvent.Unit);
        Equal("combo-44", streamEvent.StreakId);
        Equal(StreakStates.Progress, streamEvent.StreakState);
        Equal(FixedTime, streamEvent.ReceivedAt);
        Equal(false, streamEvent.Simulated);
    }

    private static void NestedGiftFinal()
    {
        var streamEvent = One(Fixture("gift-final-nested.json"));
        Equal("viewer_one", streamEvent.UserName);
        Equal("5655", streamEvent.ItemId);
        Equal("Rose", streamEvent.ItemName);
        Equal("https://example.invalid/rose-current.png", streamEvent.ItemImageUrl);
        Equal(5, streamEvent.Count);
        Equal(1m, streamEvent.UnitValue);
        Equal(5m, streamEvent.TotalValue);
        Equal(StreakStates.Final, streamEvent.StreakState);
    }

    private static void NonStreakGift()
    {
        var streamEvent = One(Fixture("gift-single.json"));
        Equal(StreakStates.None, streamEvent.StreakState);
        Equal(25m, streamEvent.TotalValue);
    }

    private static void StreakIdempotency()
    {
        var progress = One(Fixture("gift-progress-flat.json"));
        var repeatedProgress = One(Fixture("gift-progress-flat.json"));
        var final = One(Fixture("gift-final-nested.json"));

        Equal(progress.IdempotencyKey, repeatedProgress.IdempotencyKey);
        NotEqual(progress.IdempotencyKey, final.IdempotencyKey);
    }

    private static void UnknownStreakEnd()
    {
        var streamEvent = One("""
            {"event":"gift","data":{"msgId":"pending-1","giftId":"8","giftType":1,"repeatCount":2}}
            """);
        Equal(StreakStates.Progress, streamEvent.StreakState);
    }

    private static void TotalCoinFallback()
    {
        var streamEvent = One("""
            {"event":"gift","data":{"msgId":"coins-1","giftId":"7","repeatCount":4,"coins":20}}
            """);
        Equal(5m, streamEvent.UnitValue);
        Equal(20m, streamEvent.TotalValue);
        Equal("coin", streamEvent.Unit);
    }

    private static void LikeNormalization()
    {
        var streamEvent = One("""
            {"event":"like","data":{"msgId":"like-1","user":{"userId":"9","uniqueId":"liker"},"likeCount":15}}
            """);
        Equal("like", streamEvent.Type);
        Equal(15, streamEvent.Count);
        Equal(null, streamEvent.UnitValue);
        Equal(null, streamEvent.Unit);
        Equal(null, streamEvent.StreakState);
    }

    private static void ArrayEnvelope()
    {
        var batch = Normalize("""
            [{"event":"follow","data":{"msgId":"f1"}},{"event":"subscribe","data":{"msgId":"s1"}}]
            """);
        Equal(0, batch.Errors.Count);
        Equal(2, batch.Events.Count);
        Equal("follow", batch.Events[0].Type);
        Equal("subscription", batch.Events[1].Type);
    }

    private static void MalformedInput()
    {
        var batch = Normalize("{broken");
        Equal(0, batch.Events.Count);
        Equal(1, batch.Errors.Count);
    }

    private static void UnsupportedEventIsDropped()
    {
        var batch = Normalize("""
            {"event":"chat","data":{"msgId":"chat-1","comment":"hello"}}
            """);
        Equal(0, batch.Events.Count);
        Equal(0, batch.Errors.Count);
    }

    private static void StatusJsonContract()
    {
        var status = new CompanionStatus
        {
            State = CompanionStatusStates.Connected,
            Message = "Ready",
            OccurredAt = FixedTime,
            RetryAttempt = 2,
        };
        using var json = JsonDocument.Parse(NdjsonWriter.Serialize(status));
        var root = json.RootElement;
        Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Equal("status", root.GetProperty("kind").GetString());
        Equal("connected", root.GetProperty("state").GetString());
        Equal("tikfinity-local", root.GetProperty("connectionId").GetString());
        Equal(2, root.GetProperty("retryAttempt").GetInt32());
    }

    private static void EventJsonNullContract()
    {
        var streamEvent = One("""
            {"event":"follow","data":{"msgId":"follow-1"}}
            """);
        using var json = JsonDocument.Parse(NdjsonWriter.Serialize(streamEvent));
        var root = json.RootElement;
        foreach (var name in new[]
                 {
                     "eventId", "idempotencyKey", "connectionId", "platform", "connector",
                     "type", "userName", "userId", "itemId", "itemName", "itemImageUrl",
                     "count", "unitValue", "totalValue", "unit", "currency", "streakId",
                     "streakState", "receivedAt", "simulated", "rawEventType",
                 })
        {
            True(root.TryGetProperty(name, out _), "Missing JSON property " + name);
        }

        Equal(JsonValueKind.Null, root.GetProperty("itemId").ValueKind);
        Equal(false, root.GetProperty("simulated").GetBoolean());
    }

    private static void ArgumentParsing()
    {
        True(CompanionOptions.TryParse(
            new[] { "--parent-pid", "123" },
            out var options,
            out _));
        Equal(123, options!.ParentProcessId);

        True(CompanionOptions.TryParse(
            new[] { "--parent-pid=456" },
            out options,
            out _));
        Equal(456, options!.ParentProcessId);

        Equal(false, CompanionOptions.TryParse(Array.Empty<string>(), out _, out _));
        Equal(false, CompanionOptions.TryParse(new[] { "--other" }, out _, out _));
    }

    private static void Backoff()
    {
        Equal(TimeSpan.FromSeconds(1), ReconnectBackoff.ForAttempt(1));
        Equal(TimeSpan.FromSeconds(2), ReconnectBackoff.ForAttempt(2));
        Equal(TimeSpan.FromSeconds(4), ReconnectBackoff.ForAttempt(3));
        Equal(TimeSpan.FromSeconds(30), ReconnectBackoff.ForAttempt(10));
        Equal(false, ReconnectBackoff.WasStable(TimeSpan.FromSeconds(29)));
        Equal(true, ReconnectBackoff.WasStable(TimeSpan.FromSeconds(30)));
    }

    private static void LongImageUrl()
    {
        var acceptedUrl = "https://example.invalid/" + new string('a', 1900);
        var accepted = One(
            "{\"event\":\"gift\",\"data\":{\"msgId\":\"url-1\",\"giftId\":\"1\"," +
            "\"giftPictureUrl\":\"" + acceptedUrl + "\"}}");
        Equal(acceptedUrl, accepted.ItemImageUrl);

        var rejectedUrl = "https://example.invalid/" + new string('b', 2100);
        var rejected = One(
            "{\"event\":\"gift\",\"data\":{\"msgId\":\"url-2\",\"giftId\":\"1\"," +
            "\"giftPictureUrl\":\"" + rejectedUrl + "\"}}");
        Equal(null, rejected.ItemImageUrl);
    }

    private static CompanionEvent One(string json)
    {
        var batch = Normalize(json);
        Equal(0, batch.Errors.Count);
        Equal(1, batch.Events.Count);
        return batch.Events[0];
    }

    private static NormalizationBatch Normalize(string json)
    {
        return new TikFinityEventNormalizer().Normalize(json, FixedTime);
    }

    private static string Fixture(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected <{expected}> but got <{actual}>.");
    }

    private static void NotEqual<T>(T left, T right)
    {
        if (EqualityComparer<T>.Default.Equals(left, right))
            throw new InvalidOperationException($"Expected values to differ, but both were <{left}>.");
    }

    private static void True(bool value, string message = "Expected true.")
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
}
