namespace MusRoyalePC.Models
{
    public sealed class User
    {
        public string Name { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public decimal Balance { get; set; }
    }
}
