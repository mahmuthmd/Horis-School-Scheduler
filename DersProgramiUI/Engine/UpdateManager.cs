using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace DersProgramiUI.Engine
{
    public static class UpdateManager
    {
        // 🎯 1. Mevcut Uygulama Sürümü
        public static readonly string MevcutVersiyon = "1.0.5";

        // 🎯 2. Supabase Storage Doğru Public URL'si
        private static readonly string VersionJsonUrl = "https://atkaxgwiqemhjsdkendo.supabase.co/storage/v1/object/public/Updates/version.json";

        public static async Task GuncellemeKontrolEtAsync()
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "DersProgrami-Updater");

                    HttpResponseMessage response = await client.GetAsync(VersionJsonUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[UpdateManager] Sunucuya erişilemedi: {response.StatusCode}");
                        return;
                    }

                    string jsonText = await response.Content.ReadAsStringAsync();

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var veriler = JsonSerializer.Deserialize<VersionInfo>(jsonText, options);

                    if (veriler == null || string.IsNullOrEmpty(veriler.Version))
                        return;

                    Version sunucuVersiyon = Version.Parse(veriler.Version);
                    Version yerelVersiyon = Version.Parse(MevcutVersiyon);

                    if (sunucuVersiyon > yerelVersiyon)
                    {
                        // UI Thread üzerinde MessageBox tetikleme
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            var cevap = MessageBox.Show(
                                $"🚀 Yeni bir güncelleme mevcut!\n\n" +
                                $"Mevcut Sürüm: v{MevcutVersiyon}\n" +
                                $"Yeni Sürüm: v{veriler.Version}\n\n" +
                                $"Yenilikler / Değişiklikler:\n{veriler.ReleaseNotes}\n\n" +
                                $"Güncelleme şimdi indirilip kurulsun mu?",
                                "Otomatik Güncelleme",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);

                            if (cevap == MessageBoxResult.Yes)
                            {
                                await GuncellemeyiIndirVeCalistir(veriler.DownloadUrl);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[UpdateManager] Hata: " + ex.Message);
            }
        }

        private static async Task GuncellemeyiIndirVeCalistir(string downloadUrl)
        {
            try
            {
                string tempFolder = Path.GetTempPath();
                string tempSetupPath = Path.Combine(tempFolder, "DersProgrami_Guncelleme.exe");

                if (File.Exists(tempSetupPath))
                {
                    try { File.Delete(tempSetupPath); } catch { }
                }

                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "DersProgrami-Updater");

                    byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(tempSetupPath, fileBytes);
                }

                MessageBox.Show("Güncelleme paketi başarıyla indirildi. Kurulum başlatılıyor...", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);

                var processInfo = new ProcessStartInfo
                {
                    FileName = tempSetupPath,
                    Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS", // Kurulumu otomatik ve arka planda yapar
                    UseShellExecute = true
                };

                Process.Start(processInfo);

                // Mevcut uygulamayı kapat (Inno Setup dosyaları rahatça güncellesin)
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Güncelleme indirilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class VersionInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = "";

        [JsonPropertyName("release_notes")]
        public string ReleaseNotes { get; set; } = "";
    }
}