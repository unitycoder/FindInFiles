// simple Find In Files tool for searching files containting given string
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinForms = System.Windows.Forms;

namespace FindInFiles
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string[] fileExtensions;
        const int previewSnippetLength = 32;
        const int maxRecentItems = 32;
        bool isSearching = false;

        public MainWindow()
        {
            InitializeComponent();
            Start();
        }

        void Start()
        {
            // window size
            this.Width = Properties.Settings.Default.windowWidth;
            this.Height = Properties.Settings.Default.windowHeight;

            // get extensions
            txtExtensions.Text = Properties.Settings.Default.extensionList;
            RefreshExtensionsList();

            // restore search history
            cmbSearch.ItemsSource = Properties.Settings.Default.recentSearches;
            cmbFolder.ItemsSource = Properties.Settings.Default.recentFolders;

            // select first item
            cmbFolder.SelectedIndex = 0;

            // get commandline params if any
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                cmbFolder.Text = args[1].Replace("\"", "");
            }


            // focus on searchbox
            cmbSearch.Focus();

            // get close event, so can save settings
            Application.Current.MainWindow.Closing += new CancelEventHandler(OnWindowClose);
            // keypress events
            Application.Current.MainWindow.KeyDown += new KeyEventHandler(KeyDownEventX);
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

        // save settings on exit
        void OnWindowClose(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.windowWidth = (int)this.Width;
            Properties.Settings.Default.windowHeight = (int)this.Height;
            Properties.Settings.Default.extensionList = txtExtensions.Text;
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

            // remove old items if too many
            if (Properties.Settings.Default.recentSearches.Count > maxRecentItems)
            {
                Console.WriteLine("too many items, removing " + Properties.Settings.Default.recentSearches[0]);
                Properties.Settings.Default.recentSearches.RemoveAt(0);
            }

            // skip if duplicate already in list
            if (Properties.Settings.Default.recentSearches.Contains(searchString) == false)
            {
                Properties.Settings.Default.recentSearches.Add(searchString);
            }
            Properties.Settings.Default.Save();

            // rebuild dropdown
            cmbSearch.ItemsSource = null;
            cmbSearch.ItemsSource = Properties.Settings.Default.recentSearches;
        }

        void AddFolderHistory(string folderString)
        {
            // handle settings
            if (Properties.Settings.Default.recentFolders == null)
            {
                Properties.Settings.Default.recentFolders = new StringCollection();
            }

            // remove old items
            if (Properties.Settings.Default.recentFolders.Count > maxRecentItems)
            {
                Properties.Settings.Default.recentFolders.RemoveAt(0);
            }

            // skip if duplicate
            if (Properties.Settings.Default.recentFolders.Contains(folderString) == false)
            {
                Properties.Settings.Default.recentFolders.Add(folderString);
            }
            Properties.Settings.Default.Save();

            // rebuild dropdown
            cmbFolder.ItemsSource = null;
            cmbFolder.ItemsSource = Properties.Settings.Default.recentFolders;
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            // Create Folder Browser and give it a title
            WinForms.FolderBrowserDialog folderBrowser = new WinForms.FolderBrowserDialog();
            folderBrowser.Description = "Select a foooooolder";

            // show it to the user
            folderBrowser.ShowDialog();

            // retrieve the input
            cmbFolder.Text = folderBrowser.SelectedPath;
        }

        // special keys for search field
        private void OnKeyDownSearch(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    ((ComboBox)sender).Text = "";
                    break;
                case Key.Return:
                    Search(cmbSearch.Text, cmbFolder.Text);
                    break;
                default:
                    break;
            }
        }

        // special keys for folder field
        private void OnKeyDownFolder(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    ((ComboBox)sender).Text = "";
                    break;
                case Key.Return:
                    AddFolderHistory(((ComboBox)sender).Text);
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

        Thread searchThread;
        public struct SearchParams
        {
            public string searchString;
            public string searchFolder;
        }

        // main search method
        void Search(string searchString, string sourceFolder)
        {
            if (string.IsNullOrEmpty(searchString) == true) return;
            if (string.IsNullOrEmpty(sourceFolder) == true) return;
            // min search length
            int searchLen = searchString.Length;
            if (searchLen < 2) return;
            AddSearchHistory(cmbSearch.Text);
            AddFolderHistory(cmbFolder.Text);

            // validate folder
            if (Directory.Exists(sourceFolder) == false) return;

            // start thread
            ParameterizedThreadStart start = new ParameterizedThreadStart(SearchLoop);
            searchThread = new Thread(start);
            searchThread.IsBackground = true;
            var searchParams = new SearchParams();
            searchParams.searchString = searchString;
            searchParams.searchFolder = sourceFolder;
            searchThread.Start(searchParams);

        }

        void SearchLoop(System.Object a)
        {
            var pars = (SearchParams)a;
            string searchString = pars.searchString;
            string sourceFolder = pars.searchFolder;

            // get all files and subfolder files
            string[] files = fileExtensions.SelectMany(f => Directory.GetFiles(sourceFolder, f, SearchOption.AllDirectories)).ToArray();

            // search each file, if hit, add to results
            isSearching = true;
            var results = new List<ResultItem>();
            for (int i = 0, length = files.Length; i < length; i++)
            {
                if (isSearching == false)
                {
                    break;
                }
                // brute-search, measure later..
                string wholeFile = File.ReadAllText(files[i]);

                int hitIndex = wholeFile.IndexOf(searchString, StringComparison.OrdinalIgnoreCase);
                if (hitIndex > -1)
                {
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

            Dispatcher.Invoke(() =>
            {
                gridResults.ItemsSource = results;
            });
        }

        // recent search item selected
        private void cmbSearch_DropDownClosed(object sender, EventArgs e)
        {
            Search(cmbSearch.Text, cmbFolder.Text);
        }


        void KeyDownEventX(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Console.WriteLine("Search cancelled");
                    isSearching = false;
                    break;
                default:
                    break;
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            isSearching = false;
        }

        private void RefreshExtensionsList()
        {
            var newExtensions = txtExtensions.Text.Split('|');
            if (newExtensions != null && newExtensions.Length > 0)
            {
                fileExtensions = newExtensions;
            }
            else // use default list if nothing else
            {
                fileExtensions = new[] { "*.txt", "*.shader", "*.cs", "*.log", "*.js", "*.cginc", "*.rtf" };
            }
        }

        private void OnLostFocusExtensions(object sender, EventArgs e)
        {
            RefreshExtensionsList();
        }

        // enter pressed in extensions textbox
        private void txtExtensions_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RefreshExtensionsList();
                cmbSearch.Focus();
            }
        }

    } // class
} // namespace

public class ResultItem
{
    public string path { get; set; }
    public string snippet { get; set; }
    //    public int lineNumber { get; set; }
}


