﻿using Jackett;
using Jackett.Indexers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JackettConsole
{
    public class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    switch (args[0].ToLowerInvariant())
                    {
                        case "/i":
                            Engine.ServiceConfig.Install();
                            return;
                        case "/r":
                            Engine.Server.ReserveUrls();
                            return;
                        case "/u":
                            Engine.Server.ReserveUrls(false);
                            Engine.ServiceConfig.Uninstall();
                            return;
                    }
                }

                Engine.Server.Start();
                Engine.Logger.Info("Running in headless mode.");
                Engine.RunTime.Spin();
                Engine.Logger.Info("Server thread exit");
            }
            catch(Exception e)
            {
                 Engine.Logger.Error(e, "Top level exception");
            }
        }
    }
}
