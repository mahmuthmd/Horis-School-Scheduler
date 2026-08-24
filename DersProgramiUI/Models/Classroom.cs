using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DersProgramiUI.Models
{
    public class DersYukBilgisi
    {
        public int Saat { get; set; }
        public Teacher ZorunluOgretmen { get; set; }

        public DersYukBilgisi() { }
        public DersYukBilgisi(int saat, Teacher zorunluOgretmen)
        {
            Saat = saat;
            ZorunluOgretmen = zorunluOgretmen;
        }
    }

    public class Classroom
    {
        public string Ad { get; set; } = string.Empty;
        public Dictionary<string, DersYukBilgisi> DersProgramiYukDetailed { get; set; } = new();

        [JsonIgnore]
        public Dictionary<string, int> DersProgramiYuk =>
            DersProgramiYukDetailed.ToDictionary(k => k.Key, v => v.Value.Saat);

        public List<TimeSlot> UygunZamanlar { get; set; } = new();
        public int HedefGunSayisi { get; set; } = 0;

        [JsonIgnore]
        public int ToplamDersSaati => DersProgramiYukDetailed.Values.Sum(x => x.Saat);

        public Dictionary<Day, int> GunlukMaxDersSaatleri { get; set; } = new Dictionary<Day, int>();

        public int GunlukMaxDersSaatiGetir(Day gun)
        {
            if (GunlukMaxDersSaatleri != null && GunlukMaxDersSaatleri.ContainsKey(gun))
            {
                return GunlukMaxDersSaatleri[gun];
            }
            return 10;
        }

        [JsonIgnore]
        public string DersYukOzet => DersProgramiYukDetailed.Count > 0
            ? string.Join(", ", DersProgramiYukDetailed.Select(kv =>
                kv.Value.ZorunluOgretmen != null
                ? $"{kv.Key} ({kv.Value.ZorunluOgretmen.Ad} - {kv.Value.Saat}s)"
                : $"{kv.Key} ({kv.Value.Saat}s)"))
            : "Ders Eklemedi";

        // 🔒 Sabitlenmiş/Kilitlenmiş dersler listesi
        public List<SabitDers> SabitDersler { get; set; } = new List<SabitDers>();

        public Classroom() { }
        public Classroom(string ad) : this()
        {
            Ad = ad;
        }

        public void DersEkle(string dersAdi, int saatSayisi, Teacher zorunluOgretmen = null)
        {
            DersProgramiYukDetailed[dersAdi] = new DersYukBilgisi(saatSayisi, zorunluOgretmen);
        }
    }

    public class SinifDersYukItem
    {
        public string DersAdi { get; set; }
        public int SaatSayisi { get; set; }
        public Teacher ZorunluOgretmen { get; set; }


        // 🎯 LİSTEDE GÖRÜNECEK ŞIK METİN (Örn: Matematik (6 Saat) - [Ahmet Hoca])
        public string GosterimMetni
        {
            get
            {
                string ogrBilgi = ZorunluOgretmen != null ? ZorunluOgretmen.Ad : "Otomatik Seçim";
                return $"{DersAdi} - {SaatSayisi} Saat  ({ogrBilgi})";
            }
        }

        public override string ToString()
        {
            return GosterimMetni;
        }
    }
}