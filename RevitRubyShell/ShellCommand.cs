﻿using Autodesk.Revit;

namespace RevitRubyShell
{
    /// <summary>
    /// An object of this class is instantiated every time the user clicks on the
    /// button for opening the shell.
    /// </summary>
    public class ShellCommand : IExternalCommand
    {
        /// <summary>
        /// Open a window to let the user enter python code.
        /// </summary>
        /// <returns></returns>
        public IExternalCommand.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var gui = new ShellWindow(commandData.Application);
            gui.Show();
            gui.BringIntoView();
            return IExternalCommand.Result.Succeeded;
        }
    }
}
