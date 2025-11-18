using DiscUtils.Iso9660;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace IsoCreatorWpf
{
    public partial class MainWindow : Window
    {
        private string sourceFolder = string.Empty;
        private string destFolder = string.Empty;
        private string currentIsoPath; // memorizza l'ISO aperto

        public MainWindow()
        {
            InitializeComponent();

            // Default: Desktop come destinazione
            destFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            txtDestFolder.Text = destFolder;

            // Default: nome file ISO
            txtIsoName.Text = "OUTPUT";
        }

        private void btnSelectSource_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Seleziona la cartella da includere nell'ISO"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                sourceFolder = dialog.FileName;
                txtSourceFolder.Text = sourceFolder;
            }
        }

        private void btnSelectDest_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Seleziona la cartella di destinazione per il file ISO"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                destFolder = dialog.FileName;
                txtDestFolder.Text = destFolder;
            }
        }

        private async void btnCreateIso_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                MessageBox.Show("Seleziona prima una cartella sorgente valida!", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(destFolder) || !Directory.Exists(destFolder))
            {
                MessageBox.Show("Seleziona una cartella di destinazione valida!", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string isoName = string.IsNullOrWhiteSpace(txtIsoName.Text) ? "OUTPUT" : txtIsoName.Text.Trim();
            string isoPath = Path.Combine(destFolder, isoName + ".iso");

            btnCreateIso.IsEnabled = false;
            progressBar.Value = 0;

            try
            {
                await Task.Run(() =>
                {
                    using (FileStream fs = new FileStream(isoPath, FileMode.Create, FileAccess.Write))
                    {
                        CDBuilder builder = new CDBuilder
                        {
                            UseJoliet = true,
                            VolumeIdentifier = isoName
                        };

                        string rootName = Path.GetFileName(sourceFolder);
                        if (string.IsNullOrEmpty(rootName))
                            rootName = "ROOT";

                        builder.AddDirectory(rootName);

                        var directories = Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories);
                        foreach (var dir in directories)
                        {
                            string relativeDir = Path.GetRelativePath(sourceFolder, dir);
                            string isoDirPath = Path.Combine(rootName, relativeDir);
                            builder.AddDirectory(isoDirPath);
                        }

                        var files = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
                        int total = files.Length;
                        int count = 0;

                        foreach (var file in files)
                        {
                            string relativePath = Path.GetRelativePath(sourceFolder, file);
                            string isoFilePath = Path.Combine(rootName, relativePath);
                            builder.AddFile(isoFilePath, file);

                            count++;
                            int progress = (int)((double)count / total * 100);

                            // Aggiorna la ProgressBar in modo thread-safe
                            Dispatcher.Invoke(() => progressBar.Value = progress);
                        }

                        builder.Build(fs);
                    }
                });

                FileInfo fi = new FileInfo(isoPath);
                if (fi.Exists && fi.Length > 0)
                {
                    MessageBox.Show($"ISO creato con successo!\nPercorso: {isoPath}", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("ISO creato ma risulta vuoto o mancante.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante la creazione ISO:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCreateIso.IsEnabled = true;
                progressBar.Value = 0; // puoi lasciarla a 100 se vuoi mostrare completamento
            }
        }

        private void btnOpenIso_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                Title = "Seleziona un file ISO",
                DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Filters = { new CommonFileDialogFilter("File ISO", "*.iso") }
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                currentIsoPath = dialog.FileName;

                try
                {
                    using (FileStream fs = new FileStream(currentIsoPath, FileMode.Open, FileAccess.Read))
                    {
                        CDReader cd = new CDReader(fs, true);

                        isoTreeView.Items.Clear();

                        TreeViewItem rootItem = new TreeViewItem { Header = Path.GetFileName(currentIsoPath), Tag = "" };
                        isoTreeView.Items.Add(rootItem);

                        AddEntries(cd, "", rootItem);

                        ExpandFirstLevel(rootItem);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Errore durante la lettura ISO:\n{ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddEntries(CDReader cd, string path, TreeViewItem parentItem)
        {
            foreach (var entry in cd.GetFileSystemEntries(path))
            {
                bool isDir = cd.GetAttributes(entry).HasFlag(FileAttributes.Directory);

                // Determina l'icona in base al tipo
                string iconPath;
                if (isDir)
                {
                    iconPath = "pack://application:,,,/Images/folder.png";
                }
                else
                {
                    string ext = Path.GetExtension(entry).ToLower().Split(';')[0];
                    switch (ext)
                    {
                        case ".pdf":
                            iconPath = "pack://application:,,,/Images/pdf.png";
                            break;
                        case ".doc":
                        case ".docx":
                            iconPath = "pack://application:,,,/Images/doc.png";
                            break;
                        case ".xls":
                        case ".xlsx":
                            iconPath = "pack://application:,,,/Images/xls.png";
                            break;
                        case ".txt":
                            iconPath = "pack://application:,,,/Images/txt.png";
                            break;
                        case ".rar":
                        case ".zip":
                            iconPath = "pack://application:,,,/Images/rar.png";
                            break;
                        case ".jpg":
                        case ".tif":
                        case ".bmp":
                            iconPath = "pack://application:,,,/Images/jpg.png";
                            break;
                        default:
                            iconPath = "pack://application:,,,/Images/file.png"; // icona generica
                            break;
                    }
                }

                // StackPanel con icona + testo
                StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal };
                Image icon = new Image
                {
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 5, 0),
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath))
                };

                // Nome file/cartella ripulito da ;1
                string displayName = Path.GetFileName(entry);
                if (displayName.Contains(";"))
                {
                    displayName = displayName.Substring(0, displayName.IndexOf(";"));
                }

                sp.Children.Add(icon);
                sp.Children.Add(new TextBlock { Text = displayName });

                // 🔑 Salva nel Tag il percorso interno dell’ISO
                TreeViewItem item = new TreeViewItem
                {
                    Header = sp,
                    Tag = entry
                };

                parentItem.Items.Add(item);

                // Se è una directory, esplora ricorsivamente
                if (isDir)
                {
                    AddEntries(cd, entry, item);
                }
            }
        }

        private void isoTreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (isoTreeView.SelectedItem is TreeViewItem selectedItem)
            {
                if (selectedItem.Tag is string entryPath)
                {
                    using (FileStream fs = new FileStream(currentIsoPath, FileMode.Open, FileAccess.Read))
                    {
                        CDReader cd = new CDReader(fs, true);

                        // Se non è una directory, estrai e apri
                        if (!cd.GetAttributes(entryPath).HasFlag(FileAttributes.Directory))
                        {
                            string displayName = Path.GetFileName(entryPath);
                            if (displayName.Contains(";"))
                                displayName = displayName.Substring(0, displayName.IndexOf(";"));

                            string tempPath = Path.Combine(Path.GetTempPath(), displayName);

                            using (Stream isoStream = cd.OpenFile(entryPath, FileMode.Open))
                            using (FileStream outStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                            {
                                isoStream.CopyTo(outStream);
                            }

                            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                        }
                    }
                }
            }
        }

        private void ExpandFirstLevel(TreeViewItem rootItem)
        {
            // Espandi la root
            rootItem.IsExpanded = true;

            // Espandi solo i figli diretti (primo livello)
            foreach (TreeViewItem child in rootItem.Items)
            {
                child.IsExpanded = true;
            }
        }
    }
}