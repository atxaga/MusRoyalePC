using QuestPDF.Infrastructure;
using System.Configuration;
using System.Data;
using System.Windows;
using MusRoyalePC.Services;

namespace MusRoyalePC
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // QuestPDF: lizentzia aukeratu (gehienetan Community)
            QuestPDF.Settings.License = LicenseType.Community;

            base.OnStartup(e);

            // Listener global de invitaciones Duo (se activará cuando exista sesión)
            DuoInviteCoordinator.Instance.Start();
        }
    }

}