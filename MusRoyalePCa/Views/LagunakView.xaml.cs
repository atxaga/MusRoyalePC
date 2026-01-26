using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace MusRoyalePC.Views
{
    public partial class LagunakView : UserControl
    {
        // Esta es la lista que leerá el XAML
        public ObservableCollection<FriendRowVm> FriendsList { get; set; }

        public LagunakView()
        {
            InitializeComponent();
            FriendsList = new ObservableCollection<FriendRowVm>();

            // Ejemplo rápido para probar que se ve bien:
            CargarEjemplos();

            this.DataContext = this;
        }

        private void CargarEjemplos()
        {
            // name, status, avatar, showPrimary, labelPrimary, showSecondary, labelSecondary
            FriendsList.Add(new FriendRowVm("Iker", "Online", "ava1.png", false));
            FriendsList.Add(new FriendRowVm("Miren", "Eskaera bidalita", "ava2.png", true, "Utzi", true, "Ezabatu"));
            FriendsList.Add(new FriendRowVm("Jon", "Jolasten", "ava3.png", false));
        }
    }
}
public sealed class FriendRowVm
{
    public string Name { get; }
    public string Status { get; }
    public string Avatar { get; } // Añadida la propiedad

    public bool ShowPrimaryAction { get; }
    public string PrimaryActionLabel { get; }
    public bool ShowSecondaryAction { get; }
    public string SecondaryActionLabel { get; }

    public FriendRowVm(
        string name,
        string status,
        string avatar, // El valor que viene de Firebase (ej: "ava1.png")
        bool showPrimaryAction,
        string primary = "Aceptar",
        bool showSecondaryAction = false,
        string secondary = "Eliminar")
    {
        Name = name;
        Status = status;
        Avatar = avatar; // Corregido: Ahora se guarda en la propiedad
        ShowPrimaryAction = showPrimaryAction;
        PrimaryActionLabel = primary;
        ShowSecondaryAction = showSecondaryAction;
        SecondaryActionLabel = secondary;
    }

    public string DisplayIcon => Avatar switch
    {
        "ava1.png" => "🛡️",
        "ava2.png" => "🧙",
        "ava3.png" => "👑",
        "ava4.png" => "🤖",
        "ava5.png" => "👤",
        _ => "👤"
    };
}