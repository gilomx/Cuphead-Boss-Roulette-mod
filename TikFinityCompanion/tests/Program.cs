using System.Text.Json;
using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.Runtime;
using LaPichiRuleta.TikFinity.TikFinity;

namespace LaPichiRuleta.TikFinity.Tests;

internal static partial class Program
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 8, 26, 18, 30, 0, TimeSpan.Zero);

    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("flat gift progress", FlatGiftProgress),
            ("nested gift final", NestedGiftFinal),
            ("display name fallback", DisplayNameFallback),
            ("avatar URL variants", AvatarUrlVariants),
            ("unsafe avatar URLs", UnsafeAvatarUrls),
            ("long avatar URL", LongAvatarUrl),
            ("non-streak gift", NonStreakGift),
            ("community gift detection", CommunityGiftDetection),
            ("unknown streak end is provisional", UnknownStreakEnd),
            ("streak idempotency", StreakIdempotency),
            ("total coin fallback", TotalCoinFallback),
            ("like normalization", LikeNormalization),
            ("array envelope", ArrayEnvelope),
            ("unsupported event is dropped", UnsupportedEventIsDropped),
            ("chat ballot normalization", ChatBallotNormalization),
            ("malformed input", MalformedInput),
            ("status JSON contract", StatusJsonContract),
            ("event JSON null contract", EventJsonNullContract),
            ("event JSON user contract", EventJsonUserContract),
            ("argument parsing", ArgumentParsing),
            ("reconnect backoff", Backoff),
            ("long image URL", LongImageUrl),
            ("Twitch ballot and channel filtering", TwitchBallots),
            ("Twitch local receipt time ignores server clock skew and survives queues", TwitchLocalReceiptTime),
            ("Twitch monetary and subscription events", TwitchEventValues),
            ("Twitch API public client and safe device URL", TwitchPublicApi),
            ("Twitch restore and rotating refresh", () => TwitchRestoreAsync().GetAwaiter().GetResult()),
            ("Twitch cancel rejects late authorization", () => TwitchCancelAsync().GetAwaiter().GetResult()),
            ("Twitch disconnect clears offline and retries failed vault delete", () => TwitchDisconnectAsync().GetAwaiter().GetResult()),
            ("Twitch vault errors and wrong account reject startup", () => TwitchStartupErrorsAsync().GetAwaiter().GetResult()),
            ("Twitch protocol excludes credentials", TwitchPublicProtocol),
            ("Twitch command pipe accepts ASCII and legacy UTF-8 preambles", () => TwitchCommandPipeAsync().GetAwaiter().GetResult()),
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
        Equal("viewer_one", streamEvent.UserDisplayName);
        Equal("1234567890123456789", streamEvent.UserId);
        Equal(null, streamEvent.UserAvatarUrl);
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
        Equal("Viewer One", streamEvent.UserDisplayName);
        Equal("https://example.invalid/viewer-one.png", streamEvent.UserAvatarUrl);
        Equal("5655", streamEvent.ItemId);
        Equal("Rose", streamEvent.ItemName);
        Equal("https://example.invalid/rose-current.png", streamEvent.ItemImageUrl);
        Equal(5, streamEvent.Count);
        Equal(1m, streamEvent.UnitValue);
        Equal(5m, streamEvent.TotalValue);
        Equal(StreakStates.Final, streamEvent.StreakState);
    }

    private static void DisplayNameFallback()
    {
        var explicitDisplayName = One("""
            {"event":"follow","data":{"msgId":"display-1","uniqueId":"viewer_handle","displayName":"Viewer Display"}}
            """);
        Equal("viewer_handle", explicitDisplayName.UserName);
        Equal("Viewer Display", explicitDisplayName.UserDisplayName);

        var fallback = One("""
            {"event":"follow","data":{"msgId":"display-2","username":"fallback_handle"}}
            """);
        Equal("fallback_handle", fallback.UserName);
        Equal("fallback_handle", fallback.UserDisplayName);

        var nicknameOnly = One("""
            {"event":"follow","data":{"msgId":"display-3","nickname":"Nickname Only"}}
            """);
        Equal(null, nicknameOnly.UserName);
        Equal("Nickname Only", nicknameOnly.UserDisplayName);
    }

    private static void AvatarUrlVariants()
    {
        var profilePicture = One("""
            {"event":"follow","data":{"msgId":"avatar-1","profilePictureUrl":"https://cdn.example/avatar-1.png"}}
            """);
        Equal("https://cdn.example/avatar-1.png", profilePicture.UserAvatarUrl);

        var avatarObject = One("""
            {"event":"follow","data":{"msgId":"avatar-2","avatar":{"urlList":["https://cdn.example/avatar-2.png"]}}}
            """);
        Equal("https://cdn.example/avatar-2.png", avatarObject.UserAvatarUrl);

        var profilePictureArray = One("""
            {"event":"follow","data":{"msgId":"avatar-3","profilePictureUrls":["https://cdn.example/avatar-3.png"]}}
            """);
        Equal("https://cdn.example/avatar-3.png", profilePictureArray.UserAvatarUrl);

        var userDetails = One("""
            {"event":"follow","data":{"msgId":"avatar-4","userDetails":{"profilePictureUrl":{"url_list":["https://cdn.example/avatar-4.png"]}}}}
            """);
        Equal("https://cdn.example/avatar-4.png", userDetails.UserAvatarUrl);
    }

    private static void UnsafeAvatarUrls()
    {
        foreach (var unsafeUrl in new[]
                 {
                     "http://cdn.example/avatar.png",
                     "/avatars/viewer.png",
                     "not a URL",
                     "https://[::1",
                     "file:///avatars/viewer.png",
                     "https://localhost/avatar.png",
                     "https://127.0.0.1/avatar.png",
                     "https://[::1]/avatar.png",
                     "https://192.168.1.10/avatar.png",
                     "https://avatar-host/avatar.png",
                 })
        {
            Equal(null, AvatarEvent(unsafeUrl).UserAvatarUrl);
        }
    }

    private static void LongAvatarUrl()
    {
        const string prefix = "https://cdn.example/";
        var acceptedUrl = prefix + new string('a', 2048 - prefix.Length);
        Equal(acceptedUrl, AvatarEvent(acceptedUrl).UserAvatarUrl);

        var rejectedUrl = prefix + new string('b', 2049 - prefix.Length);
        Equal(null, AvatarEvent(rejectedUrl).UserAvatarUrl);
    }

    private static void NonStreakGift()
    {
        var streamEvent = One(Fixture("gift-single.json"));
        Equal(StreakStates.None, streamEvent.StreakState);
        Equal(25m, streamEvent.TotalValue);
    }

    private static void CommunityGiftDetection()
    {
        var flagged = One("""
            {"event":"gift","data":{"msgId":"community-1","giftId":"7001","ownCommunityGift":true}}
            """);
        Equal(true, flagged.IsCommunityGift);

        var subtype = One("""
            {"event":"gift","data":{"msgId":"community-2","giftId":"7002","giftDetails":{"giftSubtype":"personalized_gift"}}}
            """);
        Equal(true, subtype.IsCommunityGift);

        var saliency = One("""
            {"event":"gift","data":{"msgId":"community-3","giftId":"7003","giftPictureUrl":"https://example.invalid/saliency_seg_custom.png"}}
            """);
        Equal(true, saliency.IsCommunityGift);

        var ordinary = One("""
            {"event":"gift","data":{"msgId":"ordinary-1","giftId":"5655","ownCommunityGift":false}}
            """);
        Equal(false, ordinary.IsCommunityGift);
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

    private static void ChatBallotNormalization()
    {
        var ballot = One("""
            {"event":"chat","data":{"msgId":"chat-vote-1","userId":"123","uniqueId":"viewer","comment":" 2 "}}
            """);
        Equal("chat", ballot.Type);
        Equal("2", ballot.ChatText);
        Equal("123", ballot.UserId);
        Equal("viewer", ballot.UserName);
        using var json = JsonDocument.Parse(NdjsonWriter.Serialize(ballot));
        Equal("2", json.RootElement.GetProperty("chatText").GetString());
        var comment = One("""{"event":"comment","data":{"comment":"6","userId":"9"}}""");
        Equal("6", comment.ChatText);
        foreach (var value in new[] { "hello", "7", "0", "1 2", "!1", "12", "" })
        {
            var result = new TikFinityEventNormalizer().Normalize(
                JsonSerializer.Serialize(new { @event = "chat", data = new { comment = value } }), FixedTime);
            Equal(0, result.Events.Count);
        }
        var repeat = One("""{"event":"chat","data":{"comment":"6","userId":"9"}}""");
        NotEqual(comment.IdempotencyKey, repeat.IdempotencyKey);
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
                     "type", "userName", "userDisplayName", "userId", "userAvatarUrl",
                     "itemId", "itemName", "itemImageUrl", "isCommunityGift",
                     "count", "unitValue",
                     "totalValue", "unit", "currency", "streakId", "streakState",
                     "receivedAt", "simulated", "rawEventType",
                 })
        {
            True(root.TryGetProperty(name, out _), "Missing JSON property " + name);
        }

        Equal(JsonValueKind.Null, root.GetProperty("itemId").ValueKind);
        Equal(JsonValueKind.Null, root.GetProperty("userDisplayName").ValueKind);
        Equal(JsonValueKind.Null, root.GetProperty("userAvatarUrl").ValueKind);
        Equal(false, root.GetProperty("simulated").GetBoolean());
    }

    private static void EventJsonUserContract()
    {
        var streamEvent = One(Fixture("gift-final-nested.json"));
        using var json = JsonDocument.Parse(NdjsonWriter.Serialize(streamEvent));
        var root = json.RootElement;
        Equal("viewer_one", root.GetProperty("userName").GetString());
        Equal("Viewer One", root.GetProperty("userDisplayName").GetString());
        Equal(
            "https://example.invalid/viewer-one.png",
            root.GetProperty("userAvatarUrl").GetString());
    }

    private static void ArgumentParsing()
    {
        True(CompanionOptions.TryParse(
            new[] { "--parent-pid", "123" },
            out var options,
            out _));
        Equal(123, options!.ParentProcessId);
        Equal(false, options.TwitchControl);

        True(CompanionOptions.TryParse(new[] { "--parent-pid", "123", "--twitch-control" }, out options, out _));
        Equal(true, options!.TwitchControl);
        Equal(false, CompanionOptions.TryParse(new[] { "--parent-pid", "123", "--twitch-control", "--twitch-control" }, out _, out _));

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

    private static CompanionEvent AvatarEvent(string avatarUrl)
    {
        return One(
            "{\"event\":\"follow\",\"data\":{\"msgId\":\"avatar-url\"," +
            "\"profilePictureUrl\":" + JsonSerializer.Serialize(avatarUrl) + "}}");
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
