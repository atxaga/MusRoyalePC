using Xunit;
using MusRoyalePC.Services;
using System.Threading.Tasks;

namespace MusRoyalePC.Tests
{
    public class LoginFirebaseTests
    {
        private readonly AuthService _authService;

        public LoginFirebaseTests()
        {
            _authService = new AuthService();
        }

        // 7. Agertokia: Admin real en Firebase (role: 1)
        [Fact]
        [Trait("Type", "Integration")]
        public async Task DB_AdminReal_ReturnsTrue()
        {
            // Datos semilla: admin / admin1234 / role: 1
            bool valid = await _authService.ValidateUserAsync("bittor", "bittortelletxea@gmail.com");
            bool admin = await _authService.IsAdminAsync("bittor");

            Assert.True(admin);
        }

        // 8. Agertokia: User real en Firebase (role: 0)
        [Fact]
        [Trait("Type", "Integration")]
        public async Task DB_UserReal_ReturnsTrue()
        {
            // Datos semilla: user / 1234 / role: 0
            bool valid = await _authService.ValidateUserAsync("iker", "123456");
            bool admin = await _authService.IsAdminAsync("iker");

            Assert.True(valid);
            Assert.False(admin);
        }

        // 9. Agertokia: Kredentzial txarrak en DB real
        [Fact]
        [Trait("Type", "Integration")]
        public async Task DB_WrongPass_ReturnsFalse()
        {
            bool valid = await _authService.ValidateUserAsync("bittor", "incorrecto");
            Assert.False(valid);
        }

        // 10. Agertokia: Usuario que no existe en Firebase
        [Fact]
        [Trait("Type", "Integration")]
        public async Task DB_NonExistentUser_ReturnsFalse()
        {
            bool valid = await _authService.ValidateUserAsync("ezdago", "123");
            Assert.False(valid);
        }
    }
}