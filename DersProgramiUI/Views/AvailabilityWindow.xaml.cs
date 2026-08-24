using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DersProgramiUI.Models;

namespace DersProgramiUI
{
    public partial class MusaitlikPenceresi : Window
    {
        private Teacher _ogretmen;
        private Classroom _sinif;
        private bool[,] _engelliMatris = new bool[7, 10]; // [Gun (0-6), Saat (0-9)]

        // Gün bazlı ComboBox'ları hafızada tutmak için liste
        private List<ComboBox> _gunlukMaxComboList = new List<ComboBox>();

        public MusaitlikPenceresi(Teacher ogretmen)
        {
            InitializeComponent();
            _ogretmen = ogretmen;
            lblBaslik.Text = $"{_ogretmen.Ad} (Öğretmen) - Müsaitlik Zamanları";

            foreach (var z in _ogretmen.MusaitOlmayanZamanlar)
            {
                _engelliMatris[(int)z.Gun, z.SaatIndex] = true;
            }

            MatrisiOlustur();
        }

        public MusaitlikPenceresi(Classroom sinif)
        {
            InitializeComponent();
            _sinif = sinif;
            lblBaslik.Text = $"{_sinif.Ad} (Sınıf) - Müsaitlik & Günlük Max Ders Ayarları";

            foreach (var z in _sinif.UygunZamanlar)
            {
                _engelliMatris[(int)z.Gun, z.SaatIndex] = true;
            }

            MatrisiOlustur();
        }

        private void MatrisiOlustur()
        {
            gridMatris.Children.Clear();
            _gunlukMaxComboList.Clear();

            gridMatris.Columns = 12;
            gridMatris.Rows = 8;

            // 1. SATIR: Üst Başlıklar ve Max Saat ComboBox'ları
            gridMatris.Children.Add(new TextBlock
            {
                Text = "Max Saat",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            gridMatris.Children.Add(new TextBlock
            {
                Text = "Gün / Saat",
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            for (int s = 0; s < 10; s++)
            {
                gridMatris.Children.Add(new TextBlock
                {
                    Text = $"{s + 1}. Saat",
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            string[] gunler = { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };

            for (int g = 0; g < 7; g++)
            {
                // 🎯 YENİ: Hem Sınıf Hem Öğretmen Modunda Max Saat ComboBox'ı Gösterilir
                ComboBox cmbMax = new ComboBox
                {
                    Margin = new Thickness(2),
                    FontSize = 11,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    ToolTip = $"{gunler[g]} günü için maksimum ders saati sınırı"
                };

                for (int i = 1; i <= 10; i++) cmbMax.Items.Add($"{i} Sa");

                int mevcutLimit = _sinif != null
                    ? _sinif.GunlukMaxDersSaatiGetir((Day)g)
                    : _ogretmen.GunlukMaxDersSaatiGetir((Day)g);

                cmbMax.SelectedIndex = mevcutLimit - 1;
                _gunlukMaxComboList.Add(cmbMax);
                gridMatris.Children.Add(cmbMax);

                // Gün Başlık Butonu
                Button btnGunBaslik = new Button
                {
                    Content = gunler[g],
                    FontWeight = FontWeights.Bold,
                    Background = Brushes.LightSlateGray,
                    Foreground = Brushes.White,
                    Margin = new Thickness(2),
                    Tag = g
                };
                btnGunBaslik.Click += GunBaslik_Click;
                gridMatris.Children.Add(btnGunBaslik);

                // 10 Saatlik Hücreler
                for (int s = 0; s < 10; s++)
                {
                    Button btn = new Button { Margin = new Thickness(2), Tag = new Point(g, s), FontWeight = FontWeights.Bold };
                    bool engelliMi = _engelliMatris[g, s];
                    ButonGörunumGuncelle(btn, engelliMi);
                    btn.Click += MusaitlikKutu_Click;
                    gridMatris.Children.Add(btn);
                }
            }
        }

        private void GunBaslik_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int gunIndex = (int)btn.Tag;

            // ComboBox değerlerini hafızaya alma
            if (_gunlukMaxComboList.Count == 7)
            {
                for (int g = 0; g < 7; g++)
                {
                    int anlikSecim = _gunlukMaxComboList[g].SelectedIndex + 1;
                    if (_sinif != null) _sinif.GunlukMaxDersSaatleri[(Day)g] = anlikSecim;
                    else if (_ogretmen != null) _ogretmen.GunlukMaxDersSaatleri[(Day)g] = anlikSecim;
                }
            }

            bool hepsiEngelliMi = true;
            for (int s = 0; s < 10; s++)
            {
                if (!_engelliMatris[gunIndex, s]) { hepsiEngelliMi = false; break; }
            }

            bool yeniDurum = !hepsiEngelliMi;
            for (int s = 0; s < 10; s++) _engelliMatris[gunIndex, s] = yeniDurum;

            MatrisiOlustur();
        }

        private void MusaitlikKutu_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            Point nokta = (Point)btn.Tag;
            int g = (int)nokta.X;
            int s = (int)nokta.Y;

            _engelliMatris[g, s] = !_engelliMatris[g, s];
            ButonGörunumGuncelle(btn, _engelliMatris[g, s]);
        }

        private void ButonGörunumGuncelle(Button btn, bool engelliMi)
        {
            if (engelliMi)
            {
                btn.Content = "❌ Engelli";
                btn.Background = Brushes.LightCoral;
                btn.Foreground = Brushes.DarkRed;
            }
            else
            {
                btn.Content = "✓ Müsait";
                btn.Background = Brushes.LightGreen;
                btn.Foreground = Brushes.DarkGreen;
            }
        }

        private void btnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (_ogretmen != null)
            {
                _ogretmen.MusaitOlmayanZamanlar.Clear();
                for (int g = 0; g < 7; g++)
                {
                    for (int s = 0; s < 10; s++)
                    {
                        if (_engelliMatris[g, s])
                            _ogretmen.MusaitOlmayanZamanEkle((Day)g, s);
                    }
                }

                // 🎯 YENİ: Öğretmenin Günlük Max Ders Saatlerini Kaydet
                _ogretmen.GunlukMaxDersSaatleri.Clear();
                for (int g = 0; g < _gunlukMaxComboList.Count; g++)
                {
                    int secilenLimit = _gunlukMaxComboList[g].SelectedIndex + 1;
                    _ogretmen.GunlukMaxDersSaatleri[(Day)g] = secilenLimit;
                }

                MessageBox.Show($"{_ogretmen.Ad} öğretmeninin zaman ve günlük max ders saati tercihleri kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (_sinif != null)
            {
                _sinif.UygunZamanlar.Clear();
                for (int g = 0; g < 7; g++)
                {
                    for (int s = 0; s < 10; s++)
                    {
                        if (_engelliMatris[g, s])
                            _sinif.UygunZamanlar.Add(new TimeSlot((Day)g, s));
                    }
                }

                _sinif.GunlukMaxDersSaatleri.Clear();
                for (int g = 0; g < _gunlukMaxComboList.Count; g++)
                {
                    int secilenLimit = _gunlukMaxComboList[g].SelectedIndex + 1;
                    _sinif.GunlukMaxDersSaatleri[(Day)g] = secilenLimit;
                }
                MessageBox.Show($"{_sinif.Ad} sınıfının zaman ve gün bazlı ders saati tercihleri kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            this.Close();
        }

        private void btnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}