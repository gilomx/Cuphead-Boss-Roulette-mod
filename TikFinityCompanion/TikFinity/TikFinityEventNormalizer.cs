using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LaPichiRuleta.TikFinity.Protocol;

namespace LaPichiRuleta.TikFinity.TikFinity;

internal sealed class TikFinityEventNormalizer
{
    private const int MaximumCount = 1_000_000;
    private const decimal MaximumValue = 1_000_000_000m;

    internal NormalizationBatch Normalize(
        string message,
        DateTimeOffset receivedAt)
    {
        var events = new List<CompanionEvent>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(message))
        {
            errors.Add("TikFinity sent an empty WebSocket message.");
            return new NormalizationBatch(events, errors);
        }

        try
        {
            using var document = JsonDocument.Parse(message, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 64,
            });

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var envelope in document.RootElement.EnumerateArray())
                    NormalizeEnvelope(envelope, receivedAt, events, errors);
            }
            else
            {
                NormalizeEnvelope(document.RootElement, receivedAt, events, errors);
            }
        }
        catch (JsonException)
        {
            errors.Add("TikFinity sent malformed JSON; the message was ignored.");
        }

        return new NormalizationBatch(events, errors);
    }

    private static void NormalizeEnvelope(
        JsonElement envelope,
        DateTimeOffset receivedAt,
        ICollection<CompanionEvent> events,
        ICollection<string> errors)
    {
        if (envelope.ValueKind != JsonValueKind.Object)
        {
            errors.Add("TikFinity sent a non-object event envelope; the value was ignored.");
            return;
        }

        var rawEventType = JsonFieldReader.String(envelope, "event", "eventType", "type");
        if (string.IsNullOrWhiteSpace(rawEventType))
        {
            errors.Add("TikFinity sent an event without an event name; the event was ignored.");
            return;
        }

        if (!JsonFieldReader.TryGetProperty(envelope, "data", out var data))
            data = envelope;

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
                NormalizeData(rawEventType, item, receivedAt, events, errors);
            return;
        }

        if (data.ValueKind == JsonValueKind.String)
        {
            NormalizeStringData(rawEventType, data.GetString(), receivedAt, events, errors);
            return;
        }

        NormalizeData(rawEventType, data, receivedAt, events, errors);
    }

    private static void NormalizeStringData(
        string rawEventType,
        string? data,
        DateTimeOffset receivedAt,
        ICollection<CompanionEvent> events,
        ICollection<string> errors)
    {
        try
        {
            using var document = JsonDocument.Parse(data ?? string.Empty);
            NormalizeData(rawEventType, document.RootElement, receivedAt, events, errors);
        }
        catch (JsonException)
        {
            errors.Add("TikFinity sent an event whose data field was not an object; the event was ignored.");
        }
    }

    private static void NormalizeData(
        string rawEventType,
        JsonElement data,
        DateTimeOffset receivedAt,
        ICollection<CompanionEvent> events,
        ICollection<string> errors)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            errors.Add("TikFinity sent an event whose data field was not an object; the event was ignored.");
            return;
        }

        var type = NormalizeEventType(rawEventType);
        if (!IsEmittedType(type))
            return;

        var userName = CleanOptional(JsonFieldReader.String(
            data,
            "uniqueId",
            "username",
            "user.uniqueId",
            "user.username"));
        var userDisplayName = CleanOptional(JsonFieldReader.String(
            data,
            "nickname",
            "displayName",
            "display_name",
            "user.nickname",
            "user.displayName",
            "user.display_name")) ?? userName;
        var userId = CleanOptional(JsonFieldReader.String(
            data,
            "userId",
            "user_id",
            "user.userId",
            "user.user_id",
            "user.id"));
        var userAvatarUrl = CleanHttpsUrl(JsonFieldReader.Url(
            data,
            "profilePictureUrl",
            "profile_picture_url",
            "avatarUrl",
            "avatar_url",
            "avatar",
            "profilePictureUrls",
            "profile_picture_urls",
            "user.profilePictureUrl",
            "user.profile_picture_url",
            "user.avatarUrl",
            "user.avatar_url",
            "user.avatar",
            "user.profilePictureUrls",
            "user.profile_picture_urls",
            "userDetails.profilePictureUrl",
            "userDetails.profile_picture_url",
            "userDetails.profilePictureUrls",
            "userDetails.profile_picture_urls",
            "user.userDetails.profilePictureUrl",
            "user.userDetails.profile_picture_url",
            "user.userDetails.profilePictureUrls",
            "user.userDetails.profile_picture_urls"));

        var itemId = type == "gift"
            ? CleanOptional(JsonFieldReader.String(
                data,
                "giftId",
                "gift_id",
                "gift.giftId",
                "gift.gift_id",
                "gift.id",
                "giftDetails.giftId",
                "giftDetails.id",
                "extendedGiftInfo.giftId",
                "extendedGiftInfo.id"))
            : null;
        var itemName = type == "gift"
            ? CleanOptional(JsonFieldReader.String(
                data,
                "giftName",
                "gift_name",
                "gift.name",
                "giftDetails.giftName",
                "giftDetails.name",
                "extendedGiftInfo.name"))
            : null;
        var itemImageUrl = type == "gift"
            ? CleanUrl(JsonFieldReader.Url(
                data,
                "giftPictureUrl",
                "giftImageUrl",
                "gift.pictureUrl",
                "gift.imageUrl",
                "gift.picture",
                "gift.image",
                "giftDetails.picture",
                "giftDetails.image",
                "extendedGiftInfo.picture",
                "extendedGiftInfo.image"))
            : null;

        var count = ResolveCount(type, data);
        var streakState = type == "gift" ? ResolveStreakState(data) : null;
        var streakId = type == "gift"
            ? CleanOptional(JsonFieldReader.String(
                data,
                "streakId",
                "streak_id",
                "groupId",
                "group_id",
                "gift.streakId",
                "gift.groupId"))
            : null;

        ResolveValue(
            type,
            data,
            count,
            out var unitValue,
            out var totalValue,
            out var unit);

        var upstreamEventId = CleanOptional(JsonFieldReader.String(
            data,
            "msgId",
            "msg_id",
            "eventId",
            "event_id",
            "messageId",
            "message_id",
            "common.msgId",
            "common.msg_id"));
        var dataFingerprint = Fingerprint(rawEventType + "\n" + data.GetRawText());
        var eventId = upstreamEventId ?? "generated:" + dataFingerprint;
        var idempotencyKey = BuildIdempotencyKey(
            rawEventType,
            eventId,
            itemId,
            streakId,
            count,
            streakState);

        events.Add(new CompanionEvent
        {
            EventId = eventId,
            IdempotencyKey = idempotencyKey,
            Type = type,
            UserName = userName,
            UserDisplayName = userDisplayName,
            UserId = userId,
            UserAvatarUrl = userAvatarUrl,
            ItemId = itemId,
            ItemName = itemName,
            ItemImageUrl = itemImageUrl,
            Count = count,
            UnitValue = unitValue,
            TotalValue = totalValue,
            Unit = unit,
            Currency = null,
            StreakId = streakId,
            StreakState = streakState,
            ReceivedAt = receivedAt,
            Simulated = false,
            RawEventType = ProtocolText.Clean(rawEventType),
        });
    }

    private static int ResolveCount(string type, JsonElement data)
    {
        int? count = type switch
        {
            "gift" => JsonFieldReader.Integer(
                data,
                "repeatCount",
                "repeat_count",
                "gift.repeatCount",
                "gift.repeat_count",
                "count"),
            "like" => JsonFieldReader.Integer(
                data,
                "likeCount",
                "like_count",
                "count"),
            _ => JsonFieldReader.Integer(data, "count"),
        };

        return Math.Clamp(count.GetValueOrDefault(1), 1, MaximumCount);
    }

    private static string ResolveStreakState(JsonElement data)
    {
        var explicitState = JsonFieldReader.String(data, "streakState", "streak_state");
        if (!string.IsNullOrWhiteSpace(explicitState))
        {
            var normalized = explicitState.Trim().ToLowerInvariant();
            if (normalized is "progress" or "pending" or "active" or "ongoing")
                return StreakStates.Progress;
            if (normalized is "final" or "finished" or "complete" or "completed" or "end")
                return StreakStates.Final;
            if (normalized is "none" or "single")
                return StreakStates.None;
        }

        var giftType = JsonFieldReader.Integer(
            data,
            "giftType",
            "gift_type",
            "gift.giftType",
            "gift.gift_type",
            "giftDetails.giftType",
            "giftDetails.gift_type");
        if (giftType.HasValue && giftType.Value != 1)
            return StreakStates.None;

        var repeatEnd = JsonFieldReader.Boolean(
            data,
            "repeatEnd",
            "repeat_end",
            "isFinal",
            "is_final",
            "gift.repeatEnd",
            "gift.repeat_end");
        return repeatEnd switch
        {
            false => StreakStates.Progress,
            true => StreakStates.Final,
            null when giftType == 1 => StreakStates.Progress,
            null => StreakStates.None,
        };
    }

    private static void ResolveValue(
        string type,
        JsonElement data,
        int count,
        out decimal? unitValue,
        out decimal? totalValue,
        out string? unit)
    {
        unitValue = null;
        totalValue = null;
        unit = null;
        if (type != "gift")
            return;

        unitValue = PositiveValue(JsonFieldReader.Decimal(
            data,
            "diamondCount",
            "diamond_count",
            "coinsPerUnit",
            "coinValue",
            "gift.diamondCount",
            "gift.diamond_count",
            "giftDetails.diamondCount",
            "giftDetails.diamond_count",
            "extendedGiftInfo.diamondCount",
            "extendedGiftInfo.diamond_count"));
        var explicitTotal = PositiveValue(JsonFieldReader.Decimal(
            data,
            "totalCoins",
            "total_coins",
            "coins",
            "totalValue",
            "total_value"));

        if (unitValue.HasValue)
        {
            totalValue = explicitTotal ?? SafeMultiply(unitValue.Value, count);
        }
        else if (explicitTotal.HasValue)
        {
            totalValue = explicitTotal;
            unitValue = decimal.Round(
                explicitTotal.Value / count,
                decimals: 6,
                MidpointRounding.AwayFromZero);
        }

        if (unitValue.HasValue || totalValue.HasValue)
            unit = "coin";
    }

    private static decimal? PositiveValue(decimal? value)
    {
        if (!value.HasValue || value.Value < 0m)
            return null;
        return Math.Min(value.Value, MaximumValue);
    }

    private static decimal SafeMultiply(decimal value, int count)
    {
        try
        {
            return Math.Min(value * count, MaximumValue);
        }
        catch (OverflowException)
        {
            return MaximumValue;
        }
    }

    private static string NormalizeEventType(string rawEventType)
    {
        var token = new string(rawEventType
            .Trim()
            .Select(character => char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '_')
            .ToArray())
            .Trim('_');

        return token switch
        {
            "subscribe" or "subscription" or "sub" => "subscription",
            "roomuser" or "room_user" => "room_user",
            "comment" => "chat",
            "" => "unknown",
            _ => token.Length <= 64 ? token : token[..64],
        };
    }

    private static bool IsEmittedType(string type)
    {
        // The mod's v1 boundary currently accepts only actionable TikTok
        // event types. Chat, shares, room-user updates, and unknown future
        // types are intentionally dropped here so a busy LIVE cannot flood
        // the protocol reader with unsupported messages.
        return type is "gift" or "like" or "follow" or "subscription";
    }

    private static string? CleanOptional(string? value)
    {
        var cleaned = ProtocolText.Clean(value);
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static string? CleanUrl(string? value)
    {
        const int maximumUrlLength = 2048;
        var cleaned = ProtocolText.Clean(value, maximumUrlLength + 1);
        return cleaned.Length is 0 or > maximumUrlLength ? null : cleaned;
    }

    private static string? CleanHttpsUrl(string? value)
    {
        var cleaned = CleanUrl(value);
        if (cleaned is null ||
            !Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) ||
            !uri.IsWellFormedOriginalString() ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            IsLocalHost(uri))
        {
            return null;
        }

        return cleaned;
    }

    private static bool IsLocalHost(Uri uri)
    {
        if (uri.IsLoopback)
            return true;

        var host = uri.Host.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
            return !host.Contains('.');

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] is 0 or 10 or 127 ||
                   bytes[0] == 169 && bytes[1] == 254 ||
                   bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                   bytes[0] == 192 && bytes[1] == 168;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return address.Equals(IPAddress.IPv6Any) ||
                   address.Equals(IPAddress.IPv6Loopback) ||
                   address.IsIPv6LinkLocal ||
                   address.IsIPv6SiteLocal ||
                   address.IsIPv6Multicast ||
                   (bytes[0] & 0xfe) == 0xfc;
        }

        return true;
    }

    private static string BuildIdempotencyKey(
        string rawEventType,
        string eventId,
        string? itemId,
        string? streakId,
        int count,
        string? streakState)
    {
        var material = string.Join(
            "\n",
            CompanionProtocol.ConnectionId,
            rawEventType,
            eventId,
            itemId ?? string.Empty,
            streakId ?? string.Empty,
            count.ToString(CultureInfo.InvariantCulture),
            streakState ?? string.Empty);
        return "tfn1:" + Fingerprint(material);
    }

    private static string Fingerprint(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }
}

internal sealed class NormalizationBatch
{
    internal NormalizationBatch(
        IReadOnlyList<CompanionEvent> events,
        IReadOnlyList<string> errors)
    {
        Events = events;
        Errors = errors;
    }

    internal IReadOnlyList<CompanionEvent> Events { get; }

    internal IReadOnlyList<string> Errors { get; }
}
