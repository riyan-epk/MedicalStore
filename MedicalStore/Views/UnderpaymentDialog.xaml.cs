using MedicalStore.Common.Helpers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MedicalStore.Views
{
    public enum UnderpaymentAction { AddToDues, AdjustSale, Cancelled }

    public partial class UnderpaymentDialog : Window
    {
        public UnderpaymentAction SelectedAction { get; private set; } = UnderpaymentAction.Cancelled;

        private bool _duesSelected = false;
        private bool _adjustSelected = false;

        private static readonly SolidColorBrush _activeGreen = new(Color.FromRgb(76, 175, 80));
        private static readonly SolidColorBrush _activeBlue = new(Color.FromRgb(25, 118, 210));
        private static readonly SolidColorBrush _inactive = new(Color.FromRgb(208, 208, 208));

        public UnderpaymentDialog(decimal billTotal, decimal amountReceived, decimal remainingAmount, bool isWalkIn = false)
        {
            InitializeComponent();

            lblBillTotal.Text = $"{(billTotal).FormatRs()}";
            lblReceived.Text = $"{(amountReceived).FormatRs()}";
            lblRemaining.Text = $"{(remainingAmount).FormatRs()}";

            if (isWalkIn)
            {
                if (borderDuesOption != null)
                    borderDuesOption.Visibility = Visibility.Collapsed;
                
                _duesSelected = false;
                _adjustSelected = true;
                SelectedAction = UnderpaymentAction.AdjustSale;
                btnConfirm.IsEnabled = true;

                Loaded += (_, _) => SetAdjustSelected();
            }
            else
            {
                _duesSelected = true;
                _adjustSelected = false;
                SelectedAction = UnderpaymentAction.AddToDues;
                btnConfirm.IsEnabled = true;

                Loaded += (_, _) => SetDuesSelected();
            }
        }

        private void DuesOption_Click(object sender, MouseButtonEventArgs e)
        {
            _duesSelected = true;
            _adjustSelected = false;
            SetDuesSelected();
            btnConfirm.IsEnabled = true;
        }

        private void SetDuesSelected()
        {
            borderDuesOption.BorderBrush = _activeBlue;
            borderDuesOption.Background = new SolidColorBrush(Color.FromRgb(227, 242, 253));
            chkDues.Text = "✓";

            borderAdjustOption.BorderBrush = _inactive;
            borderAdjustOption.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
            chkAdjust.Text = "";
        }

        private void AdjustOption_Click(object sender, MouseButtonEventArgs e)
        {
            _adjustSelected = true;
            _duesSelected = false;
            SetAdjustSelected();
            btnConfirm.IsEnabled = true;
        }

        private void SetAdjustSelected()
        {
            borderAdjustOption.BorderBrush = _activeGreen;
            borderAdjustOption.Background = new SolidColorBrush(Color.FromRgb(241, 248, 233));
            chkAdjust.Text = "✓";

            if (borderDuesOption != null)
            {
                borderDuesOption.BorderBrush = _inactive;
                borderDuesOption.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                chkDues.Text = "";
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (_duesSelected)
                SelectedAction = UnderpaymentAction.AddToDues;
            else if (_adjustSelected)
                SelectedAction = UnderpaymentAction.AdjustSale;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = UnderpaymentAction.Cancelled;
            DialogResult = false;
            Close();
        }
    }
}
