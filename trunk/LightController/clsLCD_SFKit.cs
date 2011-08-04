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
    public class clsLCD_SFKit : clsLCD_SF
    {

        private byte[] readBuffer = new byte[100];
        private int intBytesRead = 0;
        private int intBufferOffset = 0;
        const byte CMD_SPECIAL = 0xFE;
        public event CommandEventHandler Command;

        public clsLCD_SFKit(string strPort, Int16 intBaud, byte rows, byte cols)
        {
            lcd = new SerialPort(strPort, intBaud, Parity.None, 8, StopBits.One);
            lcd.Open();
            SetScreenSize(rows, cols);
  
            strBlankRow = new string(' ', cols);
            lcd.DataReceived += new SerialDataReceivedEventHandler(lcd_DataReceived);
        }

        void lcd_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // read the data
                intBytesRead = lcd.Read(readBuffer, intBufferOffset, lcd.BytesToRead);
                Debug.Print("in lcd_DataReceived: " + intBytesRead);
                if (intBytesRead == 0 || readBuffer[intBufferOffset + intBytesRead - 1] != 0x0a)
                {
                    // only got a partial command, so append whatever comes next to this partial command
                    intBufferOffset += intBytesRead;
                    return;
                }

                // ensure there is an ending 0x00 for the string
                readBuffer[intBufferOffset + intBytesRead] = 0x00;
                String strInData = new String(Encoding.UTF8.GetChars(readBuffer));
                intBufferOffset = 0;
                Debug.Print("Got data: " + strInData);

                // if there is more than 1 command, each one is terminated by cr/lf

                string[] strCommands = strInData.Split(new char[] { (char)10, (char)13 });

                for (int i = 0; i < strCommands.Length; i++)
                {
                    if (strCommands[i] != "") // ignore empty string between CR and LF
                    {
                        // if it looks like a command, tell rest of application that it was received
                        if (strCommands[i][1] == ':')
                        {
                            raiseCommand(strCommands[i]);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Debug.Print("Exception in lcd_DataReceived: " + ex.Message);
                Debug.Assert(false);
            }
        }

        /// <summary>
        /// Raise event telling rest of program that a command was received over the serial port
        /// </summary>
        /// <param name="strCommand">Command text (format is x:[xxxx...])</param>
        /// <returns>true if command was handled by one of the event recipients</returns>
        private bool raiseCommand(string strCommand)
        {
            if (Command == null)
            {
                // there are no event handlers out there
                return false;
            }
            CommandArgs args = new CommandArgs(strCommand);
            // raise event using Command delegate
            Command(this, args);
            return args.blnHandled;
        }

        new public void SelectLine(byte intLine, bool blnClearLine)
        {

            byte[] command = new byte[3];
            command[0] = CMD_SPECIAL;
            command[1] = 0x80;
            command[2] = (byte)((intLine - 1) * numCols);


            lcd.Write(command, 0, command.Length);


            if (blnClearLine)
            {
                Thread.Sleep(10); // let LCD catch up
                WriteStringToLCD(strBlankRow);
                Thread.Sleep(10); // let LCD catch up
                lcd.Write(command, 0, command.Length);
            }
            
        }

        new public void SetBrightness(byte bytBrightness)
        {
            try
            {
                byte[] command = new byte[2];
                command[0] = 0x80;
                command[1] = bytBrightness;
                lcd.Write(command, 0, command.Length);

            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in SetBrightness: " + ex.Message);
            }

        }

        /// <summary>
        /// Set LCD dimensions
        /// </summary>
        /// <param name="lcdRows"></param>
        /// <param name="lcdCols"></param>
        new public void SetScreenSize(byte lcdRows, byte lcdCols)
        {
            numRows = lcdRows;
            numCols = lcdCols;

            byte[] command = new byte[2];
            command[0] = CMD_SPECIAL;

            // set num rows
            lcd.Write(command, 0, command.Length);
            if (lcdRows == 4)
            {
                command[1] = 5;
            }
            else
            {
                // 2 rows - only other setting supported
                command[1] = 6;
            }
            lcd.Write(command, 0, command.Length);
            Thread.Sleep(10); // let LCD catch up

            // set num cols
            if (lcdCols == 20)
            {
                command[1] = 3;
            }
            else
            {
                // 16 cols - only other settings supported
                command[1] = 4;
            }
            lcd.Write(command, 0, command.Length);

        }


        new public void SetCursorPos(byte row, byte col)
        {
            byte[] command = new byte[3];
            command[0] = CMD_SPECIAL;
            command[1] = 0x80;
            command[2] = (byte)((row - 1) * numCols + col  - 1);


            lcd.Write(command, 0, command.Length);
        }


        public void RequestSettings()
        {
            byte[] command = new byte[2];
            command[0] = CMD_SPECIAL;
            command[1] = 70;
            lcd.Write(command, 0, command.Length);
        }


        public void RequestHumidityAndTemp()
        {
            byte[] command = new byte[2];
            command[0] = CMD_SPECIAL;
            command[1] = 71;
            lcd.Write(command, 0, command.Length);
        }

        public void RequestSetting(byte address)
        {
            byte[] command = new byte[3];
            command[0] = CMD_SPECIAL;
            command[1] = 72;
            command[2] = address;
            lcd.Write(command, 0, command.Length);
           
        }

        public void SaveSetting(byte address, byte setting)
        {
            byte[] command = new byte[4];
            command[0] = CMD_SPECIAL;
            command[1] = 73;
            command[2] = address;
            command[3] = setting;
            lcd.Write(command, 0, command.Length);
        }
    }
}
