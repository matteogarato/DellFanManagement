﻿namespace DellFanManagement.App.FanControllers
{
    /// <summary>
    /// Determine system capabilities and select an appropriate fan speed controller to use.
    /// </summary>
    internal class FanControllerFactory
    {
        /// <summary>
        /// Selects a fan speed reader and returns it.
        /// </summary>
        /// <returns>Fan speed reader appropriate for the system.</returns>
        public static FanController GetFanFanController()
        {
            FanController controller = new BzhFanController();
            if (controller.IsAutomaticFanControlDisableSupported)
            {
                return controller;
            }
            else
            {
                return new NullFanController();
            }

            /*
            if (DellSmbiosSmi.IsFanControlOverrideAvailable())
            {
                // If the WMI/SMI interface is available, use it.
                Log.Write("Using SMI fan control.");
                return new SmiFanController();
            }
            else
            {
                // Fall back to BZH SMM fan control.
                Log.Write("Using BZH fan control.");
                return new BzhFanController();
            }
            */
        }
    }
}