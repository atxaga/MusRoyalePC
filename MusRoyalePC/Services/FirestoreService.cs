using Google.Cloud.Firestore;
using System;
using System.IO;
using System.Windows;

namespace MusRoyalePC.Services
{
    public class FirestoreService
    {
        private static FirestoreService _instance;
        public static FirestoreService Instance => _instance ??= new FirestoreService();

        public FirestoreDb Db { get; private set; }
        public string CurrentUserId { get; set; } 

        private FirestoreService()
        {
            try
            {
                // En el constructor o método de inicialización
<<<<<<< HEAD
                string fileName = "D:\\MusRoyalePC\\MusRoyalePC\\musroyale-488aa-267ecdd8a92b.json";
=======
                string fileName = "D:\\MusRoyalePC\\MusRoyalePC\\musroyale-488aa-3bbfbc7f364c.json";
>>>>>>> 8f3f08258d110f0b0edc0ae21e867def8f9c4f70
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                if (File.Exists(path))
                {
                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
                    // Inicializa tu FirestoreDb.Create("tu-id-proyecto") aquí
                }
                else
                {
                    MessageBox.Show($"Error crítico: No se encuentra el archivo de credenciales en {path}");
                }

                // 3. Crear la conexión usando tu project_id del JSON
                Db = FirestoreDb.Create("musroyale-488aa");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error al conectar con Firebase: " + ex.Message);
            }
        }
    }
}