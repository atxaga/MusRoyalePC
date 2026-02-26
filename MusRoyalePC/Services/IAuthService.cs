using Google.Cloud.Firestore;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;


namespace MusRoyalePC.Services
{
    // El "Contrato" para poder hacer Mocks
    public interface IAuthService
    {
        Task<bool> ValidateUserAsync(string username, string password);
        Task<bool> IsAdminAsync(string username);
    }

    // La implementación real que usa tu FirestoreService
    public class AuthService : IAuthService
    {
        private readonly FirestoreDb _db;

        public AuthService()
        {
            // Usamos la instancia que ya configuraste de Firebase
            _db = FirestoreService.Instance.Db;
        }

        public async Task<bool> ValidateUserAsync(string username, string password)
        {
            try
            {
                Query query = _db.Collection("Users").WhereEqualTo("username", username);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                if (snapshot.Documents.Count == 0) return false;

                var userData = snapshot.Documents[0].ToDictionary();
                string storedHash = userData["password"].ToString();

                // Hasheamos la contraseña que mete el usuario en el login
                string inputHash = GetSha256Hash(password);

                // Comparamos los dos hashes (el de la DB y el generado ahora)
                // Usamos OrdinalIgnoreCase por si acaso el de la DB está en mayúsculas
                return string.Equals(storedHash, inputHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> IsAdminAsync(string username)
        {
            Query query = _db.Collection("Users").WhereEqualTo("username", username);
            QuerySnapshot snapshot = await query.GetSnapshotAsync();

            if (snapshot.Documents.Count == 0) return false;

            var userData = snapshot.Documents[0].ToDictionary();

            if (userData.ContainsKey("rol"))
            {
                var roleValue = userData["rol"];
                if (roleValue is bool b) return b;
                if (roleValue is long l) return l == 1;
                if (roleValue is int i) return i == 1;

                // Por si acaso viene como string "1" o "0"
                string s = roleValue.ToString();
                return s == "1" || s.ToLower() == "true";
            }
            return false;
        }

        private string GetSha256Hash(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}