using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusRoyalePC.Models
{
    public class Laguna
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string Avatar { get; set; } // Ejemplo: "ava5.png"
        public string PrimaryActionLabel { get; set; } // "Laguna" o "Gehitu"

        // Propiedad para convertir el nombre del avatar en una ruta real o emoji
        public string DisplayAvatar => Avatar?.Contains("ava") == true ? "🛡️" : "👤";
    }
}
