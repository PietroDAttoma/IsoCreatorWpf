using DiscUtils.Iso9660;
using Microsoft.WindowsAPICodePack.Dialogs;
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
        private string? currentIsoPath; // memorizza l'ISO aperto

        public MainWindow()
        {
            InitializeComponent();

            // Default: Desktop come destinazione
            destFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            txtDestFolder.Text = destFolder;

            // Default: nome file ISO
            txtIsoName.Text = "OUTPUT";

            // Crea la root iniziale
            CreateIsoRoot();
        }

        private void CreateIsoRoot()
        {
            // 🔑 Data e ora attuale nel formato richiesto
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");

            // 🔑 StackPanel con icona + testo
            StackPanel rootPanel = new StackPanel { Orientation = Orientation.Horizontal };

            Image isoIcon = new Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0),
                Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/iso.png"))
            };

            TextBlock isoName = new TextBlock
            {
                Text = timestamp,
                VerticalAlignment = VerticalAlignment.Center
            };

            rootPanel.Children.Add(isoIcon);
            rootPanel.Children.Add(isoName);

            // 🔑 Root item con timestamp come Tag (utile per future operazioni)
            TreeViewItem rootItem = new TreeViewItem
            {
                Header = rootPanel,
                Tag = timestamp
            };

            // Se vuoi mantenere più root (una per ogni sessione), NON fare Clear()
            isoTreeView.Items.Clear();
            isoTreeView.Items.Add(rootItem);
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

                // 🔑 Usa la root già creata con CreateIsoRoot()
                if (isoTreeView.Items.Count > 0 && isoTreeView.Items[0] is TreeViewItem rootItem)
                {
                    rootItem.Items.Clear(); // svuota eventuali contenuti precedenti
                    AddDirectoryNodes(sourceFolder, rootItem); // aggiungi cartelle e file
                    rootItem.IsExpanded = true; // espandi la root
                }
            }
        }

        private void PopulateTreeView(string sourcePath)
        {
            isoTreeView.Items.Clear();

            // Nodo root con icona cartella
            TreeViewItem rootItem = CreateTreeViewItem(sourcePath, true);
            isoTreeView.Items.Add(rootItem);

            // Aggiungi contenuti
            AddDirectoryNodes(sourcePath, rootItem);
        }
        private void AddDirectoryNodes(string path, TreeViewItem parentItem)
        {
            try
            {
                // 🔑 Solo cartelle (ordinate alfabeticamente)
                var dirs = Directory.GetDirectories(path).OrderBy(d => Path.GetFileName(d).ToLower());
                foreach (var dir in dirs)
                {
                    TreeViewItem dirItem = CreateTreeViewItem(dir, true);
                    parentItem.Items.Add(dirItem);

                    // Ricorsione: aggiungi sottocartelle
                    AddDirectoryNodes(dir, dirItem);
                }

                // ❌ Niente file qui → i file verranno mostrati solo nella detailsListView
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante la lettura della cartella:\n{ex.Message}",
                                "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private TreeViewItem CreateTreeViewItem(string fullPath, bool isDir)
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal };

            string iconPath;
            if (isDir)
            {
                iconPath = "pack://application:,,,/Images/folder.png";
            }
            else
            {
                string ext = Path.GetExtension(fullPath).ToLower();
                switch (ext)
                {
                    case ".txt": iconPath = "pack://application:,,,/Images/txt.png"; break;
                    case ".doc":
                    case ".docx": iconPath = "pack://application:,,,/Images/doc.png"; break;
                    case ".xls":
                    case ".xlsx": iconPath = "pack://application:,,,/Images/xls.png"; break;
                    case ".pdf": iconPath = "pack://application:,,,/Images/pdf.png"; break;
                    case ".jpg":
                    case ".jpeg":
                    case ".bmp":
                    case ".tif":
                    case ".tiff": iconPath = "pack://application:,,,/Images/jpg.png"; break;
                    case ".rar":
                    case ".zip": iconPath = "pack://application:,,,/Images/rar.png"; break;
                    case ".iso": iconPath = "pack://application:,,,/Images/iso.png"; break;
                    default: iconPath = "pack://application:,,,/Images/file.png"; break;
                }
            }

            Image img = new Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0),
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath))
            };

            TextBlock tb = new TextBlock { Text = Path.GetFileName(fullPath) };

            sp.Children.Add(img);
            sp.Children.Add(tb);

            return new TreeViewItem
            {
                Header = sp,
                Tag = fullPath
            };
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

                        // 🔧 StackPanel con icona ISO + nome file
                        StackPanel rootPanel = new StackPanel { Orientation = Orientation.Horizontal };

                        Image isoIcon = new Image
                        {
                            Width = 16,
                            Height = 16,
                            Margin = new Thickness(0, 0, 5, 0),
                            Source = new System.Windows.Media.Imaging.BitmapImage(
                                new Uri("pack://application:,,,/Images/iso.png"))
                        };

                        TextBlock isoName = new TextBlock { Text = Path.GetFileName(currentIsoPath) };

                        rootPanel.Children.Add(isoIcon);
                        rootPanel.Children.Add(isoName);

                        // Root con icona + nome
                        TreeViewItem rootItem = new TreeViewItem
                        {
                            Header = rootPanel,
                            Tag = "" // puoi lasciare vuoto o usare "/" come root
                        };

                        isoTreeView.Items.Add(rootItem);

                        AddEntries(cd, "", rootItem);
                        ExpandFirstLevel(rootItem);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Errore durante la lettura ISO:\n{ex.Message}",
                                    "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void AddEntries(CDReader cd, string path, TreeViewItem parentItem)
        {
            foreach (var entry in cd.GetFileSystemEntries(path))
            {
                bool isDir = cd.GetAttributes(entry).HasFlag(FileAttributes.Directory);

                // Mostra SOLO le cartelle nella TreeView
                if (isDir)
                {
                    string iconPath = "pack://application:,,,/Images/folder.png";

                    StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new Image
                    {
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(0, 0, 5, 0),
                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath))
                    });

                    string displayName = Path.GetFileName(entry);
                    if (displayName.Contains(";"))
                        displayName = displayName.Substring(0, displayName.IndexOf(";"));

                    sp.Children.Add(new TextBlock { Text = displayName });

                    TreeViewItem item = new TreeViewItem
                    {
                        Header = sp,
                        Tag = entry
                    };

                    parentItem.Items.Add(item);

                    // Ricorsione solo per cartelle
                    AddEntries(cd, entry, item);
                }
            }
        }
        private void detailsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (detailsListView.SelectedItem is IsoEntry selectedItem && selectedItem.Type == "File")
            {
                try
                {
                    using (FileStream fs = new FileStream(currentIsoPath, FileMode.Open, FileAccess.Read))
                    {
                        CDReader cd = new CDReader(fs, true);

                        string entryPath = selectedItem.EntryPath; // percorso interno ISO
                        string displayName = selectedItem.Name;

                        // Cartella temporanea dedicata
                        string tempDir = Path.Combine(Path.GetTempPath(), "IsoExtract");
                        Directory.CreateDirectory(tempDir);

                        string tempPath = Path.Combine(tempDir, displayName);

                        // Evita conflitti di nome
                        if (File.Exists(tempPath))
                        {
                            string uniqueName = $"{Path.GetFileNameWithoutExtension(displayName)}_{Guid.NewGuid()}{Path.GetExtension(displayName)}";
                            tempPath = Path.Combine(tempDir, uniqueName);
                        }

                        using (Stream isoStream = cd.OpenFile(entryPath, FileMode.Open))
                        using (FileStream outStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                        {
                            isoStream.CopyTo(outStream);
                        }

                        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Errore durante l'apertura del file:\n{ex.Message}",
                                    "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        public class IsoEntry
        {
            public string Name { get; set; }
            public string Type { get; set; }   // "Cartella" o "File"
            public string Size { get; set; }   // Dimensione in KB
            public string Icon { get; set; }   // Percorso icona
            public string EntryPath { get; set; } // Percorso interno ISO
        }
        private void isoTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (isoTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string entryPath)
            {
                detailsListView.Items.Clear();

                // Caso ISO (currentIsoPath valorizzato e file esistente)
                if (!string.IsNullOrEmpty(currentIsoPath) && File.Exists(currentIsoPath))
                {
                    using (FileStream fs = new FileStream(currentIsoPath, FileMode.Open, FileAccess.Read))
                    {
                        CDReader cd = new CDReader(fs, true);

                        if (cd.GetAttributes(entryPath).HasFlag(FileAttributes.Directory))
                        {
                            var subEntries = cd.GetFileSystemEntries(entryPath);

                            var dirs = subEntries
                                .Where(se => cd.GetAttributes(se).HasFlag(FileAttributes.Directory))
                                .OrderBy(se => Path.GetFileName(se).ToLower());

                            var files = subEntries
                                .Where(se => !cd.GetAttributes(se).HasFlag(FileAttributes.Directory))
                                .OrderBy(se => Path.GetFileName(se).ToLower());

                            foreach (var subEntry in dirs.Concat(files))
                            {
                                bool isDir = cd.GetAttributes(subEntry).HasFlag(FileAttributes.Directory);

                                string displayName = Path.GetFileName(subEntry);
                                if (displayName.Contains(";"))
                                    displayName = displayName.Substring(0, displayName.IndexOf(";"));

                                long size = 0;
                                if (!isDir)
                                {
                                    using (Stream s = cd.OpenFile(subEntry, FileMode.Open))
                                    {
                                        size = s.Length;
                                    }
                                }

                                string sizeText = isDir ? "" : $"{Math.Ceiling(size / 1024.0)} KB";
                                string iconPath = isDir ? "pack://application:,,,/Images/folder.png"
                                                        : GetIconForExtension(Path.GetExtension(subEntry));

                                detailsListView.Items.Add(new IsoEntry
                                {
                                    Name = displayName,
                                    Type = isDir ? "Cartella" : "File",
                                    Size = sizeText,
                                    Icon = iconPath,
                                    EntryPath = subEntry
                                });
                            }
                        }
                    }
                }
                // Caso cartella sorgente (Directory)
                else if (Directory.Exists(entryPath))
                {
                    var dirs = Directory.GetDirectories(entryPath).OrderBy(d => Path.GetFileName(d).ToLower());
                    var files = Directory.GetFiles(entryPath).OrderBy(f => Path.GetFileName(f).ToLower());

                    foreach (var subEntry in dirs.Concat(files))
                    {
                        bool isDir = Directory.Exists(subEntry);

                        string displayName = Path.GetFileName(subEntry);
                        long size = !isDir ? new FileInfo(subEntry).Length : 0;

                        string sizeText = isDir ? "" : $"{Math.Ceiling(size / 1024.0)} KB";
                        string iconPath = isDir ? "pack://application:,,,/Images/folder.png"
                                                : GetIconForExtension(Path.GetExtension(subEntry));

                        detailsListView.Items.Add(new IsoEntry
                        {
                            Name = displayName,
                            Type = isDir ? "Cartella" : "File",
                            Size = sizeText,
                            Icon = iconPath,
                            EntryPath = subEntry
                        });
                    }
                }
            }
        }

        private string GetIconForExtension(string ext)
        {
            ext = ext.ToLower();
            switch (ext)
            {
                case ".txt": return "pack://application:,,,/Images/txt.png";
                case ".doc":
                case ".docx": return "pack://application:,,,/Images/doc.png";
                case ".xls":
                case ".xlsx": return "pack://application:,,,/Images/xls.png";
                case ".pdf": return "pack://application:,,,/Images/pdf.png";
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".tif":
                case ".tiff": return "pack://application:,,,/Images/jpg.png";
                case ".rar":
                case ".zip": return "pack://application:,,,/Images/rar.png";
                case ".iso": return "pack://application:,,,/Images/iso.png";
                default: return "pack://application:,,,/Images/file.png";
            }
        }

        private void ExpandFirstLevel(TreeViewItem rootItem)
        {
            rootItem.IsExpanded = true;
            foreach (TreeViewItem child in rootItem.Items)
                child.IsExpanded = true;
        }
    }
}