using Google.Cloud.Firestore;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MusRoyalePC.Services
{
    public class FirestoreAuthService : IAuthService
    {
        public async Task<AuthResult> LoginAsync(string email, string password)
        {
            try
            {
                var db = FirestoreService.Instance.Db;
                var query = db.Collection("Users").WhereEqualTo("email", email);
                var snapshot = await query.GetSnapshotAsync();

                if (snapshot.Documents.Count == 0)
                    return new AuthResult { Success = false, ErrorMessage = "Erabiltzailea ez da existitzen" };

                var userDoc = snapshot.Documents[0];
                string passDb = userDoc.GetValue<string>("password");

                if (passDb != HashSHA256(password))
                    return new AuthResult { Success = false, ErrorMessage = "Pasahitza okerra" };

                return new AuthResult
                {
                    Success = true,
                    UserId = userDoc.Id,
                    UserName = userDoc.GetValue<string>("username"),
                    Balance = userDoc.ContainsField("dinero") ? userDoc.GetValue<object>("dinero").ToString() : "0",
                    Role = userDoc.ContainsField("rol") ? userDoc.GetValue<int>("rol").ToString() : "0"
                };
            }
            catch
            {
                return new AuthResult { Success = false, ErrorMessage = "Konexio errorea" };
            }
        }

        public async Task<bool> IsAdminAsync(string email)
        {
            try
            {
                var db = FirestoreService.Instance.Db;
                var query = db.Collection("Users").WhereEqualTo("email", email);
                var snapshot = await query.GetSnapshotAsync();

                if (snapshot.Documents.Count == 0)
                    return false;

                var userDoc = snapshot.Documents[0];
                return userDoc.ContainsField("rol") && userDoc.GetValue<int>("rol") == 1;
            }
            catch
            {
                return false;
            }
        }

        private string HashSHA256(string input)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLower();
        }
    }
}
