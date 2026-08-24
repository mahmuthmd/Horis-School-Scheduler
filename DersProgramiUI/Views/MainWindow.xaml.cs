using DersProgramiUI.Engine;
using DersProgramiUI.Models;
using Supabase;
using Supabase.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DersProgramiUI
{
    public partial class MainWindow : Window
    {
        private Point _dragStartPoint;
        private static readonly string SupabaseUrl = "https://atkaxgwiqemhjsdkendo.supabase.co/rest/v1/"; //Burası kendi supabase bilgi alanınız...
        private static readonly string SupabaseKey = "sb_publishable_Hr-lprRTwxKl10SeVo0_6A_sYmeDbj1"; //Burası kendi supabase bilgi alanınız...

        private Supabase.Client _supabase;

        private List<Teacher> ogretmenler = new List<Teacher>();
        private List<Lesson> dersler = new List<Lesson>();
        private List<Classroom> siniflar = new List<Classroom>();

        private Teacher seciliOgretmenDuzenleme = null;
        private Lesson seciliDersDuzenleme = null;
        private Classroom seciliSinifDuzenleme = null;

        private List<SinifDersYukItem> gecerliSinifDersYukleri = new List<SinifDersYukItem>();
        private Dictionary<Classroom, CourseAssignment[,]> olusturulanProgramlar = null;

        private string kurumLogoBase64 = "";
        private System.Threading.CancellationTokenSource _cts;

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(string eposta, DateTime expireDate, bool isActive, Supabase.Client supabaseClient = null)
        {
            InitializeComponent();
            _supabase = supabaseClient;
            LisansBilgileriniGoster(eposta, expireDate, isActive);
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

        public void LisansBilgileriniGoster(string eposta, DateTime expireDate, bool isActive)
        {
            lblLisansEposta.Text = eposta;
            lblLisansBitisTarihi.Text = expireDate.ToString("dd MMMM yyyy HH:mm");

            TimeSpan kalanSure = expireDate - DateTime.Now;
            int kalanGun = (int)Math.Ceiling(kalanSure.TotalDays);

            if (isActive && kalanGun > 0)
            {
                lblLisansDurumu.Text = "AKTİF";
                brdLisansDurum.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
                lblLisansKalanGun.Text = $"{kalanGun} Gün Kaldı";
            }
            else
            {
                lblLisansDurumu.Text = "SÜRESİ DOLDU";
                brdLisansDurum.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                lblLisansKalanGun.Text = "0 Gün (Pasif)";
            }
        }

        #region ÖĞRETMEN İŞLEMLERİ (1. SEKME)
        private List<Teacher> katiOgretmenler = new List<Teacher>();

        private void TabloyuYenile()
        {
            string aramaMetni = txtOgretmenAra.Text.Trim().ToLower();

            foreach (var ogr in ogretmenler)
            {
                int toplamSaat = 0;
                foreach (var sinif in siniflar)
                {
                    foreach (var yuk in sinif.DersProgramiYukDetailed)
                    {
                        if (yuk.Value.ZorunluOgretmen != null &&
                            yuk.Value.ZorunluOgretmen.Ad.Equals(ogr.Ad, StringComparison.OrdinalIgnoreCase))
                        {
                            toplamSaat += yuk.Value.Saat;
                        }
                    }
                }
                ogr.ToplamDersSaati = toplamSaat;
            }

            var filtrelenmisList = ogretmenler.Where(o =>
                o.Ad.ToLower().Contains(aramaMetni) ||
                o.Brans.ToLower().Contains(aramaMetni)
            ).ToList();

            dgOgretmenler.ItemsSource = null;
            dgOgretmenler.ItemsSource = filtrelenmisList;

            OgretmenSecimListesiniYenile();
            KatiOgretmenListeleriniYenile();
        }

        private void KatiOgretmenListeleriniYenile()
        {
            if (cmbKatiOgretmenSecim != null)
            {
                cmbKatiOgretmenSecim.ItemsSource = null;
                cmbKatiOgretmenSecim.ItemsSource = ogretmenler.Where(o => !katiOgretmenler.Contains(o)).ToList();
            }

            if (lstKatiOgretmenler != null)
            {
                lstKatiOgretmenler.ItemsSource = null;
                lstKatiOgretmenler.ItemsSource = katiOgretmenler;
            }
        }

        private void btnOgretmenEkle_Click(object sender, RoutedEventArgs e)
        {
            string ad = txtOgretmenAd.Text.Trim();
            string brans = txtOgretmenBrans.Text.Trim();

            if (string.IsNullOrEmpty(ad) || string.IsNullOrEmpty(brans))
            {
                MessageBox.Show("Lütfen öğretmen adı ve branşını boş bırakmayın!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int hedefGun = cmbOgretmenHedefGun != null ? cmbOgretmenHedefGun.SelectedIndex : 0;

            Teacher yeniOgretmen = new Teacher(ad, brans) { HedefGunSayisi = hedefGun };
            ogretmenler.Add(yeniOgretmen);

            TabloyuYenile();

            txtOgretmenAd.Clear();
            txtOgretmenBrans.Clear();
            if (cmbOgretmenHedefGun != null) cmbOgretmenHedefGun.SelectedIndex = 0;
        }

        private void btnOgretmenSil_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            Teacher silinecekOgretmen = btn?.DataContext as Teacher;

            if (silinecekOgretmen != null)
            {
                var cevap = MessageBox.Show($"{silinecekOgretmen.Ad} isimli öğretmeni silmek istediğinize emin misiniz?",
                                            "Öğretmen Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (cevap == MessageBoxResult.Yes)
                {
                    ogretmenler.Remove(silinecekOgretmen);
                    katiOgretmenler.Remove(silinecekOgretmen);

                    foreach (var d in dersler)
                    {
                        if (d.VerenOgretmenler.Any(o => o.Ad.Equals(silinecekOgretmen.Ad, StringComparison.OrdinalIgnoreCase)))
                        {
                            d.VerenOgretmenler.RemoveAll(o => o.Ad.Equals(silinecekOgretmen.Ad, StringComparison.OrdinalIgnoreCase));
                        }
                    }

                    foreach (var s in siniflar)
                    {
                        s.SabitDersler.RemoveAll(sb => sb.Ogretmen != null && sb.Ogretmen.Ad.Equals(silinecekOgretmen.Ad, StringComparison.OrdinalIgnoreCase));
                    }

                    TabloyuYenile();
                    DersTablosunuYenile();
                    SinifTablosunuYenile();
                }
            }
        }

        private void btnOgretmenDuzenle_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            seciliOgretmenDuzenleme = btn?.DataContext as Teacher;

            if (seciliOgretmenDuzenleme != null)
            {
                txtOgretmenAd.Text = seciliOgretmenDuzenleme.Ad;
                txtOgretmenBrans.Text = seciliOgretmenDuzenleme.Brans;

                if (cmbOgretmenHedefGun != null)
                    cmbOgretmenHedefGun.SelectedIndex = seciliOgretmenDuzenleme.HedefGunSayisi;

                lblFormBaslik.Text = "Öğretmen Bilgilerini Düzenle";
                btnOgretmenEkle.Visibility = Visibility.Collapsed;
                pnlOgretmenDuzenleButonlari.Visibility = Visibility.Visible;
            }
        }

        private void btnOgretmenGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (seciliOgretmenDuzenleme != null)
            {
                string ad = txtOgretmenAd.Text.Trim();
                string brans = txtOgretmenBrans.Text.Trim();

                if (string.IsNullOrEmpty(ad) || string.IsNullOrEmpty(brans))
                {
                    MessageBox.Show("Lütfen alanları boş bırakmayın!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                seciliOgretmenDuzenleme.Ad = ad;
                seciliOgretmenDuzenleme.Brans = brans;

                if (cmbOgretmenHedefGun != null)
                    seciliOgretmenDuzenleme.HedefGunSayisi = cmbOgretmenHedefGun.SelectedIndex;

                TabloyuYenile();
                DuzenlemeModundanCik();
            }
        }

        private void btnDuzenleIptal_Click(object sender, RoutedEventArgs e)
        {
            DuzenlemeModundanCik();
        }

        private void txtOgretmenAra_TextChanged(object sender, TextChangedEventArgs e)
        {
            TabloyuYenile();
        }

        private void DuzenlemeModundanCik()
        {
            seciliOgretmenDuzenleme = null;
            txtOgretmenAd.Clear();
            txtOgretmenBrans.Clear();

            if (cmbOgretmenHedefGun != null) cmbOgretmenHedefGun.SelectedIndex = 0;

            lblFormBaslik.Text = "Yeni Öğretmen Ekle";
            btnOgretmenEkle.Visibility = Visibility.Visible;
            pnlOgretmenDuzenleButonlari.Visibility = Visibility.Collapsed;
        }

        private void DersDuzenlemeModundanCik()
        {
            seciliDersDuzenleme = null;
            DersFormunuTemizle();

            if (lblDersFormBaslik != null) lblDersFormBaslik.Text = "Yeni Ders Ekle";
            btnDersEkle.Visibility = Visibility.Visible;
            pnlDersDuzenleButonlari.Visibility = Visibility.Collapsed;
        }

        private void btnMusaitlik_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            Teacher ogretmen = btn?.DataContext as Teacher;

            if (ogretmen != null)
            {
                MusaitlikPenceresi pencere = new MusaitlikPenceresi(ogretmen);
                pencere.Owner = this;
                pencere.ShowDialog();
            }
        }

        private void btnKatiOgretmenEkle_Click(object sender, RoutedEventArgs e)
        {
            Teacher secilen = cmbKatiOgretmenSecim.SelectedItem as Teacher;
            if (secilen != null)
            {
                katiOgretmenler.Add(secilen);
                KatiOgretmenListeleriniYenile();
            }
        }

        private void btnKatiOgretmenSil_Click(object sender, RoutedEventArgs e)
        {
            Teacher secilen = lstKatiOgretmenler.SelectedItem as Teacher;
            if (secilen != null)
            {
                katiOgretmenler.Remove(secilen);
                KatiOgretmenListeleriniYenile();
            }
        }
        #endregion

        #region DERS İŞLEMLERİ (2. SEKME)
        private void OgretmenSecimListesiniYenile()
        {
            lstOgretmenSecim.ItemsSource = null;
            lstOgretmenSecim.ItemsSource = ogretmenler;
        }

        private void DersTablosunuYenile()
        {
            string aramaMetni = txtDersAra.Text.Trim().ToLower();

            var filtrelenmisList = dersler.Where(d =>
                d.Ad.ToLower().Contains(aramaMetni) ||
                d.KisaAd.ToLower().Contains(aramaMetni)
            ).ToList();

            dgDersler.ItemsSource = null;
            dgDersler.ItemsSource = filtrelenmisList;

            SinifDersComboBoxYenile();
        }

        private List<Teacher> SeciliOgretmenleriGetir()
        {
            return ogretmenler.Where(o => o.IsSelected).ToList();
        }

        private void btnDersEkle_Click(object sender, RoutedEventArgs e)
        {
            string ad = txtDersAd.Text.Trim();
            string kisaAd = txtDersKisaAd.Text.Trim();

            if (string.IsNullOrEmpty(ad))
            {
                MessageBox.Show("Lütfen ders adını girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<Teacher> secilenOgretmenler = SeciliOgretmenleriGetir();

            if (secilenOgretmenler.Count == 0)
            {
                MessageBox.Show("Lütfen bu dersi verebilecek en az 1 öğretmen seçin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Lesson yeniDers = new Lesson(ad, kisaAd);
            yeniDers.VerenOgretmenler = secilenOgretmenler;
            dersler.Add(yeniDers);

            DersTablosunuYenile();
            DersFormunuTemizle();
        }

        private void btnDersSil_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            Lesson silinecekDers = btn?.DataContext as Lesson;

            if (silinecekDers != null)
            {
                var cevap = MessageBox.Show($"{silinecekDers.Ad} dersini silmek istediğinize emin misiniz?",
                                            "Ders Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (cevap == MessageBoxResult.Yes)
                {
                    dersler.Remove(silinecekDers);
                    DersTablosunuYenile();

                    if (seciliDersDuzenleme == silinecekDers)
                    {
                        DersDuzenlemeModundanCik();
                    }
                }
            }
        }

        private void btnDersDuzenle_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            seciliDersDuzenleme = btn?.DataContext as Lesson;

            if (seciliDersDuzenleme != null)
            {
                txtDersAd.Text = seciliDersDuzenleme.Ad;
                txtDersKisaAd.Text = seciliDersDuzenleme.KisaAd;

                foreach (var ogr in ogretmenler)
                {
                    ogr.IsSelected = seciliDersDuzenleme.VerenOgretmenler.Any(v => v.Ad == ogr.Ad);
                }

                lstOgretmenSecim.Items.Refresh();

                if (lblDersFormBaslik != null) lblDersFormBaslik.Text = "Ders Bilgilerini Düzenle";
                btnDersEkle.Visibility = Visibility.Collapsed;
                pnlDersDuzenleButonlari.Visibility = Visibility.Visible;
            }
        }

        private void btnDersGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (seciliDersDuzenleme != null)
            {
                string ad = txtDersAd.Text.Trim();
                string kisaAd = txtDersKisaAd.Text.Trim();
                List<Teacher> secilenOgretmenler = SeciliOgretmenleriGetir();

                if (string.IsNullOrEmpty(ad) || secilenOgretmenler.Count == 0)
                {
                    MessageBox.Show("Lütfen ders adını girin ve en az 1 öğretmen seçin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                seciliDersDuzenleme.Ad = ad;
                seciliDersDuzenleme.KisaAd = string.IsNullOrWhiteSpace(kisaAd) ? (ad.Length > 3 ? ad.Substring(0, 3).ToUpper() : ad.ToUpper()) : kisaAd;
                seciliDersDuzenleme.VerenOgretmenler = secilenOgretmenler;

                DersTablosunuYenile();
                DersDuzenlemeModundanCik();
            }
        }

        private void btnDersDuzenleIptal_Click(object sender, RoutedEventArgs e)
        {
            DersDuzenlemeModundanCik();
        }

        private void DersFormunuTemizle()
        {
            txtDersAd.Clear();
            txtDersKisaAd.Clear();

            foreach (var ogr in ogretmenler)
            {
                ogr.IsSelected = false;
            }

            lstOgretmenSecim.Items.Refresh();
        }

        private void txtDersAra_TextChanged(object sender, TextChangedEventArgs e)
        {
            DersTablosunuYenile();
        }
        #endregion

        #region SINIF İŞLEMLERİ (3. SEKME)
        private void SinifDersComboBoxYenile()
        {
            cmbSinifDersSecim.ItemsSource = null;
            cmbSinifDersSecim.ItemsSource = dersler;
        }

        private void cmbSinifDersSecim_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Lesson secilenDers = cmbSinifDersSecim.SelectedItem as Lesson;
            if (secilenDers != null)
            {
                List<Teacher> secenekler = new List<Teacher>();
                secenekler.Add(new Teacher(" (Otomatik Seçim)", ""));
                secenekler.AddRange(secilenDers.VerenOgretmenler);

                cmbSinifOgretmenSecim.ItemsSource = null;
                cmbSinifOgretmenSecim.ItemsSource = secenekler;
                cmbSinifOgretmenSecim.SelectedIndex = 0;
            }
        }

        private void SinifTablosunuYenile()
        {
            string aramaMetni = txtSinifAra.Text.Trim().ToLower();

            var filtrelenmisList = siniflar.Where(s =>
                s.Ad.ToLower().Contains(aramaMetni)
            ).ToList();

            dgSiniflar.ItemsSource = null;
            dgSiniflar.ItemsSource = filtrelenmisList;

            ProgramSinifComboBoxYenile();
        }

        private void btnSinifDersYukEkle_Click(object sender, RoutedEventArgs e)
        {
            Lesson secilenDers = cmbSinifDersSecim.SelectedItem as Lesson;
            if (secilenDers == null)
            {
                MessageBox.Show("Lütfen bir ders seçin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtSinifDersSaat.Text.Trim(), out int saat) || saat <= 0)
            {
                MessageBox.Show("Lütfen geçerli bir saat sayısı girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Teacher zorunluOgretmen = cmbSinifOgretmenSecim.SelectedItem as Teacher;
            if (zorunluOgretmen != null && zorunluOgretmen.Ad.Contains("(Otomatik Seçim)"))
            {
                zorunluOgretmen = null;
            }

            var varOlan = gecerliSinifDersYukleri.FirstOrDefault(x => x.DersAdi == secilenDers.Ad);
            if (varOlan != null)
            {
                varOlan.SaatSayisi = saat;
                varOlan.ZorunluOgretmen = zorunluOgretmen;
            }
            else
            {
                gecerliSinifDersYukleri.Add(new SinifDersYukItem
                {
                    DersAdi = secilenDers.Ad,
                    SaatSayisi = saat,
                    ZorunluOgretmen = zorunluOgretmen
                });
            }

            SinifDersYukListesiniYenile();
        }

        private void btnSinifDersYukSil_Click(object sender, RoutedEventArgs e)
        {
            SinifDersYukItem secilen = lstSinifDersYukleri.SelectedItem as SinifDersYukItem;
            if (secilen != null)
            {
                gecerliSinifDersYukleri.Remove(secilen);
                SinifDersYukListesiniYenile();
            }
        }

        private void SinifDersYukListesiniYenile()
        {
            lstSinifDersYukleri.ItemsSource = null;
            lstSinifDersYukleri.ItemsSource = gecerliSinifDersYukleri;

            TabloyuYenile();
        }

        private void btnSinifEkle_Click(object sender, RoutedEventArgs e)
        {
            string ad = txtSinifAd.Text.Trim();

            if (string.IsNullOrEmpty(ad))
            {
                MessageBox.Show("Lütfen sınıf adını girin (Örn: 9-A)!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (gecerliSinifDersYukleri.Count == 0)
            {
                MessageBox.Show("Lütfen bu sınıfa en az 1 ders yükü ekleyin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int hedefGun = 0;
            if (txtSinifHedefGunSayisi != null)
            {
                int.TryParse(txtSinifHedefGunSayisi.Text.Trim(), out hedefGun);
            }

            Classroom yeniSinif = new Classroom(ad) { HedefGunSayisi = hedefGun };

            foreach (var item in gecerliSinifDersYukleri)
            {
                yeniSinif.DersEkle(item.DersAdi, item.SaatSayisi, item.ZorunluOgretmen);
            }

            siniflar.Add(yeniSinif);

            SinifTablosunuYenile();
            SinifFormunuTemizle();
        }

        private void btnSinifDuzenle_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            seciliSinifDuzenleme = btn?.DataContext as Classroom;

            if (seciliSinifDuzenleme != null)
            {
                txtSinifAd.Text = seciliSinifDuzenleme.Ad;

                if (txtSinifHedefGunSayisi != null)
                    txtSinifHedefGunSayisi.Text = seciliSinifDuzenleme.HedefGunSayisi.ToString();

                gecerliSinifDersYukleri.Clear();
                foreach (var kvp in seciliSinifDuzenleme.DersProgramiYukDetailed)
                {
                    gecerliSinifDersYukleri.Add(new SinifDersYukItem
                    {
                        DersAdi = kvp.Key,
                        SaatSayisi = kvp.Value.Saat,
                        ZorunluOgretmen = kvp.Value.ZorunluOgretmen
                    });
                }
                SinifDersYukListesiniYenile();

                lblSinifFormBaslik.Text = "Sınıf Bilgilerini Düzenle";
                btnSinifEkle.Visibility = Visibility.Collapsed;
                pnlSinifDuzenleButonlari.Visibility = Visibility.Visible;
            }
        }

        private void btnSinifGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (seciliSinifDuzenleme != null)
            {
                string ad = txtSinifAd.Text.Trim();

                if (string.IsNullOrEmpty(ad) || gecerliSinifDersYukleri.Count == 0)
                {
                    MessageBox.Show("Lütfen sınıf adını girin ve en az 1 ders yükü ekleyin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int hedefGun = 0;
                if (txtSinifHedefGunSayisi != null)
                {
                    int.TryParse(txtSinifHedefGunSayisi.Text.Trim(), out hedefGun);
                }

                seciliSinifDuzenleme.Ad = ad;
                seciliSinifDuzenleme.HedefGunSayisi = hedefGun;

                seciliSinifDuzenleme.DersProgramiYukDetailed.Clear();
                foreach (var item in gecerliSinifDersYukleri)
                {
                    seciliSinifDuzenleme.DersEkle(item.DersAdi, item.SaatSayisi, item.ZorunluOgretmen);
                }

                var mevcutDersAdlari = seciliSinifDuzenleme.DersProgramiYukDetailed.Keys.ToList();
                seciliSinifDuzenleme.SabitDersler.RemoveAll(sb => !mevcutDersAdlari.Contains(sb.DersAdi, StringComparer.OrdinalIgnoreCase));

                SinifTablosunuYenile();
                SinifDuzenlemeModundanCik();
            }
        }

        private void btnSinifDuzenleIptal_Click(object sender, RoutedEventArgs e)
        {
            SinifDuzenlemeModundanCik();
        }

        private void SinifDuzenlemeModundanCik()
        {
            seciliSinifDuzenleme = null;
            SinifFormunuTemizle();

            lblSinifFormBaslik.Text = "Yeni Sınıf Ekle";
            btnSinifEkle.Visibility = Visibility.Visible;
            pnlSinifDuzenleButonlari.Visibility = Visibility.Collapsed;
        }

        private void SinifFormunuTemizle()
        {
            txtSinifAd.Clear();
            if (txtSinifHedefGunSayisi != null) txtSinifHedefGunSayisi.Text = "0";
            gecerliSinifDersYukleri.Clear();
            SinifDersYukListesiniYenile();
        }

        private void btnSinifMusaitlik_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            Classroom sinif = btn?.DataContext as Classroom;

            if (sinif != null)
            {
                MusaitlikPenceresi pencere = new MusaitlikPenceresi(sinif);
                pencere.Owner = this;
                pencere.ShowDialog();
            }
        }

        private void btnSinifSil_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            Classroom silinecekSinif = btn?.DataContext as Classroom;

            if (silinecekSinif != null)
            {
                var cevap = MessageBox.Show($"{silinecekSinif.Ad} sınıfını silmek istediğinize emin misiniz?",
                                            "Sınıf Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (cevap == MessageBoxResult.Yes)
                {
                    siniflar.Remove(silinecekSinif);
                    SinifTablosunuYenile();
                }
            }
        }

        private void txtSinifAra_TextChanged(object sender, TextChangedEventArgs e)
        {
            SinifTablosunuYenile();
        }
        #endregion

        #region PROGRAM HAZIRLA & MOTOR (4. SEKME)
        private void ProgramSinifComboBoxYenile()
        {
            if (cmbGorunumTuru != null && cmbGorunumTuru.SelectedIndex == 1) return;

            if (cmbProgramSecim != null)
            {
                cmbProgramSecim.ItemsSource = null;
                cmbProgramSecim.ItemsSource = siniflar;
                cmbProgramSecim.DisplayMemberPath = "Ad";
                if (siniflar.Count > 0)
                    cmbProgramSecim.SelectedIndex = 0;
            }
        }

        private void cmbGorunumTuru_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGorunumTuru == null || cmbProgramSecim == null) return;

            cmbProgramSecim.ItemsSource = null;

            if (cmbGorunumTuru.SelectedIndex == 0)
            {
                if (lblSecimEtiket != null) lblSecimEtiket.Text = "Sınıf Seç:";
                cmbProgramSecim.ItemsSource = siniflar;
                cmbProgramSecim.DisplayMemberPath = "Ad";
            }
            else
            {
                if (lblSecimEtiket != null) lblSecimEtiket.Text = "Öğretmen Seç:";
                cmbProgramSecim.ItemsSource = ogretmenler;
                cmbProgramSecim.DisplayMemberPath = "Ad";
            }

            if (cmbProgramSecim.Items.Count > 0)
                cmbProgramSecim.SelectedIndex = 0;
        }

        private async void btnProgramOlustur_Click(object sender, RoutedEventArgs e)
        {
            if (siniflar.Count == 0 || ogretmenler.Count == 0 || dersler.Count == 0)
            {
                MessageBox.Show("Ders programı oluşturabilmek için lütfen önce Öğretmen, Ders ve Sınıf verilerini ekleyin!",
                                "Eksik Veri", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int maxBlok = 2;
            int minBlok = 2;

            int.TryParse(txtMaxBlok.Text, out maxBlok);
            if (txtMinBlok != null) int.TryParse(txtMinBlok.Text, out minBlok);

            pnlYukleniyor.Visibility = Visibility.Visible;
            btnProgramOlustur.IsEnabled = false;

            _cts = new System.Threading.CancellationTokenSource();

            bool basarili = false;
            CozucuMotor motor = null;

            await System.Threading.Tasks.Task.Run(() =>
            {
                motor = new CozucuMotor(siniflar, ogretmenler, dersler);
                motor.MaxUstUsteDers = maxBlok;
                motor.TercihEdilenMinBlok = minBlok;
                motor.TimeoutSaniye = 20;
                motor.KatiOgretmenler = katiOgretmenler;
                motor.CancellationToken = _cts.Token;

                basarili = motor.Coz();
            });

            pnlYukleniyor.Visibility = Visibility.Collapsed;
            btnProgramOlustur.IsEnabled = true;

            if (basarili)
            {
                olusturulanProgramlar = motor.Programlar;
                string siralamaOzeti = string.Join("\n", motor.SiralamaPuanRaporu.Take(15));
                MessageBox.Show($"🎉 Ders programı başarıyla oluşturuldu!\n\n📊 **AKILLI SIRALAMA VE PUAN RAPORU (ÖNİZLEME):**\n{siralamaOzeti}\n\n...",
                                "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

                ProgramTablosunuCiz();
            }
            else
            {
                olusturulanProgramlar = motor.Programlar;
                ProgramTablosunuCiz();

                string raporMetni = (motor.HataRaporu != null && motor.HataRaporu.Count > 0)
                    ? string.Join("\n\n", motor.HataRaporu)
                    : "Belirli bir kısıt çakışmasından dolayı programın tamamı otomatik yerleştirilemedi.";

                if (_cts.IsCancellationRequested)
                {
                    lblProgramBaslik.Text = "⏹️ Arama durduruldu. Ulaşılan en iyi yerleşim gösteriliyor.";
                    MessageBox.Show($"Arama işlemi isteğiniz üzerine durduruldu.\n\n📋 **DURUM RAPORU:**\n\n{raporMetni}\n\n💡 Eksik kalan dersleri tablodan manuel olarak düzenleyebilirsiniz.",
                                    "İşlem İptal Edildi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    lblProgramBaslik.Text = "❌ Program tam oluşturulamadı! Ulaşılan en iyi yerleşim gösteriliyor.";

                    MessageBox.Show($"Çözücü motor programı tam olarak bitiremedi ancak ulaşılan en iyi tablo ekrana getirildi.\n\n📋 **DARBOĞAZ VE ÇAKIŞMA RAPORU:**\n\n{raporMetni}\n\n💡 Bu dersler yerleştirilemedi, onları manuel olarak düzenlemek isteyebilirsiniz.",
                                    "Eksik Yerleşim Analizi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void cmbProgramSecim_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ProgramTablosunuCiz();
        }

        private void ProgramTablosunuCiz()
        {
            gridProgramMatris.Children.Clear();
            bool sinifModu = cmbGorunumTuru == null || cmbGorunumTuru.SelectedIndex == 0;

            gridProgramMatris.Children.Add(new Border { Background = Brushes.DarkSlateGray, Child = new TextBlock { Text = "Saat / Gün", Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });

            string[] gunler = { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };
            foreach (var gun in gunler)
            {
                gridProgramMatris.Children.Add(new Border { Background = Brushes.SlateGray, Child = new TextBlock { Text = gun, Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            }

            if (sinifModu)
            {
                Classroom seciliSinif = cmbProgramSecim.SelectedItem as Classroom;
                if (seciliSinif == null) return;

                lblProgramBaslik.Text = $"{seciliSinif.Ad} Sınıfı (Müdahale etmek için boş hücrelere SAĞ TIKLAYIN)";

                bool programVar = olusturulanProgramlar != null && olusturulanProgramlar.ContainsKey(seciliSinif);
                CourseAssignment[,] matris = programVar ? olusturulanProgramlar[seciliSinif] : new CourseAssignment[7, 10];

                for (int s = 0; s < 10; s++)
                {
                    gridProgramMatris.Children.Add(new Border { Background = Brushes.LightGray, Child = new TextBlock { Text = $"{s + 1}. Saat", FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });

                    for (int g = 0; g < 7; g++)
                    {
                        CourseAssignment atama = matris[g, s];

                        if (!programVar)
                        {
                            var sabit = seciliSinif.SabitDersler.FirstOrDefault(x => (int)x.Gun == g && x.SaatIndex == s);
                            if (sabit != null)
                            {
                                var dersNesnesi = dersler.FirstOrDefault(d => d.Ad == sabit.DersAdi);
                                if (dersNesnesi != null) atama = new CourseAssignment(dersNesnesi, sabit.Ogretmen, seciliSinif);
                            }
                        }

                        string dersMetni = atama != null ? (!string.IsNullOrEmpty(atama.Ders.KisaAd) ? atama.Ders.KisaAd : atama.Ders.Ad) : "- BOŞ -";
                        string altMetin = atama != null ? atama.Ogretmen.Ad : "";
                        HucreEkle(dersMetni, altMetin, atama != null, seciliSinif, g, s, atama, null);
                    }
                }
            }
            else
            {
                Teacher seciliOgr = cmbProgramSecim.SelectedItem as Teacher;
                if (seciliOgr == null) return;

                lblProgramBaslik.Text = $"{seciliOgr.Ad} Öğretmenin Ders Programı";

                for (int s = 0; s < 10; s++)
                {
                    gridProgramMatris.Children.Add(new Border { Background = Brushes.LightGray, Child = new TextBlock { Text = $"{s + 1}. Saat", FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });

                    for (int g = 0; g < 7; g++)
                    {
                        CourseAssignment bulunanAtama = null;
                        if (olusturulanProgramlar != null)
                        {
                            foreach (var kvp in olusturulanProgramlar)
                            {
                                var atama = kvp.Value[g, s];
                                if (atama != null && atama.Ogretmen.Ad == seciliOgr.Ad)
                                {
                                    bulunanAtama = atama;
                                    break;
                                }
                            }
                        }

                        string dersMetni = bulunanAtama != null ? (!string.IsNullOrEmpty(bulunanAtama.Ders.KisaAd) ? bulunanAtama.Ders.KisaAd : bulunanAtama.Ders.Ad) : "- BOŞ -";
                        string altMetin = bulunanAtama != null ? bulunanAtama.Sinif.Ad : "";
                        HucreEkle(dersMetni, altMetin, bulunanAtama != null, null, g, s, bulunanAtama, seciliOgr);
                    }
                }
            }
        }

        private void HucreEkle(string baslik, string altBaslik, bool doluMu, Classroom sinifContext, int gun, int saat, CourseAssignment atama, Teacher ogrContext = null)
        {
            bool isSinifModu = sinifContext != null;
            bool isOgrModu = ogrContext != null;

            // Öğretmen modunda sadece dolu hücreler (ders olanlar) sürüklenebilir
            bool suruklenebilir = isSinifModu || (isOgrModu && atama != null);
            bool birakilabilir = isSinifModu || isOgrModu;

            Border hucre = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0.5),
                Margin = new Thickness(1),
                Background = doluMu ? Brushes.AliceBlue : Brushes.WhiteSmoke,
                Cursor = suruklenebilir ? Cursors.Hand : Cursors.Arrow,
                AllowDrop = birakilabilir
            };

            // 🎯 ÖNEMLİ: Öğretmen ekranındayken sınıfı atamadan çekiyoruz ki arka planda sınıfın programına etki edebilelim.
            Classroom ilgiliSinif = sinifContext ?? atama?.Sinif;

            hucre.Tag = new HucreVerisi
            {
                Sinif = ilgiliSinif,
                Gun = gun,
                Saat = saat,
                Atama = atama,
                ViewOgretmeni = ogrContext
            };

            if (suruklenebilir)
            {
                hucre.PreviewMouseLeftButtonDown += (s, e) => { _dragStartPoint = e.GetPosition(null); };
                hucre.PreviewMouseMove += (s, e) =>
                {
                    if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                    {
                        Point currentPos = e.GetPosition(null);
                        Vector diff = _dragStartPoint - currentPos;

                        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                        {
                            var data = hucre.Tag as HucreVerisi;
                            if (data != null && data.Atama != null) DragDrop.DoDragDrop(hucre, data, DragDropEffects.Move);
                        }
                    }
                };
            }

            if (birakilabilir)
            {
                hucre.DragEnter += (s, e) => { if (e.Data.GetDataPresent(typeof(HucreVerisi))) hucre.Background = Brushes.LightGreen; };

                hucre.DragLeave += (s, e) =>
                {
                    var data = hucre.Tag as HucreVerisi;
                    bool isSabit = ilgiliSinif != null && ilgiliSinif.SabitDersler.Any(sb => (int)sb.Gun == gun && sb.SaatIndex == saat);
                    hucre.Background = isSabit ? Brushes.PeachPuff : ((data != null && data.Atama != null) ? Brushes.AliceBlue : Brushes.WhiteSmoke);
                };

                hucre.Drop += (s, e) =>
                {
                    if (e.Data.GetDataPresent(typeof(HucreVerisi)))
                    {
                        var kaynak = e.Data.GetData(typeof(HucreVerisi)) as HucreVerisi;
                        var hedef = hucre.Tag as HucreVerisi;

                        if (kaynak != null && hedef != null && (kaynak.Gun != hedef.Gun || kaynak.Saat != hedef.Saat))
                        {
                            // 🎯 ÖĞRETMEN EKRANI MANTIĞI: Boş hücreye bırakılırsa hedef sınıf belirsizdir, kaynağın sınıfı geçerlidir.
                            if (hedef.Sinif == null && kaynak.Atama != null)
                            {
                                hedef.Sinif = kaynak.Atama.Sinif;
                            }

                            if (kaynak.Sinif != null && hedef.Sinif != null)
                            {
                                DersiTasiVeKontrolEt(kaynak, hedef);
                            }
                        }
                    }
                };
            }

            // --- KİLİT (SABİT DERS) MENÜSÜ ---
            bool sabitDersMi = ilgiliSinif != null && ilgiliSinif.SabitDersler.Any(sb => (int)sb.Gun == gun && sb.SaatIndex == saat && (atama == null || sb.DersAdi == atama.Ders.Ad));

            if (sabitDersMi)
            {
                hucre.Background = Brushes.PeachPuff;
                baslik = "🔒 " + baslik;
            }

            ContextMenu ctx = new ContextMenu();
            if (sabitDersMi)
            {
                MenuItem itemSil = new MenuItem { Header = "🔓 Kilidi Kaldır (Sabit Dersi Sil)", Foreground = Brushes.Red, FontWeight = FontWeights.Bold };
                itemSil.Click += (s, e) => {
                    var silinecek = ilgiliSinif.SabitDersler.FirstOrDefault(sb => (int)sb.Gun == gun && sb.SaatIndex == saat);
                    if (silinecek != null) ilgiliSinif.SabitDersler.Remove(silinecek);
                    ProgramTablosunuCiz();
                };
                ctx.Items.Add(itemSil);
            }
            else
            {
                if (isSinifModu)
                {
                    // Sınıf modunda boş veya dolu hücreye diyalog ile ders seçerek kilit konabilir
                    MenuItem itemEkle = new MenuItem { Header = "🔒 Buraya Sabit Ders Kilitle", FontWeight = FontWeights.Bold };
                    itemEkle.Click += (s, e) => SabitDersEkleDialogAc(ilgiliSinif, gun, saat);
                    ctx.Items.Add(itemEkle);
                }
                else if (isOgrModu && atama != null)
                {
                    // Öğretmen modunda sadece "zaten orada olan sınıfın" dersini tıklayarak anında kilitleriz (Diyaloğa gerek kalmaz)
                    MenuItem itemEkle = new MenuItem { Header = $"🔒 Bu Dersi ({ilgiliSinif.Ad}) Kilitle", FontWeight = FontWeights.Bold };
                    itemEkle.Click += (s, e) => {
                        ilgiliSinif.SabitDersler.Add(new SabitDers
                        {
                            Gun = (Day)gun,
                            SaatIndex = saat,
                            DersAdi = atama.Ders.Ad,
                            Ogretmen = atama.Ogretmen
                        });
                        ProgramTablosunuCiz();
                    };
                    ctx.Items.Add(itemEkle);
                }
            }

            if (ctx.Items.Count > 0)
            {
                hucre.ContextMenu = ctx;
            }

            StackPanel icerik = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            icerik.Children.Add(new TextBlock { Text = baslik, FontWeight = FontWeights.Bold, Foreground = doluMu ? Brushes.Navy : Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center });

            if (!string.IsNullOrEmpty(altBaslik))
            {
                icerik.Children.Add(new TextBlock { Text = altBaslik, FontSize = 11, Foreground = Brushes.DarkSlateGray, HorizontalAlignment = HorizontalAlignment.Center });
            }

            hucre.Child = icerik;
            gridProgramMatris.Children.Add(hucre);
        }

        private List<string> SaatDilimleriniHesapla()
        {
            List<string> saatler = new List<string>();

            if (txtSerbestSaatler != null && !string.IsNullOrWhiteSpace(txtSerbestSaatler.Text))
            {
                var parcalar = txtSerbestSaatler.Text.Split(',');
                foreach (var p in parcalar)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                        saatler.Add(p.Trim());
                }
            }

            while (saatler.Count < 10)
            {
                int saatNo = saatler.Count + 1;
                saatler.Add($"{saatNo:00}:00");
            }

            return saatler;
        }

        private void btnPdfOnizleme_Click(object sender, RoutedEventArgs e)
        {
            if (olusturulanProgramlar == null || olusturulanProgramlar.Count == 0)
            {
                MessageBox.Show("Lütfen önce ders programını oluşturun!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int secim = cmbPdfTur.SelectedIndex;
                string htmlIcerik = ResmiHtmlRaporOlustur(secim);

                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Ders_Programi_Onizleme_{DateTime.Now:HHmmss}.html");
                System.IO.File.WriteAllText(tempPath, htmlIcerik, System.Text.Encoding.UTF8);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Önizleme oluşturulurken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ResmiHtmlRaporOlustur(int raporTuru)
        {
            var saatler = SaatDilimleriniHesapla();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            string kurumAd = (txtKurumAdi != null && !string.IsNullOrWhiteSpace(txtKurumAdi.Text))
                ? txtKurumAdi.Text.Trim().ToUpper()
                : "T.C. MİLLİ EĞİTİM BAKANLIĞI";

            string mudurAd = (txtMudurAd != null && !string.IsNullOrWhiteSpace(txtMudurAd.Text))
                ? txtMudurAd.Text.Trim().ToUpper()
                : "OKUL MÜDÜRÜ";

            string ilIlce = (txtIl != null && txtIlce != null && (!string.IsNullOrWhiteSpace(txtIl.Text) || !string.IsNullOrWhiteSpace(txtIlce.Text)))
                ? $"{txtIlce.Text.Trim().ToUpper()} / {txtIl.Text.Trim().ToUpper()}"
                : "";

            string konuMetni = (txtRaporKonu != null && !string.IsNullOrWhiteSpace(txtRaporKonu.Text))
                ? txtRaporKonu.Text.Trim()
                : "Haftalık Ders Programı";

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'><title>Haftalık Ders Programı Raporu</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("@page { size: A4 portrait; margin: 10mm 15mm; }");
            sb.AppendLine("@page landscape-page { size: A4 landscape; margin: 5mm; }");
            sb.AppendLine("body { font-family: 'Times New Roman', Times, serif; margin: 0 auto; padding: 0; background: #fff; color: #000; box-sizing: border-box; }");
            sb.AppendLine(".page { page-break-after: always; width: 100%; box-sizing: border-box; padding: 0 5mm; }");
            sb.AppendLine(".page-landscape { page-break-after: always; page: landscape-page; width: 100%; box-sizing: border-box; padding: 0; }");
            sb.AppendLine(".header-table { width: 100%; margin-bottom: 5px; border-collapse: collapse; }");
            sb.AppendLine(".header-table td { border: none; padding: 2px 0; font-size: 10pt; }");
            sb.AppendLine(".kurum-baslik { text-align: center; font-weight: bold; font-size: 14pt; margin: 2px 0; letter-spacing: 0.5px; }");
            sb.AppendLine(".konu-baslik { text-align: center; font-weight: bold; font-size: 11pt; margin: 4px 0 8px 0; }");
            sb.AppendLine(".mudur-imza { text-align: right; font-size: 10pt; margin-bottom: 8px; font-weight: bold; }");

            sb.AppendLine("table.program { width: 100%; border-collapse: collapse; margin: 10px auto; table-layout: fixed; box-sizing: border-box; }");
            sb.AppendLine("table.program th, table.program td { border: 1px solid #000; padding: 2px 1px; text-align: center; font-size: 8pt; height: 40px; vertical-align: middle; word-wrap: break-word; }");
            sb.AppendLine("table.program th { background-color: #fff; color: #000; font-weight: bold; height: 26px; }");
            sb.AppendLine(".saat-baslik { font-size: 6.5pt; font-weight: normal; border-top: 1px solid #000; display: block; margin-top: 2px; padding-top: 1px; }");

            sb.AppendLine("table.carsaf { width: 100%; border-collapse: collapse; margin-top: 5px; font-size: 6.5pt; table-layout: fixed; box-sizing: border-box; }");
            sb.AppendLine("table.carsaf th, table.carsaf td { border: 1px solid #000; padding: 1px; text-align: center; vertical-align: middle; height: 20px; word-wrap: break-word; overflow: hidden; }");
            sb.AppendLine("table.carsaf th { background-color: #f2f2f2; font-weight: bold; font-size: 7pt; }");
            sb.AppendLine(".ogr-isim { text-align: left !important; font-weight: bold; padding-left: 3px !important; font-size: 7.5pt; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }");

            sb.AppendLine("table.alt-liste { width: 85%; margin: 15px auto 0 auto; border-collapse: collapse; font-size: 9pt; }");
            sb.AppendLine("table.alt-liste td { border: none; border-bottom: 1px solid #ccc; padding: 3px 5px; text-align: left; }");
            sb.AppendLine("table.alt-liste th { border-bottom: 1.5px solid #000; padding: 3px 5px; text-align: left; font-weight: bold; }");
            sb.AppendLine("@media print { .page-landscape { page-break-after: always; } }");
            sb.AppendLine("</style></head><body>");

            string[] gunler = { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };

            if (raporTuru == 0 || raporTuru == 3)
            {
                foreach (var sinif in siniflar)
                {
                    if (!olusturulanProgramlar.ContainsKey(sinif)) continue;

                    sb.AppendLine("<div class='page'>");
                    sb.AppendLine("<table class='header-table'><tr>");
                    if (!string.IsNullOrEmpty(kurumLogoBase64))
                    {
                        sb.AppendLine($"<td style='width:65px; text-align:left; vertical-align:middle;'><img src='data:image/png;base64,{kurumLogoBase64}' style='max-height:55px; max-width:60px;'/></td>");
                    }
                    sb.AppendLine("<td>");
                    sb.AppendLine($"<div class='kurum-baslik'>{kurumAd}</div>");
                    if (!string.IsNullOrEmpty(ilIlce)) sb.AppendLine($"<div style='text-align:center; font-size:10pt; font-weight:bold;'>{ilIlce}</div>");
                    sb.AppendLine($"<div class='konu-baslik'>Sınıfın Adı : {sinif.Ad.ToUpper()}</div>");
                    sb.AppendLine("</td></tr></table>");

                    sb.AppendLine("<table class='header-table'>");
                    sb.AppendLine($"<tr><td style='width:60%;'><b>Konu :</b> {konuMetni}</td><td style='text-align:right;'><b>Tarih :</b> {DateTime.Now:dd.MM.yyyy}</td></tr>");
                    sb.AppendLine("</table>");

                    sb.AppendLine($"<div class='mudur-imza'>{mudurAd}<br><span style='font-weight:normal; font-size:10pt;'>Okul Müdürü</span></div>");

                    sb.AppendLine("<table class='program'><tr><th style='width:65px;'></th>");
                    // Sınıf raporunda saati 10'dan 6'ya düşürdük
                    for (int i = 0; i < 6; i++)
                    {
                        string saatMetin = i < saatler.Count ? saatler[i] : "";
                        sb.AppendLine($"<th>{i + 1}<span class='saat-baslik'>{saatMetin}</span></th>");
                    }
                    sb.AppendLine("</tr>");

                    var matris = olusturulanProgramlar[sinif];
                    for (int g = 0; g < 7; g++)
                    {
                        sb.AppendLine($"<tr><td><b>{gunler[g]}</b></td>");
                        // Sınıf raporunda matrisi çekerken s < 6 yaptık
                        for (int s = 0; s < 6; s++)
                        {
                            var atama = matris[g, s];
                            if (atama != null)
                            {
                                string ders = !string.IsNullOrEmpty(atama.Ders.KisaAd) ? atama.Ders.KisaAd : atama.Ders.Ad;
                                sb.AppendLine($"<td><b>{ders.ToUpper()}</b><br><span style='font-size:8pt;'>{atama.Ogretmen.Ad.ToUpper()}</span></td>");
                            }
                            else
                            {
                                sb.AppendLine("<td></td>");
                            }
                        }
                        sb.AppendLine("</tr>");
                    }
                    sb.AppendLine("</table>");

                    sb.AppendLine("<table class='alt-liste'>");
                    sb.AppendLine("<tr><th>Dersin Adı</th><th style='width:50px;'>HDS</th><th>Öğretmen</th></tr>");
                    foreach (var yuk in sinif.DersProgramiYukDetailed)
                    {
                        string dersAdi = yuk.Key;
                        int hds = yuk.Value.Saat;
                        string ogrAd = yuk.Value.ZorunluOgretmen != null ? yuk.Value.ZorunluOgretmen.Ad : "";

                        if (string.IsNullOrEmpty(ogrAd))
                        {
                            for (int g = 0; g < 7; g++)
                            {
                                for (int s = 0; s < 10; s++) // Burası motorla eşleşmek için 10 kalabilir, asıl aramayı tüm haftada yapıyor.
                                {
                                    var atama = matris[g, s];
                                    if (atama != null && atama.Ders.Ad == dersAdi)
                                    {
                                        ogrAd = atama.Ogretmen.Ad;
                                        break;
                                    }
                                }
                                if (!string.IsNullOrEmpty(ogrAd)) break;
                            }
                        }

                        sb.AppendLine($"<tr><td>{dersAdi.ToUpper()}</td><td>{hds}</td><td>{ogrAd.ToUpper()}</td></tr>");
                    }
                    sb.AppendLine("</table></div>");
                }
            }

            if (raporTuru == 1 || raporTuru == 3)
            {
                foreach (var ogr in ogretmenler)
                {
                    sb.AppendLine("<div class='page'>");
                    sb.AppendLine("<table class='header-table'><tr>");
                    if (!string.IsNullOrEmpty(kurumLogoBase64))
                    {
                        sb.AppendLine($"<td style='width:65px; text-align:left; vertical-align:middle;'><img src='data:image/png;base64,{kurumLogoBase64}' style='max-height:55px; max-width:60px;'/></td>");
                    }
                    sb.AppendLine("<td>");
                    sb.AppendLine($"<div class='kurum-baslik'>{kurumAd}</div>");
                    if (!string.IsNullOrEmpty(ilIlce)) sb.AppendLine($"<div style='text-align:center; font-size:10pt; font-weight:bold;'>{ilIlce}</div>");
                    sb.AppendLine($"<div class='konu-baslik'>Öğretmenin Adı : {ogr.Ad.ToUpper()} ({ogr.Brans.ToUpper()})</div>");
                    sb.AppendLine("</td></tr></table>");

                    sb.AppendLine("<table class='header-table'>");
                    sb.AppendLine($"<tr><td style='width:60%;'><b>Konu :</b> {konuMetni}</td><td style='text-align:right;'><b>Tarih :</b> {DateTime.Now:dd.MM.yyyy}</td></tr>");
                    sb.AppendLine("</table>");

                    sb.AppendLine($"<div class='mudur-imza'>{mudurAd}<br><span style='font-weight:normal; font-size:10pt;'>Okul Müdürü</span></div>");

                    sb.AppendLine("<table class='program'><tr><th style='width:65px;'></th>");
                    // Öğretmen raporunda saati 10'dan 6'ya düşürdük
                    for (int i = 0; i < 6; i++)
                    {
                        string saatMetin = i < saatler.Count ? saatler[i] : "";
                        sb.AppendLine($"<th>{i + 1}<span class='saat-baslik'>{saatMetin}</span></th>");
                    }
                    sb.AppendLine("</tr>");

                    for (int g = 0; g < 7; g++)
                    {
                        sb.AppendLine($"<tr><td><b>{gunler[g]}</b></td>");
                        // Öğretmen raporunda matrisi çekerken s < 6 yaptık
                        for (int s = 0; s < 6; s++)
                        {
                            CourseAssignment bulunan = null;
                            foreach (var kvp in olusturulanProgramlar)
                            {
                                var atama = kvp.Value[g, s];
                                if (atama != null && atama.Ogretmen.Ad == ogr.Ad)
                                {
                                    bulunan = atama;
                                    break;
                                }
                            }

                            if (bulunan != null)
                            {
                                string ders = !string.IsNullOrEmpty(bulunan.Ders.KisaAd) ? bulunan.Ders.KisaAd : bulunan.Ders.Ad;
                                sb.AppendLine($"<td><b>{ders.ToUpper()}</b><br><span style='font-size:8pt;'>{bulunan.Sinif.Ad.ToUpper()}</span></td>");
                            }
                            else
                            {
                                sb.AppendLine("<td></td>");
                            }
                        }
                        sb.AppendLine("</tr>");
                    }
                    sb.AppendLine("</table></div>");
                }
            }

            if (raporTuru == 2 || raporTuru == 3)
            {
                sb.AppendLine("<div class='page-landscape'>");
                sb.AppendLine("<table class='header-table'><tr>");
                if (!string.IsNullOrEmpty(kurumLogoBase64))
                {
                    sb.AppendLine($"<td style='width:65px; text-align:left; vertical-align:middle;'><img src='data:image/png;base64,{kurumLogoBase64}' style='max-height:45px;'/></td>");
                }
                sb.AppendLine("<td>");
                sb.AppendLine($"<div class='kurum-baslik'>{kurumAd}</div>");
                sb.AppendLine("<div class='konu-baslik'>- ÖĞRETMENLERİN HAFTALIK DERS PROGRAMI (ÇARŞAF LİSTE) -</div>");
                sb.AppendLine("</td></tr></table>");

                sb.AppendLine("<table class='carsaf'>");
                sb.AppendLine("<tr><th style='width:120px;' rowspan='2'>ÖĞRETMEN ADI</th><th style='width:30px;' rowspan='2'>HDS</th>");

                for (int g = 0; g < 5; g++)
                {
                    // Çarşaf liste başlığında gün başına hücreyi 10'dan 6'ya çektik
                    sb.AppendLine($"<th colspan='6'>{gunler[g]}</th>");
                }
                sb.AppendLine("</tr>");

                sb.AppendLine("<tr>");
                for (int g = 0; g < 5; g++)
                {
                    // Çarşaf liste gün altı saat indeksleri 10'dan 6'ya çektik
                    for (int s = 1; s <= 6; s++)
                    {
                        sb.AppendLine($"<th>{s}</th>");
                    }
                }
                sb.AppendLine("</tr>");

                int ogrSira = 1;
                foreach (var ogr in ogretmenler)
                {
                    int toplamHds = 0;
                    foreach (var kvp in olusturulanProgramlar)
                    {
                        for (int g = 0; g < 7; g++)
                        {
                            // HDS Toplamını genel matristen alıyoruz, burayı değiştirmedim
                            for (int s = 0; s < 10; s++)
                            {
                                var atama = kvp.Value[g, s];
                                if (atama != null && atama.Ogretmen.Ad == ogr.Ad) toplamHds++;
                            }
                        }
                    }

                    sb.AppendLine($"<tr><td class='ogr-isim'>{ogrSira++}- {ogr.Ad.ToUpper()}</td><td><b>{toplamHds}</b></td>");

                    for (int g = 0; g < 5; g++)
                    {
                        // Çarşaf listede ders atamalarını çekerken s < 6 yaptık
                        for (int s = 0; s < 6; s++)
                        {
                            CourseAssignment bulunan = null;
                            foreach (var kvp in olusturulanProgramlar)
                            {
                                var atama = kvp.Value[g, s];
                                if (atama != null && atama.Ogretmen.Ad == ogr.Ad)
                                {
                                    bulunan = atama;
                                    break;
                                }
                            }

                            if (bulunan != null)
                            {
                                string dersKisa = !string.IsNullOrEmpty(bulunan.Ders.KisaAd) ? bulunan.Ders.KisaAd : bulunan.Ders.Ad;
                                sb.AppendLine($"<td><b>{bulunan.Sinif.Ad.ToUpper()}</b><br><span style='font-size:6pt;'>{dersKisa.ToUpper()}</span></td>");
                            }
                            else
                            {
                                sb.AppendLine("<td></td>");
                            }
                        }
                    }
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</table></div>");
            }
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
        #endregion

        #region KURUM AYARLARI VE LOGO (5. SEKME)
        private void btnLogoSec_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Resim Dosyaları (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Kurum Logosunu Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    byte[] imageBytes = System.IO.File.ReadAllBytes(dialog.FileName);
                    kurumLogoBase64 = Convert.ToBase64String(imageBytes);

                    var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(dialog.FileName));
                    imgLogoOnizleme.Source = bitmap;
                    lblLogoDurum.Text = "Logo başarıyla yüklendi!";
                    lblLogoDurum.Foreground = System.Windows.Media.Brushes.Green;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Logo yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnLogoTemizle_Click(object sender, RoutedEventArgs e)
        {
            kurumLogoBase64 = "";
            imgLogoOnizleme.Source = null;
            lblLogoDurum.Text = "Logo kaldırıldı.";
            lblLogoDurum.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private void btnCikisYap_Click(object sender, RoutedEventArgs e)
        {
            var cevap = MessageBox.Show(
                "Oturumunuz kapatılacak ve giriş ekranına yönlendirileceksiniz.\nEmin misiniz?",
                "Çıkış Yap",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (cevap == MessageBoxResult.Yes)
            {
                try
                {
                    string appDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Horis"
                    );

                    string sessionPath = Path.Combine(appDataFolder, "session.dat");
                    string licensePath = Path.Combine(appDataFolder, "license.dat");

                    if (File.Exists(sessionPath)) File.Delete(sessionPath);
                    if (File.Exists(licensePath)) File.Delete(licensePath);

                    LoginWindow loginWindow = new LoginWindow();
                    loginWindow.Show();

                    Window anaPencere = Window.GetWindow(this);
                    anaPencere?.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Çıkış yapılırken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnOnSartlar_Click(object sender, RoutedEventArgs e)
        {
            if (siniflar.Count == 0 || dersler.Count == 0)
            {
                MessageBox.Show("Ön şart tanımlamadan önce lütfen Sınıf ve Ders verilerini ekleyin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OnSartPenceresi pencere = new OnSartPenceresi(siniflar, dersler);
            pencere.Owner = this;
            pencere.ShowDialog();
        }

        private void btnAramayiIptalEt_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }
        #endregion

        #region YEREL YEDEKLEME İŞLEMLERİ (LİSTE DESTEKLİ)
        private void tabControlAna_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void Sayfa_Loaded(object sender, RoutedEventArgs e)
        {
            YedekListesiniYenile();
        }

        private void YedekListesiniYenile()
        {
            try
            {
                var yedekler = BackupManager.YedekleriGetir();
                dgYedekler.ItemsSource = null;
                dgYedekler.ItemsSource = yedekler;
            }
            catch { }
        }

        private void btnYedekAl_Click(object sender, RoutedEventArgs e)
        {
            string yedekAdi = txtYedekAdi.Text.Trim();
            if (string.IsNullOrEmpty(yedekAdi))
            {
                MessageBox.Show("Lütfen yedeğe bir isim verin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string tempJsonPath = Path.Combine(Path.GetTempPath(), $"Horis_Temp_{Guid.NewGuid()}.json");
                DataManager.KaydetFarkliYol(ogretmenler, siniflar, dersler, tempJsonPath);

                var sonuc = BackupManager.YedekAl(yedekAdi, tempJsonPath);

                if (File.Exists(tempJsonPath)) File.Delete(tempJsonPath);

                MessageBox.Show(sonuc.message, sonuc.success ? "Başarılı" : "Hata", MessageBoxButton.OK, sonuc.success ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (sonuc.success)
                {
                    txtYedekAdi.Clear();
                    YedekListesiniYenile();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yedek alınırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnGeriYukle_Click(object sender, RoutedEventArgs e)
        {
            var secilenYedek = dgYedekler.SelectedItem as BackupModel;
            if (secilenYedek == null)
            {
                MessageBox.Show("Lütfen listeden geri yüklemek istediğiniz yedeği seçin!", "Seçim Yapılmadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var onay = MessageBox.Show(
                $"'{secilenYedek.BackupName}' yedeğini geri yüklemek üzeresiniz.\nMevcut verilerin üzerine yazılacaktır. Onaylıyor musunuz?",
                "Geri Yükleme Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (onay == MessageBoxResult.Yes)
            {
                var sonuc = BackupManager.YedekDosyasiGetir(secilenYedek);

                if (sonuc.success && !string.IsNullOrEmpty(sonuc.filePath))
                {
                    var yuklenenVeri = DataManager.YukleFarkliYol(sonuc.filePath);
                    if (yuklenenVeri != null)
                    {
                        ogretmenler = yuklenenVeri.Ogretmenler ?? new List<Teacher>();
                        siniflar = yuklenenVeri.Siniflar ?? new List<Classroom>();
                        dersler = yuklenenVeri.Dersler ?? new List<Lesson>();

                        TabloyuYenile();
                        DersTablosunuYenile();
                        SinifTablosunuYenile();

                        MessageBox.Show("📂 Seçili yedek başarıyla yüklendi ve tüm verileriniz güncellendi!", "Yükleme Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show(sonuc.message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnYenile_Click(object sender, RoutedEventArgs e)
        {
            YedekListesiniYenile();
        }

        private void btnYedekSil_Click(object sender, RoutedEventArgs e)
        {
            var secilenYedek = dgYedekler.SelectedItem as BackupModel;
            if (secilenYedek == null)
            {
                MessageBox.Show("Lütfen silmek istediğiniz yedeği listeden seçin!", "Seçim Yapılmadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var onay = MessageBox.Show(
                $"'{secilenYedek.BackupName}' yedeğini kalıcı olarak silmek istediğinize emin misiniz?\nBu işlem geri alınamaz.",
                "Yedek Silme Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (onay == MessageBoxResult.Yes)
            {
                var sonuc = BackupManager.YedekSil(secilenYedek);
                MessageBox.Show(sonuc.message, sonuc.success ? "Başarılı" : "Hata", MessageBoxButton.OK, sonuc.success ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (sonuc.success)
                {
                    YedekListesiniYenile();
                }
            }
        }

        private void SabitDersEkleDialogAc(Classroom sinif, int gun, int saat)
        {
            Window dialog = new Window
            {
                Title = $"🔒 {sinif.Ad} - {saat + 1}. Saat İçin Ders Kilitle",
                Width = 360,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.WhiteSmoke
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock { Text = "Hangi Dersi Kilitlemek İstiyorsunuz?", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            ComboBox cmbDers = new ComboBox { ItemsSource = dersler, DisplayMemberPath = "Ad", Margin = new Thickness(0, 0, 0, 15) };
            panel.Children.Add(cmbDers);

            panel.Children.Add(new TextBlock { Text = "Hangi Öğretmen Girecek?", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            ComboBox cmbOgretmen = new ComboBox { ItemsSource = ogretmenler, DisplayMemberPath = "Ad", Margin = new Thickness(0, 0, 0, 20) };
            panel.Children.Add(cmbOgretmen);

            Button btnKaydet = new Button { Content = "🔒 DERSİ KİLİTLE", Background = Brushes.MediumSeaGreen, Foreground = Brushes.White, Height = 40, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
            btnKaydet.Click += (s, ev) =>
            {
                if (cmbDers.SelectedItem == null || cmbOgretmen.SelectedItem == null)
                {
                    MessageBox.Show("Lütfen kilitlenecek dersi ve öğretmeni seçin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Lesson secilenDers = cmbDers.SelectedItem as Lesson;
                Teacher secilenOgr = cmbOgretmen.SelectedItem as Teacher;

                sinif.SabitDersler.Add(new SabitDers
                {
                    Gun = (Day)gun,
                    SaatIndex = saat,
                    DersAdi = secilenDers.Ad,
                    Ogretmen = secilenOgr
                });

                dialog.DialogResult = true;
            };

            panel.Children.Add(btnKaydet);
            dialog.Content = panel;

            if (dialog.ShowDialog() == true)
            {
                ProgramTablosunuCiz();
            }
        }

        private void DersiTasiVeKontrolEt(HucreVerisi kaynak, HucreVerisi hedef)
        {
            // 1. KONTROL: Öğretmen ekranında farklı sınıfları birbirinin üstüne sürüklemeyi engelle
            if (kaynak.Sinif != hedef.Sinif)
            {
                MessageBox.Show($"❌ Sınıf Uyuşmazlığı!\n\nÖğretmenin {kaynak.Sinif.Ad} sınıfındaki dersini, {hedef.Sinif.Ad} sınıfındaki dersinin üzerine sürükleyemezsiniz. Lütfen dersi boş bir hücreye taşıyın.", "Geçersiz Taşıma", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProgramTablosunuCiz();
                return;
            }

            // Gerçek verileri her zaman sınıfın ana matrisinden çekiyoruz
            var matris = olusturulanProgramlar[kaynak.Sinif];
            var tasinanDersAtama = matris[kaynak.Gun, kaynak.Saat];
            var hedefDersAtama = matris[hedef.Gun, hedef.Saat];

            bool kaynakSabitMi = kaynak.Sinif.SabitDersler.Any(s => (int)s.Gun == kaynak.Gun && s.SaatIndex == kaynak.Saat);
            bool hedefSabitMi = hedef.Sinif.SabitDersler.Any(s => (int)s.Gun == hedef.Gun && s.SaatIndex == hedef.Saat);

            if (kaynakSabitMi || hedefSabitMi)
            {
                MessageBox.Show("🔒 Kilitli (Sabit) dersler sürükle-bırak yöntemiyle taşınamaz veya üzerine başka ders yazılamaz!\nÖnce hücreye sağ tıklayıp kilidi kaldırmalısınız.", "İşlem Engellendi", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProgramTablosunuCiz();
                return;
            }

            // TAŞINAN DERSİN YENİ SAATTEKİ MÜSAİTLİĞİ
            if (tasinanDersAtama != null)
            {
                var ogr = tasinanDersAtama.Ogretmen;
                bool ogretmenMusaitDegil = ogr.MusaitOlmayanZamanlar.Any(z => (int)z.Gun == hedef.Gun && z.SaatIndex == hedef.Saat);
                if (ogretmenMusaitDegil)
                {
                    MessageBox.Show($"❌ TAŞIMA ENGELLENDİ!\n\n{ogr.Ad} isimli öğretmen {gunlerDeseni(hedef.Gun)} günü {hedef.Saat + 1}. saatte MÜSAİT DEĞİLDİR (Engelli saat).", "Müsaitlik Kısıtı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ProgramTablosunuCiz();
                    return;
                }
            }

            // GİZLİ TAKAS KONTROLÜ 1: Hedefteki Hoca, kaynağın saatine geçebilir mi? (Müsaitlik)
            if (hedefDersAtama != null)
            {
                var ogr = hedefDersAtama.Ogretmen;
                bool ogretmenMusaitDegil = ogr.MusaitOlmayanZamanlar.Any(z => (int)z.Gun == kaynak.Gun && z.SaatIndex == kaynak.Saat);
                if (ogretmenMusaitDegil)
                {
                    MessageBox.Show($"❌ GİZLİ TAKAS ENGELLENDİ!\n\nSiz bu dersi boş bir yere çektiğinizi sanıyorsunuz ancak bu ders '{kaynak.Sinif.Ad}' sınıfına ait ve o sınıfın hedef saatinde {ogr.Ad} ders veriyor.\n\nSistem otomatik olarak hocaları yer değiştirmek istedi ancak {ogr.Ad}, taşımak istediğiniz ilk saatte müsait değil!", "Arka Plan Takas Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ProgramTablosunuCiz();
                    return;
                }
            }

            // TAŞINAN DERSİN YENİ SAATTEKİ ÇAKIŞMASI
            if (tasinanDersAtama != null)
            {
                foreach (var kvp in olusturulanProgramlar)
                {
                    if (kvp.Key == kaynak.Sinif) continue;
                    var digerSinifAtama = kvp.Value[hedef.Gun, hedef.Saat];
                    if (digerSinifAtama != null && digerSinifAtama.Ogretmen.Ad == tasinanDersAtama.Ogretmen.Ad)
                    {
                        MessageBox.Show($"❌ ÇAKIŞMA ENGELLENDİ!\n\n{tasinanDersAtama.Ogretmen.Ad} isimli öğretmenin o saatte zaten '{kvp.Key.Ad}' sınıfında dersi bulunmaktadır!", "Taşıma Yapılamaz", MessageBoxButton.OK, MessageBoxImage.Warning);
                        ProgramTablosunuCiz();
                        return;
                    }
                }
            }

            // GİZLİ TAKAS KONTROLÜ 2: Hedefteki Hoca, kaynağın saatine geçerken başka sınıfla çakışır mı? (Senin yaşadığın hata)
            if (hedefDersAtama != null)
            {
                foreach (var kvp in olusturulanProgramlar)
                {
                    if (kvp.Key == kaynak.Sinif) continue;
                    var digerSinifAtama = kvp.Value[kaynak.Gun, kaynak.Saat];
                    if (digerSinifAtama != null && digerSinifAtama.Ogretmen.Ad == hedefDersAtama.Ogretmen.Ad)
                    {
                        MessageBox.Show($"❌ GİZLİ TAKAS ÇAKIŞMASI!\n\nSiz bu dersi boş bir hücreye sürüklediğinizi düşünüyorsunuz ancak o ders '{kaynak.Sinif.Ad}' sınıfına ait.\n\nHedef saatte '{kaynak.Sinif.Ad}' sınıfında {hedefDersAtama.Ogretmen.Ad} ders veriyor. Sistem sizin için bu iki hocanın saatlerini kendi aralarında yer değiştirmeye (takas) çalıştı.\n\nANCAK, {hedefDersAtama.Ogretmen.Ad} hocamızın eski saatte zaten '{kvp.Key.Ad}' sınıfında dersi olduğu için bu takas gerçekleştirilemedi.", "Sınıf İçi Çakışma", MessageBoxButton.OK, MessageBoxImage.Error);
                        ProgramTablosunuCiz();
                        return;
                    }
                }
            }

            // Tüm engeller aşıldıysa takası / taşımayı gerçekleştir
            matris[hedef.Gun, hedef.Saat] = tasinanDersAtama;
            matris[kaynak.Gun, kaynak.Saat] = hedefDersAtama;
            ProgramTablosunuCiz();
        }

        private string gunlerDeseni(int gunIndex)
        {
            string[] g = { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };
            return g[gunIndex];
        }
        #endregion
    }
}