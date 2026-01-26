using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MusRoyalePC.Services;
using Google.Cloud.Firestore;

namespace MusRoyalePC.Views
{
    public partial class RegisterView : UserControl
    {
        public RegisterView()
        {
            InitializeComponent();
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string user = TxtUser.Text.Trim();
            string email = TxtEmail.Text.Trim();
            string pass = TxtPass.Password;

            // Validación básica de campos
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Mesedez, bete eremu guztiak.");
                return;
            }

            try
            {
                var db = FirestoreService.Instance.Db;

                // 1. Verificar si el usuario ya existe
                DocumentReference userRef = db.Collection("Users").Document(user);
                DocumentSnapshot snapshot = await userRef.GetSnapshotAsync();

                if (snapshot.Exists)
                {
                    MessageBox.Show("Erabiltzaile izen hori hartuta dago.");
                    return;
                }

                // 2. Preparar los datos del nuevo usuario
                // Incluimos los arrays vacíos para que el sistema de amigos no de errores de "null" después
                var userData = new Dictionary<string, object>
                {
                    { "username", user },
                    { "email", email },
                    { "password", HashSHA256(pass) },
                    { "dinero", 0 },
                    { "oro", 1000 },
                    { "amigos", new List<string>() },
                    { "solicitudMandada", new List<string>() },
                    { "solicitudRecivida", new List<string>() },
                    { "premium", false }
                };

                // 3. Guardar en Firestore
                await userRef.SetAsync(userData);

                MessageBox.Show("Erregistroa ondo egin da! Orain saioa hasi dezakezu.");

                // 4. Volver a la vista de Login automáticamente
                var mainWin = (MainWindow)Application.Current.MainWindow;
                if (mainWin.DataContext is MainViewModel vm)
                {
                    vm.CurrentView = new LoginView();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errorea erregistratzerakoan: " + ex.Message);
            }
        }

        /// <summary>
        /// Hashea la contraseña en SHA256 para no guardarla en texto plano.
        /// Debe ser el mismo método que usas en el LoginView.
        /// </summary>
        private string HashSHA256(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}