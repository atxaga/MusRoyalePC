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
<<<<<<< HEAD
                // En el constructor o método de inicialización
                string fileName = "musroyale-488aa-4f22ac7baa9a.json";
=======

                string fileName = "D:\\MusRoyalePC\\MusRoyalePC\\musroyale-488aa-a528eff79a06.json";
>>>>>>> 7db0b477b305e5772623f6b7a7d2aeac67d40546
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