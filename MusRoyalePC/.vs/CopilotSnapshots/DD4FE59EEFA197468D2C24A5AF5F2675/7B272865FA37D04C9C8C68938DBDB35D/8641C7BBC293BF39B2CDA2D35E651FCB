using Google.Cloud.Firestore;
using MusRoyalePC.Models;
using MusRoyalePC.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MusRoyalePC.Views
{
    public partial class LagunakView : UserControl
    {
        public ObservableCollection<FriendRowVm> FriendsList { get; set; }

        public LagunakView()
        {
            InitializeComponent();
            FriendsList = new ObservableCollection<FriendRowVm>();
            CargarRelaciones("amigos", "Offline", false);
            this.DataContext = this;
        }

        private void BtnVerAmigos_Click(object sender, RoutedEventArgs e) => CargarRelaciones("amigos", "Offline", false);
        private void BtnVerEnviadas_Click(object sender, RoutedEventArgs e) => CargarRelaciones("solicitudMandada", "Zure zain...", true, "Cancelar");
        private void BtnVerRecibidas_Click(object sender, RoutedEventArgs e) => CargarRelaciones("solicitudRecibida", "Laguna izan nahi du", true, "Aceptar");

        private async void CargarRelaciones(string campoLista, string estadoTexto, bool mostrarBoton, string etiquetaBoton = "")
        {
            try
            {
                var db = Services.FirestoreService.Instance.Db;
                string currentUserId = Properties.Settings.Default.savedId;
                DocumentSnapshot userDoc = await db.Collection("Users").Document(currentUserId).GetSnapshotAsync();

                if (!userDoc.Exists) return;
                FriendsList.Clear();

                if (userDoc.ContainsField(campoLista))
                {
                    var listaIds = userDoc.GetValue<List<string>>(campoLista);
                    foreach (string id in listaIds)
                    {
                        DocumentSnapshot docAmigo = await db.Collection("Users").Document(id).GetSnapshotAsync();
                        if (docAmigo.Exists)
                        {
                            string nombre = docAmigo.GetValue<string>("username") ?? "Sin Nombre";
                            string avatarBD = docAmigo.GetValue<string>("avatarActual");
                            string avatarRuta = "/Assets/" + (avatarBD ?? "default.png");

                            FriendsList.Add(new FriendRowVm(id, nombre, estadoTexto, avatarRuta, mostrarBoton, etiquetaBoton, false, ""));
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Errorea: {ex.Message}"); }
        }

        private async void UserSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string textoBusqueda = UserSearchBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(textoBusqueda)) { CargarRelaciones("amigos", "Offline", false); return; }
            await BuscarUsuariosGlobal(textoBusqueda);
        }

        private async Task BuscarUsuariosGlobal(string filtro)
        {
            try
            {
                var db = Services.FirestoreService.Instance.Db;
                string miId = Services.FirestoreService.Instance.CurrentUserId;
                DocumentSnapshot yoDoc = await db.Collection("Users").Document(miId).GetSnapshotAsync();

                List<string> misAmigos = yoDoc.ContainsField("amigos") ? yoDoc.GetValue<List<string>>("amigos") : new List<string>();

                QuerySnapshot snapshot = await db.Collection("Users").GetSnapshotAsync();
                FriendsList.Clear();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (doc.Id == miId) continue;
                    string nombre = doc.GetValue<string>("username");

                    if (nombre != null && nombre.ToLower().Contains(filtro))
                    {
                        string avatarRuta = "/Assets/" + (doc.GetValue<string>("avatarActual") ?? "default.png");
                        bool yaEsAmigo = misAmigos.Contains(doc.Id);

                        if (yaEsAmigo)
                            FriendsList.Add(new FriendRowVm(doc.Id, nombre, "Laguna duzu", avatarRuta, false, "", false, ""));
                        else
                            FriendsList.Add(new FriendRowVm(doc.Id, nombre, "Mus Royale erabiltzailea", avatarRuta, true, "Gehitu", false, ""));
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}"); }
        }

        private async void ActionBotton_Click(object sender, RoutedEventArgs e)
        {
            var boton = sender as Button;
            var amigoVm = boton?.DataContext as FriendRowVm;
            if (amigoVm == null) return;

            string miId = Services.FirestoreService.Instance.CurrentUserId;
            var db = Services.FirestoreService.Instance.Db;

            try
            {
                switch (amigoVm.PrimaryActionLabel)
                {
                    case "Gehitu":
                        await db.Collection("Users").Document(miId).UpdateAsync("solicitudMandada", FieldValue.ArrayUnion(amigoVm.Id));
                        await db.Collection("Users").Document(amigoVm.Id).UpdateAsync("solicitudRecibida", FieldValue.ArrayUnion(miId));
                        MessageBox.Show("Eskaera bidalita!");
                        break;
                    case "Aceptar":
                    case "Aceptatu":
                        await db.Collection("Users").Document(miId).UpdateAsync("amigos", FieldValue.ArrayUnion(amigoVm.Id));
                        await db.Collection("Users").Document(amigoVm.Id).UpdateAsync("amigos", FieldValue.ArrayUnion(miId));
                        await db.Collection("Users").Document(miId).UpdateAsync("solicitudRecibida", FieldValue.ArrayRemove(amigoVm.Id));
                        await db.Collection("Users").Document(amigoVm.Id).UpdateAsync("solicitudMandada", FieldValue.ArrayRemove(miId));
                        MessageBox.Show("Orain lagunak zarete!");
                        CargarRelaciones("amigos", "Offline", false);
                        break;
                    case "Cancelar":
                        await db.Collection("Users").Document(miId).UpdateAsync("solicitudMandada", FieldValue.ArrayRemove(amigoVm.Id));
                        await db.Collection("Users").Document(amigoVm.Id).UpdateAsync("solicitudRecibida", FieldValue.ArrayRemove(miId));
                        CargarRelaciones("solicitudMandada", "Zure zain...", true, "Cancelar");
                        break;
                }
            }
            catch (Exception ex) { MessageBox.Show("Errorea: " + ex.Message); }
        }
    }

    public sealed class FriendRowVm
    {
        public string Id { get; }
        public string Name { get; }
        public string Status { get; }
        public string Avatar { get; }
        public bool ShowPrimaryAction { get; }
        public string PrimaryActionLabel { get; }
        public bool ShowSecondaryAction { get; }
        public string SecondaryActionLabel { get; }

        public FriendRowVm(string id, string name, string status, string avatar, bool showPrimaryAction, string primary, bool showSecondaryAction, string secondary)
        {
            Id = id;
            Name = name;
            Status = status;
            Avatar = avatar;
            ShowPrimaryAction = showPrimaryAction;
            PrimaryActionLabel = primary;
            ShowSecondaryAction = showSecondaryAction;
            SecondaryActionLabel = secondary;
        }
    }
}