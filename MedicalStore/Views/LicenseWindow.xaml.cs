using System.Windows;
using MedicalStore.BLL.Services;
using MedicalStore.Common.Constants;

namespace MedicalStore.Views
{
    public partial class LicenseWindow : Window
    {
        private readonly LicenseService _license;

        /// <summary>True once the user holds a usable license (activated or still in trial).</summary>
        public bool Activated { get; private set; }

        public LicenseWindow(LicenseService license, bool trialStillActive)
        {
            InitializeComponent();
            _license = license;
            lblBranding.Text = AppConstants.DeveloperBranding;

            var status = _license.Evaluate();
            lblStatus.Text = status.Message;

            // "Continue Trial" is only available while the trial has not ended.
            btnContinue.Visibility = trialStillActive ? Visibility.Visible : Visibility.Collapsed;
            Activated = trialStillActive;
        }

        private void Activate_Click(object sender, RoutedEventArgs e)
        {
            lblMsg.Visibility = Visibility.Collapsed;
            var key = txtKey.Text?.Trim() ?? "";
            var res = _license.Activate(key);
            if (res.Success)
            {
                Activated = true;
                MessageBox.Show(res.Message, "Activation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                lblMsg.Text = res.Message;
                lblMsg.Visibility = Visibility.Visible;
            }
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            Activated = true;      // still within the trial window
            DialogResult = true;
            Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Activated = false;
            DialogResult = false;
            Close();
        }
    }
}
