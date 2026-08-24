using System.Collections.Generic;
using System.Linq;

namespace DersProgramiUI.Models
{
    public class Lesson
    {
        public string Ad { get; set; } = string.Empty;
        public string KisaAd { get; set; } = string.Empty;
        public List<Teacher> VerenOgretmenler { get; set; } = new List<Teacher>();

        public string VerenOgretmenlerMetni => VerenOgretmenler.Count > 0
            ? string.Join(", ", VerenOgretmenler.Select(o => o.Ad))
            : "Öğretmen Atanmadı";

        public Lesson() { }

        public Lesson(string ad) : this()
        {
            Ad = ad;
            KisaAd = ad.Length > 3 ? ad.Substring(0, 3).ToUpper() : ad.ToUpper();
        }

        // Hatanın çözümü: 2 Parametreli Constructor
        public Lesson(string ad, string kisaAd) : this()
        {
            Ad = ad;
            KisaAd = string.IsNullOrWhiteSpace(kisaAd) ? (ad.Length > 3 ? ad.Substring(0, 3).ToUpper() : ad.ToUpper()) : kisaAd;
        }
    }
}