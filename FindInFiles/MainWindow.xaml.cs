// simple Find In Files tool for searching files containting given string
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FindInFiles
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string[] fileExtensions = new[] { "*.txt", "*.shader", "*.cs", "*.log", "*.js", "*.cging" };
        const int previewSnippetLength = 32;
        const int maxRecentItems = 32;

        public MainWindow()
        {
            InitializeComponent();
            Start();
        }

        void Start()
        {
            // get commandline params if any
            string[] args = Environment.GetCommandLineArgs();

            // have any args? first item is exe name, skip that
            if (args.Length > 1)
            {
                cmbFolder.Text = args[1];
            }

            // window size
            this.Width = Properties.Settings.Default.windowWidth;
            this.Height = Properties.Settings.Default.windowHeight;

            // search history
            //cmbSearch.Items.Add(Properties.Settings.Default.recentSearches.Cast<string[]>());
            cmbSearch.ItemsSource = Properties.Settings.Default.recentSearches;

            // focus on searchbox
            cmbSearch.Focus();

            // get close event, so can save settings
            Application.Current.MainWindow.Closing += new CancelEventHandler(OnWindowClose);
        }

        // force combobox text to be selected at start https://stackoverflow.com/q/31483650/5452781
        private void cmbSearch_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cmBox = (System.Windows.Controls.ComboBox)sender;
            var textBox = (cmBox.Template.FindName("PART_EditableTextBox",
                           cmBox) as TextBox);
            if (textBox != null)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        void OnWindowClose(object sender, CancelEventArgs e)
        {
            // save settings
            Properties.Settings.Default.windowWidth = (int)this.Width;
            Properties.Settings.Default.windowHeight = (int)this.Height;
            Properties.Settings.Default.Save();
        }

        private void btnFind_Click(object sender, RoutedEventArgs e)
        {
            Search(cmbSearch.Text, cmbFolder.Text);
        }

        void AddSearchHistory(string searchString)
        {
            // handle settings
            if (Properties.Settings.Default.recentSearches == null)
            {
                Properties.Settings.Default.recentSearches = new StringCollection();
            }

            // remove old items
            if (Properties.Settings.Default.recentSearches.Count > maxRecentItems)
            {
                Properties.Settings.Default.recentSearches.RemoveAt(0);
            }

            // skip if duplicate
            if (Properties.Settings.Default.recentSearches.Contains(searchString) == false)
            {
                Properties.Settings.Default.recentSearches.Add(searchString);
                Console.WriteLine("added");
            }
            Properties.Settings.Default.Save();

            // rebuild dropdown
            cmbSearch.ItemsSource = null;
            cmbSearch.ItemsSource = Properties.Settings.Default.recentSearches;
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            // TODO browse for folder
        }

        // special keys for search field
        private void OnKeyDownSearch(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    cmbSearch.Text = "";
                    break;
                case Key.Return:
                    Search(cmbSearch.Text, cmbFolder.Text);
                    break;
                default:
                    break;
            }
        }

        // open file on double click
        private void gridResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (gridResults.SelectedItem == null) return;
            var selectedRow = gridResults.SelectedItem as ResultItem;

            Process myProcess = new Process();
            myProcess.StartInfo.FileName = selectedRow.path;
            //myProcess.StartInfo.Arguments = "-n###" ;// TODO jump to line in notepad++, but need to know linenumber..
            myProcess.Start();
        }

        // main search method
        void Search(string searchString, string sourceFolder)
        {
            if (string.IsNullOrEmpty(searchString) == true) return;
            if (string.IsNullOrEmpty(sourceFolder) == true) return;

            AddSearchHistory(cmbSearch.Text);

            // validate folder
            if (Directory.Exists(sourceFolder) == false)
            {
                return;
            }

            int searchLen = searchString.Length;

            // get all files and subfolder files
            string[] files = fileExtensions.SelectMany(f => Directory.GetFiles(sourceFolder, f, SearchOption.AllDirectories)).ToArray();

            // search each file, if hit, add to results.. use threads later maybe
            var results = new List<ResultItem>();
            for (int i = 0, length = files.Length; i < length; i++)
            {
                // brute-search, measure later..
                string wholeFile = File.ReadAllText(files[i]);

                int hitIndex = wholeFile.IndexOf(searchString, StringComparison.OrdinalIgnoreCase);
                if (hitIndex > -1)
                {
                    Console.WriteLine(files[i] + " :" + hitIndex);
                    var o = new ResultItem();
                    o.path = files[i];
                    o.snippet = wholeFile.Substring(hitIndex, (previewSnippetLength + hitIndex >= wholeFile.Length) ? wholeFile.Length : previewSnippetLength);
                    o.snippet = o.snippet.Replace('\n', '¶'); // replace end of lines

                    // TODO get linenumber, or estimate?
                    // but that would mean have to parse again??
                    // maybe do this only if selected file.. (but that means need to read file again..)
                    // o.lineNumber = ...

                    results.Add(o);
                    continue;
                }
            }
            gridResults.ItemsSource = results;
        }

        // recent search item selected
        private void cmbSearch_DropDownClosed(object sender, EventArgs e)
        {
            Search(cmbSearch.Text, cmbFolder.Text);
        }
    }
}

public class ResultItem
{
    public string path { get; set; }
    public string snippet { get; set; }
    //    public int lineNumber { get; set; }
}


