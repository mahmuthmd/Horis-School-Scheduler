using DersProgramiUI.Models;

namespace DersProgramiUI.Models
{
    public class CourseAssignmentRequest
    {
        public string DersAdi { get; set; }
        public Teacher ZorunluOgretmen { get; set; }
        public int ToplamDersSaati { get; set; }
        public int BlokUzunlugu { get; set; } // 🎯 Eksik olan tanım burası
        public int EtkiliMusaitlikPuani { get; set; }
        public int OgretmenDerecesi { get; set; }
    }

    public class SaatAdayi
    {
        public int Saat { get; set; }
        public int Skor { get; set; }
    }
}