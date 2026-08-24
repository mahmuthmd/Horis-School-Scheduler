using System;

namespace DersProgramiUI.Models
{
    // Giriş İsteği
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    // Sunucudan Dönen Yanıt
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime? ExpireDate { get; set; }
        public int RemainingDays { get; set; }
    }

    // Versiyon Kontrolü Yanıtı
    public class VersionCheckResponse
    {
        public string LatestVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseNotes { get; set; }
    }
}