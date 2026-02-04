using MusRoyalePC;
using MusRoyalePC.Models;
using MusRoyalePC.Services;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace MusRoyalePC.Tests.ViewModels
{
    public class LoginViewModelTests
    {
        [Fact]
        public async Task Login_Success_ReturnsTrue()
        {
            // Arrange
            var mockAuth = new Mock<IAuthService>();
            mockAuth.Setup(a => a.LoginAsync("user@mail.com", "1234"))
                    .ReturnsAsync(new AuthResult { Success = true, UserId = "id123", UserName = "User", Balance = "100" });

            var vm = new LoginViewModel(mockAuth.Object)
            {
                Username = "user@mail.com",
                Password = "1234"
            };

            // Act
            var result = await vm.Login();

            // Assert
            Assert.True(result);
            Assert.Equal("User", UserSession.Instance.Username);
            Assert.Equal("id123", UserSession.Instance.DocumentId);
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsFalse()
        {
            var mockAuth = new Mock<IAuthService>();
            mockAuth.Setup(a => a.LoginAsync("user@mail.com", "wrong"))
                    .ReturnsAsync(new AuthResult { Success = false, ErrorMessage = "Pasahitza okerra" });

            var vm = new LoginViewModel(mockAuth.Object)
            {
                Username = "user@mail.com",
                Password = "wrong"
            };

            var result = await vm.Login();

            Assert.False(result);
            Assert.Equal("Pasahitza okerra", vm.ErrorMessage);
        }

        [Fact]
        public async Task Login_UserNotFound_ReturnsFalse()
        {
            var mockAuth = new Mock<IAuthService>();
            mockAuth.Setup(a => a.LoginAsync("nouser@mail.com", "1234"))
                    .ReturnsAsync(new AuthResult { Success = false, ErrorMessage = "Erabiltzailea ez da existitzen" });

            var vm = new LoginViewModel(mockAuth.Object)
            {
                Username = "nouser@mail.com",
                Password = "1234"
            };

            var result = await vm.Login();

            Assert.False(result);
            Assert.Equal("Erabiltzailea ez da existitzen", vm.ErrorMessage);
        }

        [Fact]
        public async Task Login_EmptyFields_ReturnsFalse()
        {
            var mockAuth = new Mock<IAuthService>();
            var vm = new LoginViewModel(mockAuth.Object)
            {
                Username = "",
                Password = ""
            };

            var result = await vm.Login();

            Assert.False(result);
            Assert.Equal("Datuak hutsik", vm.ErrorMessage);
        }
    }
}
