using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for QuickStartPage.xaml
    /// </summary>
    public partial class QuickStartPage : Page
    {
        private MainWindow mainWindow;
        public QuickStartPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
        }

        private void SetAccount_EventHandler(object sender, MouseButtonEventArgs e)
        {
           this.mainWindow.GotoAccountPage();
        }

        private void CreateNewSyncJob_EventHandler(object sender, MouseButtonEventArgs e)
        {
            this.mainWindow.GotoSyncSettingPage();
        }
    }
}
