using DersProgramiUI.Models;

public class TimeSlot
{
    public Day Gun { get; set; }
    public int SaatIndex { get; set; }
    public TimeSlot() { }
    public TimeSlot(Day gun, int saatIndex)
    {
        Gun = gun;
        SaatIndex = saatIndex;
    }
}