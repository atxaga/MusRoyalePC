using MusRoyalePC.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MusRoyalePC.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private string _username = "";
        private string _password = "";
        private string _errorMessage = "";

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        // CONSTRUCTOR: Aquí inyectamos la interfaz
        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
        }

        // El método que llamaremos desde los Tests y desde la View
        public async Task<bool> LoginCommand()
        {
            // 1. Validación de campos vacíos
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Eremu guztiak bete";
                return false;
            }

            try
            {
                // 2. Intentar validar usuario
                bool isValid = await _authService.ValidateUserAsync(Username, Password);

                if (isValid)
                {
                    // 3. Comprobar si es admin
                    bool isAdmin = await _authService.IsAdminAsync(Username);
                    ErrorMessage = ""; // Limpiar errores
                    // Aquí podrías guardar el estado: AuthenticatedUser.IsAdmin = isAdmin;
                    return true;
                }
                else
                {
                    ErrorMessage = "Kredentzial okerrak";
                    return false;
                }
            }
            catch (Exception)
            {
                ErrorMessage = "Konexio errorea";
                return false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}