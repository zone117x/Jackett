﻿using CommandLine;
using CommandLine.Text;
using Jackett;
using Jackett.Console;
using Jackett.Utils;
using System;

namespace JackettConsole
{
    public class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var options = new ConsoleOptions();
                if (!Parser.Default.ParseArguments(args, options) || options.ShowHelp == true)
                {
                    var text = HelpText.AutoBuild(options, (HelpText current) => HelpText.DefaultParsingErrorsHandler(options, current));
                    text.Copyright = " ";
                    text.Heading = "Jackett v" + Engine.ConfigService.GetVersion() + " options:";
                    Console.WriteLine(text);
                    Environment.ExitCode = 1;
                    return;
                }
                else
                {
                    /*  ======     Options    =====  */

                    // Use curl
                    if (options.UseCurlExec)
                        Startup.CurlSafe = true;

                    // Logging
                    if (options.Logging)
                        Startup.LogRequests = true;

                    // Tracing
                    if (options.Tracing)
                        Startup.TracingEnabled = true;

                    // Log after the fact as using the logger will cause the options above to be used

                    if (options.UseCurlExec)
                        Engine.Logger.Info("Safe curl enabled.");

                    if (options.Logging)
                        Engine.Logger.Info("Logging enabled.");

                    if (options.Tracing)
                        Engine.Logger.Info("Tracing enabled.");

                    /*  ======     Actions    =====  */

                    // Install service
                    if (options.Install)
                    {
                        Engine.ServiceConfig.Install();
                        return;
                    }

                    // Uninstall service
                    if (options.Uninstall)
                    {
                        Engine.Server.ReserveUrls(doInstall: false);
                        Engine.ServiceConfig.Uninstall();
                        return;
                    }

                    // Reserve urls
                    if (options.ReserveUrls)
                    {
                        Engine.Server.ReserveUrls(doInstall: true);
                        return;
                    }

                    // Start Service
                    if (options.StartService)
                    {
                        if (!Engine.ServiceConfig.ServiceRunning())
                        {
                            Engine.ServiceConfig.Start();
                        }
                        return;
                    }

                    // Stop Service
                    if (options.StopService)
                    {
                        if (Engine.ServiceConfig.ServiceRunning())
                        {
                            Engine.ServiceConfig.Stop();
                        }
                        return;
                    }

                    // Migrate settings
                    if (options.MigrateSettings)
                    {
                        Engine.ConfigService.PerformMigration();
                        return;
                    }


                    // Show Version
                    if (options.ShowVersion)
                    {
                        Console.WriteLine("Jackett v" + Engine.ConfigService.GetVersion());
                        return;
                    }

                    /*  ======     Overrides    =====  */

                    // Override listen public
                    if(options.ListenPublic.HasValue)
                    {
                        if (Engine.Server.Config.AllowExternal != options.ListenPublic)
                        {
                            Engine.Logger.Info("Overriding external access to " + options.ListenPublic);
                            Engine.Server.Config.AllowExternal = options.ListenPublic.Value;
                            if (System.Environment.OSVersion.Platform != PlatformID.Unix)
                            {
                                if (ServerUtil.IsUserAdministrator())
                                {
                                    Engine.Server.ReserveUrls(doInstall: true);
                                }
                                else
                                {
                                    Engine.Logger.Error("Unable to switch to public listening without admin rights.");
                                    Environment.ExitCode = 1;
                                    return;
                                }
                            }

                            Engine.Server.SaveConfig();
                        }
                    }

                    // Override port
                    if(options.Port != 0)
                    {
                        if (Engine.Server.Config.Port != options.Port)
                        {
                            Engine.Logger.Info("Overriding port to " + options.Port);
                            Engine.Server.Config.Port = options.Port;
                            if (System.Environment.OSVersion.Platform != PlatformID.Unix)
                            {
                                if (ServerUtil.IsUserAdministrator())
                                {
                                    Engine.Server.ReserveUrls(doInstall: true);
                                }
                                else
                                {
                                    Engine.Logger.Error("Unable to switch ports when not running as administrator");
                                    Environment.ExitCode = 1;
                                    return;
                                }
                            }

                            Engine.Server.SaveConfig();
                        }
                    }
                }

                Engine.Server.Initalize();
                Engine.Server.Start();
                Engine.Logger.Info("Running in console mode!");
                Engine.RunTime.Spin();
                Engine.Logger.Info("Server thread exit");
            }
            catch (Exception e)
            {
                Engine.Logger.Error(e, "Top level exception");
            }
        }
    }
}

