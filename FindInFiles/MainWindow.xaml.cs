// simple Find In Files tool for searching files containting given string
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace FindInFiles
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string[] fileExtensions = new[] { "*.txt", "*.shader", "*.cs", "*.log", "*.js", "*.cging" };
        const int previewSnippetLength = 32;

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
                txtFolder.Text = args[1];
            }

        }

        private void btnFind_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtString.Text) == false)
            {
                SearchFiles(txtString.Text, txtFolder.Text);
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            // TODO browse for folder
        }

        void SearchFiles(string searchString, string sourceFolder)
        {
            // validate folder
            if (Directory.Exists(sourceFolder)==false)
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
                    results.Add(o);
                    continue;
                }
            }
            gridResults.ItemsSource = results;
        }
    }
}

public class ResultItem
{
    public string path { get; set; }
    public string snippet { get; set; }
}


