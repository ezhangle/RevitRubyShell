﻿using System;
using Autodesk.Revit.UI;
using System.Xml.Linq;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using System.Windows;
using System.Collections.Generic;
using Microsoft.Scripting.Hosting;
using IronRuby;
using SThread = System.Threading;
using Autodesk.Revit.UI.Events;

namespace RevitRubyShell
{
    [Regeneration(RegenerationOption.Manual)]
    [Transaction(TransactionMode.Manual)]
    class RevitRubyShellApplication : IExternalApplication
    {
        //Task queue for executing script
        private static Queue<Action<UIApplication>> tasks;

        //Ironruby
        private ScriptEngine _rubyEngine;
        private ScriptScope _scope;
    
        public ScriptEngine RubyEngine { get { return _rubyEngine; } }        
        public ScriptScope RubyScope { get { return _scope; } }
      
        #region IExternalApplication Members

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            //Create panel
            RibbonPanel ribbonPanel = application.CreateRibbonPanel("Ruby scripting");
            PushButton pushButton = ribbonPanel.AddItem(new PushButtonData("RevitRubyShell", "Open Shell",
                                       typeof(RevitRubyShellApplication).Assembly.Location,
                                      "RevitRubyShell.ShellCommand")) as PushButton;
            pushButton.LargeImage = GetImage("console-5.png");
            
            //Start ruby interpreter
            _rubyEngine = Ruby.CreateEngine();
            _scope = _rubyEngine.CreateScope();
            // Cute little trick: warm up the Ruby engine by running some code on another thread:
            new SThread.Thread(new SThread.ThreadStart(() => _rubyEngine.Execute("2 + 2", _scope))).Start();

            //Init ideling 
            tasks = new Queue<Action<UIApplication>>();
            application.Idling += OnIdling;

            return Result.Succeeded;
        }
        
        #endregion
        
        #region App Icon handling
        private BitmapImage GetImage(string resourcePath)
        {
            var image = new BitmapImage();

            string moduleName = this.GetType().Assembly.GetName().Name;
            string resourceLocation =
                string.Format("pack://application:,,,/{0};component/{1}", moduleName,
                              resourcePath);

            try
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.UriSource = new Uri(resourceLocation);
                image.EndInit();
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e.ToString());
            }

            return image;
        }

        #endregion

        #region Commands        
        public static XDocument GetSettings()
        {
            //Whould be nice to use YAML instead!
            string assemblyFolder = new FileInfo(typeof(RevitRubyShellApplication).Assembly.Location).DirectoryName;
            string settingsFile = System.IO.Path.Combine(assemblyFolder, "RevitRubyShell.xml");
            return XDocument.Load(settingsFile);
        }

        public static RevitRubyShellApplication GetApplication(ExternalCommandData commandData)
        {
            //Get the externalApp
            ExternalApplicationArray apps = commandData.Application.LoadedApplications;
            foreach (IExternalApplication app in apps)
            {
                if (app is RevitRubyShellApplication)
                {
                    return (RevitRubyShellApplication) app;
                }
            }
            return null;
        }

        public static void EnqueueTask(Action<UIApplication> task)
        {
            lock (tasks)
            {
                tasks.Enqueue(task);
            }
        }

        public bool ExecuteCode(string code, ref string output)
        {
            try
            {
                // Run the code
                var result = _rubyEngine.Execute(code, _scope);
                // write the result to the output window
                output = string.Format("=> {0}\n", ((IronRuby.Runtime.RubyContext)Microsoft.Scripting.Hosting.Providers.HostingHelpers.GetLanguageContext(_rubyEngine)).Inspect(result));
                return true;              
            }
            catch (Microsoft.Scripting.SyntaxErrorException e)
            {
                output = string.Format("Syntax error at line {1}: {0}\n", e.Message, e.Line);
            }
            catch (Exception e)
            {
                var exceptionService = _rubyEngine.GetService<ExceptionOperations>();
                string message, typeName;
                exceptionService.GetExceptionMessage(e, out message, out typeName);
                output = string.Format("{0} ({1})\n", message, typeName);                
            }
            return false;
        }

        private void OnIdling(object sender, IdlingEventArgs e)
        {
            UIApplication uiapp = sender as UIApplication; 
            lock (tasks)
            {
                if (tasks.Count > 0)
                {
                    Action<UIApplication> task = tasks.Dequeue();
                    task(uiapp);                   
                }
            }
        }


        #endregion

    }
}
