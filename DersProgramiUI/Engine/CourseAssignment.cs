namespace DersProgramiUI.Models
{
    public class CourseAssignment
    {
        public Lesson Ders { get; set; }
        public Teacher Ogretmen { get; set; }
        public Classroom Sinif { get; set; }

        public CourseAssignment() { }

        public CourseAssignment(Lesson ders, Teacher ogretmen)
        {
            Ders = ders;
            Ogretmen = ogretmen;
        }
        // 3 PARAMETRELİ CONSTRUCTOR (Hatanın çözümü):
        public CourseAssignment(Lesson ders, Teacher ogretmen, Classroom sinif)
        {
            Ders = ders;
            Ogretmen = ogretmen;
            Sinif = sinif;
        }
    }
}