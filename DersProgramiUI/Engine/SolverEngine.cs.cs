using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DersProgramiUI.Models;
using Google.OrTools.Sat;

namespace DersProgramiUI.Engine
{
    public class CozucuMotor
    {
        public List<Classroom> Siniflar { get; set; }
        public int TimeoutSaniye { get; set; } = 20;
        public List<Teacher> Ogretmenler { get; set; }
        public List<Lesson> Dersler { get; set; }
        public List<Teacher> KatiOgretmenler { get; set; } = new List<Teacher>();

        public int MaxUstUsteDers { get; set; } = 2;
        public int TercihEdilenMinBlok { get; set; } = 2;

        public CancellationToken CancellationToken { get; set; }

        public Dictionary<Classroom, CourseAssignment[,]> Programlar { get; private set; }
        public List<string> HataRaporu { get; private set; } = new List<string>();
        public List<string> SiralamaPuanRaporu { get; private set; } = new List<string>();

        private int _toplamGerekenBlok = 0;
        private int _yerlesenBlokSayisi = 0;

        public CozucuMotor(List<Classroom> siniflar, List<Teacher> ogretmenler, List<Lesson> dersler)
        {
            Siniflar = siniflar ?? new List<Classroom>();
            Ogretmenler = ogretmenler ?? new List<Teacher>();
            Dersler = dersler ?? new List<Lesson>();
            Programlar = new Dictionary<Classroom, CourseAssignment[,]>();
        }

        public bool Coz()
        {
            Programlar.Clear();
            HataRaporu.Clear();
            SiralamaPuanRaporu.Clear();
            _toplamGerekenBlok = 0;
            _yerlesenBlokSayisi = 0;

            foreach (var sinif in Siniflar) 
                Programlar[sinif] = new CourseAssignment[7, 10];

            CpModel model = new CpModel();

            // 1. Sabit Dersleri İşle
            var sinifSabitDers = new bool[Siniflar.Count, 7, 10];
            var ogrSabitDers = new bool[Ogretmenler.Count, 7, 10];
            var sinifSabitSaatToplami = new int[Siniflar.Count, 7];
            var ogrSabitSaatToplami = new int[Ogretmenler.Count, 7];

            for (int sIdx = 0; sIdx < Siniflar.Count; sIdx++)
            {
                var sinif = Siniflar[sIdx];
                if (sinif.SabitDersler == null) continue;

                foreach (var sabit in sinif.SabitDersler)
                {
                    int g = (int)sabit.Gun;
                    int h = sabit.SaatIndex;
                    if (g < 0 || g >= 7 || h < 0 || h >= 10) continue;

                    sinifSabitDers[sIdx, g, h] = true;
                    sinifSabitSaatToplami[sIdx, g]++;

                    if (sabit.Ogretmen != null)
                    {
                        int oIdx = Ogretmenler.FindIndex(o => o.Ad.Equals(sabit.Ogretmen.Ad, StringComparison.OrdinalIgnoreCase));
                        if (oIdx >= 0)
                        {
                            ogrSabitDers[oIdx, g, h] = true;
                            ogrSabitSaatToplami[oIdx, g]++;
                        }
                    }

                    var d = Dersler.FirstOrDefault(x => x.Ad.Equals(sabit.DersAdi, StringComparison.OrdinalIgnoreCase));
                    if (d != null && sabit.Ogretmen != null)
                    {
                        Programlar[sinif][g, h] = new CourseAssignment(d, sabit.Ogretmen, sinif);
                    }
                }
            }

            // 2. Talepleri (Blokları) Ayrıştır
            List<TalepNode> talepler = new List<TalepNode>();

            foreach (var sinif in Siniflar)
            {
                if (sinif.DersProgramiYukDetailed == null) continue;

                foreach (var kvp in sinif.DersProgramiYukDetailed)
                {
                    string dersAdi = kvp.Key;
                    var yuk = kvp.Value;

                    int kilitliSaat = (sinif.SabitDersler != null) 
                        ? sinif.SabitDersler.Count(s => s.DersAdi.Equals(dersAdi, StringComparison.OrdinalIgnoreCase)) 
                        : 0;

                    int kalanSaat = yuk.Saat - kilitliSaat;
                    if (kalanSaat <= 0) continue;

                    List<int> bloklar = DersSaatleriniBloklaraBol(kalanSaat);
                    Lesson ders = Dersler.FirstOrDefault(d => d.Ad.Equals(dersAdi, StringComparison.OrdinalIgnoreCase));
                    if (ders == null) continue;

                    List<Teacher> adaylar = new List<Teacher>();
                    if (yuk.ZorunluOgretmen != null)
                    {
                        var eslesenOgr = Ogretmenler.FirstOrDefault(o => o.Ad.Equals(yuk.ZorunluOgretmen.Ad, StringComparison.OrdinalIgnoreCase));
                        if (eslesenOgr != null) adaylar.Add(eslesenOgr);
                    }
                    else if (ders.VerenOgretmenler != null)
                    {
                        adaylar = Ogretmenler.Where(o => ders.VerenOgretmenler.Any(v => v.Ad.Equals(o.Ad, StringComparison.OrdinalIgnoreCase))).ToList();
                    }

                    if (adaylar.Count == 0)
                    {
                        HataRaporu.Add($"❌ {sinif.Ad} sınıfındaki '{dersAdi}' dersini verebilecek hiçbir öğretmen tanımlı değil!");
                        continue;
                    }

                    foreach (var blokBoyutu in bloklar)
                    {
                        talepler.Add(new TalepNode { Sinif = sinif, Ders = ders, Sure = blokBoyutu, AdayOgretmenler = adaylar });
                        _toplamGerekenBlok++;
                    }
                }
            }

            if (talepler.Count == 0)
            {
                HataRaporu.Add("❌ Dağıtılacak hiçbir ders yükü bulunamadı.");
                return false;
            }

            // 3. Karar Değişkenleri ve Hücre Takip Listeleri
            List<BoolVar> allVars = new List<BoolVar>();
            Dictionary<int, List<BoolVar>> sinifHucreVars = new Dictionary<int, List<BoolVar>>();
            Dictionary<int, List<BoolVar>> ogrHucreVars = new Dictionary<int, List<BoolVar>>();

            for (int i = 0; i < Siniflar.Count; i++)
                for (int d = 0; d < 7; d++)
                    for (int h = 0; h < 10; h++)
                        sinifHucreVars[i * 100 + d * 10 + h] = new List<BoolVar>();

            for (int i = 0; i < Ogretmenler.Count; i++)
                for (int d = 0; d < 7; d++)
                    for (int h = 0; h < 10; h++)
                        ogrHucreVars[i * 100 + d * 10 + h] = new List<BoolVar>();

            int varCounter = 0;
            foreach (var talep in talepler)
            {
                int sIdx = Siniflar.IndexOf(talep.Sinif);

                foreach (var ogr in talep.AdayOgretmenler)
                {
                    int oIdx = Ogretmenler.IndexOf(ogr);
                    if (oIdx < 0) continue;

                    for (int d = 0; d < 7; d++)
                    {
                        for (int h = 0; h <= 10 - talep.Sure; h++)
                        {
                            bool musait = true;
                            for (int b = 0; b < talep.Sure; b++)
                            {
                                int currentH = h + b;

                                // Sabit ders çakışması
                                if (sinifSabitDers[sIdx, d, currentH] || ogrSabitDers[oIdx, d, currentH])
                                {
                                    musait = false;
                                    break;
                                }

                                // Öğretmen müsait değil listesi
                                if (ogr.MusaitOlmayanZamanlar != null && ogr.MusaitOlmayanZamanlar.Any(z => (int)z.Gun == d && z.SaatIndex == currentH))
                                {
                                    musait = false;
                                    break;
                                }

                                if (talep.Sinif.UygunZamanlar != null && talep.Sinif.UygunZamanlar.Any(z => (int)z.Gun == d && z.SaatIndex == currentH))
                                {
                                    musait = false;
                                    break;
                                }
                            }

                            if (musait)
                            {
                                BoolVar v = model.NewBoolVar($"v_{varCounter++}");
                                talep.Ihtimaller.Add(new TalepVar { Var = v, Ogretmen = ogr, Gun = d, Saat = h });
                                allVars.Add(v);

                                for (int b = 0; b < talep.Sure; b++)
                                {
                                    sinifHucreVars[sIdx * 100 + d * 10 + (h + b)].Add(v);
                                    ogrHucreVars[oIdx * 100 + d * 10 + (h + b)].Add(v);
                                }
                            }
                        }
                    }
                }
            }

            if (allVars.Count == 0)
            {
                HataRaporu.Add("❌ Olası hiçbir yerleşim alanı bulunamadı. Öğretmenlerin müsaitlik pencerelerini (engelli saatlerini) kontrol edin.");
                return false;
            }

            // Kısıt 1: Her talep bloğu en fazla 1 kez yerleşir
            foreach (var talep in talepler)
            {
                var vars = talep.Ihtimaller.Select(x => x.Var).ToList();
                if (vars.Count > 0)
                    model.Add(LinearExpr.Sum(vars) <= 1);
            }

            // Kısıt 2: Hücre çakışmaları
            foreach (var kvp in sinifHucreVars)
                if (kvp.Value.Count > 1) model.Add(LinearExpr.Sum(kvp.Value) <= 1);

            foreach (var kvp in ogrHucreVars)
                if (kvp.Value.Count > 1) model.Add(LinearExpr.Sum(kvp.Value) <= 1);

            // Kısıt 3: Günlük Saat Limitleri
            for (int d = 0; d < 7; d++)
            {
                for (int sIdx = 0; sIdx < Siniflar.Count; sIdx++)
                {
                    List<ILiteral> vars = new List<ILiteral>();
                    List<int> coeffs = new List<int>();
                    foreach (var talep in talepler.Where(t => t.Sinif == Siniflar[sIdx]))
                    {
                        foreach (var ihtimal in talep.Ihtimaller.Where(i => i.Gun == d))
                        {
                            vars.Add(ihtimal.Var);
                            coeffs.Add(talep.Sure);
                        }
                    }

                    int rawLimit = Siniflar[sIdx].GunlukMaxDersSaatiGetir((Day)d);
                    int sinifLimit = rawLimit > 0 ? rawLimit : 10;
                    sinifLimit = Math.Max(0, sinifLimit - sinifSabitSaatToplami[sIdx, d]);

                    if (vars.Count > 0)
                        model.Add(LinearExpr.WeightedSum(vars, coeffs) <= sinifLimit);
                }

                for (int oIdx = 0; oIdx < Ogretmenler.Count; oIdx++)
                {
                    List<ILiteral> vars = new List<ILiteral>();
                    List<int> coeffs = new List<int>();
                    foreach (var talep in talepler)
                    {
                        foreach (var ihtimal in talep.Ihtimaller.Where(i => i.Gun == d && Ogretmenler.IndexOf(i.Ogretmen) == oIdx))
                        {
                            vars.Add(ihtimal.Var);
                            coeffs.Add(talep.Sure);
                        }
                    }

                    int rawLimit = Ogretmenler[oIdx].GunlukMaxDersSaatiGetir((Day)d);
                    int ogrLimit = rawLimit > 0 ? rawLimit : 10;
                    ogrLimit = Math.Max(0, ogrLimit - ogrSabitSaatToplami[oIdx, d]);

                    if (vars.Count > 0)
                        model.Add(LinearExpr.WeightedSum(vars, coeffs) <= ogrLimit);
                }
            }

            // ==============================================================================
            // Kısıt 3.5: Katı Öğretmenler (Boşluksuz/Penceresiz Program - Hard Constraint)
            // ==============================================================================
            foreach (var katiOgr in KatiOgretmenler)
            {
                int oIdx = Ogretmenler.FindIndex(o => o.Ad.Equals(katiOgr.Ad, StringComparison.OrdinalIgnoreCase));
                if (oIdx < 0) continue;

                for (int d = 0; d < 7; d++)
                {
                    var gunHucreleri = new List<BoolVar>();
                    for (int h = 0; h < 10; h++)
                    {
                        var hucreVars = ogrHucreVars[oIdx * 100 + d * 10 + h];
                        BoolVar isDerste = model.NewBoolVar($"kati_{oIdx}_{d}_{h}");

                        // O saatte hocanın herhangi bir dersi varsa isDerste = 1 olur
                        if (hucreVars.Count > 0)
                            model.Add(isDerste == LinearExpr.Sum(hucreVars));
                        else
                            model.Add(isDerste == 0);

                        gunHucreleri.Add(isDerste);
                    }

                    // CP-SAT Matematiksel Boşluk Kısıtı (Max 1 Geçiş): 
                    // 0'dan 1'e geçiş sayısını sayarız. Boşluksuz bir programda derse sadece 1 kere başlanır (Geçiş <= 1 olmalıdır).
                    var transitions = new List<IntVar>();
                    transitions.Add(gunHucreleri[0]); // İlk saat dersteyse 1 geçiş sayılır

                    for (int h = 1; h < 10; h++)
                    {
                        IntVar t = model.NewIntVar(0, 1, $"t_{oIdx}_{d}_{h}");
                        // t >= gunHucreleri[h] - gunHucreleri[h-1]
                        model.Add(t - gunHucreleri[h] + gunHucreleri[h - 1] >= 0);
                        transitions.Add(t);
                    }
                    // O gün içindeki derse başlama (0'dan 1'e geçiş) sayısı en fazla 1 olabilir. 
                    // Yani dersler bitişik olmak ZORUNDADIR.
                    model.Add(LinearExpr.Sum(transitions) <= 1);
                }
            }

            // Kısıt 4: Hedef Gün Sayısı (Soft - Esnek Ceza)
            LinearExprBuilder obj = LinearExpr.NewBuilder();
            Dictionary<Teacher, IntVar> ogrGunAsimi = new Dictionary<Teacher, IntVar>();
            Dictionary<Classroom, IntVar> sinifGunAsimi = new Dictionary<Classroom, IntVar>();

            for (int oIdx = 0; oIdx < Ogretmenler.Count; oIdx++)
            {
                var ogr = Ogretmenler[oIdx];
                if (ogr.HedefGunSayisi > 0)
                {
                    List<BoolVar> gunAktif = new List<BoolVar>();
                    for (int d = 0; d < 7; d++)
                    {
                        BoolVar act = model.NewBoolVar($"act_o_{oIdx}_{d}");
                        gunAktif.Add(act);
                        if (ogrSabitSaatToplami[oIdx, d] > 0) model.Add(act == 1);

                        foreach (var talep in talepler)
                        {
                            foreach (var ihtimal in talep.Ihtimaller.Where(i => i.Gun == d && i.Ogretmen == ogr))
                            {
                                model.AddImplication(ihtimal.Var, act);
                            }
                        }
                    }

                    IntVar asim = model.NewIntVar(0, 7, $"asim_o_{oIdx}");
                    model.Add(LinearExpr.Sum(gunAktif) - ogr.HedefGunSayisi <= asim);
                    obj.AddTerm(asim, -1000);
                    ogrGunAsimi[ogr] = asim;
                }
            }

            for (int sIdx = 0; sIdx < Siniflar.Count; sIdx++)
            {
                var sinif = Siniflar[sIdx];
                if (sinif.HedefGunSayisi > 0)
                {
                    List<BoolVar> gunAktif = new List<BoolVar>();
                    for (int d = 0; d < 7; d++)
                    {
                        BoolVar act = model.NewBoolVar($"act_s_{sIdx}_{d}");
                        gunAktif.Add(act);
                        if (sinifSabitSaatToplami[sIdx, d] > 0) model.Add(act == 1);

                        foreach (var talep in talepler.Where(t => t.Sinif == sinif))
                        {
                            foreach (var ihtimal in talep.Ihtimaller.Where(i => i.Gun == d))
                            {
                                model.AddImplication(ihtimal.Var, act);
                            }
                        }
                    }

                    IntVar asim = model.NewIntVar(0, 7, $"asim_s_{sIdx}");
                    model.Add(LinearExpr.Sum(gunAktif) - sinif.HedefGunSayisi <= asim);
                    obj.AddTerm(asim, -1000);
                    sinifGunAsimi[sinif] = asim;
                }
            }
            // ==============================================================================
            // Kısıt 5: Maksimum Üst Üste Ders (Max Blok) Kısıtı - Kayan Pencere Yöntemi
            // ==============================================================================
            if (MaxUstUsteDers > 0)
            {
                // Sınıf ve ders bazında grupluyoruz (Örn: 9-A sınıfının Matematik dersleri)
                var sinifDersTalepleri = talepler.GroupBy(t => new { t.Sinif, t.Ders });

                foreach (var grup in sinifDersTalepleri)
                {
                    for (int d = 0; d < 7; d++)
                    {
                        var saatIhtimalleri = new List<ILiteral>[10];
                        var saatSabitleri = new int[10];

                        // O günün 10 saati için, bu dersin aktif olup olmama ihtimallerini topluyoruz
                        for (int h = 0; h < 10; h++)
                        {
                            saatIhtimalleri[h] = new List<ILiteral>();

                            // 1. Sabit Ders Kontrolü (Eğer kullanıcı arayüzden kilitlediyse)
                            if (grup.Key.Sinif.SabitDersler != null &&
                                grup.Key.Sinif.SabitDersler.Any(sb => (int)sb.Gun == d && sb.SaatIndex == h && sb.DersAdi.Equals(grup.Key.Ders.Ad, StringComparison.OrdinalIgnoreCase)))
                            {
                                saatSabitleri[h] = 1;
                            }

                            // 2. Esnek Ders İhtimalleri
                            foreach (var talep in grup)
                            {
                                foreach (var ihtimal in talep.Ihtimaller.Where(i => i.Gun == d))
                                {
                                    // Eğer ders bu ihtimalle o saate taşıyorsa
                                    if (h >= ihtimal.Saat && h < ihtimal.Saat + talep.Sure)
                                    {
                                        saatIhtimalleri[h].Add(ihtimal.Var);
                                    }
                                }
                            }
                        }

                        // Kayan Pencere (Sliding Window) 
                        // Pencere Boyutu = MaxUstUsteDers + 1 (Örn: Max Blok 2 ise, her 3 saatlik aralığı tararız)
                        int windowSize = MaxUstUsteDers + 1;
                        for (int h = 0; h <= 10 - windowSize; h++)
                        {
                            var windowVars = new List<ILiteral>();
                            int windowConstant = 0;

                            for (int w = 0; w < windowSize; w++)
                            {
                                windowVars.AddRange(saatIhtimalleri[h + w]);
                                windowConstant += saatSabitleri[h + w];
                            }

                            // O 3 saatlik (windowSize) pencerede, ders sayısı 2'yi (MaxUstUsteDers) geçemez!
                            if (windowVars.Count > 0 || windowConstant > 0)
                            {
                                model.Add(LinearExpr.Sum(windowVars) + windowConstant <= MaxUstUsteDers);
                            }
                        }
                    }
                }
            }

            // Maksimizasyon Hedefi
            foreach (var talep in talepler)
            {
                foreach (var ihtimal in talep.Ihtimaller)
                {
                    // Saat odaklamasını kaldırdık. Sadece dersin programda yer bulmasına çok yüksek puan veriyoruz.
                    // Böylece solver, gün aşımı (asim) cezalarından kaçınmaya daha çok odaklanacak.
                    obj.AddTerm(ihtimal.Var, 10000 * talep.Sure);
                }
            }
            model.Maximize(obj);

            // Çözücü Ayarları
            // Çözücü Ayarları ve Çözüm Aşaması (GÜNCELLENEN KISIM)
            CpSolver solver = new CpSolver();
            int threads = Math.Max(1, Environment.ProcessorCount);
            solver.StringParameters = $"max_time_in_seconds:{TimeoutSaniye}.0; num_search_workers:{threads};";

            Stopwatch sw = Stopwatch.StartNew();
            
            // 🎯 Durdurma tetikleyicisini solver'a bağlıyoruz
            DurdurmaCallback callback = new DurdurmaCallback(CancellationToken);
            CpSolverStatus status = solver.Solve(model, callback); 
            
            sw.Stop();
            
            // Kullanıcı iptal ettiyse manuel olarak durumu bildir
            if (CancellationToken.IsCancellationRequested)
            {
                HataRaporu.Add("⚠️ İşlem kullanıcı tarafından durduruldu.");
                return false;
            }

            if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
            {
                foreach (var talep in talepler)
                {
                    foreach (var ihtimal in talep.Ihtimaller)
                    {
                        if (solver.BooleanValue(ihtimal.Var))
                        {
                            for (int b = 0; b < talep.Sure; b++)
                            {
                                Programlar[talep.Sinif][ihtimal.Gun, ihtimal.Saat + b] = new CourseAssignment(talep.Ders, ihtimal.Ogretmen, talep.Sinif);
                            }
                            _yerlesenBlokSayisi++;
                            break;
                        }
                    }
                }
            }

            bool kuralIhlali = TeshisAnalizi(solver, status, ogrGunAsimi, sinifGunAsimi);

            SiralamaPuanRaporu.Add($"Durum: {status}");
            SiralamaPuanRaporu.Add($"Süre: {sw.ElapsedMilliseconds / 1000.0:F2} sn");
            SiralamaPuanRaporu.Add($"Yerleşen Blok: {_yerlesenBlokSayisi} / {_toplamGerekenBlok}");

            return (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible) && _yerlesenBlokSayisi == _toplamGerekenBlok && !kuralIhlali;
        }

        private bool TeshisAnalizi(CpSolver solver, CpSolverStatus status, Dictionary<Teacher, IntVar> ogrAsim, Dictionary<Classroom, IntVar> sinifAsim)
        {
            bool hataVar = false;
            HataRaporu.Add("📋 TEŞHİS RAPORU:\n");

            if (solver != null && (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible))
            {
                foreach (var kvp in ogrAsim)
                {
                    int val = (int)solver.Value(kvp.Value);
                    if (val > 0)
                    {
                        HataRaporu.Add($"⚠️ [{kvp.Key.Ad}] Öğretmeni: Hedef {kvp.Key.HedefGunSayisi} gün yerine {kvp.Key.HedefGunSayisi + val} güne yerleşti.");
                        hataVar = true;
                    }
                }

                foreach (var kvp in sinifAsim)
                {
                    int val = (int)solver.Value(kvp.Value);
                    if (val > 0)
                    {
                        HataRaporu.Add($"⚠️ [{kvp.Key.Ad}] Sınıfı: Hedef {kvp.Key.HedefGunSayisi} gün yerine {kvp.Key.HedefGunSayisi + val} güne yerleşti.");
                        hataVar = true;
                    }
                }
            }

            int eksikSaat = 0;
            foreach (var sinif in Siniflar)
            {
                var atamalar = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int g = 0; g < 7; g++)
                {
                    for (int s = 0; s < 10; s++)
                    {
                        var atama = Programlar[sinif][g, s];
                        if (atama != null)
                        {
                            if (!atamalar.ContainsKey(atama.Ders.Ad)) atamalar[atama.Ders.Ad] = 0;
                            atamalar[atama.Ders.Ad]++;
                        }
                    }
                }

                foreach (var kvp in sinif.DersProgramiYukDetailed)
                {
                    int gereken = kvp.Value.Saat;
                    int yerlesen = atamalar.ContainsKey(kvp.Key) ? atamalar[kvp.Key] : 0;
                    int fark = gereken - yerlesen;
                    if (fark > 0)
                    {
                        eksikSaat += fark;
                        HataRaporu.Add($"🔸 {sinif.Ad} ➔ {kvp.Key} ({fark} Saat Yerleşemedi)");
                        hataVar = true;
                    }
                }
            }

            if (eksikSaat > 0)
                HataRaporu.Insert(1, $"Toplama göre {eksikSaat} saat açıkta kaldı.\n");

            return hataVar;
        }

        private List<int> DersSaatleriniBloklaraBol(int toplamSaat)
        {
            List<int> bloklar = new List<int>();
            while (toplamSaat > 0)
            {
                if (toplamSaat >= TercihEdilenMinBlok)
                {
                    bloklar.Add(TercihEdilenMinBlok);
                    toplamSaat -= TercihEdilenMinBlok;
                }
                else
                {
                    bloklar.Add(1);
                    toplamSaat -= 1;
                }
            }
            return bloklar;
        }
    }

    public class TalepNode
    {
        public Classroom Sinif { get; set; }
        public Lesson Ders { get; set; }
        public int Sure { get; set; }
        public List<Teacher> AdayOgretmenler { get; set; }
        public List<TalepVar> Ihtimaller { get; set; } = new List<TalepVar>();
    }

    public class TalepVar
    {
        public BoolVar Var { get; set; }
        public Teacher Ogretmen { get; set; }
        public int Gun { get; set; }
        public int Saat { get; set; }
    }

    // 🎯 YENİ EKLENEN: Arama sırasında Durdur/İptal butonunu dinleyen Callback sınıfı
    public class DurdurmaCallback : CpSolverSolutionCallback
    {
        private CancellationToken _ct;
        public DurdurmaCallback(CancellationToken ct) { _ct = ct; }

        public override void OnSolutionCallback()
        {
            if (_ct.IsCancellationRequested)
            {
                StopSearch();
            }
        }
    }

}