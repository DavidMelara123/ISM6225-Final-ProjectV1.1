namespace AffiliateDataChat.Models;

public sealed record ChatTurn(string Role, string Text, DateTimeOffset TimeUtc);
