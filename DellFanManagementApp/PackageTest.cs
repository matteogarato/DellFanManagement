﻿using DellFanManagement.DellSmbiosSmiLib;
using DellFanManagement.DellSmbiozBzhLib;
using LibreHardwareMonitor.Hardware;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native.Exceptions;
using System;

namespace DellFanManagement.App
{
    /// <summary>
    /// The purpose of this class is to test each of the packages for basic compatibility with the selected .NET
    /// version.
    /// </summary>
    internal static class PackageTest
    {
        /// <summary>
        /// Run the package tests.
        /// </summary>
        /// <returns>True if all tests were successful, false otherwise</returns>
        public static bool RunPackageTests()
        {
            return OpenHardwareMonitorTest() && NvapiTest() && DellSmbiosBzhTest() && DellSmbiosSmiTest() && IrrKlangTest();
        }

        /// <summary>
        /// Run a quick test of the Open Hardware Montior package.
        /// </summary>
        /// <returns>True if the test was successful, false otherwise</returns>
        private static bool OpenHardwareMonitorTest()
        {
            try
            {
                Console.WriteLine("Running Open Hardware Monitor test.");

                Computer computer = new()
                {
                    IsCpuEnabled = true
                };

                computer.Open();

                foreach (IHardware hardware in computer.Hardware)
                {
                    hardware.Update();

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                        {
                            Console.WriteLine("  {0}: {1}", sensor.Name, sensor.Value);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("{0}: {1}\n{2}", exception.GetType().ToString(), exception.Message, exception.StackTrace);
                return false;
            }

            Console.WriteLine("  ...Passed.");
            return true;
        }

        /// <summary>
        /// Run a quick test of the NVAPI package.
        /// </summary>
        /// <returns>True if the test was successful, false otherwise</returns>
        private static bool NvapiTest()
        {
            bool foundGpu = false;

            try
            {
                Console.WriteLine("Running NVAPI test.");

                foreach (PhysicalGPU gpu in PhysicalGPU.GetPhysicalGPUs())
                {
                    Console.WriteLine("  Found GPU: {0}", gpu.FullName);
                    foundGpu = true;

                    try
                    {
                        foreach (GPUThermalSensor sensor in gpu.ThermalInformation.ThermalSensors)
                        {
                            Console.WriteLine("    Current GPU temperature: {0}", sensor.CurrentTemperature);
                        }
                    }
                    catch (NVIDIAApiException exception)
                    {
                        if (exception.Message == "NVAPI_GPU_NOT_POWERED")
                        {
                            Console.WriteLine("    GPU is currently powered off.");
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("{0}: {1}\n{2}", exception.GetType().ToString(), exception.Message, exception.StackTrace);
                return false;
            }

            Console.WriteLine("  ...Passed.");
            if (!foundGpu)
            {
                Console.WriteLine("  (Note: No NVIDIA GPUs found.)");
            }

            return true;
        }

        /// <summary>
        /// Run a quick test of the BZH driver/library.
        /// </summary>
        /// <returns>True if the test was successful, false otherwise</returns>
        private static bool DellSmbiosBzhTest()
        {
            try
            {
                Console.WriteLine("Running DellSmbiosBzhLib test.");

                if (!DellSmbiosBzh.Initialize())
                {
                    Console.WriteLine("  Failed to load driver.");
                    return false;
                }

                uint? result = DellSmbiosBzh.GetFanRpm(BzhFanIndex.Fan1);
                Console.WriteLine("  Fan 1 RPM: {0}", result);

                DellSmbiosBzh.Shutdown();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("{0}: {1}\n{2}", exception.GetType().ToString(), exception.Message, exception.StackTrace);
                return false;
            }

            Console.WriteLine("  ...Passed.");
            return true;
        }

        /// <summary>
        /// Run a quick test of the DellSmmIoLib package.
        /// </summary>
        /// <returns>True if the test was successful, false otherwise</returns>
        private static bool DellSmbiosSmiTest()
        {
            try
            {
                Console.WriteLine("Running DellSmbiosSmiLib test.");

                ThermalSetting currentSetting = DellSmbiosSmi.GetThermalSetting();
                Console.WriteLine("Thermal setting: {0}", currentSetting);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("{0}: {1}\n{2}", exception.GetType().ToString(), exception.Message, exception.StackTrace);
                return false;
            }

            Console.WriteLine("  ...Passed.");
            return true;
        }

        /// <summary>
        /// Run a quick test of the irrKlang package.
        /// </summary>
        /// <returns>True if the test was successful, false otherwise</returns>
        private static bool IrrKlangTest()
        {
            try
            {
                Console.WriteLine("Running irrKlang test.");

                new SoundPlayer().PlaySound(@"C:\Windows\Media\Windows Logon.wav");

                foreach (AudioDevice audioDevice in Utility.GetAudioDevices())
                {
                    Console.WriteLine("  {0}: {1}", audioDevice.DeviceId, audioDevice.DeviceName);
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("{0}: {1}\n{2}", exception.GetType().ToString(), exception.Message, exception.StackTrace);
                return false;
            }

            Console.WriteLine("  ...Passed.");
            return true;
        }
    }
}