using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using VupisjkaWpf.Models;
using VupisjkaWpf.Services;
using Xceed.Words.NET;

namespace VupisjkaWpf.Windows
{
    public partial class MainWindow : Window
    {
        private AdvancedCriteria? _advancedCriteria;

        public MainWindow()
        {
            InitializeComponent();
            ThemeCombo.SelectedIndex = 0;
        }

        private void ThemeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            string? theme = (ThemeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
            string uri = "/Themes/Light.xaml";

            switch (theme)
            {
                case "Тёмная":
                    uri = "/Themes/Dark.xaml"; break;
                case "Синяя матрица":
                    uri = "/Themes/BlueMatrix.xaml"; break;
                case "Чёрно-зелёная":
                    uri = "/Themes/GreenMatrix.xaml"; break;
            }

            try
            {
                var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(dict);
            }
            catch { }
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Кратко:\n" +
                "1. Выберите таблицу с ФИО и личными номерами.\n" +
                "2. Укажите папку с приказами (.docx).\n" +
                "3. Укажите папку для сохранения выписок.\n" +
                "4. Заполните должность, звание и И.Фамилию заверителя.\n" +
                "5. Отметьте режимы поиска (200, зачисление, назначение и т.п.).\n" +
                "6. При необходимости настройте расширенный поиск и ключевые слова.\n" +
                "7. Нажмите 'Составить выписки'.\n\n" +
                "Программа найдёт совпадения по личным номерам/ФИО, определит пункт и параграф приказа, сформирует выписку" +
                " с шапкой 'ВЫПИСКА ИЗ ПРИКАЗА', пунктом и подписантами, а также блоком 'ВЕРНО: ...'.",
                "Справка",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void PickTable_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel|*.xlsx;*.xls|Все файлы|*.*"
            };
            if (dlg.ShowDialog(this) == true)
                TablePathBox.Text = dlg.FileName;
        }

        private void PickOrders_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
                OrdersFolderBox.Text = dlg.SelectedPath;
        }

        private void PickSave_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
                SaveFolderBox.Text = dlg.SelectedPath;
        }

        private void OpenSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(SaveFolderBox.Text) && Directory.Exists(SaveFolderBox.Text))
                    System.Diagnostics.Process.Start("explorer.exe", SaveFolderBox.Text);
            }
            catch { }
        }

        private void UseKeywords_Checked(object sender, RoutedEventArgs e)
        {
            BtnKeywords_Click(sender, e);
        }

        private void UseKeywords_Unchecked(object sender, RoutedEventArgs e)
        {
            SearchService.CustomKeywords = Array.Empty<string>();
            SearchService.CustomCaseSensitive = false;
        }

        private void BtnKeywords_Click(object sender, RoutedEventArgs e)
        {
            if (UseKeywordsBox.IsChecked != true)
            {
                MessageBox.Show("Сначала включите галочку 'По ключевым словам'.", "Ключевые слова",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new KeywordWindow();
            if (win.ShowDialog() == true)
            {
                SearchService.CustomKeywords = win.Keywords;
                SearchService.CustomCaseSensitive = win.CaseSensitive;
                Log($"Ключевые слова: {string.Join(", ", win.Keywords.Where(s => !string.IsNullOrWhiteSpace(s)))}");
            }
        }

        private void AdvancedSearch_Click(object sender, RoutedEventArgs e)
        {
            var win = new AdvancedSearchWindow(_advancedCriteria);
            if (win.ShowDialog() == true)
            {
                _advancedCriteria = win.Criteria;
                Log("Расширенный поиск включён.");
            }
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RunButton.IsEnabled = false;
                Log("Старт поиска...");

                if (!File.Exists(TablePathBox.Text))
                    throw new Exception("Не выбрана таблица.");
                if (!Directory.Exists(OrdersFolderBox.Text))
                    throw new Exception("Не выбрана папка с приказами.");
                if (!Directory.Exists(SaveFolderBox.Text))
                    throw new Exception("Не выбрана папка для сохранения.");
                if (string.IsNullOrWhiteSpace(ApproverPositionBox.Text) ||
                    string.IsNullOrWhiteSpace(ApproverRankBox.Text) ||
                    string.IsNullOrWhiteSpace(ApproverNameBox.Text))
                    throw new Exception("Заполните должность, звание и И.Фамилию заверителя.");

                var people = ExcelService.ReadPeople(TablePathBox.Text);
                if (people.Count == 0)
                    throw new Exception("В таблице нет данных.");

                var modes = new SearchModes
                {
                    Mode200 = Mode200Box.IsChecked == true,
                    ModeEnlist = ModeEnlistBox.IsChecked == true,
                    ModeDisposition = ModeDispositionBox.IsChecked == true,
                    ModeAppointment = ModeAppointmentBox.IsChecked == true,
                    ModeDismissed = ModeDismissedBox.IsChecked == true,
                    UseKeywords = UseKeywordsBox.IsChecked == true,
                    AlwaysPreview = false
                };

                var opt = new ExtractOptions
                {
                    SeparateDocs = SeparateDocsBox.IsChecked == true,
                    Recursive = RecursiveBox.IsChecked == true,
                    ApproverPosition = ApproverPositionBox.Text.Trim(),
                    ApproverRank = ApproverRankBox.Text.Trim(),
                    ApproverNameShort = ApproverNameBox.Text.Trim()
                };

                var searchOpt = opt.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var docFiles = Directory.GetFiles(OrdersFolderBox.Text, "*.docx", searchOpt).ToList();
                if (docFiles.Count == 0)
                    throw new Exception("В папке с приказами нет файлов .docx.");

                Log($"Найдено приказов: {docFiles.Count}.");

                string commonPath = Path.Combine(SaveFolderBox.Text, "Выписки.docx");
                DocX? commonDoc = opt.SeparateDocs ? null : DocxService.CreateOrOpen(commonPath);

                int built = 0;

                foreach (var person in people)
                {
                    if (!SearchService.TryNormalizePersonal(person.Personal, out var normalized))
                        continue;

                    bool doneForThisPerson = false;

                    foreach (var file in docFiles)
                    {
                        if (doneForThisPerson) break;

                        var lines = DocxService.ReadAllLines(file);
                        if (lines.Count == 0) continue;

                        var hitIdxs = SearchService.FindPersonalHits(lines, normalized).ToList();
                        if (hitIdxs.Count == 0) continue;

                        foreach (var idx in hitIdxs)
                        {
                            var (pointNo, block) = SearchService.ExtractPointBlock(lines, idx);
                            if (!SearchService.MatchAdvancedCriteria(block, _advancedCriteria))
                                continue;

                            var (kwFound, kwList, modesList) = SearchService.MatchKeywords(block, modes);

                            if (modes.Any && modes.UseKeywords && !kwFound)
                                continue;

                            var secNo = SearchService.FindSectionNo(lines, idx) ?? string.Empty;

                            void AppendNow()
                            {
                                if (opt.SeparateDocs)
                                {
                                    var path = Path.Combine(SaveFolderBox.Text,
                                        $"{SafeFileName(person.Fio)}_{normalized}.docx");
                                    using var one = DocX.Create(path);
                                    DocxService.AppendExtract(one, lines, Path.GetFileName(file), secNo, pointNo, block, opt);
                                    one.Save();
                                }
                                else
                                {
                                    DocxService.AppendExtract(commonDoc!, lines, Path.GetFileName(file), secNo, pointNo, block, opt);
                                }

                                built++;
                                doneForThisPerson = true;
                            }

                            AppendNow();
                            break;
                        }
                    }
                }

                if (!opt.SeparateDocs && commonDoc != null)
                    commonDoc.Save();

                Log($"Готово. Сформировано выписок: {built}.");
                MessageBox.Show($"Сформировано выписок: {built}.", "Готово",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Log("Ошибка: " + ex.Message);
            }
            finally
            {
                RunButton.IsEnabled = true;
            }
        }

        private void Log(string msg)
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
            LogBox.ScrollToEnd();
        }

        private static string SafeFileName(string name)
            => string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
