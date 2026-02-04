using MusRoyalePC.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MusRoyalePC.Models
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _auth;

        public string Username { get; set; }
        public string Password { get; set; }
        public string ErrorMessage { get; private set; }

        public ICommand LoginCommand { get; }

        public LoginViewModel(IAuthService auth)
        {
            _auth = auth;
            LoginCommand = new RelayCommand<object>(async _ => await Login());
        }

        public async Task<bool> Login()
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Datuak hutsik";
                return false;
            }

            var result = await _auth.LoginAsync(Username, Password);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage;
                return false;
            }

            // hemen bakarrik negozio-logika
            UserSession.Instance.Username = result.UserName;
            UserSession.Instance.DocumentId = result.UserId;

            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

}
