using DersProgramiUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DersProgramiUI.Engine
{
    public static class BackupManager
    {
        // Yerel Yedek Klasörü
        public static readonly string LocalBackupFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Horis",
            "Backups"
        );

        private static readonly string IndexFilePath = Path.Combine(LocalBackupFolder, "backups_index.json");

        // 🎯 1. YEDEK AL (Yerel Klasöre Kaydeder ve Listeye Ekler)
        public static (bool success, string message) YedekAl(string backupTitle, string tempSourceFilePath)
        {
            try
            {
                if (!Directory.Exists(LocalBackupFolder))
                    Directory.CreateDirectory(LocalBackupFolder);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"Backup_{timestamp}.json";
                string localTarget = Path.Combine(LocalBackupFolder, fileName);

                // Dosyayı yerel yedek klasörüne kopyala
                File.Copy(tempSourceFilePath, localTarget, true);

                // Listeyi oku ve yeni kaydı ekle
                var yedekler = YedekleriGetir();
                yedekler.Insert(0, new BackupModel
                {
                    BackupName = backupTitle,
                    FileName = fileName,
                    CreatedAt = DateTime.Now
                });

                IndexKaydet(yedekler);

                return (true, "Yedekleme başarıyla yerel hafızaya kaydedildi!");
            }
            catch (Exception ex)
            {
                return (false, $"Yedekleme hatası: {ex.Message}");
            }
        }

        // 🎯 2. KAYITLI YEDEKLERİ GETİR
        public static List<BackupModel> YedekleriGetir()
        {
            try
            {
                if (!File.Exists(IndexFilePath)) return new List<BackupModel>();

                string json = File.ReadAllText(IndexFilePath);
                var liste = JsonSerializer.Deserialize<List<BackupModel>>(json) ?? new List<BackupModel>();

                // Gerçekte dosyası silinmiş olanları temizle
                liste = liste.Where(b => File.Exists(Path.Combine(LocalBackupFolder, b.FileName))).ToList();

                return liste;
            }
            catch
            {
                return new List<BackupModel>();
            }
        }

        // 🎯 3. YEDEKTEN GERİ YÜKLEME DOSYASINI BUL
        public static (bool success, string filePath, string message) YedekDosyasiGetir(BackupModel selectedBackup)
        {
            try
            {
                string localPath = Path.Combine(LocalBackupFolder, selectedBackup.FileName);

                if (!File.Exists(localPath))
                    return (false, null, "Yedek dosyası bulunamadı!");

                return (true, localPath, "Yedek dosyası hazır.");
            }
            catch (Exception ex)
            {
                return (false, null, $"Hata: {ex.Message}");
            }
        }

        private static void IndexKaydet(List<BackupModel> liste)
        {
            try
            {
                string json = JsonSerializer.Serialize(liste, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(IndexFilePath, json);
            }
            catch { }
        }
        // 🎯 4. YEDEĞİ HEM DİSKTEN HEM LİSTEDEN SİL
        // 🎯 4. YEDEĞİ HEM DİSKTEN HEM LİSTEDEN SİL (GÜNCELLENDİ)
        public static (bool success, string message) YedekSil(BackupModel selectedBackup)
        {
            try
            {
                // A) Disk üzerindeki fiziksel JSON yedek dosyasını sil
                if (!string.IsNullOrEmpty(selectedBackup.FileName))
                {
                    string localPath = Path.Combine(LocalBackupFolder, selectedBackup.FileName);
                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }
                }

                // B) backups_index.json içinden kaydı çıkar ve güncelle
                var yedekler = YedekleriGetir();
                var silinecekKayit = yedekler.FirstOrDefault(x => x.FileName == selectedBackup.FileName || x.Id == selectedBackup.Id);

                if (silinecekKayit != null)
                {
                    yedekler.Remove(silinecekKayit);
                    IndexKaydet(yedekler);
                }

                return (true, $"'{selectedBackup.BackupName}' yedeği hem listeden hem de diskten tamamen temizlendi.");
            }
            catch (Exception ex)
            {
                return (false, $"Silme işlemi başarısız: {ex.Message}");
            }
        }

    }
}