namespace MusRoyalePC.Models;

public sealed class AvatarStoreItem
{
    public string Id { get; init; } = string.Empty; // Firestore doc id
    public string File { get; init; } = string.Empty; // e.g. "ava1.png"
    public int PriceOro { get; init; }
    public string Name { get; init; } = string.Empty;
}
