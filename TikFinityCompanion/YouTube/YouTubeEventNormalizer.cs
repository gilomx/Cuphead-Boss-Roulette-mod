using LaPichiRuleta.TikFinity.Protocol;
using LaPichiRuleta.TikFinity.YouTube.Wire;

namespace LaPichiRuleta.TikFinity.YouTube;

internal sealed class YouTubeEventNormalizer
{
    private readonly Dictionary<string, int> seen = new(StringComparer.Ordinal);
    private readonly Queue<string> order = new();

    internal CompanionEvent? Normalize(LiveChatMessage message, string channelId, string chatId,
        DateTimeOffset receivedAt, bool history = false)
    {
        lock (seen) return NormalizeLocked(message, channelId, chatId, receivedAt, history);
    }

    private CompanionEvent? NormalizeLocked(LiveChatMessage message, string channelId, string chatId,
        DateTimeOffset receivedAt, bool history)
    {
        var snippet = message.Snippet;
        if (snippet == null || message.Id.Length == 0 || snippet.LiveChatId != chatId) return null;
        var gift = snippet.GiftDetails;
        var combo = gift == null ? 1 : Math.Max(1, gift.ComboCount);
        var identity = channelId + ":" + chatId + ":" + message.Id;
        seen.TryGetValue(identity, out var previous);
        if (previous >= combo) return null;
        if (previous == 0) order.Enqueue(identity);
        seen[identity] = combo;
        while (order.Count > 32768) seen.Remove(order.Dequeue());
        // First response contains chat history. Seed identities, never replay donations or old votes.
        if (history) return null;

        string type, rawType, unit = "", currency = "", itemId = "", itemName = "", image = "", chat = "";
        var count = 1; decimal value = 0;
        switch ((int)snippet.Type)
        {
            case 1:
                chat = snippet.TextMessageDetails?.MessageText.Trim() ?? "";
                if (chat.Length != 1 || chat[0] < '1' || chat[0] > '6') return null;
                type = "chat"; rawType = "textMessageEvent"; break;
            case 7:
                if (snippet.NewSponsorDetails == null) return null;
                type = "subscription"; unit = snippet.NewSponsorDetails.IsUpgrade ? "upgrade" : "new";
                itemName = snippet.NewSponsorDetails.MemberLevelName; rawType = "newSponsorEvent"; break;
            case 15:
                if (snippet.SuperChatDetails == null) return null;
                type = "currency"; unit = "money"; rawType = "superChatEvent";
                value = snippet.SuperChatDetails.AmountMicros / 1000000m;
                currency = snippet.SuperChatDetails.Currency; itemName = "Super Chat"; break;
            case 16:
                if (snippet.SuperStickerDetails == null) return null;
                type = "currency"; unit = "money"; rawType = "superStickerEvent";
                value = snippet.SuperStickerDetails.AmountMicros / 1000000m;
                currency = snippet.SuperStickerDetails.Currency; itemName = "Super Sticker";
                itemId = snippet.SuperStickerDetails.SuperStickerMetadata?.StickerId ?? ""; break;
            case 17:
                type = "subscription"; unit = "milestone"; rawType = "memberMilestoneChatEvent";
                itemName = snippet.MemberMilestoneChatDetails?.MemberLevelName ?? ""; break;
            case 18:
                count = Math.Clamp(snippet.MembershipGiftingDetails?.GiftMembershipsCount ?? 0, 0, 1000000);
                if (count == 0) return null;
                type = "subscription"; unit = "gift"; rawType = "membershipGiftingEvent";
                itemName = snippet.MembershipGiftingDetails?.GiftMembershipsLevelName ?? ""; break;
            case 21:
                if (gift == null || gift.JewelsAmount <= 0) return null;
                type = "gift"; unit = "jewel"; rawType = "giftEvent";
                count = Math.Clamp(combo - previous, 1, 1000000); value = gift.JewelsAmount;
                itemName = gift.GiftName; image = SafeImage(gift.GiftUrl);
                itemId = gift.GiftName; break;
            // Gift recipients do not represent another purchase; no double-counting.
            default: return null;
        }
        var author = message.AuthorDetails;
        return new CompanionEvent {
            EventId = message.Id, IdempotencyKey = "youtube:" + identity + ":" + combo,
            ConnectionId = "youtube", Platform = "youtube", Connector = "youtube-live-chat",
            Type = type, RawEventType = rawType, UserId = author?.ChannelId ?? snippet.AuthorChannelId,
            UserName = author?.DisplayName ?? "", UserDisplayName = author?.DisplayName ?? "",
            UserAvatarUrl = SafeImage(author?.ProfileImageUrl ?? ""), ChatText = chat,
            Count = count, UnitValue = value, TotalValue = value * count,
            Unit = unit, Currency = currency, ItemId = itemId, ItemName = itemName, ItemImageUrl = image,
            ReceivedAt = receivedAt, StreakState = "none",
        };
    }

    private static string SafeImage(string value) => value.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.UserInfo.Length == 0 ? value : "";
}
