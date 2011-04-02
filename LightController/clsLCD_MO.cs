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

    public class clsLCD_MO : ILCD
    {
        private SerialPort lcd;

        private byte numRows;
        private byte numCols;
        private string strBlankRow;

        /// <summary>
        /// Create instance of a Serial LCD using the Matrix Orbital command set
        /// </summary>
        /// <param name="strPort">Serial port ID (e.g. "COM1", "COM2") </param>
        /// <param name="intBaud">Baud rate (e.g. 9600, 19200</param>
        /// <param name="rows"># of rows</param>
        /// <param name="cols"># of columms</param>
        public clsLCD_MO(string strPort, Int16 intBaud, byte rows, byte cols)
        {
            lcd = new SerialPort(strPort, intBaud, Parity.None, 8, StopBits.One);
            lcd.Open();
            this.numRows = rows;
            this.numCols = cols;

            strBlankRow = new string(' ', cols);

        }

        /// <summary>
        /// Clear contents of LCD and set cursor to 1st col of 1st row
        /// </summary>
        public void ClearScreen()
        {

            byte[] command = new byte[] { 254, 88 };
            // NOTE - Matrix Orbital clear command also sets cursor to home
            lcd.Write(command, 0, command.Length);
        }

        /// <summary>
        /// Set the cursor to column 1 of the specified row, and optionally clear that row.
        /// </summary>
        /// <param name="intLine">row # (1-based)</param>
        /// <param name="blnClearLine">if true, blanks are written to the row to clear it</param>
        public void SelectLine(byte intLine, bool blnClearLine)
        {

            SetCursorPos(intLine, 1);

            if (blnClearLine)
            {
                Thread.Sleep(10); // let LCD catch up
                WriteStringToLCD(strBlankRow);
                Thread.Sleep(10); // let LCD catch up
                SetCursorPos(intLine, 1);
            }
        }

        /// <summary>
        /// Set LCD backlight brightness
        /// </summary>
        /// <param name="brightness">Brightness level (0-255)</param>
        public void SetBrightness(byte brightness)
        {
            byte[] command = new byte[3];
            command[0] = 254;
            command[1] = 153;
            command[2] = brightness; 
 
            lcd.Write(command, 0, command.Length);

        }

        /// <summary>
        /// Sets LCD Autowrap - if true, string that extends past last columns will wrap into next row.
        /// </summary>
        /// <param name="blnState">Autowrap setting</param>
        public void SetAutowrap(bool blnState)
        {
            byte[] command = new byte[2];
            command[0] = 254;
            if (blnState)
            {
                command[1] = 67;
            }
            else
            {
                command[1] = 68;
            }

            lcd.Write(command, 0, command.Length);
        }

        /// <summary>
        /// Write string to current cursor position of LCD.  If string extends past last column
        /// of LCD, remainder will either be truncated or will wrap onto next row.  (Wrap is
        /// off by default, but can be set on by calling setAutoWrap()
        /// </summary>
        /// <param name="strData"></param>
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
                command[1] = 83; // block cursor
            }
            else
            {
                command[1] = 74; // underline cursor
            }

            lcd.Write(command, 0, command.Length);
        }

        public void SetCursorOff(bool blnBlock)
        {
            byte[] command = new byte[2];
            command[0] = 254;
            // turn off underline cursor
            command[1] = 75;
            lcd.Write(command, 0, command.Length);
            Thread.Sleep(10);

            // turn off block cursor
            command[1] = 84;
            lcd.Write(command, 0, command.Length);
        }

        public void SetCursorPos(byte row, byte col)
        {
            byte[] command = new byte[4];
            command[0] = 254;
            command[1] = 71;
            command[2] = col; // always column 1
            command[3] = row;

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

        public void SetBacklight(bool blnOn)
        {
            byte[] command;
            if (blnOn)
            {
                command = new byte[3];
                command[0] = 254;
                command[1] = 66;
                // 3 parm is 0 - never turn off
                lcd.Write(command, 0, command.Length);
            }
            else
            {
                command = new byte[2];
                command[0] = 254;
                command[1] = 70;
                lcd.Write(command, 0, command.Length);
            }
        }
    }
}
