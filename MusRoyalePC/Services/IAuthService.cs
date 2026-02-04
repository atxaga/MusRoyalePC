using System.Threading.Tasks;

namespace MusRoyalePC.Services
{
    public interface IAuthService
    {
        Task<AuthResult> LoginAsync(string email, string password);
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Balance { get; set; }
        public string Role { get; set; } = "0";
        public bool IsAdmin => Role == "1";
    }
}
