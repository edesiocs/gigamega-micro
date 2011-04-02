using System;
using Microsoft.SPOT;
using System.IO.Ports;
using System.Text;
using System.Threading;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using MicroLiquidCrystal;

namespace LightController
{
    public class clsLCD_MLC : ILCD
    {
        private GpioLcdTransferProvider lcdProvider;
        private Lcd lcd;

        private byte numRows;
        private byte numCols;
        private string strBlankRow;

        public clsLCD_MLC(Cpu.Pin rs, Cpu.Pin rw, Cpu.Pin enable, Cpu.Pin d4, Cpu.Pin d5, Cpu.Pin d6, 
            Cpu.Pin d7, byte rows, byte cols)
        {
            lcdProvider = new GpioLcdTransferProvider(rs, rw, enable, d4, d5, d6, d7);
            lcd = new Lcd(lcdProvider);
            SetScreenSize(rows, cols);
            lcd.Write("hello, world!");
            strBlankRow = new string(' ', cols);
        }

        /// <summary>
        /// Clear contents of LCD and set cursor to 1st col of 1st row
        /// </summary>
        public void ClearScreen()
        {

            lcd.Clear();
        }

        /// <summary>
        /// Set the cursor to column 1 of the specified row, and optionally clear that row.
        /// </summary>
        /// <param name="intLine">row # (1-based)</param>
        /// <param name="blnClearLine">if true, blanks are written to the row to clear it</param>
        public void SelectLine(byte intLine, bool blnClearLine)
        {
            lcd.SetCursorPosition(0, intLine - 1);
            lcd.Write(strBlankRow);
            lcd.SetCursorPosition(0, intLine - 1);
        }

        /// <summary>
        /// Set LCD backlight brightness
        /// </summary>
        /// <param name="brightness">Brightness level (0-255)</param>
        public void SetBrightness(byte brightness)
        {
            if (brightness == 0)
            {
                lcd.Backlight = false;
            }
            else
            {
                lcd.Backlight = true;
            }

        }

        /// <summary>
        /// Sets LCD Autowrap - if true, string that extends past last columns will wrap into next row.
        /// </summary>
        /// <param name="blnState">Autowrap setting</param>
        public void SetAutowrap(bool blnState)
        {
            // unimplemented
        }

        /// <summary>
        /// Write string to current cursor position of LCD.  If string extends past last column
        /// of LCD, remainder will either be truncated or will wrap onto next row.  (Wrap is
        /// off by default, but can be set on by calling setAutoWrap()
        /// </summary>
        /// <param name="strData"></param>
        public void WriteStringToLCD(String strData)
        {
            lcd.Write(strData);
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
            lcd.Begin(lcdCols, lcdRows);
        }

        public void SetCursorOn(bool blnBlock)
        {
            if (blnBlock)
            {
                lcd.BlinkCursor = true;
            }
            else
            {
                lcd.ShowCursor = true;
            }
        }

        public void SetCursorOff(bool blnBlock)
        {
            if (blnBlock)
            {
                lcd.BlinkCursor = false;
            }
            else
            {
                lcd.ShowCursor = false;
            }
        }

        public void SetCursorPos(byte row, byte col)
        {
            lcd.SetCursorPosition(col - 1, row - 1);
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
            lcd.Backlight = blnOn;
        }
    }
}
