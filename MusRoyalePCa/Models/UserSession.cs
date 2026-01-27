using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusRoyalePC.Models
{
    public class UserSession
    {
        private static UserSession _instance;
        public static UserSession Instance => _instance ??= new UserSession();

        // Aquí guardas lo que necesites del usuario actual
        public string Username { get; set; }
        public string Email { get; set; }
        public string Avatar { get; set; }
        public string DocumentId { get; set; }

        private UserSession() { } // Constructor privado para Singleton

        public void LogOut()
        {
            Username = null;
            Email = null;
            DocumentId = null;
        }
    }
}
