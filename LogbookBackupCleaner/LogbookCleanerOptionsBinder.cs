using System.CommandLine;
using System.CommandLine.Binding;

namespace LogbookBackupCleaner
{
    /// <summary>
    /// Class to bind the System.CommandLine parser to our options container class
    /// </summary>
    public class LogbookCleanerOptionsBinder : BinderBase<LogbookCleanerOptions>
    {
        private readonly Option<DebugLevelType> _DebugLevelOption;
        private readonly Option<string[]> _FoldersOption;
        private readonly Option<string[]> _ZipFilesOption;
        private readonly Option<int> _AgeInDaysOption;
        private readonly Option<int> _FileCountOption;
        private readonly Option<bool> _QuietModeOption;
        private readonly Option<bool> _TestModeOption;

        /// <summary>
        /// Constructor for LogbookCleanerOptionsBinder
        /// </summary>
        /// <param name="foldersOption"></param>
        /// <param name="AgeInDaysOption"></param>
        /// <param name="FileCountOption"></param>
        /// <param name="debugLevelOption"></param>
        /// <param name="quietModeOption"></param>
        /// <param name="testModeOption"></param>
        public LogbookCleanerOptionsBinder(Option<string[]> foldersOption, Option<int> AgeInDaysOption, Option<int> FileCountOption, 
            Option<DebugLevelType> debugLevelOption, Option<bool> quietModeOption, Option<bool>testModeOption, Option<string[]> zipFilesOption)
        {
            _FoldersOption = foldersOption;
            _ZipFilesOption = zipFilesOption;
            _AgeInDaysOption = AgeInDaysOption;
            _FileCountOption = FileCountOption;
            _DebugLevelOption = debugLevelOption;
            _QuietModeOption = quietModeOption;
            _TestModeOption = testModeOption;
        }

        /// <summary>
        /// Use the passed in binding context to create an instance of LogbookCleanerOptions class with the
        /// options specified by the user on the command line
        /// </summary>
        /// <param name="bindingContext"></param>
        /// <returns></returns>
        protected override LogbookCleanerOptions GetBoundValue(BindingContext bindingContext) =>
            new LogbookCleanerOptions
            {
                Folders = bindingContext.ParseResult.GetValueForOption(_FoldersOption),
                ZipFiles = bindingContext.ParseResult.GetValueForOption(_ZipFilesOption),
                AgeInDays = bindingContext.ParseResult.GetValueForOption(_AgeInDaysOption),
                FileCount = bindingContext.ParseResult.GetValueForOption(_FileCountOption),
                DebugLevel = bindingContext.ParseResult.GetValueForOption(_DebugLevelOption),
                QuietMode = bindingContext.ParseResult.GetValueForOption(_QuietModeOption),
                TestMode = bindingContext.ParseResult.GetValueForOption(_TestModeOption)
            };
    }
}
