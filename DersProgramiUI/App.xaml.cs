using System.Windows;

namespace DersProgramiUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // İlk olarak Giriş / Güncelleme Penceresini Başlat
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }
}