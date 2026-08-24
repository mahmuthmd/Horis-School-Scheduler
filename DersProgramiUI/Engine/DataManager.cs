using DersProgramiUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DersProgramiUI.Engine
{
    public class OkulVerisi
    {
        public List<Teacher> Ogretmenler { get; set; } = new();
        public List<Classroom> Siniflar { get; set; } = new();
        public List<Lesson> Dersler { get; set; } = new();
    }

    public static class DataManager
    {
        private static string dosyaYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "okul_verileri.json");

        public static void Kaydet(List<Teacher> ogretmenler, List<Classroom> siniflar, List<Lesson> dersler)
        {
            try
            {
                OkulVerisi veri = new OkulVerisi
                {
                    Ogretmenler = ogretmenler,
                    Siniflar = siniflar,
                    Dersler = dersler
                };

                // ReferenceHandler.IgnoreCycles eklenerek sonsuz döngü çökmesi engellendi
                var secenekler = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                };

                string json = JsonSerializer.Serialize(veri, secenekler);
                File.WriteAllText(dosyaYolu, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kaydetme hatası: {ex.Message}");
            }
        }

        public static OkulVerisi Yukle()
        {
            if (!File.Exists(dosyaYolu)) return null;

            try
            {
                string json = File.ReadAllText(dosyaYolu);
                var secenekler = new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                };
                var veri = JsonSerializer.Deserialize<OkulVerisi>(json, secenekler);

                // 🎯 VERİ BÜTÜNLÜĞÜ KONTROLÜ (Hayalet Verileri Belleğe Yüklenirken Temizle)
                VeriButunlugunuTemizle(veri);
                return veri;
            }
            catch
            {
                return null;
            }
        }

        public static void KaydetFarkliYol(List<Teacher> ogretmenler, List<Classroom> siniflar, List<Lesson> dersler, string yol)
        {
            OkulVerisi veri = new OkulVerisi
            {
                Ogretmenler = ogretmenler,
                Siniflar = siniflar,
                Dersler = dersler
            };
            var secenekler = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            string json = JsonSerializer.Serialize(veri, secenekler);
            File.WriteAllText(yol, json);
        }

        // İstediğimiz yoldan yedek yüklemek için:
        public static OkulVerisi YukleFarkliYol(string yol)
        {
            if (!File.Exists(yol)) return null;
            string json = File.ReadAllText(yol);
            var secenekler = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
            var veri = JsonSerializer.Deserialize<OkulVerisi>(json, secenekler);

            // 🎯 VERİ BÜTÜNLÜĞÜ KONTROLÜ (Yedek Geri Yüklenirken Hayalet Verileri Temizle)
            VeriButunlugunuTemizle(veri);
            return veri;
        }

        // 🧹 HAYALET VERİ TEMİZLEME MOTORU (Garbage Collection)
        private static void VeriButunlugunuTemizle(OkulVerisi veri)
        {
            if (veri == null) return;

            var ogretmenAdlari = new HashSet<string>(veri.Ogretmenler.Select(o => o.Ad), StringComparer.OrdinalIgnoreCase);
            var dersAdlari = new HashSet<string>(veri.Dersler.Select(d => d.Ad), StringComparer.OrdinalIgnoreCase);

            // 1. Dersleri veren öğretmenler listesinden, sistemden silinmiş öğretmenleri uçur
            foreach (var ders in veri.Dersler)
            {
                if (ders.VerenOgretmenler != null)
                {
                    ders.VerenOgretmenler.RemoveAll(o => o == null || !ogretmenAdlari.Contains(o.Ad));
                }
            }

            // 2. Sınıfların ders yüklerinden silinmiş dersleri ve öğretmenleri temizle
            foreach (var sinif in veri.Siniflar)
            {
                // Artık sistemde olmayan dersleri yükten çıkar
                var silinecekDersler = sinif.DersProgramiYukDetailed.Keys
                    .Where(ad => !dersAdlari.Contains(ad))
                    .ToList();

                foreach (var d in silinecekDersler)
                {
                    sinif.DersProgramiYukDetailed.Remove(d);
                }

                // Ders yükündeki zorunlu öğretmen silindiyse zorunluluğu kaldır (null yap)
                foreach (var kvp in sinif.DersProgramiYukDetailed.ToList())
                {
                    if (kvp.Value.ZorunluOgretmen != null && !ogretmenAdlari.Contains(kvp.Value.ZorunluOgretmen.Ad))
                    {
                        kvp.Value.ZorunluOgretmen = null;
                    }
                }

                // 3. Sabit/Kilitli derslerden silinmiş dersleri veya silinmiş öğretmenleri temizle
                if (sinif.SabitDersler != null)
                {
                    sinif.SabitDersler.RemoveAll(sb =>
                        !dersAdlari.Contains(sb.DersAdi) ||
                        (sb.Ogretmen != null && !ogretmenAdlari.Contains(sb.Ogretmen.Ad))
                    );
                }
            }
        }
    }
}