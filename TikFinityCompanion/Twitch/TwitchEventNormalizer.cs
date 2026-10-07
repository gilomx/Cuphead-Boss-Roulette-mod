using System.Text.Json;
using LaPichiRuleta.TikFinity.Protocol;

namespace LaPichiRuleta.TikFinity.Twitch;

internal static class TwitchEventNormalizer
{
    internal static CompanionEvent? Normalize(JsonElement root, string broadcasterId, DateTimeOffset receivedAt, bool localTest = false)
    {
        var metadata = root.GetProperty("metadata");
        var payload = root.GetProperty("payload");
        var subscription = payload.GetProperty("subscription");
        var data = payload.GetProperty("event");
        var type = TwitchApi.Text(subscription, "type");
        if (TwitchApi.Text(data, "broadcaster_user_id") != broadcasterId) return null;
        var messageId = TwitchApi.Text(metadata, "message_id");
        if (messageId.Length == 0) return null;
        var userId = TwitchApi.Text(data, type == "channel.chat.message" ? "chatter_user_id" : "user_id");
        var login = TwitchApi.Text(data, type == "channel.chat.message" ? "chatter_user_login" : "user_login");
        var name = TwitchApi.Text(data, type == "channel.chat.message" ? "chatter_user_name" : "user_name");
        var eventType = ""; var chatText = ""; var itemId = ""; var itemName = "";
        var count = 1; var value = 0m; var unit = "";
        switch (type)
        {
            case "channel.chat.message":
                chatText = TwitchApi.Text(data.GetProperty("message"), "text").Trim();
                // Preserve only ballots; other chat messages are unnecessary user data.
                if (chatText.Length != 1 || chatText[0] < '1' || chatText[0] > '6') return null;
                eventType = "chat";
                break;
            case "channel.follow": eventType = "follow"; break;
            case "channel.subscribe":
                if (data.TryGetProperty("is_gift", out var gift) && gift.GetBoolean()) return null;
                eventType = "subscription"; break;
            case "channel.subscription.message": eventType = "subscription"; break;
            case "channel.subscription.gift":
                eventType = "subscription";
                count = Math.Clamp(data.GetProperty("total").GetInt32(), 1, 1000000);
                break;
            case "channel.cheer":
                eventType = "currency"; unit = "bit";
                value = Math.Clamp(data.GetProperty("bits").GetInt32(), 0, 1000000000);
                break;
            case "channel.channel_points_custom_reward_redemption.add":
                eventType = "redemption"; unit = "point";
                var reward = data.GetProperty("reward");
                itemId = TwitchApi.Text(reward, "id"); itemName = TwitchApi.Text(reward, "title");
                value = Math.Clamp(reward.GetProperty("cost").GetInt32(), 0, 1000000000);
                break;
            default: return null;
        }
        var timestamp = TwitchApi.Text(metadata, "message_timestamp");
        if (!DateTimeOffset.TryParse(timestamp, out _)) return null;
        return new CompanionEvent {
            EventId = messageId, IdempotencyKey = "twitch:" + broadcasterId + ":" + messageId,
            ConnectionId = "twitch", Platform = "twitch", Connector = localTest ? "twitch-cli" : "twitch-eventsub",
            Simulated = localTest,
            Type = eventType, UserId = ProtocolText.Clean(userId, 160), UserName = ProtocolText.Clean(login, 80),
            UserDisplayName = ProtocolText.Clean(name, 160), ChatText = chatText,
            ItemId = ProtocolText.Clean(itemId, 160), ItemName = ProtocolText.Clean(itemName, 160),
            Count = count, UnitValue = value, TotalValue = value, Unit = unit,
            // Round boundaries use the PC clock, not Twitch's server clock.
            ReceivedAt = receivedAt.ToUniversalTime(), RawEventType = type,
        };
    }
}
