using System.Windows;
using VupisjkaWpf.Models;

namespace VupisjkaWpf.Windows
{
    public partial class AdvancedSearchWindow : Window
    {
        public AdvancedCriteria Criteria { get; private set; }

        public AdvancedSearchWindow(AdvancedCriteria? initial)
        {
            InitializeComponent();
            Criteria = initial ?? new AdvancedCriteria();

            PersonalBox.Text = Criteria.Personal;
            LastNameBox.Text = Criteria.LastName;
            FirstNameBox.Text = Criteria.FirstName;
            MiddleNameBox.Text = Criteria.MiddleName;

            ReqPersonalBox.IsChecked = Criteria.RequirePersonal;
            ReqLastNameBox.IsChecked = Criteria.RequireLastName;
            ReqFirstNameBox.IsChecked = Criteria.RequireFirstName;
            ReqMiddleNameBox.IsChecked = Criteria.RequireMiddleName;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var crit = new AdvancedCriteria
            {
                Personal = (PersonalBox.Text ?? string.Empty).Trim(),
                LastName = (LastNameBox.Text ?? string.Empty).Trim(),
                FirstName = (FirstNameBox.Text ?? string.Empty).Trim(),
                MiddleName = (MiddleNameBox.Text ?? string.Empty).Trim(),
                RequirePersonal = ReqPersonalBox.IsChecked == true,
                RequireLastName = ReqLastNameBox.IsChecked == true,
                RequireFirstName = ReqFirstNameBox.IsChecked == true,
                RequireMiddleName = ReqMiddleNameBox.IsChecked == true
            };

            if (!crit.IsActive)
            {
                MessageBox.Show("Нужно заполнить хотя бы одно поле.", "Расширенный поиск",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Criteria = crit;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
