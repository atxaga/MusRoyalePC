namespace MusRoyalePC.Models;

public sealed class UserStatsDto
{
    public string Id { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int Partidak { get; init; }
    public int PartidaIrabaziak { get; init; }

    public bool IsFriend { get; init; }
    public bool RequestAlreadySent { get; init; }
    public bool RequestAlreadyReceived { get; init; }
}
