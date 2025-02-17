﻿using DellFanManagement.DellSmbiosSmiLib.DellSmi;
using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace DellFanManagement.DellSmbiosSmiLib
{
    /// <summary>
    /// Handles issuing commands to the SMM BIOS via the ACPI/WMI interface.
    /// </summary>
    public static class DellSmbiosSmi
    {
        private static readonly string WmiScopeRoot = "root/wmi";
        private static readonly string WmiClassNameBdat = "BDat";
        private static readonly string WmiClassNameBfn = "BFn";
        private static readonly string WmiBfnMethodDobfn = "DoBFn";
        private static readonly string AcpiManagementInterfaceHardwareId = @"ACPI\PNP0C14\0_0";

        private static readonly int MinimumBufferLength = 36;
        private static readonly int BufferLength = 32768;

        /// <summary>
        /// Get the current thermal setting for the system.
        /// </summary>
        /// <returns>Current thermal setting; ThermalSetting.Error on error</returns>
        public static ThermalSetting GetThermalSetting()
        {
            try
            {
                SmiObject message = new SmiObject
                {
                    Class = Class.Info,
                    Selector = Selector.ThermalMode
                };

                bool result = ExecuteCommand(ref message);
                if (result)
                {
                    return (ThermalSetting)message.Output3;
                }
                else
                {
                    return ThermalSetting.Error;
                }
            }
            catch (Exception)
            {
                return ThermalSetting.Error;
            }
        }

        /// <summary>
        /// Apply a new thermal setting to the system.
        /// </summary>
        /// <param name="thermalSetting">Thermal setting to apply</param>
        /// <returns>True if successful, false if not</returns>
        public static bool SetThermalSetting(ThermalSetting thermalSetting)
        {
            if (thermalSetting != ThermalSetting.Error)
            {
                try
                {
                    SmiObject message = new SmiObject
                    {
                        Class = Class.Info,
                        Selector = Selector.ThermalMode,
                        Input1 = 1,
                        Input2 = (uint)thermalSetting
                    };

                    return ExecuteCommand(ref message);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determine whether or not fan control override is available through the WMI/SMI interface.
        /// </summary>
        /// <returns>True if fan control override is available, false if not.</returns>
        public static bool IsFanControlOverrideAvailable()
        {
            try
            {
                SmiObject? message = GetToken(Token.FanControlOverrideEnable);
                return (message?.Output1 == 0);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Disable automatic fan control.
        /// </summary>
        /// <returns>True on success, false on failure.</returns>
        public static bool DisableAutomaticFanControl()
        {
            return SetToken(Token.FanControlOverrideEnable);
        }

        /// <summary>
        /// Enable automatic fan control.
        /// </summary>
        /// <returns>True on success, false on failure.</returns>
        public static bool EnableAutomaticFanControl()
        {
            return SetToken(Token.FanSpeedAuto) && SetToken(Token.FanControlOverrideDisable);
        }

        /// <summary>
        /// Sets the system fan level.
        /// </summary>
        /// <param name="level">Which fan level to set.</param>
        /// <returns>True on success, false on failure.</returns>
        public static bool SetFanLevel(SmiFanLevel level)
        {
            Token token;

            /// ...The actual system behavior doesn't match up with the token names in the libsmbios documentation.
            switch (level)
            {
                case SmiFanLevel.Off:
                    token = Token.FanSpeedMediumHigh;
                    break;

                case SmiFanLevel.Low:
                    token = Token.FanSpeedMedium;
                    break;

                case SmiFanLevel.Medium:
                    token = Token.FanSpeedHigh;
                    break;

                case SmiFanLevel.High:
                    token = Token.FanSpeedLow;
                    break;

                default:
                    return false;
            }

            return SetToken(token);
        }

        /// <summary>
        /// Get the current value of a token.
        /// </summary>
        /// <param name="token">Token to get value of.</param>
        /// <returns>Current setting for the token; null if failure.</returns>
        public static uint? GetTokenCurrentValue(Token token)
        {
            return GetToken(token)?.Output2;
        }

        /// <summary>
        /// Get the value that should be used to set this token.
        /// </summary>
        /// <param name="token">Token to get the "set" value for.</param>
        /// <returns>"Set" value for the token; null if failure.</returns>
        public static uint? GetTokenSetValue(Token token)
        {
            return GetToken(token)?.Output3;
        }

        /// <summary>
        /// Execute a "get token" call and return the entire result.
        /// </summary>
        /// <param name="token">Token to get.</param>
        /// <returns>SmiObject result of the token call; null if failure.</returns>
        public static SmiObject? GetToken(Token token)
        {
            SmiObject message = new SmiObject
            {
                Class = Class.TokenRead,
                Selector = Selector.Standard,
                Input1 = (uint)token
            };

            ExecuteCommand(ref message);

            if (message.Output1 == 0)
            {
                return message;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Set a token.
        /// </summary>
        /// <param name="token">Token to set.</param>
        /// <param name="value">Optional value; for "bit" tokens it can be pulled automatically.</param>
        /// <param name="selector">Optional selector.</param>
        /// <returns>True on success, false on failure.</returns>
        public static bool SetToken(Token token, uint? value = null, Selector selector = Selector.Standard)
        {
            // If a value wasn't provided, query the SMBIOS for what it should be.
            if (value == null)
            {
                value = GetTokenSetValue(token);
                if (value == null)
                {
                    return false;
                }
            }

            SmiObject message = new SmiObject
            {
                Class = Class.TokenWrite,
                Selector = selector,
                Input1 = (uint)token,
                Input2 = (uint)value,
                //Input3 = securityKey
                // TODO: Implement security key.
            };

            if (ExecuteCommand(ref message))
            {
                return message.Output1 == 0;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Fetch the password encoding format from the BIOS.
        /// </summary>
        /// <param name="which">Which type of password to request the encoding format for.</param>
        /// <param name="properties">Optional password properties object; it will be fetched if not provided.</param>
        /// <returns>Password encoding format, or null on error.</returns>
        /// <seealso cref="https://github.com/dell/libsmbios/blob/master/src/libsmbios_c/smi/smi_password.c"/>
        public static SmiPasswordFormat? GetPasswordFormat(SmiPassword which, PasswordProperties? properties = null)
        {
            if (properties == null)
            {
                properties = GetPasswordProperties(which);
            }

            if (properties != null)
            {
                if ((properties?.Characteristics & 1) != 0)
                {
                    return SmiPasswordFormat.Ascii;
                }
                else
                {
                    return SmiPasswordFormat.Scancode;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Fetch password properties from the BIOS.
        /// </summary>
        /// <param name="which">Which type of password to request properties for.</param>
        /// <returns>Password properties structure, or null on error.</returns>
        /// <seealso cref="https://github.com/dell/libsmbios/blob/master/src/libsmbios_c/smi/smi_password.c"/>
        public static PasswordProperties? GetPasswordProperties(SmiPassword which)
        {
            SmiObject message = new SmiObject
            {
                Class = (Class)which,
                Selector = Selector.PasswordProperties
            };

            if (ExecuteCommand(ref message) && message.Output1 == 0)
            {
                return new PasswordProperties
                {
                    Installed = (SmiPasswordInstalled)Utility.GetByte(0, message.Output2),
                    MaximumLength = Utility.GetByte(1, message.Output2),
                    MinimumLength = Utility.GetByte(2, message.Output2),
                    Characteristics = Utility.GetByte(3, message.Output2),
                    MinimumAlphabeticCharacters = Utility.GetByte(0, message.Output3),
                    MinimumNumericCharacters = Utility.GetByte(1, message.Output3),
                    MinimumSpecialCharacters = Utility.GetByte(2, message.Output3),
                    MaximumRepeatingCharacters = Utility.GetByte(3, message.Output3)
                };
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Get the security key.
        /// </summary>
        /// <param name="which">Which type of BIOS password is being provided.</param>
        /// <param name="password">BIOS password.</param>
        /// <returns>Security key, null on failure.</returns>
        /// <seealso cref="https://github.com/dell/libsmbios/blob/master/src/libsmbios_c/smi/smi_password.c"/>
        public static uint? GetSecurityKey(SmiPassword which, string password)
        {
            // NOTE – This is work in progress, currently not functional.

            PasswordProperties? properties = GetPasswordProperties(which);
            if (properties != null)
            {
                if (properties?.Installed == SmiPasswordInstalled.Installed)
                {
                    uint? key = GetSecurityKeyNew(which, password, (PasswordProperties)properties);
                    if (key != null)
                    {
                        return key;
                    }
                    else
                    {
                        // TODO: Try "old" method.
                        return null;
                    }
                }
                else
                {
                    // No password has been set.
                    return 0;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Get the security key using the "new" method.
        /// </summary>
        /// <param name="which">Which type of BIOS password is being provided.</param>
        /// <param name="password">BIOS password.</param>
        /// <param name="properties">Pre-filled password properties object.</param>
        /// <returns>Security key, null on failure.</returns>
        /// <seealso cref="https://github.com/dell/libsmbios/blob/master/src/libsmbios_c/smi/smi_password.c"/>
        private static uint? GetSecurityKeyNew(SmiPassword which, string password, PasswordProperties properties)
        {
            // NOTE – Non-functional, need to figure out the string pointer before it will work.

            if (GetPasswordFormat(which, properties) == SmiPasswordFormat.Scancode)
            {
                throw new NotImplementedException("BIOS wants scancode-encoded passwords, but only ASCII-encoded passwords are supported at this time.");
            }

            SmiObject message = new SmiObject
            {
                Class = (Class)which,
                Selector = Selector.VerifyPasswordNew
            };

            // Allocate a buffer for the password.
            int bufferSize = properties.MaximumLength * 2;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

            // Zero out the buffer.
            for (byte index = 0; index < bufferSize; index++)
            {
                Marshal.WriteByte(buffer, index, 0);
            }

            // Copy password into the buffer (ASCII-encoded).
            byte[] passwordBytes = ASCIIEncoding.ASCII.GetBytes(password);
            Marshal.Copy(passwordBytes, 0, buffer, Math.Min(password.Length, bufferSize));

            message.Input1 = (uint)buffer.ToInt32();

            ExecuteCommand(ref message);

            Marshal.FreeHGlobal(buffer);

            if (message.Input1 == (uint)SmiPasswordCheckResult.Correct)
            {
                return message.Input2;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Execute a command against the SMM BIOS via the ACPI/WMI interface.
        /// </summary>
        /// <param name="message">SMM BIOS message (a packaged-up command to be executed)</param>
        /// <returns>True if successful, false if not; note that exceptions on error conditions can come back from this method as well</returns>
        public static bool ExecuteCommand(ref SmiObject message)
        {
            byte[] bytes = StructToByteArray(message);
            byte[] buffer = new byte[BufferLength];
            Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);
            bool result = ExecuteCommand(ref buffer);
            message = ByteArrayToStruct(buffer);

            return result;
        }

        /// <summary>
        /// Execute a command against the SMM BIOS via the ACPI/WMI interface.
        /// </summary>
        /// <param name="buffer">Byte array buffer containing the command to be executed; results from the command will be filled into this array.</param>
        /// <returns>True if successful, false if not; note that exceptions on error conditions can come back from this method as well</returns>
        private static bool ExecuteCommand(ref byte[] buffer)
        {
            bool result = false;

            if (buffer.Length < MinimumBufferLength)
            {
                throw new Exception(string.Format("Buffer length is less than the minimum {0} bytes", MinimumBufferLength));
            }

            ManagementBaseObject instance = new ManagementClass(WmiScopeRoot, WmiClassNameBdat, null).CreateInstance();
            instance["Bytes"] = buffer;

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(new ManagementScope(WmiScopeRoot), new SelectQuery(WmiClassNameBfn))
            {
                Options = new EnumerationOptions()
                {
                    EnsureLocatable = true
                }
            };

            foreach (ManagementObject managementObject in searcher.Get())
            {
                if (managementObject["InstanceName"].ToString().ToUpper().Equals(AcpiManagementInterfaceHardwareId))
                {
                    ManagementBaseObject methodParameters = managementObject.GetMethodParameters(WmiBfnMethodDobfn);
                    methodParameters["Data"] = instance;
                    ManagementBaseObject managementBaseObject = (ManagementBaseObject)managementObject.InvokeMethod(WmiBfnMethodDobfn, methodParameters, null).Properties["Data"].Value;
                    buffer = (byte[])managementBaseObject["Bytes"];
                    result = true;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Convert a SMM BIOS message struct to a byte array.
        /// </summary>
        /// <param name="message">SMM BIOS message struct</param>
        /// <returns>Byte array</returns>
        /// <seealso cref="https://stackoverflow.com/questions/3278827/how-to-convert-a-structure-to-a-byte-array-in-c"/>
        private static byte[] StructToByteArray(SmiObject message)
        {
            int size = Marshal.SizeOf(message);
            byte[] array = new byte[size];

            IntPtr pointer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(message, pointer, true);
            Marshal.Copy(pointer, array, 0, size);
            Marshal.FreeHGlobal(pointer);
            return array;
        }

        /// <summary>
        /// Convert a byte array to a SMM BIOS message struct.
        /// </summary>
        /// <param name="array">Byte array</param>
        /// <returns>SMM BIOS message struct</returns>
        /// <seealso cref="https://stackoverflow.com/questions/3278827/how-to-convert-a-structure-to-a-byte-array-in-c"/>
        private static SmiObject ByteArrayToStruct(byte[] array)
        {
            SmiObject message = new SmiObject();

            int size = Marshal.SizeOf(message);
            IntPtr pointer = Marshal.AllocHGlobal(size);

            Marshal.Copy(array, 0, pointer, size);

            message = (SmiObject)Marshal.PtrToStructure(pointer, message.GetType());
            Marshal.FreeHGlobal(pointer);

            return message;
        }
    }
}