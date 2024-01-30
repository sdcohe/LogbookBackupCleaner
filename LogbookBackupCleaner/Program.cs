using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace LogbookBackupCleaner
{
    public class Program
    {
        #region Global variables
        private static readonly LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();
        private static bool quietMode = false;
        #endregion

        #region Main Processing
        /// <summary>
        /// Utility program to clean up backup files created by HRD Logbook based on age and/or
        /// file count.
        /// </summary>
        /// <param name="args">The command line arguments</param>
        /// <returns>0 on success, -1 if fail</returns>
        static async Task<int> Main(string[] args)
        {
            int returnCode = 0;

            // create root command and command line options
            RootCommand rootCommand = new RootCommand("Clean up HRD Logbook backup files");
            BuildCommandLineOptions(out Option<string[]> foldersOption, out Option<int> ageInDaysOption, 
                out Option<int> fileCountOption, out Option<DebugLevelType> debugLevelOption, 
                out Option<bool> quietModeOption, out Option<bool> testModeOption, out Option<string[]> zipFilesOption);
            AddOptionsToRootCommand(rootCommand, foldersOption, ageInDaysOption, fileCountOption, debugLevelOption, 
                quietModeOption, testModeOption, zipFilesOption);

            // set up a handler for the root command to perform the actual cleanup
            rootCommand.SetHandler((options) =>
            {
                returnCode =  Cleanup(options);
            },
            new LogbookCleanerOptionsBinder(foldersOption, ageInDaysOption, fileCountOption, debugLevelOption, quietModeOption, testModeOption, zipFilesOption));

            // fire everything off
            await rootCommand.InvokeAsync(args);

            return returnCode;
        }

        /// <summary>
        /// Main driver method that, based on the command line options, removes HRD Logbook backup files
        /// </summary>
        /// <param name="LogbookCleanerOptions">A container class that holds the command line options</param>
        /// <returns>0 if successful, -1 on failure</returns>
        private static int Cleanup(LogbookCleanerOptions options)
        {
            int returnValue = 0;
            quietMode = options.QuietMode;

            DisplayMessage("Starting cleanup");
            InitializeDebugging(options);

            foreach (string folder in options.Folders)
            {
                ProcessFolder(folder, options);
            }

            foreach(string zipFile in options.ZipFiles)
            {
                ProcessZipArchive(zipFile, options);
            }

            Log.CloseAndFlush();
            DisplayMessage("Cleanup complete");

            return returnValue;
        }
        #endregion

        #region Command Line Processing
        /// <summary>
        /// Specify the command line options for the System.CommandLine parser
        /// </summary>
        /// <param name="foldersOption">List of folders to process for xml and zip backups</param>
        /// <param name="ageInDaysOption">The age in days to retain backup files</param>
        /// <param name="fileCountOption">The number of backup files to always retain</param>
        /// <param name="debugLevelOption">Debug level for Serilog</param>
        /// <param name="quietModeOption">Whether to suppress messages from the application</param>
        /// <param name="testModeOption">Whether to prevent actual file deletion when processing backups. Useful for testing.</param>
        /// <param name="zipFileOption">List of zip archives to process</param>
        private static void BuildCommandLineOptions(out Option<string[]> foldersOption, out Option<int> ageInDaysOption,
            out Option<int> fileCountOption, out Option<DebugLevelType> debugLevelOption,
            out Option<bool> quietModeOption, out Option<bool> testModeOption, out Option<string[]> zipFileOption)
        {
            foldersOption = new Option<string[]>(
                name: "--folders",
                description: "A list of backup folders containing XML or ZIP backup files")
            { AllowMultipleArgumentsPerToken = true };
            foldersOption.AddAlias("-f");
            foldersOption.IsRequired = false;

            zipFileOption = new Option<string[]>(
                name: "--zipfiles",
                description: "A list of zip archives each containing multiple XML backups")
            { AllowMultipleArgumentsPerToken = true };
            zipFileOption.AddAlias("-z");
            zipFileOption.IsRequired = false;

            ageInDaysOption = new Option<int>(
                name: "--age",
                description: "Age in days. Backups older than this will be purged");
            ageInDaysOption.AddAlias("-a");

            fileCountOption = new Option<int>(
                name: "--count",
                description: "Count of files to retain. This option ensures that there will always be some backups not purged regardless of their age.");
            fileCountOption.AddAlias("-c");

            debugLevelOption = new Option<DebugLevelType>(
                name: "--debug",
                description: "Turn on debug logging. Order of severity is None, Warning, Information, Debug, Verbose.",
                getDefaultValue: () => DebugLevelType.None
                )
            {
                //IsHidden = true
            };
            debugLevelOption.AddAlias("-d");

            quietModeOption = new Option<bool>(
                name: "--quiet",
                description: "Don't display prompts to the console",
                getDefaultValue: () => false);
            quietModeOption.AddAlias("-q");

            testModeOption = new Option<bool>(
                name: "--test",
                description: "Test changes but don't actually delete any files",
                getDefaultValue: () => false);
            testModeOption.AddAlias("-t");

        }

        /// <summary>
        /// Add command line options to the root command.
        /// </summary>
        /// <param name="rootCommand">The root command for this application</param>
        /// <param name="foldersOption">Option for the list of folders to process</param>
        /// <param name="ageInDaysOption">Option for the age in days to retain backup files</param>
        /// <param name="fileCountOption">Option for the number of backup files to always retain</param>
        /// <param name="debugLevelOption">Option for Serilog debug level</param>
        /// <param name="quietModeOption">Whether to suppress messages from the application</param>
        /// <param name="testModeOption">Whether to prevent actual file deletion when processing backups. Useful for testing.</param>
        /// <param name="zipFilesOption">Option for the list of zip files to process</param>
        private static void AddOptionsToRootCommand(RootCommand rootCommand, Option<string[]> foldersOption,
            Option<int> ageInDaysOption, Option<int> fileCountOption, Option<DebugLevelType> debugLevelOption,
            Option<bool> quietModeOption, Option<bool> testModeOption, Option<string[]> zipFilesOption)
        {
            rootCommand.AddOption(foldersOption);
            rootCommand.AddOption(zipFilesOption);
            rootCommand.AddOption(ageInDaysOption);
            rootCommand.AddOption(fileCountOption);
            rootCommand.AddOption(quietModeOption);
            rootCommand.AddOption(debugLevelOption);
            rootCommand.AddOption(testModeOption);

            // Permit age, count, or both. At least one must be specified
            rootCommand.AddValidator(commandResult =>
            {
                ValidateOneOrBoth(commandResult, "You must specify either age (-a), count (-c), or both", ageInDaysOption, fileCountOption);
            });

            // Permit folders, zipfiles, or both. At least one must be specified
            rootCommand.AddValidator(commandResult =>
            {
                ValidateOneOrBoth(commandResult, "You must specify either folders (-f), zipfiles (-z), or both", foldersOption, zipFilesOption);
            });
        }

        /// <summary>
        /// Ensure that one of the passed in options in the Option array has been specified on the command line
        /// </summary>
        /// <param name="commandResult">Result of the validation</param>
        /// <param name="errorMessage">The error message to display if the validation fails</param>
        /// <param name="options">An array of options to be checked</param>
        static void ValidateOneOrBoth(CommandResult commandResult, string errorMessage, params Option[] options)
        {
            Debug.Assert(options.Length >= 2);

            if (options.Count(option => commandResult.FindResultFor(option) != null) == 0)
            {
                commandResult.ErrorMessage = errorMessage;
            }
        }
        #endregion

        #region Folder Processing
        /// <summary>
        /// Method that will be called once per folder. Get a list of backup files in this folder grouped 
        /// by log (in case the user has multiple logs backed up to the folder). Then process each fiile 
        /// in that group as a candidate for cleanup.
        /// </summary>
        /// <param name="folder">The folder to process</param>
        /// <param name="options">A container class holding the parsed command line options.</param>
        private static void ProcessFolder(string folder, LogbookCleanerOptions options)
        {
            // file name pattern is <DB name> backup <date> <time>.[zip|xml]
            DisplayMessage(string.Format("Processing XML backup files in folder {0}", folder));
            Log.Information("Processing XML backups");
            ProcessFolderForPattern(folder, options, "xml");

            DisplayMessage(string.Format("Processing ZIP backup files in folder {0}", folder));
            Log.Information("Processing ZIP backups");
            ProcessFolderForPattern(folder, options, "zip");
        }

        private static void ProcessFolderForPattern(string folder, LogbookCleanerOptions options, string fileExtension)
        {
            string filePattern = string.Format("* backup ????-??-?? ????.{0}", fileExtension);
            Log.Debug("Processing folder {0} using pattern {1}", folder, filePattern);

            try
            {
                IEnumerable<IGrouping<string, FileInfo>> distinctDBGroups = GroupBackupFilesByLogbook(folder, filePattern);
                DisplayMessage(string.Format("This folder contains backups for {0} logs", distinctDBGroups.Count()));

                // process each group
                foreach (var group in distinctDBGroups)
                {
                    DisplayMessage(string.Format("Processing backups for log {0} with {1} entries", group.Key, group.Count()));

                    // Make sure files are in order by date and then by name. This should already be the case but some extra
                    // insurance sometimes helps
                    IEnumerable<FileInfo> files = group.Select(s => s).OrderBy(s => s.CreationTime).ThenBy(s => s.Name);
                    int filesRemaining = group.Count();
                    bool errorsOccurred = false;

                    // process each file in the group
                    foreach (FileInfo file in files)
                    {
                        Log.Information("Processing file '{0}'", file.Name);

                        // determine if we need to remove this file or not (check eligibility)
                        if (IsFileEligibleToDelete(file.LastWriteTime, options, filesRemaining))
                        {
                            // delete file 
                            Log.Information("Removing file {0}", file.Name);
                            try
                            {
                                if (!options.TestMode) file.Delete();
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                Log.Error(ex, "An error occurred deleting file {0}", file.Name);
                                DisplayColorMessage(ConsoleColor.Red, string.Format("An error occurred : You don't have the required permissions to delete {0}", file.Name));
                                errorsOccurred = true;
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "An error occurred deleting file {0}", file.Name);
                                DisplayColorMessage(ConsoleColor.Red, string.Format("An error occurred deleting file {0}: {1}", file.Name, ex.Message));
                                errorsOccurred = true;
                            }
                            filesRemaining--;
                        }
                    }

                    if (!errorsOccurred)
                    {
                        DisplayMessage(string.Format("Summary for log {0}: Deleted:{1} Retained:{2}", group.Key, group.Count() - filesRemaining, filesRemaining));
                    }
                    else
                    {
                        DisplayMessage(string.Format("Errors occurred while removing files for log {0}. Review any error messages for more information", group.Key));
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                DisplayColorMessage(ConsoleColor.Red, string.Format("Folder {0} cound not be found", folder));
            }
            catch (Exception ex)
            {
                DisplayColorMessage(ConsoleColor.Red, string.Format("An error occurred while processing folder {0}: {1}", folder, ex.Message));
            }
        }

        /// <summary>
        /// Return all the backup files in this folder grouped by log name. This is in case the user
        /// has stored backups from multiple logbooks
        /// </summary>
        /// <param name="folder">The name of the folder to search for XML backups</param>
        /// <returns>The backup file names grouped by log</returns>
        private static IEnumerable<IGrouping<string, FileInfo>> GroupBackupFilesByLogbook(string folder, string pattern)
        {
            // get a list of backup (xml) files in this folder
            var dir = new DirectoryInfo(folder);
            FileInfo[] fileList = dir.GetFiles(pattern);

            // group by db name
            IEnumerable<IGrouping<string, FileInfo>> distinctDBGroups = fileList.Select(s => s)
                .GroupBy(s => s.Name.Substring(0, s.Name.IndexOf(" backup")));

            return distinctDBGroups;
        }
        #endregion

        #region Zip Archive Processing
        /// <summary>
        /// Method that will be called once per zip archive file. Get a list of backup files in this zip archive grouped 
        /// by log (in case the user has multiple logs backed up to the folder). Then process each fiile 
        /// in that group as a candidate for cleanup.
        /// </summary>
        /// <param name="zipFile">The zip archive to process</param>
        /// <param name="options">A container class holding the parsed command line options.</param>
        private static void ProcessZipArchive(string zipFile, LogbookCleanerOptions options)
        {
            try
            {
                using (FileStream zipToOpen = new FileStream(zipFile, FileMode.Open))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        IEnumerable<IGrouping<string, ZipArchiveEntry>> distinctDBGroups = 
                            archive.Entries.Select(s => s).Where(s => s.Name.EndsWith(".xml"))
                            .GroupBy(s => s.Name.Substring(0, s.Name.IndexOf(" backup")));
                        DisplayMessage(string.Format("This zip file contains backups for {0} logs", distinctDBGroups.Count()));

                        // process each group
                        foreach (var group in distinctDBGroups)
                        {
                            DisplayMessage(string.Format("Processing backups for log {0} with {1} entries", group.Key, group.Count()));

                            // Make sure files are in order by date and then by name. This should already be the case but some extra
                            // insurance sometimes helps
                            IEnumerable<ZipArchiveEntry> files = group.Select(s => s).OrderBy(s => s.LastWriteTime)
                                .ThenBy(s => s.Name);

                            int filesRemaining = group.Count();
                            bool errorsOccurred = false;

                            foreach (ZipArchiveEntry entry in files)
                            {
                                Log.Information("Processing zip file entry '{0}'", entry.Name);

                                // determine if we need to remove this file or not (check eligibility)
                                if (IsFileEligibleToDelete(entry.LastWriteTime.Date, options, filesRemaining))
                                {
                                    // delete file 
                                    Log.Information("Deleting entry {0} from zip file", entry.Name);
                                    try
                                    {
                                        if (!options.TestMode) entry.Delete();
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "An error occurred deleting entry {0}", entry.Name);
                                        DisplayColorMessage(ConsoleColor.Red, string.Format("An error occurred deleting entry {0}: {1}", entry.Name, ex.Message));
                                        errorsOccurred = true;
                                    }
                                    filesRemaining--;
                                }
                            }

                            if (!errorsOccurred)
                            {
                                DisplayMessage(string.Format("Summary for log {0}: Deleted:{1} Retained:{2}", group.Key, group.Count() - filesRemaining, filesRemaining));
                            }
                            else
                            {
                                DisplayMessage(string.Format("Errors occurred while removing files for log {0}. Review any error messages for more information", group.Key));
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred opening {0}: {1}", zipFile, ex.Message);
                DisplayColorMessage(ConsoleColor.Red, string.Format("An error occurred opening {0}: {1}", zipFile, ex.Message));
            }
        }
        #endregion

        #region Common functions
        /// <summary>
        /// Check to see if a given file is a candidate for deletion.
        /// </summary>
        /// <param name="fileDate">The last write date for the file we are currently working on</param>
        /// <param name="options">A container class holding the parsed command line options.</param>
        /// <param name="filesRemaining">The nuber of files in this group still remaining (not deleted)</param>
        /// <returns>true or false</returns>
        private static bool IsFileEligibleToDelete(DateTime fileDate, LogbookCleanerOptions options, int filesRemaining)
        {
            bool OKToDelete;
            if (options.AgeInDays > 0)
            {
                OKToDelete = IsFileAgeExceeded(fileDate, options) && filesRemaining > options.FileCount;
            }
            else
            {
                OKToDelete = filesRemaining > options.FileCount;
            }

            if (filesRemaining <= options.FileCount)
            {
                Log.Debug("Must leave at least {0} files remaining, file will not be deleted", options.FileCount);
            }

            return OKToDelete;
        }

        /// <summary>
        /// Determine if a file exceeds the age limits specified on the command line
        /// </summary>
        /// <param name="fileDate">The last write date for the file we are currently working on</param>
        /// <param name="options">A container class holding the parsed command line options.</param>
        /// <returns>true or false</returns>
        private static bool IsFileAgeExceeded(DateTime fileDate, LogbookCleanerOptions options)
        {
            bool isEligible = false;

            //Log.Debug("Checking file age for {0}", file.Name);
            if (options.AgeInDays > 0)
            {
                //remove file if it is older than AgeInDays days
                var today = DateTime.Now;
                var age = today.Subtract(fileDate);
                Log.Debug("File is {0} days old", age.Days);

                if (age.Days > options.AgeInDays)
                {
                    isEligible = true;
                    Log.Debug("File exceeds age, marking for deletion based on age");
                }
                else
                {
                    Log.Debug("File not marked for deletion based on age");
                }
            }

            return isEligible;
        }

        /// <summary>
        /// Display a message on the console if quiet mode is not specified on the command line. 
        /// </summary>
        /// <param name="message">The message to display.</param>
        private static void DisplayMessage(string message)
        {
            if (!quietMode)
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Display a messge on the console in the specified color. Quiet mode is not honored. Useful for error 
        /// messages to always be displayed.
        /// </summary>
        /// <param name="foregroundColor">The text color</param>
        /// <param name="message">The message to display. Append a newline as needed</param>
        private static void DisplayColorMessage(ConsoleColor foregroundColor, string message)
        {
            Console.ForegroundColor = foregroundColor;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        #endregion

        #region Debugging functions
        /// <summary>
        /// Set the Serilog debug level based on the command line parameter
        /// </summary>
        /// <param name="debugLevel">The debug level to set</param>
        private static void SetDebugLevel(DebugLevelType debugLevel)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Warning;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console()
                .CreateLogger();

            switch (debugLevel) // in order of Serilog severity, error and fatal always come through
            {
                case DebugLevelType.Warning:
                    levelSwitch.MinimumLevel = LogEventLevel.Warning;
                    break;
                case DebugLevelType.Information:
                    levelSwitch.MinimumLevel = LogEventLevel.Information;
                    break;
                case DebugLevelType.Debug:
                    levelSwitch.MinimumLevel = LogEventLevel.Debug;
                    break;
                case DebugLevelType.Verbose:
                    levelSwitch.MinimumLevel = LogEventLevel.Verbose;
                    break;
            }
        }

        /// <summary>
        /// Display some debugging information. Show what options were parsed from the command line.
        /// </summary>
        /// <param name="options">A container class holding the parsed command line options.</param>
        private static void DisplayOptionsForDebugging(LogbookCleanerOptions options)
        {
            Log.Debug("Options: {0}", options.ToString());

            if (options.AgeInDays > 0)
            {
                Log.Information("Processing files older than {0} days", options.AgeInDays);
            }

            if (options.FileCount > 0)
            {
                Log.Information("Processing files with file count greater than {0}", options.FileCount);
            }
        }

        /// <summary>
        /// Set up Serilog debugging based on the debug level specified on the command line
        /// </summary>
        /// <param name="options">A container class holding the parsed command line options.</param>
        private static void InitializeDebugging(LogbookCleanerOptions options)
        {
            DebugLevelType debugLevel = options.DebugLevel;
            if (debugLevel != DebugLevelType.None)
            {
                SetDebugLevel(debugLevel);
                Log.Information("Debug level set to {0}", debugLevel);
            }
            DisplayOptionsForDebugging(options);
        }
        #endregion

    }
}
