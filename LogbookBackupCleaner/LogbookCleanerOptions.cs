using System.Text;

namespace LogbookBackupCleaner
{
    /// <summary>
    /// Debug levels that can be specified on the command line
    /// </summary>
    public enum DebugLevelType
    {
        None,
        Information,
        Debug,
        Verbose,
        Warning
    }

    /// <summary>
    /// This is a container class that holds the options that have been specified on and then parsed from the command line.
    /// </summary>
    public class LogbookCleanerOptions
    {
        public DebugLevelType DebugLevel { get; set; }
        public string[] Folders { get; set; }
        public string[] ZipFiles { get; set; }
        public int AgeInDays { get; set; }
        public int FileCount { get; set; }
        public bool QuietMode { get; set; }
        public bool TestMode { get; set; }

        /// <summary>
        /// Convert this class instance to a string
        /// </summary>
        /// <returns>A string representation of this class instance data</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(string.Format("Age:{0} Count:{1} DebugLevel:{2} QuietMode: {3} TestMode: {4}",
            AgeInDays, FileCount, DebugLevel, QuietMode, TestMode));
            sb.AppendLine("Folders:");
            foreach (string folder in Folders)
            {
                sb.AppendLine(string.Format("  {0}", folder));
            }
            sb.AppendLine("Zip Files:");
            foreach (string file in ZipFiles)
            {
                sb.AppendLine(string.Format("  {0}", file));
            }
            return sb.ToString();
        }
    }
}

