using System;
using System.Collections.Generic;

namespace DersProgramiUI.Models
{
    public class HucreVerisi
    {
        public Classroom Sinif { get; set; }
        public int Gun { get; set; }
        public int Saat { get; set; }
        public CourseAssignment Atama { get; set; }
        public Teacher ViewOgretmeni { get; set; }
    }

    public class SabitDers
    {
        public Day Gun { get; set; }
        public int SaatIndex { get; set; }
        public string DersAdi { get; set; }
        public Teacher Ogretmen { get; set; }

        public SabitDers() { }

        public SabitDers(Day gun, int saatIndex, string dersAdi, Teacher ogretmen)
        {
            Gun = gun;
            SaatIndex = saatIndex;
            DersAdi = dersAdi;
            Ogretmen = ogretmen;
        }
    }
}