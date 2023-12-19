using System.Text.Json.Serialization;

namespace Serein.Core.Models.OneBot.ActionParams;

public class MessageParams
{
    public string Type => UserId is not null ? "private" : "group";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GroupId { get; init; }

    public string Message { get; init; } = string.Empty;

    public bool AutoEscape { get; init; }
}