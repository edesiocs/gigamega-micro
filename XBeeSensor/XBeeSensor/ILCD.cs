using System;
namespace XBeeSensor
{
    public interface ILCD
    {
        /// <summary>
        /// Clear contents of LCD and set cursor to 1st col of 1st row
        /// </summary>
        void ClearScreen();
        /// <summary>
        /// Set the cursor to column 1 of the specified row, and optionally clear that row.
        /// </summary>
        /// <param name="intLine">row # (1-based)</param>
        /// <param name="blnClearLine">if true, blanks are written to the row to clear it</param>
        void SelectLine(byte intLine, bool blnClearLine);
        /// <summary>
        /// Set LCD backlight brightness
        /// </summary>
        /// <param name="brightness">Brightness level (0-255)</param>
        void SetBrightness(byte brightness);
        /// <summary>
        /// Write string to current cursor position of LCD.  If string extends past last column
        /// of LCD, remainder will either be truncated or will wrap onto next row.  (Wrap is
        /// off by default, but can be set on by calling setAutoWrap()
        /// </summary>
        /// <param name="strData"></param>
        void WriteStringToLCD(string strData);
        /// <summary>
        /// Sets LCD Autowrap - if true, string that extends past last columns will wrap into next row.
        /// </summary>
        /// <param name="blnState">Autowrap setting</param>
        void SetAutowrap(bool blnState);
        /// <summary>
        /// Set LCD dimensions
        /// </summary>
        /// <param name="lcdRows"></param>
        /// <param name="lcdCols"></param>
        void SetScreenSize(byte lcdRows, byte lcdCols);
        void SetCursorOn(bool blnBlock);
        void SetCursorOff(bool blnBlock);
        void SetCursorPos(byte row, byte col);
        byte GetNumRows();
        byte GetNumCols();
        void SetBacklight(bool blnOn);
    }
}
