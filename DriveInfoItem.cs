using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExperimentFileFinder
{
    public class DriveInfoItem
    {
        // хранит имя диска (например: "C:\\")
        public string DriveName { get; set; }

        // хранит имя метки для диска, установлена в системе (например: "Мой системный диск")
        public string DriveVolumeLabel { get; set; }
        
        // хранит текстовое представление формата диска (NTFS или FAT32)
        public string DriveFormat { get; set; }

        // хранит текстовое представление типа диска.
        public string DriveTypeString { get; set; }

        // Хранит общее свободное пространство на диске, в Гб
        public long TotalFreeSpaceGb { get; set; }

        // Хранит общий размер диска, в Гб
        public long TotalSizeGb { get; set; }

        // Хранит доступное свободное пространство, в Гб
        public long AvailableFreeSpaceGb { get; set; }

        // Конструктор класса, создаёт экземпляр класса по входному параметру
        public DriveInfoItem(DriveInfo driveInfo)
        {
            if (driveInfo == null)
            {
                throw new ArgumentNullException("driveInfo", "Ошибка: параметр не может быть null!");
            }

            DriveName = driveInfo.Name;
            DriveVolumeLabel = driveInfo.VolumeLabel;
            DriveFormat = driveInfo.DriveFormat;
            DriveTypeString = GetDriveTypeAsString(driveInfo.DriveType);

            TotalFreeSpaceGb = GetSizeInGigabytes(driveInfo.TotalFreeSpace);
            TotalSizeGb = GetSizeInGigabytes(driveInfo.TotalSize);
            AvailableFreeSpaceGb = GetSizeInGigabytes(driveInfo.AvailableFreeSpace);
        }

        // Переводит размер из байт в Гб
        private long GetSizeInGigabytes(long size)
        {
            return size / (long)Math.Pow(1024, 3);
        }

        // Возвращает часть описания диска, связанного с общим/доступным/свободным объемом дискового пространства
        
        private string GetVolumeSizeString()
        {
            return string.Format("Объём: {0}Гб, Всего свободно: {1}Гб, Доступно: {2}Гб", TotalSizeGb, TotalFreeSpaceGb, AvailableFreeSpaceGb);
        }

        // Переопределение метода в целях отображения текста в заданном формате в выпадающем списке с дисками на главной форме
        public override string ToString()
        {
            return GetReadableDriveName() + ": " + DriveTypeString + ", " + DriveFormat + ", " + GetVolumeSizeString();
        }

        // Возвращает читаемое имя диска для его представления в выпадающем списке.
        // Если метка диска не задана, вернёт строку в формате "[имя_диска]:\\
        // Если метка диска задана, вернёт строку в формате "[имя_метки_диска] имя_диска:\\"
        private string GetReadableDriveName()
        {
            if (DriveVolumeLabel == null || DriveVolumeLabel.Length == 0)
            {
                return "[" + DriveName + "]";
            }
            return "[" + DriveVolumeLabel + "] " + DriveName;
        }

        // Возвращает текстовое представление для различных типов дисков, которые могут быть в системе
        private string GetDriveTypeAsString(DriveType driveType)
        {
            switch (driveType)
            {
                case DriveType.Fixed:
                    return "Фиксированный диск";
                case DriveType.Network:
                    return "Сетевой диск";
                case DriveType.Removable:
                    return "Съёмный диск";
                case DriveType.Ram:
                    return "ОЗУ";
                case DriveType.NoRootDirectory:
                    return "Без корневого каталога";
                case DriveType.CDRom:
                    return "CD-ROM";
                case DriveType.Unknown:
                default:
                    return "Неизвестно";
            }
        }
    }
}
