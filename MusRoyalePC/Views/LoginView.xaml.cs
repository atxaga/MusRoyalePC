using Google.Cloud.Firestore;
using MusRoyalePC.Models;
using MusRoyalePC.Services;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace MusRoyalePC.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string userTyped = TxtUser.Text.Trim();
            string passTyped = TxtPass.Password;

            if (string.IsNullOrEmpty(userTyped)) return;

            try
            {
                var db = FirestoreService.Instance.Db;
                Query query = db.Collection("Users").WhereEqualTo("email", userTyped);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                if (snapshot.Documents.Count > 0)
                {
                    DocumentSnapshot userDoc = snapshot.Documents[0];
                    string passHashedInDb = userDoc.GetValue<string>("password");

                    if (passHashedInDb == HashSHA256(passTyped))
                    {
                        // 1. Guardar ID global
                        FirestoreService.Instance.CurrentUserId = userDoc.Id;

                        // 2. Acceder al ViewModel del MainWindow
                        var mainWin = (MainWindow)Application.Current.MainWindow;
                        if (mainWin.DataContext is MainViewModel vm)
                        {
                            // Cargamos datos en el Header desde Firestore
                            // 1. Cargamos datos del usuario
                            vm.UserName = userDoc.GetValue<string>("username");
                            var dinero = userDoc.ContainsField("dinero") ? userDoc.GetValue<object>("dinero").ToString() : "0";
                            vm.Balance = dinero;
                            // Dentro de tu lógica de Login exitoso
                            if (RecordarCheck.IsChecked == true) // Si tienes un CheckBox de "Recordarme"
                            {
                                Properties.Settings.Default.savedId = userDoc.Id;
                                Properties.Settings.Default.Save();
                            }
                            // Esto pone CurrentView en null (para el menú de madera)
                            // Y pone CurrentPageName en "Home" (para que el Footer sea visible)
                            UserSession.Instance.Username = userDoc.GetValue<string>("username");
                            UserSession.Instance.Avatar = userDoc.GetValue<string>("avatarActual");
                            UserSession.Instance.DocumentId = userDoc.Id;
                            vm.Navegar("Home");
                        }
                    }
                    else { MessageBox.Show("Pasahitza okerra."); }
                }
                else { MessageBox.Show("Erabiltzailea ez da existitzen."); }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errorea: " + ex.Message);
            }
        }

        private string HashSHA256(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}