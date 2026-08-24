using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DersProgramiUI.Models;

namespace DersProgramiUI
{
    public partial class OnSartPenceresi : Window
    {
        private List<Classroom> _siniflar;
        private List<Lesson> _dersler;
        private SabitDers[,] _geciciMatris = new SabitDers[7, 10];

        public OnSartPenceresi(List<Classroom> siniflar, List<Lesson> dersler)
        {
            InitializeComponent();
            _siniflar = siniflar;
            _dersler = dersler;

            cmbSiniflar.ItemsSource = _siniflar;
            if (_siniflar.Count > 0) cmbSiniflar.SelectedIndex = 0;
        }

        private void cmbSiniflar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Classroom seciliSinif = cmbSiniflar.SelectedItem as Classroom;
            if (seciliSinif == null) return;

            var sinifDersleri = _dersler.Where(d => seciliSinif.DersProgramiYukDetailed.ContainsKey(d.Ad)).ToList();
            cmbDersler.ItemsSource = sinifDersleri;
            if (sinifDersleri.Count > 0) cmbDersler.SelectedIndex = 0;

            _geciciMatris = new SabitDers[7, 10];
            foreach (var s in seciliSinif.SabitDersler)
            {
                _geciciMatris[(int)s.Gun, s.SaatIndex] = s;
            }

            MatrisiOlustur();
        }

        private void cmbDersler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Lesson seciliDers = cmbDersler.SelectedItem as Lesson;
            Classroom seciliSinif = cmbSiniflar.SelectedItem as Classroom;

            if (seciliDers != null && seciliSinif != null)
            {
                var yukInfo = seciliSinif.DersProgramiYukDetailed[seciliDers.Ad];
                if (yukInfo.ZorunluOgretmen != null)
                {
                    cmbOgretmenler.ItemsSource = new List<Teacher> { yukInfo.ZorunluOgretmen };
                }
                else
                {
                    cmbOgretmenler.ItemsSource = seciliDers.VerenOgretmenler;
                }
                if (cmbOgretmenler.Items.Count > 0) cmbOgretmenler.SelectedIndex = 0;
            }
        }

        private void MatrisiOlustur()
        {
            gridMatris.Children.Clear();
            Classroom seciliSinif = cmbSiniflar.SelectedItem as Classroom;

            gridMatris.Children.Add(new TextBlock());
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
                gridMatris.Children.Add(new TextBlock());
                gridMatris.Children.Add(new TextBlock
                {
                    Text = gunler[g],
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });

                for (int s = 0; s < 10; s++)
                {
                    Button btn = new Button { Margin = new Thickness(2), Tag = new Point(g, s) };

                    bool sinifKapaliMi = seciliSinif != null &&
                        seciliSinif.UygunZamanlar.Any(z => (int)z.Gun == g && z.SaatIndex == s);

                    if (sinifKapaliMi)
                    {
                        btn.Content = "❌ Sınıf Kapalı";
                        btn.Background = Brushes.LightGray;
                        btn.Foreground = Brushes.DarkGray;
                        btn.IsEnabled = false;
                        btn.ToolTip = "Bu saat dilimi Sınıf Müsaitlik Ayarlarında kapalı olarak işaretlenmiştir.";
                    }
                    else
                    {
                        var atama = _geciciMatris[g, s];

                        if (atama != null)
                        {
                            btn.Content = $"🔒 {atama.DersAdi}\n({atama.Ogretmen.Ad})";
                            btn.Background = Brushes.LightSteelBlue;
                            btn.FontWeight = FontWeights.Bold;
                        }
                        else
                        {
                            btn.Content = "Boş";
                            btn.Background = Brushes.WhiteSmoke;
                        }

                        btn.Click += HucreKilit_Click;
                    }

                    gridMatris.Children.Add(btn);
                }
            }
        }

        private void HucreKilit_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            Point p = (Point)btn.Tag;
            int g = (int)p.X;
            int s = (int)p.Y;

            Classroom seciliSinif = cmbSiniflar.SelectedItem as Classroom;

            if (seciliSinif != null && seciliSinif.UygunZamanlar.Any(z => (int)z.Gun == g && z.SaatIndex == s))
            {
                return;
            }

            if (_geciciMatris[g, s] != null)
            {
                _geciciMatris[g, s] = null;
            }
            else
            {
                Lesson ders = cmbDersler.SelectedItem as Lesson;
                Teacher ogr = cmbOgretmenler.SelectedItem as Teacher;

                if (ders == null || ogr == null)
                {
                    MessageBox.Show("Lütfen öncelikle sabitlenecek dersi ve öğretmeni seçin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _geciciMatris[g, s] = new SabitDers((Day)g, s, ders.Ad, ogr);
            }

            MatrisiOlustur();
        }

        private void btnKaydet_Click(object sender, RoutedEventArgs e)
        {
            Classroom seciliSinif = cmbSiniflar.SelectedItem as Classroom;
            if (seciliSinif != null)
            {
                seciliSinif.SabitDersler.Clear();
                for (int g = 0; g < 7; g++)
                {
                    for (int s = 0; s < 10; s++)
                    {
                        if (_geciciMatris[g, s] != null)
                            seciliSinif.SabitDersler.Add(_geciciMatris[g, s]);
                    }
                }
                MessageBox.Show($"{seciliSinif.Ad} sınıfının ders sabitleme ön şartları kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            this.Close();
        }

        private void btnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}