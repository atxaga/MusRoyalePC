using Google.Cloud.Firestore;
using MusRoyalePC.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

public class LagunakViewModel : INotifyPropertyChanged
{
    public ObservableCollection<FriendRowVm> Amigos { get; } = new();
    public ObservableCollection<FriendRowVm> Enviadas { get; } = new();
    public ObservableCollection<FriendRowVm> Recibidas { get; } = new();

    public event PropertyChangedEventHandler PropertyChanged;

    public LagunakViewModel()
    {
        EscucharCambiosFirebase();
    }

    private void EscucharCambiosFirebase()
    {
        var service = FirestoreService.Instance;
        if (string.IsNullOrEmpty(service.CurrentUserId)) return;

        // Escuchamos el documento del usuario logueado (ej: "iker")
        DocumentReference docRef = service.Db.Collection("usuarios").Document(service.CurrentUserId);

        docRef.Listen(snapshot =>
        {
            if (!snapshot.Exists) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Amigos.Clear();
                Enviadas.Clear();
                Recibidas.Clear();

                // 1. Amigos (Array 'amigos' en Firebase)
                var listaAmigos = snapshot.GetValue<List<string>>("amigos") ?? new List<string>();
                foreach (var nombre in listaAmigos)
                    Amigos.Add(new FriendRowVm(nombre, "En línea", "ava1.png", false, "Laguna", true, "Eliminar"));

                // 2. Solicitudes Mandadas (Array 'solicitudMandada')
                var listaEnviadas = snapshot.GetValue<List<string>>("solicitudMandada") ?? new List<string>();
                foreach (var nombre in listaEnviadas)
                    Enviadas.Add(new FriendRowVm(nombre, "Esperando...", "ava5.png", false, "", true, "Cancelar"));

                // 3. Solicitudes Recibidas (Array 'solicitudRecivida')
                var listaRecibidas = snapshot.GetValue<List<string>>("solicitudRecivida") ?? new List<string>();
                foreach (var nombre in listaRecibidas)
                    Recibidas.Add(new FriendRowVm(nombre, "Quiere agregarte", "ava2.png", true, "Gehitu", true, "Rechazar"));
            });
        });
    }
}