using DiscUtils.Iso9660;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IsoCreatorWpf
{
    public partial class MainWindow : Window
    {
        private string sourceFolder = string.Empty;
        private string destFolder = string.Empty;
        private string? currentIsoPath; // memorizza l'ISO aperto
        private TextBlock isoNameBlock; // 🔑 riferimento al TextBlock della root

        public MainWindow()
        {
            InitializeComponent();

            // Default: Desktop come destinazione
            destFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            txtDestFolder.Text = destFolder;

            // Crea la root iniziale
            CreateIsoRoot();

            // All’avvio, txtIsoName mostra lo stesso testo della root
            txtIsoName.Text = isoNameBlock.Text;

            // Collega gli eventi
            txtIsoName.TextChanged += TxtIsoName_TextChanged;
            txtIsoName.LostFocus += TxtIsoName_LostFocus;
        }

        private void CreateIsoRoot()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");

            StackPanel rootPanel = new StackPanel { Orientation = Orientation.Horizontal };

            Image isoIcon = new Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0),
                Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/iso.png"))
            };

            isoNameBlock = new TextBlock
            {
                Text = timestamp, // 🔑 inizialmente timestamp
                VerticalAlignment = VerticalAlignment.Center
            };

            rootPanel.Children.Add(isoIcon);
            rootPanel.Children.Add(isoNameBlock);

            TreeViewItem rootItem = new TreeViewItem
            {
                Header = rootPanel,
                Tag = timestamp
            };

            isoTreeView.Items.Clear();
            isoTreeView.Items.Add(rootItem);
        }

        private void TxtIsoName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isoNameBlock != null)
            {
                isoNameBlock.Text = txtIsoName.Text; // sincronizza root con TextBox
            }
        }

        private void TxtIsoName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtIsoName.Text))
            {
                // 🔑 Genera un nuovo timestamp se il campo è vuoto
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                txtIsoName.Text = timestamp;
                isoNameBlock.Text = timestamp;
            }
        }

        // 🔎 Funzione di supporto per formattare la dimensione in KB, MB o GB
        private string FormatSize(long sizeInBytes)
        {
            double sizeKB = sizeInBytes / 1024.0;

            if (sizeKB < 1024)
            {
                // Mostra in KB
                return $"{Math.Ceiling(sizeKB)} KB";
            }

            double sizeMB = sizeKB / 1024.0;
            if (sizeMB < 1024)
            {
                // Mostra in MB
                return $"{sizeMB:F2} MB";
            }

            double sizeGB = sizeMB / 1024.0;
            // Mostra in GB
            return $"{sizeGB:F2} GB";
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

                // 🔑 Calcola la dimensione totale della cartella sorgente
                long sizeBytes = GetDirectorySize(new DirectoryInfo(sourceFolder));

                // Aggiorna la TextBox txtTotalSize con formattazione KB/MB/GB
                txtTotalSize.Text = FormatSize(sizeBytes);

                // 🔑 Calcola la percentuale rispetto a un DVD-5 (4,7 GB decimali)
                const long dvd5CapacityBytes = 4700000000; // 4,7 GB
                double percent = (double)sizeBytes / dvd5CapacityBytes * 100.0;

                // 🔑 Aggiorna la ProgressBarSize e il testo sovrapposto
                progressBarSize.Value = Math.Min(percent, 100); // max 100 per non uscire dai limiti
                progressBarSizeText.Text = $"{percent:F2}% di DVD 4,7GB";

                // 🔑 Aggiorna anche la ProgressBar della cartella e il suo testo
                progressBar.Value = Math.Min(percent, 100);
                progressBarText.Text = $"{percent:F2}%";

                // ⚠️ Avviso se supera la capacità del DVD-5
                if (percent > 100.0)
                {
                    MessageBox.Show("Attenzione: la cartella selezionata supera la capacità di un DVD-5 (4,7 GB). " +
                                    "È necessario un supporto dual-layer.",
                                    "Capacità superata", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                if (isoTreeView.Items.Count > 0 && isoTreeView.Items[0] is TreeViewItem rootItem)
                {
                    rootItem.Items.Clear(); // svuota eventuali contenuti precedenti

                    // 🔑 Nodo per la cartella sorgente (Root)
                    TreeViewItem sourceNode = CreateTreeViewItem(sourceFolder, true);

                    // 🔑 Aggiungi le sottocartelle dentro la cartella sorgente
                    AddDirectoryNodes(sourceFolder, sourceNode);

                    // 🔑 Inserisci la cartella sorgente come figlio della root ISO
                    rootItem.Items.Add(sourceNode);

                    rootItem.IsExpanded = true;
                    sourceNode.IsExpanded = true;
                }
            }
        }

        // 🔎 Funzione di supporto per calcolare la dimensione totale di una cartella locale
        private long GetDirectorySize(DirectoryInfo dir)
        {
            long size = 0;

            // Scorre tutti i file presenti direttamente nella cartella
            foreach (FileInfo fi in dir.GetFiles())
            {
                // Somma la dimensione (in byte) di ciascun file
                size += fi.Length;
            }

            // Scorre tutte le sottocartelle presenti nella cartella
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                // Richiama ricorsivamente la funzione per calcolare la dimensione delle sottocartelle
                size += GetDirectorySize(subDir);
            }

            // Restituisce la dimensione totale (in byte) della cartella e delle sue sottocartelle
            return size;
        }

        // 🔎 Funzione di supporto per calcolare la dimensione totale di una cartella interna ad un file ISO
        private long GetIsoDirectorySize(CDReader cd, string path)
        {
            long size = 0;

            // Scorre tutti i file presenti nella cartella ISO specificata
            foreach (var file in cd.GetFiles(path))
            {
                // Apre lo stream del file per leggere la sua lunghezza
                using (Stream s = cd.OpenFile(file, FileMode.Open))
                {
                    // Somma la dimensione (in byte) del file
                    size += s.Length;
                }
            }

            // Scorre tutte le sottocartelle presenti nella cartella ISO
            foreach (var dir in cd.GetDirectories(path))
            {
                // Richiama ricorsivamente la funzione per calcolare la dimensione delle sottocartelle ISO
                size += GetIsoDirectorySize(cd, dir);
            }

            // Restituisce la dimensione totale (in byte) della cartella ISO e delle sue sottocartelle
            return size;
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
            progressBar.IsIndeterminate = false;

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

                            // 🔑 Aggiorna la ProgressBar con percentuale
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                progressBar.IsIndeterminate = false;
                                progressBar.Value = progress;
                            }));
                        }

                        // 🔑 Attiva ProgressBar indeterminata durante la fase finale
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            progressBar.IsIndeterminate = true;
                        }));

                        builder.Build(fs);

                        // 🔑 Disattiva indeterminata e porta a 100%
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            progressBar.IsIndeterminate = false;
                            progressBar.Value = 100;
                        }));
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
                progressBar.IsIndeterminate = false;
                progressBar.Value = 0; // oppure lasciala a 100 per mostrare completamento
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
                // 🔑 Azzeriamo la cartella sorgente ogni volta che apriamo un ISO
                sourceFolder = string.Empty;
                txtSourceFolder.Text = string.Empty;

                currentIsoPath = dialog.FileName;

                try
                {
                    FileInfo fi = new FileInfo(currentIsoPath);
                    long isoSizeBytes = fi.Length;

                    // Aggiorna dimensione totale
                    txtTotalSize.Text = FormatSize(isoSizeBytes);

                    // 🔑 Calcola percentuale rispetto a DVD-5
                    const long dvd5CapacityBytes = 4700000000; // 4,7 GB
                    double percentIso = (double)isoSizeBytes / dvd5CapacityBytes * 100.0;

                    // 🔑 Aggiorna ProgressBar ISO e testo sovrapposto
                    progressBarSize.Value = Math.Min(percentIso, 100);
                    progressBarSizeText.Text = $"{percentIso:F2}% di DVD 4,7GB";

                    // 🔑 Colori dinamici della barra ISO
                    if (percentIso <= 80.0)
                    {
                        progressBarSize.Foreground = new SolidColorBrush(Colors.Green);
                    }
                    else if (percentIso <= 100.0)
                    {
                        progressBarSize.Foreground = new SolidColorBrush(Colors.Orange);
                    }
                    else
                    {
                        progressBarSize.Foreground = new SolidColorBrush(Colors.Red);
                    }

                    // ⚠️ Avviso se supera la capacità
                    if (percentIso > 100.0)
                    {
                        MessageBox.Show("Attenzione: l'ISO supera la capacità di un DVD-5 (4,7 GB). " +
                                        "È necessario un supporto dual-layer.",
                                        "Capacità superata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    // 🔑 Aggiorna anche la ProgressBar della cartella se esiste una sorgente
                    if (!string.IsNullOrEmpty(sourceFolder) && Directory.Exists(sourceFolder))
                    {
                        long folderSizeBytes = GetDirectorySize(new DirectoryInfo(sourceFolder));
                        txtFolderSize.Text = FormatSize(folderSizeBytes);

                        double percentFolder = (double)folderSizeBytes / isoSizeBytes * 100.0;
                        progressBar.Value = Math.Min(percentFolder, 100);
                        progressBarText.Text = $"{percentFolder:F2}%";

                        // Colori dinamici della barra cartella
                        if (percentFolder <= 80.0)
                        {
                            progressBar.Foreground = new SolidColorBrush(Colors.Green);
                        }
                        else if (percentFolder <= 100.0)
                        {
                            progressBar.Foreground = new SolidColorBrush(Colors.Orange);
                        }
                        else
                        {
                            progressBar.Foreground = new SolidColorBrush(Colors.Red);
                        }
                    }

                    using (FileStream fs = new FileStream(currentIsoPath, FileMode.Open, FileAccess.Read))
                    {
                        CDReader cd = new CDReader(fs, true);

                        isoTreeView.Items.Clear();

                        // Root ISO (icona + nome file)
                        StackPanel rootPanel = new StackPanel { Orientation = Orientation.Horizontal };
                        rootPanel.Children.Add(new Image
                        {
                            Width = 16,
                            Height = 16,
                            Margin = new Thickness(0, 0, 5, 0),
                            Source = new System.Windows.Media.Imaging.BitmapImage(
                                new Uri("pack://application:,,,/Images/iso.png"))
                        });
                        rootPanel.Children.Add(new TextBlock { Text = Path.GetFileName(currentIsoPath) });

                        TreeViewItem rootItem = new TreeViewItem
                        {
                            Header = rootPanel,
                            Tag = "" // "" = root path dell’ISO
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

        // Evento scatenato quando l'utente seleziona un nodo nel TreeView (isoTreeView)
        private void isoTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (isoTreeView.SelectedItem is TreeViewItem selectedItem)
            {
                detailsListView.Items.Clear();

                string entryPath = selectedItem.Tag as string;

                // 🔎 Caso: root del TreeView
                if (selectedItem == isoTreeView.Items[0])
                {
                    // Caso cartella sorgente (root)
                    if (!string.IsNullOrEmpty(sourceFolder) && Directory.Exists(sourceFolder))
                    {
                        ShowContents(sourceFolder);

                        long totalFolderSize = GetDirectorySize(new DirectoryInfo(sourceFolder));
                        txtFolderSize.Text = FormatSize(totalFolderSize);

                        // Root = 100% della cartella locale
                        progressBar.Value = 100;
                        progressBarText.Text = "100,00%";
                        return;
                    }

                    // Caso ISO (root)
                    if (!string.IsNullOrEmpty(currentIsoPath) && File.Exists(currentIsoPath))
                    {
                        using (FileStream fs = new FileStream(currentIsoPath, FileMode.Open, FileAccess.Read))
                        {
                            CDReader cd = new CDReader(fs, true);
                            ShowContents("", cd);
                        }

                        FileInfo fi = new FileInfo(currentIsoPath);
                        long isoSizeBytes = fi.Length;
                        txtFolderSize.Text = FormatSize(isoSizeBytes);

                        // Root = 100% dell’ISO
                        progressBar.Value = 100;
                        progressBarText.Text = "100,00%";
                        return;
                    }
                }

                // 🔎 Caso: nodo normale
                if (!string.IsNullOrEmpty(entryPath))
                {
                    // Caso ISO → percorsi interni NON hanno ":" (es. "\Piano di Studi")
                    if (!string.IsNullOrEmpty(currentIsoPath) && File.Exists(currentIsoPath) && !entryPath.Contains(":"))
                    {
                        using (FileStream fs = new FileStream(currentIsoPath, FileMode.Open, FileAccess.Read))
                        {
                            CDReader cd = new CDReader(fs, true);
                            ShowContents(entryPath, cd);

                            long folderSizeBytes = GetIsoDirectorySize(cd, entryPath);
                            txtFolderSize.Text = FormatSize(folderSizeBytes);

                            // 🔑 Percentuale della cartella rispetto alla dimensione totale ISO
                            FileInfo fi = new FileInfo(currentIsoPath);
                            long isoSizeBytes = fi.Length;
                            double percentFolder = (double)folderSizeBytes / isoSizeBytes * 100.0;

                            progressBar.Value = Math.Min(percentFolder, 100);
                            progressBarText.Text = $"{percentFolder:F2}%";
                        }
                    }
                    // Caso cartella locale → percorsi Windows hanno ":" (es. "C:\\Users\\...")
                    else if (Directory.Exists(entryPath))
                    {
                        ShowContents(entryPath);

                        long folderSizeBytes = GetDirectorySize(new DirectoryInfo(entryPath));
                        txtFolderSize.Text = FormatSize(folderSizeBytes);

                        // 🔑 Percentuale della cartella selezionata rispetto alla dimensione totale della cartella sorgente
                        long totalFolderSize = GetDirectorySize(new DirectoryInfo(sourceFolder));
                        double percentFolder = (double)folderSizeBytes / totalFolderSize * 100.0;

                        progressBar.Value = Math.Min(percentFolder, 100);
                        progressBarText.Text = $"{percentFolder:F2}%";
                    }
                }
            }
        }

        private void ShowContents(string entryPath, CDReader cd = null)
        {
            detailsListView.Items.Clear();

            // Caso ISO → cd non è null e il percorso NON contiene ":"
            if (cd != null && !entryPath.Contains(":"))
            {
                var subEntries = cd.GetFileSystemEntries(entryPath);

                var dirs = subEntries.Where(se => cd.GetAttributes(se).HasFlag(FileAttributes.Directory))
                                     .OrderBy(se => Path.GetFileName(se).ToLower());

                var files = subEntries.Where(se => !cd.GetAttributes(se).HasFlag(FileAttributes.Directory))
                                      .OrderBy(se => Path.GetFileName(se).ToLower());

                foreach (var subEntry in dirs.Concat(files))
                {
                    bool isDir = cd.GetAttributes(subEntry).HasFlag(FileAttributes.Directory);

                    string displayName = Path.GetFileName(subEntry);
                    if (displayName.Contains(";"))
                        displayName = displayName.Substring(0, displayName.IndexOf(";"));

                    long size = 0;
                    if (isDir)
                    {
                        size = GetIsoDirectorySize(cd, subEntry);
                    }
                    else
                    {
                        using (Stream s = cd.OpenFile(subEntry, FileMode.Open))
                            size = s.Length;
                    }

                    string sizeText = FormatSize(size);
                    string ext = Path.GetExtension(displayName);
                    string iconPath = isDir ? "pack://application:,,,/Images/folder.png"
                                            : GetIconForExtension(ext);

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
            // Caso cartella locale
            else if (Directory.Exists(entryPath))
            {
                var dirs = Directory.GetDirectories(entryPath).OrderBy(d => Path.GetFileName(d).ToLower());
                var files = Directory.GetFiles(entryPath).OrderBy(f => Path.GetFileName(f).ToLower());

                foreach (var subEntry in dirs.Concat(files))
                {
                    bool isDir = Directory.Exists(subEntry);
                    string displayName = Path.GetFileName(subEntry);

                    long size = isDir ? GetDirectorySize(new DirectoryInfo(subEntry))
                                      : new FileInfo(subEntry).Length;

                    string sizeText = FormatSize(size);
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