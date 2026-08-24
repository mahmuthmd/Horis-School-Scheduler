using System.Collections.Generic;

namespace DersProgramiUI.Models
{
    public class Teacher
    {
        public int ToplamDersSaati { get; set; } = 0;
        public string Ad { get; set; } = string.Empty;
        public string Brans { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = false;

        // 🎯 YENİ: Öğretmenin haftada gelmek istediği maksimum gün sayısı. (0 = Sınır yok)
        public int HedefGunSayisi { get; set; } = 0;

        public Dictionary<Day, int> GunlukMaxDersSaatleri { get; set; } = new Dictionary<Day, int>();
        public List<TimeSlot> MusaitOlmayanZamanlar { get; set; } = new();

        public Teacher() { }

        public Teacher(string ad, string brans) : this()
        {
            Ad = ad;
            Brans = brans;
        }

        public void MusaitOlmayanZamanEkle(Day gun, int saatIndex)
        {
            MusaitOlmayanZamanlar.Add(new TimeSlot(gun, saatIndex));
        }

        public int GunlukMaxDersSaatiGetir(Day gun)
        {
            if (GunlukMaxDersSaatleri != null && GunlukMaxDersSaatleri.TryGetValue(gun, out int limit))
            {
                return limit;
            }
            return 10; // Herhangi bir sınır belirlenmemişse varsayılan 10 saat
        }
    }
}