using System;
using Microsoft.SPOT;
using System.IO.Ports;
using System.Text;
using System.Threading;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace LightController
{
    public class clsLCD_SF : ILCD
    {

        protected SerialPort lcd;
        protected byte numRows;
        protected byte numCols;
        protected string strBlankRow;

        public clsLCD_SF(string strPort, Int16 intBaud, byte rows, byte cols)
        {
            lcd = new SerialPort(strPort, intBaud, Parity.None, 8, StopBits.One);
            lcd.Open();
            SetScreenSize(rows, cols);
            // removed - screen works without it, but is garbled with it
            //SF_SetScreenSize(rows, cols);
            strBlankRow = new string(' ', cols);

        }

        // empty constructor used when subclassed
        public clsLCD_SF()
        {
            
        }

        public void ClearScreen()
        {

            byte[] command = new byte[] { 254, 1 };
            lcd.Write(command, 0, command.Length);
        }

        public void SelectLine(byte intLine, bool blnClearLine)
        {

            byte[] command = new byte[2];
            command[0] = 254;

            switch (intLine)
            {
                case 1:
                    command[1] = 128;
                    break;
                case 2:
                    command[1] = 128 + 64;
                    break;
                case 3:
                    command[1] = 128 + 20;
                    break;
                case 4:
                    command[1] = 128 + 84;
                    break;
                default:
                    // shouldn't happen
                    return;
            }


            lcd.Write(command, 0, command.Length);


            if (blnClearLine)
            {
                Thread.Sleep(10); // let LCD catch up
                WriteStringToLCD(strBlankRow);
                Thread.Sleep(10); // let LCD catch up
                lcd.Write(command, 0, command.Length);
            }
            
        }

        public void SetBrightness(byte bytBrightness)
        {
            try
            {
                byte[] command = new byte[2];
                byte bytMappedBrightness = (byte)map(bytBrightness, 0, 255, 128, 157);
                command[0] = 124;
                command[1] = bytMappedBrightness;
                lcd.Write(command, 0, command.Length);
                if (bytBrightness != 0)
                {
                    bytLastBrightness = bytBrightness;
                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in SetBrightness: " + ex.Message);
            }

        }

        private long map(long x, long in_min, long in_max, long out_min, long out_max)
        {
            return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
        }

        //public void SF_SetScreenSize(byte lcdRows, byte lcdCols)
        //{
        //    byte[] command = new byte[2];
        //    command[0] = 124;
        //    if (lcdRows == 4)
        //    {
        //        command[1] = 5;
        //    }
        //    else
        //    {
        //        command[1] = 6; // 2 rows
        //    }
        //    lcd.Write(command, 0, command.Length);
        //    Thread.Sleep(10); // pause between commands
        //    command[0] = 124;
        //    if (lcdCols == 20)
        //    {
        //        command[1] = 3;
        //    }
        //    else
        //    {
        //        command[1] = 4; // 16 chars
        //    }
        //    lcd.Write(command, 0, command.Length);
        //}

        public void SetAutowrap(bool blnState)
        {
            // not supported by Sparkfun Serial Backpack
        }

        public void WriteStringToLCD(String strData)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(strData);
            lcd.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Set LCD dimensions
        /// </summary>
        /// <param name="lcdRows"></param>
        /// <param name="lcdCols"></param>
        public void SetScreenSize(byte lcdRows, byte lcdCols)
        {
            numRows = lcdRows;
            numCols = lcdCols;
            //NOTE - the Matrix Orbital command set doesn't include a setting for screen size
        }

        public void SetCursorOn(bool blnBlock)
        {
            byte[] command = new byte[2];
            command[0] = 254;
            if (blnBlock)
            {
                command[1] = 13;
            }
            else
            {
                command[1] = 14; // underline cursor
            }

            lcd.Write(command, 0, command.Length);
        }

        public void SetCursorOff(bool blnBlock)
        {
            byte[] command = new byte[2];
            command[0] = 254;
            command[1] = 12;
            lcd.Write(command, 0, command.Length);

        }

        public void SetCursorPos(byte row, byte col)
        {
            byte[] command = new byte[2];
            command[0] = 254;

            switch (row)
            {
                case 1:
                    command[1] = (byte)(127 + col);
                    break;
                case 2:
                    command[1] = (byte)(128 + 63 + col);
                    break;
                case 3:
                    command[1] = (byte)(128 + 19 + col);
                    break;
                case 4:
                    command[1] = (byte)(128 + 83 + col);
                    break;
                default:
                    // shouldn't happen
                    return;
            }


            lcd.Write(command, 0, command.Length);


        }

        public byte GetNumRows()
        {
            return numRows;
        }

        public byte GetNumCols()
        {
            return numCols;
        }

        private byte bytLastBrightness;
        public void SetBacklight(bool blnOn)
        {
            if (blnOn)
            {
                if (bytLastBrightness == 0)
                {
                    bytLastBrightness = 128; // set halfway;
                }
                SetBrightness(bytLastBrightness);
            }
            else
            {
                SetBrightness(0);
            }
        }
    }
}
