using System;

namespace DersProgramiUI.Models
{
    public class BackupModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string BackupName { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}