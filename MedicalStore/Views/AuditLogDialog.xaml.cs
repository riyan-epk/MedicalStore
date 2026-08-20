using System.Windows;
using MedicalStore.BLL.Services;

namespace MedicalStore.Views
{
    public partial class AuditLogDialog : Window
    {
        private readonly AuditService _auditService = new();

        public AuditLogDialog()
        {
            InitializeComponent();
            dgLog.ItemsSource = _auditService.GetRecent(300);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
