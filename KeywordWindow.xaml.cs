using System;
using System.Linq;
using System.Windows;

namespace VupisjkaWpf.Windows
{
    public partial class KeywordWindow : Window
    {
        public string[] Keywords { get; private set; } = Array.Empty<string>();
        public bool CaseSensitive { get; private set; }

        public KeywordWindow()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Keywords = new[]
            {
                (Word1Box.Text ?? string.Empty).Trim(),
                (Word2Box.Text ?? string.Empty).Trim(),
                (Word3Box.Text ?? string.Empty).Trim()
            };

            CaseSensitive = CaseSensitiveBox.IsChecked == true;
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
