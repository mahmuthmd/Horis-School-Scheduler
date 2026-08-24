using DersProgramiUI.Engine;
using DersProgramiUI.Models;
using Supabase;
using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace DersProgramiUI
{
    public partial class LoginWindow : Window
    {
        private static readonly string SupabaseUrl = "https://atkaxgwiqemhjsdkendo.supabase.co";
        private static readonly string SupabaseKey = "sb_publishable_Hr-lprRTwxKl10SeVo0_6A_sYmeDbj1";

        // Güvenli Dosya Yolları (AppData içinde)
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Horis"
        );

        private static readonly string LisansDosyaYolu = Path.Combine(AppDataFolder, "license.dat");
        private static readonly string BeniHatirlaDosyaYolu = Path.Combine(AppDataFolder, "session.dat");

        private Supabase.Client _supabase;

        public LoginWindow()
        {
            InitializeComponent();
            _ = BaslatVeOtoGirisKontrol();
        }

        private async Task BaslatVeOtoGirisKontrol()
        {
            // 1. Güncelleme Kontrolü
            await UpdateManager.GuncellemeKontrolEtAsync();

            // 2. Beni Hatırla Verisi Var mı?
            var kayitliOturum = KayitliOturumuOku();
            if (kayitliOturum == null) return; // Oturum kaydı yok, kullanıcı normal giriş yapacak

            // Ekrana kullanıcı adını doldur (kullanıcı görsün)
            txtKullaniciAdi.Text = kayitliOturum.Email;
            txtSifre.Password = kayitliOturum.Password;
            chkBeniHatirla.IsChecked = true;

            // 30 Günlük "Beni Hatırla" süresi dolmuş mu?
            if (DateTime.UtcNow > kayitliOturum.RememberExpireDate)
            {
                OturumuTemizle();
                lblDurum.Text = "Oturum süreniz doldu, lütfen tekrar giriş yapın.";
                return;
            }

            // 3. Otomatik Giriş Başlat
            pnlYukleniyor.Visibility = Visibility.Visible;
            btnGiris.IsEnabled = false;
            lblDurum.Text = "Otomatik giriş yapılıyor...";

            bool internetVarMi = NetworkInterface.GetIsNetworkAvailable();

            if (!internetVarMi)
            {
                // İnternet YOK ➔ Çevrimdışı Lisansı Kontrol Et
                CevrimdisiGirisDene();
                pnlYukleniyor.Visibility = Visibility.Collapsed;
                btnGiris.IsEnabled = true;
                return;
            }

            // İnternet VAR ➔ Supabase Üzerinden Üyelik ve Süre Doğrulaması
            try
            {
                await SupabaseBaslatAsync();
                await GirisIsleminiYurutAsync(kayitliOturum.Email, kayitliOturum.Password, beniHatirlaMi: true, otoGirisMi: true);
            }
            catch
            {
                CevrimdisiGirisDene();
            }
            finally
            {
                pnlYukleniyor.Visibility = Visibility.Collapsed;
                btnGiris.IsEnabled = true;
            }
        }

        private async Task SupabaseBaslatAsync()
        {
            if (_supabase == null)
            {
                var options = new SupabaseOptions { AutoConnectRealtime = true };
                _supabase = new Supabase.Client(SupabaseUrl, SupabaseKey, options);
                await _supabase.InitializeAsync();
            }
        }

        private async void btnGiris_Click(object sender, RoutedEventArgs e)
        {
            string email = txtKullaniciAdi.Text.Trim();
            string password = txtSifre.Password.Trim();
            bool beniHatirla = chkBeniHatirla.IsChecked ?? false;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Lütfen e-posta ve şifrenizi girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            pnlYukleniyor.Visibility = Visibility.Visible;
            btnGiris.IsEnabled = false;
            lblDurum.Text = "";

            try
            {
                await SupabaseBaslatAsync();
                await GirisIsleminiYurutAsync(email, password, beniHatirla, otoGirisMi: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Giriş Başarısız! E-posta, şifre veya internet bağlantınızı kontrol edin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                pnlYukleniyor.Visibility = Visibility.Collapsed;
                btnGiris.IsEnabled = true;
            }
        }

        // 🎯 TEK VE MERKEZİ GİRİŞ & ÜYELİK KONTROLÜ
        private async Task GirisIsleminiYurutAsync(string email, string password, bool beniHatirlaMi, bool otoGirisMi)
        {
            var session = await _supabase.Auth.SignIn(email, password);

            if (session?.User != null)
            {
                var response = await _supabase
                    .From<UserExtraModel>()
                    .Where(x => x.UserId == session.User.Id)
                    .Single();

                if (response != null)
                {
                    // 1. KONTROL: Hesap Dondurulmuş mu?
                    if (!response.IsActive)
                    {
                        OturumuTemizle();
                        MessageBox.Show("Üyeliğiniz dondurulmuştur! Giriş yapılamaz.", "Erişim Engellendi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 2. KONTROL: Üyelik Süresi Dolmuş mu?
                    DateTime bugun = DateTime.UtcNow;
                    if (response.ExpireDate < bugun)
                    {
                        OturumuTemizle();
                        MessageBox.Show($"Üyelik süreniz dolmuştur! ({response.ExpireDate:dd.MM.yyyy})\nLütfen lisansınızı yenileyin.", "Süre Bitti", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 3. Başarılı ise Lisansı Yerel Hafızaya Güncelle
                    LisansBilgisiniKaydet(email, response.ExpireDate);

                    // 4. Beni Hatırla İşlemi
                    if (beniHatirlaMi)
                    {
                        KayitliOturumKaydet(email, password, DateTime.UtcNow.AddDays(30)); // 30 Gün geçerli
                    }
                    else
                    {
                        OturumuTemizle();
                    }

                    int kalanGun = (int)Math.Ceiling((response.ExpireDate - bugun).TotalDays);

                    if (!otoGirisMi)
                    {
                        MessageBox.Show($"🎉 Giriş Başarılı!\n\n📆 Üyelik Bitiş: {response.ExpireDate:dd.MM.yyyy}\n⏳ Kalan Süre: {kalanGun} Gün",
                                        "Hoş Geldiniz", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    // Ana Pencereyi Aç
                    AnaUygulamayiAc(email, response.ExpireDate, response.IsActive);
                }
            }
        }

        #region OTURUM & ŞİFRELEME METOTLARI (DPAPI)

        private void KayitliOturumKaydet(string email, string password, DateTime rememberExpire)
        {
            try
            {
                var data = new BeniHatirlaVerisi { Email = email, Password = password, RememberExpireDate = rememberExpire };
                string json = JsonSerializer.Serialize(data);
                byte[] encryptedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);

                if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                File.WriteAllBytes(BeniHatirlaDosyaYolu, encryptedBytes);
            }
            catch { }
        }

        private BeniHatirlaVerisi KayitliOturumuOku()
        {
            if (!File.Exists(BeniHatirlaDosyaYolu)) return null;

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(BeniHatirlaDosyaYolu);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<BeniHatirlaVerisi>(json);
            }
            catch
            {
                return null;
            }
        }

        private void OturumuTemizle()
        {
            try
            {
                if (File.Exists(BeniHatirlaDosyaYolu)) File.Delete(BeniHatirlaDosyaYolu);
                if (File.Exists(LisansDosyaYolu)) File.Delete(LisansDosyaYolu);
            }
            catch { }
        }

        private void LisansBilgisiniKaydet(string email, DateTime expireDate)
        {
            try
            {
                var lisansData = new LisansVerisi { Email = email, ExpireDate = expireDate, LastCheck = DateTime.UtcNow };
                string json = JsonSerializer.Serialize(lisansData);
                byte[] encryptedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);

                if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                File.WriteAllBytes(LisansDosyaYolu, encryptedBytes);
            }
            catch { }
        }

        private void CevrimdisiGirisDene()
        {
            if (!File.Exists(LisansDosyaYolu)) return;

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(LisansDosyaYolu);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plainBytes);
                var lisans = JsonSerializer.Deserialize<LisansVerisi>(json);

                if (lisans != null && lisans.ExpireDate > DateTime.UtcNow)
                {
                    int kalanGun = (int)Math.Ceiling((lisans.ExpireDate - DateTime.UtcNow).TotalDays);
                    AnaUygulamayiAc(lisans.Email, lisans.ExpireDate, true);
                }
                else
                {
                    OturumuTemizle();
                    MessageBox.Show("Üyelik süreniz dolmuştur! Lütfen internete bağlanıp lisansınızı yenileyin.", "Süre Doldu", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch { }
        }

        private void AnaUygulamayiAc(string email, DateTime expireDate, bool isActive)
        {
            // 🎯 _supabase nesnesini de MainWindow'a aktarıyoruz
            MainWindow main = new MainWindow(email, expireDate, isActive, _supabase);
            main.Show();
            this.Close();
        }

        public class BeniHatirlaVerisi
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public DateTime RememberExpireDate { get; set; } // 30 günlük oturum süresi
        }

        public class LisansVerisi
        {
            public string Email { get; set; } = "";
            public DateTime ExpireDate { get; set; }
            public DateTime LastCheck { get; set; }
        }
        #endregion
    }
}