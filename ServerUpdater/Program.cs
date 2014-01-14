using NDesk.Options;
using NLog;
using System;
using System.Diagnostics;
using WUApiLib;

namespace ServerUpdater
{
    class Program
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            bool batchMode = false;
            bool verbose = false;
            bool showHelp = false;

            var p = new OptionSet()
            {
                { "b|batch", v => batchMode = true },
                { "v|verbose", v => verbose = true },
                { "h|help", v => showHelp = true}
            };

            try
            {
                p.Parse(args);
            }
            catch (OptionException)
            {
                showHelp = true;
            }

            if (showHelp)
            {
                Console.WriteLine("ServerUpdater.exe");
                Console.WriteLine();
                Console.WriteLine("Searches for, downloads, and installs windows updates");
                Console.WriteLine();
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (verbose)
            {
                var target = new NLog.Targets.ConsoleTarget();
                LogManager.Configuration.AddTarget("console", target);

                var rule = new NLog.Config.LoggingRule("*", LogLevel.Info, target);
                LogManager.Configuration.LoggingRules.Add(rule);
            }

            var updateSession = new UpdateSession();
            var searcher = updateSession.CreateUpdateSearcher();
            var searchResult = searcher.Search("IsInstalled = 0 and Type='Software'");

            if (searchResult.Updates.Count > 0)
            {
                log.Info("Found Updates:");
                foreach (IUpdate update in searchResult.Updates)
                    log.Info("\t{0}", update.Title);

                if (!batchMode)
                {
                    Console.WriteLine();
                    Console.Write("Found {0} updates. Would you like to download and install them?", searchResult.Updates.Count);
                    var result = Console.ReadLine().Trim();
                    if (char.ToUpper(result[0]) != 'Y')
                        return;
                }

                log.Info("Downloading updates...");
                var downloader = updateSession.CreateUpdateDownloader();
                downloader.Updates = searchResult.Updates;
                downloader.Download();

                InstallUpdates(updateSession, searchResult);
            }

            if (!batchMode)
            {
                Console.WriteLine();
                Console.Write("Installation complete. Would you like to restart?");
                var result = Console.ReadLine().Trim();
                if (char.ToUpper(result[0]) != 'Y')
                    return;
            }

            log.Info("Restarting server...");
            Process.Start("shutdown.exe", "-r -t 0");
        }

        private static void InstallUpdates(UpdateSession updateSession, ISearchResult searchResult)
        {
            var installer = updateSession.CreateUpdateInstaller();
            installer.Updates = new UpdateCollection();
            foreach (IUpdate update in searchResult.Updates)
            {
                if (update.IsDownloaded)
                    installer.Updates.Add(update);
            }

            log.Info("Installing updates...");
            var installResult = installer.Install();

            for (int i = 0; i < installer.Updates.Count; ++i)
            {
                if (installResult.GetUpdateResult(i).HResult == 0)
                    log.Info("Successfully installed: {0}", installer.Updates[i].Title);
                else
                    log.Info("Failed to install: {0}", installer.Updates[i].Title);
            }
        }
    }
}
