using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;


namespace ExperimentFileFinder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private BackgroundWorker BackgroundWorkerEstimateSearchTime;
        private BackgroundWorker BackgroundWorkerSearchFiles;

        /* Текущий режим работы BackgroundWorker элементов и рекурсивного обхода директорий: 
           Estimate - происходит оценка времени на поиск
           Search - происходит поиск файлов по заданной маске
        */
        enum BackgroundWorkerMode
        {
            Estimate,
            Search
        }

        public MainWindow()
        {
            InitializeComponent();
            MainWindowLoad();
        }

        private void MainWindowLoad()
        {
            LabelProgress.Visibility = Visibility.Hidden;
            LabelFilesCount.Visibility = Visibility.Hidden;
            ProgressBarMain.Visibility = Visibility.Hidden;

            LoadAvailableDrivesInfo();
            UpdateSearchDirectoryFromSelectedDrive();
        }

        // Проверка запущен ли сейчас поиск
        public bool IsSearchRunning { get; set; } = false;

        // Контекстный объект, содержащий необходимые свойства для обмена данными между формой и потоками для объектов BackgroundWorker        
        private FileSearchInfo FileSearchInfoHolder { get; set; } = new FileSearchInfo();

        // Переключает свойство IsSearchRunning, а также
        // обновляет текст кнопки ButtonStart и её состояние
        private void SetIsSearchRunningAndUpdateButtonState(bool isRunning)
        {
            if (isRunning)
            {
                ButtonStartSearch.Content = "Прервать";
            }
            else
            {
                ButtonStartSearch.Content = "Начать поиск";
                ButtonStartSearch.IsEnabled = true;
            }
            IsSearchRunning = isRunning;
        }

        // Запускает выбранный файл с помощью стандартной программы 
        private void StartSelectedFile(string pathToFile)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.FileName = pathToFile;
            processStartInfo.UseShellExecute = true;
            Process.Start(processStartInfo);
        }

        // Метод выбирает один из доступных в системе дисков в выпадающем списке по заданному пути поиска файлов.
        private void SelectDriveBySearchPath(string searchPath)
        {
            int commaSlashPosition = searchPath.IndexOf(":\\");
            if (commaSlashPosition >= 0)
            {
                string driveLetterFromPath = searchPath.Substring(0, commaSlashPosition + 2);

                foreach (var item in ComboBoxDrives.Items)
                {
                    if (item is DriveInfoItem driveInfoItem)
                    {
                        if (driveInfoItem.DriveName.Equals(driveLetterFromPath))
                        {
                            ComboBoxDrives.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Ошибка: невозможно найти диск, соответствующий выбранному пути", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSearchDirectoryFromSelectedDrive()
        {
            FileSearchInfoHolder.SearchDirectory = (ComboBoxDrives.SelectedItem as DriveInfoItem).DriveName;
            UpdateSearchPathTextBox(FileSearchInfoHolder.SearchDirectory);
        }

        private void UpdateSearchPathTextBox(string searchPath)
        {
            if (!searchPath.Equals(TextBoxSearchPath.Text) && FileSearchInfoHolder.FilesTotalCount > 0)
            {
                FileSearchInfoHolder.FilesTotalCount = 0;
            }
            TextBoxSearchPath.Text = searchPath;
        }

        // Загружает в выпадающий список все доступные в системе диски с краткой информацией о них
        private void LoadAvailableDrivesInfo()
        {
            DriveInfo[] driveInfos = DriveInfo.GetDrives();
            foreach (var driveInfo in driveInfos)
            {
                ComboBoxDrives.Items.Add(new DriveInfoItem(driveInfo));
            }
            ComboBoxDrives.SelectedIndex = 0;
        }

        // Метод запускает поиск файлов по имени файла (маске файла)
        private void StartSearchFilesByFileName()
        {
            LabelProgress.Content = "Поиск файла по маске *" + FileSearchInfoHolder.FileNameMask + "* в каталоге '" + FileSearchInfoHolder.SearchDirectory + "'...";
            LabelFilesCount.Visibility = Visibility.Hidden;
            ProgressBarMain.IsIndeterminate = false;
            FileSearchInfoHolder.FilesFound = 0;
            FileSearchInfoHolder.FilesProcessedCount = 0;
            SetIsSearchRunningAndUpdateButtonState(true);
            BackgroundWorkerSearchFiles.RunWorkerAsync(FileSearchInfoHolder);
        }

        /*
         Выполняет рекурсивный обход директорий, начиная с родительской директории.
         Может работать в двух режимах:
         1) подсчёт общего количества вложенных файлов внутри родительской директории,
         2) поиск в родительской директории файла по заданной маске
        */
        private void CalculateFilesCountRecursively(string parentDirectory, BackgroundWorkerMode workerMode, FileSearchInfo fileInfoHolder)
        {
            try
            {
                IEnumerable<string> subdirectories = Directory.EnumerateDirectories(parentDirectory, "*", SearchOption.TopDirectoryOnly);
                IEnumerable<string> files = Directory.EnumerateFiles(parentDirectory);

                if (workerMode == BackgroundWorkerMode.Estimate)
                {
                    // если было запрошено прерывание операции оценки времени поиска - выходим из рекурсии
                    if (BackgroundWorkerEstimateSearchTime.CancellationPending)
                    {
                        return;
                    }

                    fileInfoHolder.FilesTotalCount += files.LongCount();
                    BackgroundWorkerEstimateSearchTime.ReportProgress(10);
                }
                else if (workerMode == BackgroundWorkerMode.Search)
                {

                    // если было запрошено прерывание операции поиска - выходим из рекурсии
                    if (BackgroundWorkerSearchFiles.CancellationPending)
                    {
                        return;
                    }

                    foreach (string file in files)
                    {
                        if (file.Contains(fileInfoHolder.FileNameMask))
                        {
                            fileInfoHolder.FoundFiles.Add(file);
                            FileSearchInfoHolder.FilesFound++;
                        }
                    }

                    List<string> foundFiles = new List<string>(fileInfoHolder.FoundFiles);

                    fileInfoHolder.FilesProcessedCount += files.LongCount();
                    int progress = (int)(fileInfoHolder.FilesProcessedCount * 100 / fileInfoHolder.FilesTotalCount);
                    BackgroundWorkerSearchFiles.ReportProgress(progress, foundFiles);
                    fileInfoHolder.FoundFiles.Clear();
                }

                if (subdirectories.LongCount() > 0)
                {
                    foreach (string subdirectory in subdirectories)
                    {
                        CalculateFilesCountRecursively(subdirectory, workerMode, fileInfoHolder);
                    }
                }
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                // TODO: обработать исключение при необходимости...
            }
            catch (DirectoryNotFoundException directoryNotFoundException)
            {
                // TODO: обработать исключение при необходимости...
            }
            catch (Exception otherException)
            {
                // TODO: обработать исключение при необходимости...
            }
        }

        // Метод для выполнения основной работы для элемента BackgroundWorker, отвечающего за оценку времени поиска в заданной директории.
        private void BackgroundWorkerEstimateSearchTime_DoWork(object sender, DoWorkEventArgs e)
        {
            if (e.Argument is FileSearchInfo fileInfo)
            {
                if (BackgroundWorkerEstimateSearchTime.CancellationPending)
                {
                    e.Cancel = true;
                }
                else
                {
                    CalculateFilesCountRecursively(FileSearchInfoHolder.SearchDirectory, BackgroundWorkerMode.Estimate, fileInfo);
                    if (BackgroundWorkerEstimateSearchTime.CancellationPending)
                    {
                        e.Cancel = true;
                    }
                }
            }
        }

        // Метод для выполнения основной работы для элемента BackgroundWorker, отвечающего за поиск в заданной директории.
        private void BackgroundWorkerSearchFiles_DoWork(object sender, DoWorkEventArgs e)
        {
            if (e.Argument is FileSearchInfo fileInfo)
            {
                if (BackgroundWorkerSearchFiles.CancellationPending)
                {
                    e.Cancel = true;
                }
                else
                {
                    CalculateFilesCountRecursively(FileSearchInfoHolder.SearchDirectory, BackgroundWorkerMode.Search, fileInfo);
                    if (BackgroundWorkerSearchFiles.CancellationPending)
                    {
                        e.Cancel = true;
                    }
                }
            }
        }

        private void BackgroundWorkerEstimateSearchTime_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                // Оценка времени поиска завершилась с прерыванием. Выводим сообщение об этом, сбрасываем счётчики,
                // скрываем метку с количеством файлов и делаем невидимым прогресс бар
                SetIsSearchRunningAndUpdateButtonState(false);

                LabelProgress.Content = "Оценка времени поиска была прервана.";
                FileSearchInfoHolder.FilesTotalCount = 0;
                FileSearchInfoHolder.FilesFound = 0;
                ProgressBarMain.Visibility = Visibility.Hidden;
                LabelFilesCount.Content = "0";
                LabelFilesCount.Visibility = Visibility.Hidden;
            }
            else
            {
                // Оценка времени поиска завершилась без прерывания. Значит, запускаем непосредственно поиск файлов по маске
                StartSearchFilesByFileName();
            }
        }

        private void BackgroundWorkerSearchFiles_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetIsSearchRunningAndUpdateButtonState(false);

            if (e.Cancelled)
            {
                // Операция поиска файлов была прервана
                LabelProgress.Content = "Операция поиска прервана.";
                ProgressBarMain.Visibility = Visibility.Hidden;
                LabelFilesCount.Content = "0";
                LabelFilesCount.Visibility = Visibility.Hidden;
            }
            else
            {
                // Поиск завершился штатно, без прерывания
                LabelProgress.Content = "Поиск по маске *" + FileSearchInfoHolder.FileNameMask + "* в каталоге '" + FileSearchInfoHolder.SearchDirectory + "' завершён. Найдено файлов: ";
                LabelFilesCount.Content = FileSearchInfoHolder.FilesFound.ToString();
                LabelFilesCount.Visibility = Visibility.Visible;
            }
        }

        private void BackgroundWorkerEstimateSearchTime_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            LabelFilesCount.Content = FileSearchInfoHolder.FilesTotalCount.ToString();
        }

        /*Событие обработки изменения прогресса для элемента BackgroundWorker, отвечающего за поиск файлов.
        Обновляет значение прогресс бара и добавляет в элемент ListViewFoundFiles все найденные файлы.*/

        private void BackgroundWorkerSearchFiles_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBarMain.Value = e.ProgressPercentage;
            List<string> foundFiles = (List<string>)e.UserState;

            foreach (string fileName in foundFiles)
            {
                long fileSizeInBytes = -1;
                try
                {
                    FileInfo fileInfo = new FileInfo(fileName);
                    fileSizeInBytes = fileInfo.Length;
                }
                catch (FileNotFoundException fileNotFoundException)
                {
                    //TODO: обработать исключение при необходимости...
                }
                ListViewItem value = new ListViewItem() {Content = new string[] { fileName, fileSizeInBytes.ToString() } };
                //ListViewItem value = new ListViewItem(new string[] { fileName, fileSizeInBytes.ToString() }, 0, group);
                ListViewFoundFiles.Items.Add(value);
            }

            FileSearchInfoHolder.FoundFiles.Clear();
        }
        
        /* Обработка изменения события изменения текста в текстовом поле TextBoxFileName.
           Необходимо делать доступной кнопку поиска, если маска поиска для файлов задана и недоступной, 
           если поле пустое или содержит лишь пробелы.
        */
        private void TextBoxFileName_TextChanged(object sender, EventArgs e)
        {
            ButtonStartSearch.IsEnabled = !"".Equals(TextBoxFileName.Text.Trim());
        }

        // Обработка нажатия на кнопку "Начать поиск" / "Прервать"
        private void ButtonStartSearch_Click(object sender, EventArgs e)
        {
            if (IsSearchRunning)
            {
                // Поиск уже запущен - прервать

                // Запретить повторные нажатия на кнопку "Прервать"
                ButtonStartSearch.IsEnabled = false;

                // Асинхронная отмена работы BackgroundWorker-ов
                if (BackgroundWorkerEstimateSearchTime.IsBusy)
                {
                    BackgroundWorkerEstimateSearchTime.CancelAsync();
                }
                if (BackgroundWorkerSearchFiles.IsBusy)
                {
                    BackgroundWorkerSearchFiles.CancelAsync();
                }
            }
            else
            {
                // Поиск не запущен - запустить оценку времени поиска или сам поиск
                FileSearchInfoHolder.FileNameMask = TextBoxFileName.Text;
                ProgressBarMain.Value = 0;
                //ListViewFoundFiles.GroupStyle.Clear();
                //ListViewFoundFiles.GroupStyle.Add(new ListViewGroup("listViewGroupFiles", "Найденные файлы"));

                FileSearchInfoHolder.FoundFiles.Clear();

                ProgressBarMain.Visibility = Visibility.Visible;

                if (FileSearchInfoHolder.FilesTotalCount == 0)
                {
                    ProgressBarMain.IsIndeterminate = true;
                    LabelProgress.Content = "Подсчёт количества файлов в системе и оценка примерного времени... Найдено файлов:";
                    LabelProgress.Visibility = Visibility.Visible;
                    LabelFilesCount.Visibility = Visibility.Visible;
                    SetIsSearchRunningAndUpdateButtonState(true);
                    MessageBox.Show($"zx{FileSearchInfoHolder.SearchDirectory}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    BackgroundWorkerEstimateSearchTime.RunWorkerAsync(FileSearchInfoHolder);
                }
                else
                {
                    StartSearchFilesByFileName();
                }
            }
        }

        // Обработка нажатия на кнопку "Обзор" - для выбора директории, в которой необходимо производить поиск файлов
        private void ButtonSelectSearchDirectory_Click(object sender, EventArgs e)
        {
            var dialogResult = new OpenFolderDialog();
            if (dialogResult.ShowDialog() == true)
            {
                string selectedPath = dialogResult.FolderName;
                if (!selectedPath.EndsWith("\\"))
                {
                    selectedPath += "\\";
                }
                FileSearchInfoHolder.SearchDirectory = selectedPath;
                UpdateSearchPathTextBox(selectedPath);
                SelectDriveBySearchPath(selectedPath);
            }
        }

        /// Обработка двойного клика по одному из найденных файлов - для запуска стандартной программы, 
        /// которая сможет открыть файл
        private void ListViewFoundFiles_DoubleClick(object sender, EventArgs e)
        {
            var selectedItems = ListViewFoundFiles.SelectedItems;
            if (selectedItems.Count > 0)
            {
                string selectedItem = selectedItems[0].ToString();
                StartSelectedFile(selectedItem);
            }
        }

        /// Изменение пути поиска при перевыборе диска в выпадающем списке
        private void ComboBoxDrives_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedItem = ComboBoxDrives.SelectedItem;
            if (selectedItem is DriveInfoItem driveInfoItem)
            {
                string selectedPath = driveInfoItem.DriveName;
                FileSearchInfoHolder.SearchDirectory = selectedPath;
                UpdateSearchPathTextBox(selectedPath);
            }
        }

    }

    /// <summary>
    /// Класс, задающий контекст для поиска файлов. 
    /// </summary>
    public class FileSearchInfo
    {
        public long FilesTotalCount { get; set; } = 0;
        public long FilesProcessedCount { get; set; } = 0;
        public string? SearchDirectory { get; set; }
        public long FilesFound { get; set; } = 0;
        public string FileNameMask { get; set; } = "";
        public List<string> FoundFiles = new List<string>();
    }
}