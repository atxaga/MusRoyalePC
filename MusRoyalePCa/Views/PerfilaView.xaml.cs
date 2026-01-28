using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MusRoyalePC.Views
{
    public partial class PerfilaView : UserControl
    {
        public PerfilaView()
        {
            InitializeComponent();
            loadDatos();
            this.DataContext = this;
        }
        public void loadDatos()
        {
            txtSuccess.Visibility = System.Windows.Visibility.Collapsed;
            txtError.Visibility = System.Windows.Visibility.Collapsed;
            try
            {
                var db = Services.FirestoreService.Instance.Db;
                string currentUserId = Properties.Settings.Default.savedId;
                var userDoc = db.Collection("Users").Document(currentUserId).GetSnapshotAsync().Result;
                if (userDoc.Exists)
                {
                    string dirua = userDoc.ContainsField("dinero") ? userDoc.GetValue<object>("dinero").ToString() : "0";
                    string nombre = userDoc.GetValue<string>("username") ?? "Sin Nombre";
                    string email = userDoc.GetValue<string>("email") ?? "Sin Email";
                    string avatarBD = userDoc.GetValue<string>("avatarActual");
                    string uriPath = $"pack://application:,,,/Assets/{avatarBD}";
                    izena.Text = nombre;
                    txtdirua.Text = dirua;
                    txtemail.Text = email;
                    imgavatar.ImageSource = new BitmapImage(new Uri(uriPath, UriKind.RelativeOrAbsolute));
                }

            }
            catch (Exception ex)
            {
            }

        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            txtSuccess.Visibility = System.Windows.Visibility.Collapsed;
            txtError.Visibility = System.Windows.Visibility.Collapsed;

            string currentUserId = Properties.Settings.Default.savedId;
            string newName = izena.Text;
            string newEmail = txtemail.Text;
            string newPassword = pasahitza.Password;
            var db = Services.FirestoreService.Instance.Db;
            var userRef = db.Collection("Users").Document(currentUserId);

            if (!string.IsNullOrEmpty(newName))
            {
                userRef.UpdateAsync("username", newName);
            }
            if (!string.IsNullOrEmpty(newEmail))
            {
                userRef.UpdateAsync("email", newEmail);
            }
            if (!string.IsNullOrEmpty(newPassword) && newPassword.Length > 5)
            {
                userRef.UpdateAsync("password", newPassword);
            }
            if(!string.IsNullOrEmpty(newPassword) && newPassword.Length <= 5)
            {
                txtSuccess.Visibility = System.Windows.Visibility.Collapsed;
                txtError.Visibility = System.Windows.Visibility.Visible;
                txtError.Text = "Pasahitza gutxienez 6 karaktere izan behar ditu.";
            }
            txtSuccess.Visibility = System.Windows.Visibility.Visible;
        }
    }
}
