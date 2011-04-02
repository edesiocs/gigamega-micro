using System;
using System.Threading;
using System.IO.Ports;
using System.Text;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace LightController
{
    public class clsUART
    {
        // NOTE: SerialPort requires that you add Microsoft.Spot.Hardware.SerialPort to the Project's References list
         private SerialPort UART = null;
         private int read_count = 0;
         private byte[] readBuffer;
         //private byte[] writeBuffer;
         private int intBufferLen = 0; // length of read buffer
         private int intBufferIndex = 0;
         private bool blnSendResponse;
         private string strPortName;
         private Int16 intBaud;
         public event CommandEventHandler Command;

        public clsUART(string strPort, Int16 intBaud, int intReadBufferLen, bool blnSendResponse)
        {
            intBufferLen = intReadBufferLen;
            readBuffer = new byte[intReadBufferLen];
            this.blnSendResponse = blnSendResponse;
            this.strPortName = strPort;
            this.intBaud = intBaud;
            UART = new SerialPort(strPort, intBaud, Parity.None, 8, StopBits.One);
            UART.Open();
            UART.DataReceived += new SerialDataReceivedEventHandler(UART_DataReceived);
            Debug.Print(strPort + " Opened at baud " + intBaud);
            WriteToUART("Hello World");
        }

        public void WriteToUART(String strData)
        {
            WriteToUART(strData, true);
        }

        public  void WriteToUART(String strData, bool blnSendCRLF)
        {
            if (blnSendCRLF)
            {
                strData += "\r\n";
            }

            byte[] buffer = Encoding.UTF8.GetBytes(strData);
            UART.Write(buffer, 0, buffer.Length);
        }



        private void UART_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // read the data
                read_count = UART.Read(readBuffer, intBufferIndex, UART.BytesToRead);

                // did we get a CR/LF?
                if (readBuffer[intBufferIndex + read_count - 1] == 10 || readBuffer[intBufferIndex + read_count - 1] == 13)
                {
                    // process the data and clear the buffer
                    // clear the CR/LF
                    readBuffer[intBufferIndex + read_count - 1] = 0;
                    intBufferIndex = 0;
                    String strCommand = new String(Encoding.UTF8.GetChars(readBuffer));
                   
                    processCommand(strCommand);

                }
                else
                {
                    // set the buffer pointer so that the next data read is concatenated to the end
                    intBufferIndex += read_count;
                }

            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.Print(ex.Message);
            }
        }

        private bool processCommand(string strCommand)
        {
            try
            {
                strCommand = strCommand.Trim();
                if (strCommand[1] != ':')
                {
                    if (blnSendResponse)
                    {
                        WriteToUART("?");
                        return false;
                    }
                }
                char command = strCommand[0];
                switch (command)
                {
                    case 'T':

                        // format must be hh:mm[:ss]
                        string[] strTimeParts = strCommand.Substring(2).Split(new char[] { ':', ' ', '.', '-' });
                        if (strTimeParts.Length >= 2)
                        {
                            DateTime currentDayTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                                Int16.Parse(strTimeParts[0]), Int16.Parse(strTimeParts[1]), 0);
                            if (strTimeParts.Length >= 3)
                            {
                                currentDayTime.AddSeconds(Int16.Parse(strTimeParts[1]));
                            }
                            Utility.SetLocalTime(currentDayTime);
                            Debug.Print("time is now " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second);

                            // tell rest of application that the time has been set
                            raiseCommand(strCommand); 

                            if (blnSendResponse)
                            {
                                WriteToUART("OK");
                                return true;
                            }

                        }
                        break;

                    case 'D':
                        // format must be yy-mm-dd or yyyy-mm-dd
                        string[] strDateParts = strCommand.Substring(2).Split(new char[] { ':', ' ', '.', '-' });
                        if (strDateParts.Length >= 3)
                        {
                            Int16 intYear = Int16.Parse(strDateParts[0]);
                            // if yy, convert to yyyy
                            if (intYear < 100)
                            {
                                intYear += 2000;
                            }
                           
                            DateTime currentDayTime = new DateTime(intYear, 
                                Int16.Parse(strDateParts[1]), Int16.Parse(strDateParts[2]), DateTime.Now.Hour,
                                DateTime.Now.Minute, DateTime.Now.Second);


                            Utility.SetLocalTime(currentDayTime);                            
                            Debug.Print("date is now " + DateTime.Now.Year + "-" + DateTime.Now.Month + ":" + DateTime.Now.Day);

                            // tell rest of application that the date has been set
                            raiseCommand(strCommand); 

                            if (blnSendResponse)
                            {
                                WriteToUART("OK");
                                return true;
                            }
                        }

                        break;

                    default:
                        // looks like a command - maybe some other part of the program knows how to handle it?
                        //CommandArgs args = new CommandArgs(strCommand);
                        //Command(this, args);
                        //if (args.blnHandled)
                        if (raiseCommand(strCommand))
                        {
                            WriteToUART("OK");
                            return true;
                        }
                        break;
                }
                if (blnSendResponse)
                {
                    WriteToUART("?");
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.Print(ex.Message);
                if (blnSendResponse)
                {
                    WriteToUART("?");
                }
                return false;
            }
        }

        public bool ClosePort()
        {
            if (IsOpen())
            {
                UART.Close();
                UART.Dispose();
                UART = null;
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ReopenPort()
        {
            try
            {
                if (!IsOpen())
                {
                    UART = new SerialPort(strPortName, intBaud, Parity.None, 8, StopBits.One);
                    UART.Open();
                    UART.DataReceived += new SerialDataReceivedEventHandler(UART_DataReceived);
                    Debug.Print("COM port reopened");
                    WriteToUART("Hello again World");
                    return true;
                }
                else
                {
                    return false;
                }
               
            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.Print(ex.Message);
                return false;
            }
        }

        public bool IsOpen()
        {
            if (UART != null && UART.IsOpen)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Raise event telling rest of program that a command was received over the serial port
        /// </summary>
        /// <param name="strCommand">Command text (format is x:[xxxx...])</param>
        /// <returns>true if command was handled by one of the event recipients</returns>
        private bool raiseCommand(string strCommand)
        {
            CommandArgs args = new CommandArgs(strCommand);
            // raise event using Command delegate
            Command(this, args);
            return args.blnHandled;
        }

    }

    


    public class CommandArgs : EventArgs
    {
        public string StrCommand { get; set; }

        public CommandArgs(string strCommand)
        {
            this.StrCommand = strCommand;
        }

        /// <summary>
        /// if true, command was handled by the event handler
        /// </summary>
        public bool blnHandled;
    }
}
