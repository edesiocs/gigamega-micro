using System;
using System.Threading;
using System.IO.Ports;
using System.Text;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Runtime.CompilerServices; // required for methodimpl attribute
#if FEZ
using GHIElectronics.NETMF.FEZ;
using GHIElectronics.NETMF.Hardware;
#if DHT22 || DHT11
using GHIElectronics.NETMF.Native;
#endif
#else
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
#endif

namespace XBeeSensor
{
    public delegate void CommandEventHandler(object sender, CommandArgs e);

    public class Program
    {
        // !!!!!!!!!!!!!!!! USER MUST SET THE FOLLOWING 3 LINES!!!!!!!!!!!!!!!!!!!!!!!
        const int NUM_ANALOG_SENSORS = 5; // number of analog sensors
#if FEZ
        static FEZ_Pin.AnalogIn[] sensorPins = new FEZ_Pin.AnalogIn[] { FEZ_Pin.AnalogIn.An0, FEZ_Pin.AnalogIn.An1,
            FEZ_Pin.AnalogIn.An2, FEZ_Pin.AnalogIn.An3, FEZ_Pin.AnalogIn.An4};
#else
        static Cpu.Pin[] sensorPins = new Cpu.Pin[] { Pins.GPIO_PIN_A0, Pins.GPIO_PIN_A1, Pins.GPIO_PIN_A, Pins.GPIO_PIN_A3, Pins.GPIO_PIN_A4 };
#endif
        static string[] sensorIDs = new string[] { "M1", "M2", "M3", "M4", "L1", "T1", "H1" };
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

#if DHT22 || DHT11
        static byte[] bytArray = new byte[5];
        static int success;

        static RLP.Procedure ReadDHT;
        static RLP.Procedure DHTSetup;
#endif

        static ILCD lcd = null;
        static string lcdLock = "LockMe!";
        static clsUART uart = null;
        static InterruptPort buttonRed;
        static InterruptPort buttonGreen;

        static InterruptPort buttonLDR;

#if FEZ
        static AnalogIn[] sensors = new AnalogIn[NUM_ANALOG_SENSORS];
#if DHT22 || DHT11
        const int NUM_DIGITAL_SENSORS = 2;
#else
        const int NUM_DIGITAL_SENSORS = 0;
#endif
#else
        static AnalogInput[] sensors = new AnalogInput[NUM_ANALOG_SENSORS];
#endif
        static int[] sensorReadings = new int[NUM_ANALOG_SENSORS + NUM_DIGITAL_SENSORS]; // total of all readings done on sensor since last upload
        static int[] intNumReadings = new int[NUM_ANALOG_SENSORS + NUM_DIGITAL_SENSORS]; // # of readings done since last upload
        static Timer timerReadSensors;
        static Timer timerUploadSensors;

        static Timer timerButtonRed;
        static Timer timerButtonGreen;
        static bool blnButtonHeld;

        const int BUTTON_DELAY = 2000;

#if DEBUG_LCD
        private static string[] strLCDBuffer;
#endif
# if FEZ
        //private static byte[] bytFlashData;
#endif

        public static void Main()
        {
            initializeLCD();
#if DHT22 || DHT11
            initializeRLP();
#endif
            initializePins();

            // check sensors every 5 seconds, starting in 7 seconds
            timerReadSensors = new Timer(new TimerCallback(readSensorData), null, 7000, 5000);
            // upload sensor readings once a minute
            timerUploadSensors = new Timer(new TimerCallback(uploadSensorData), null, 60000, 60000);

            while (1 == 1)
            {
                // read from DHT22 every 10 seconds
                Thread.Sleep(10000); //Timeout.Infinite);
#if DHT22 || DHT11
                getDHTReadings();
                //HACK multiply by 10 to avoid decimal point with DHT22
                sensorReadings[NUM_ANALOG_SENSORS] += (int)System.Math.Round(fltTemperature*10);
                intNumReadings[NUM_ANALOG_SENSORS] += 1;
                sensorReadings[NUM_ANALOG_SENSORS + 1] += (int)System.Math.Round(fltHumidity*10);
                intNumReadings[NUM_ANALOG_SENSORS + 1] += 1;
#endif
            }

        }

        private static void initializePins()
        {
            // ---- the following pins are must-have - if initialization fails, then the program aborts ----

            // initialize - will be set to actual values based on which LCD is being used
            string strUARTPort = "COM2";
#if MATRIX_ORBITAL || SPARKFUN || MLC_I2C
            strUARTPort = "COM1";
#endif
            // last parm should be true for UART terminal (to send user confirmation), but false for UART XBee
            uart = new clsUART(strUARTPort, 9600, 512, false);
            uart.Command += new CommandEventHandler(uart_Command);

            for (int i = 0; i < NUM_ANALOG_SENSORS; i++)
            {
#if FEZ
                sensors[i] = new AnalogIn((AnalogIn.Pin)sensorPins[i]);

#else
                sensors[i] = new AnalogInput(sensorPins[i]);
#endif
            }

#if DHT22 || DHT11
            // Call setup function - use Di4 for DHT22
            if (DHTSetup != null)
            {
                success = DHTSetup.Invoke((int)(FEZ_Pin.Interrupt.Di4));
            }
            if (success == 0)
            {
                Debug_WriteToLCD("DHT Setup failed!");
                DHTSetup = null; // disable use of DHT22
            }
            else
            {
                // give DHT time to stabilize
                Thread.Sleep(1000);
            }

#endif

#if BUTTONS
            bool blnGlitchFilter = true;
#if FEZ
            blnGlitchFilter = false; // not needed for Fez
#endif
            try
            {
#if FEZ
                buttonGreen = new InterruptPort((Cpu.Pin)FEZ_Pin.Digital.Di7, blnGlitchFilter, Port.ResistorMode.PullDown,
                    Port.InterruptMode.InterruptEdgeBoth);
                buttonRed = new InterruptPort((Cpu.Pin)FEZ_Pin.Digital.Di6, blnGlitchFilter, Port.ResistorMode.PullDown,
                    Port.InterruptMode.InterruptEdgeBoth);
#else
                buttonGreen = new InterruptPort(Pins.GPIO_PIN_D8, true, SecretLabs.NETMF.Hardware.Netduino.ResistorModes.Disabled,
                    SecretLabs.NETMF.Hardware.Netduino.InterruptModes.InterruptEdgeBoth);
                buttonRed = new InterruptPort(Pins.GPIO_PIN_D7, true, SecretLabs.NETMF.Hardware.Netduino.ResistorModes.Disabled,
                    SecretLabs.NETMF.Hardware.Netduino.InterruptModes.InterruptEdgeBoth);
#endif

                buttonGreen.OnInterrupt += new NativeEventHandler(buttonGreen_OnInterrupt);
                buttonRed.OnInterrupt += new NativeEventHandler(buttonRed_OnInterrupt);

            }
            catch (Exception ex)
            {
                // catch and release
                //Debug.Assert(false);
                Debug_WriteToLCD("Exception in initializePins: " + ex.Message);
            }
#endif

#if FEZ
            buttonLDR = new  InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.LDR, 
                true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            buttonLDR.OnInterrupt += new NativeEventHandler(buttonLDR_OnInterrupt);
#endif      
        }

        static void buttonLDR_OnInterrupt(uint data1, uint data2, DateTime time)
        {

             toggleLCDBacklight();

        }

        private static void initializeLCD()
        {
            try
            {
                Monitor.Enter(lcdLock);
#if MATRIX_ORBITAL
                lcd = new clsLCD_MO("COM2", 9600, 2, 16);

#else
#if SPARKFUN
                lcd = new clsLCD_SF("COM2", 9600, 4, 20);
#else
#if MICRO_LIQUID_CRYSTAL
                lcd = new clsLCD_MLC(Pins.GPIO_PIN_D4, Pins.GPIO_PIN_D13, Pins.GPIO_PIN_D5,
                    Pins.GPIO_PIN_D9, Pins.GPIO_PIN_D10, Pins.GPIO_PIN_D11, Pins.GPIO_PIN_D12, 2, 8);
#else
#if MLC_I2C

                lcd = new clsLCD_MLC(4, 20);


#endif
#endif
#endif
#endif
                if (lcd == null)
                {
                    Debug.Print("no lcd for you!");
                    return;
                }



                lcd.ClearScreen();
                lcd.SetAutowrap(true);

                strLCDBuffer = new string[lcd.GetNumRows()];
                for (int i = 0; i < strLCDBuffer.Length; i++)
                {
                    strLCDBuffer[i] = "";
                }

            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.Print("LCD initialization failed: " + ex.Message);
            }
            finally
            {
                Monitor.Exit(lcdLock);
            }
        }

#if DHT22 || DHT11
        private static void initializeRLP()
        {
            try
            {
                // DAW: the following is required on the Panda (or Panda II)
                RLP.Enable();
                // DAW: the following is always required: it is e-mailed to you when you request an RLP key from your GHI Account Page
                //TODO - If your device isn't already unlocked, you MUST paste YOUR ID and byte array into the code below
                //RLP.Unlock();

                byte[] elf_file = Resources.GetBytes(Resources.BinaryResources.dht11_rlp);
                RLP.LoadELF(elf_file);

                RLP.InitializeBSSRegion(elf_file);

                ReadDHT = RLP.GetProcedure(elf_file, "ReadDHT");
                DHTSetup = RLP.GetProcedure(elf_file, "Setup");

                // We don't need this anymore
                elf_file = null;
                Debug.GC(true);
            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.Print("RLP initialization failed: " + ex.Message);
            }
        }
#endif

        #region Sensors

        private static void readSensorData(object state)
        {
            try
            {
                for (int i = 0; i < NUM_ANALOG_SENSORS; i++)
                {
                    int intValue = sensors[i].Read();
                    Debug_WriteToLCD("sensor " + i + " = " + intValue);
                    sensorReadings[i] += intValue;
                    intNumReadings[i] += 1;
                }               

            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug_WriteToLCD("Exception in readSensorData: " + ex.Message);
            }
        }


        private static void uploadSensorData(object data)
        {
            try
            {

                string strReading = "";
                for (int i = 0; i < NUM_ANALOG_SENSORS + NUM_DIGITAL_SENSORS; i++)
                {
                    if (intNumReadings[i] == 0)
                    {
                        // nothing read since last upload
                        Debug.Assert(false, "shouldn't happen");
                        Debug_WriteToLCD("No data for " + i);
                        continue; // skip this sensor
                    }
                    int intValue = sensorReadings[i] / intNumReadings[i];
                    //HACK - for DHT readings, need to divide by 10 to restore decimal point
                    if (i < NUM_ANALOG_SENSORS)
                    {
                        strReading += sensorIDs[i] + ":" + intValue + "\r\n";
                    }
                    else
                    {
                        float fltValue = intValue;
                        fltValue = fltValue / 10;
                        strReading += sensorIDs[i] + ":" + fltValue.ToString("F1") + "\r\n";
                    }

                }
                uart.WriteToUART(strReading);
                Debug_WriteToLCD("Sent " + strReading);
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug_WriteToLCD("Exception in uploadSensorData: " + ex.Message);
            }
            finally
            {
                // clear sensor data
                for (int i = 0; i < NUM_ANALOG_SENSORS + NUM_DIGITAL_SENSORS; i++)
                {
                    sensorReadings[i] = 0;
                    intNumReadings[i] = 0;
                }
            }

        }

        #endregion

        #region ComPort
        static void uart_Command(object sender, CommandArgs e)
        {
            try
            {

                Debug_WriteToLCD("in uart_Command: " + e.StrCommand);

            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug_WriteToLCD("Exception in uart_Command: " + ex.Message);
            }
        }

        private static void ToggleComPort()
        {
            try
            {
                if (uart != null)
                {
                    if (uart.IsOpen())
                    {
                        if (uart.ClosePort())
                        {
                            Debug_WriteToLCD("COM Port Closed");

                        }
                    }
                    else
                    {
                        if (uart.ReopenPort())
                        {
                            Debug_WriteToLCD("COM Port Opened");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug_WriteToLCD("Exception in ToggleComPort: " + ex.Message);
            }
        }

        #endregion

        // buttons are currently unused by this program
        #region Buttons
        static void buttonRed_OnInterrupt(uint port, uint state, DateTime time)
        {
            try
            {
                if (state == 1)
                {
                    // start the button hold timer
                    timerButtonRed = new Timer(new TimerCallback(buttonHold), buttonRed, BUTTON_DELAY, BUTTON_DELAY);

                }
                else
                {
                    // cancel the button hold timer
                    if (timerButtonRed != null)
                    {
                        timerButtonRed.Dispose();
                    }
                    if (blnButtonHeld)
                    {
                        // timer went off, so ignore the release
                        blnButtonHeld = false;
                        Debug.Print("ignoring button release - it was held");
                        return;
                    }

                    // normal button press - handle it

                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug_WriteToLCD("Exception in buttonRed_OnInterrupt: " + ex.Message);
            }
        }

        static void buttonGreen_OnInterrupt(uint port, uint state, DateTime time)
        {
            try
            {
                Debug.Print("buttonGreen Interrupt, state " + state);
                if (state == 1)
                {
                    // start the button hold timer
                    timerButtonGreen = new Timer(new TimerCallback(buttonHold), buttonGreen, BUTTON_DELAY, BUTTON_DELAY);

                }
                else
                {
                    // cancel the button hold timer
                    if (timerButtonGreen != null)
                    {
                        timerButtonGreen.Dispose();
                    }
                    if (blnButtonHeld)
                    {
                        // timer went off, so ignore the release
                        blnButtonHeld = false;
                        Debug.Print("ignoring button release - it was held");
                        return;
                    }

                    // normal button press - handle it
                    toggleLCDBacklight();
                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug_WriteToLCD("Exception in buttonGreen_OnInterrupt: " + ex.Message);
            }
        }

        private static bool blnLCDBacklight;
        static private void toggleLCDBacklight()
        {
            try
            {
                if (blnDisableLCD)
                {
                    return;
                }
                // prevent other thread from writing to lcd at same time (mandatory for I2C LCD)
                Monitor.Enter(lcdLock);

                //blnDisableLCD = true;
                if (lcd != null)
                {
                    blnLCDBacklight = !blnLCDBacklight;
                    lcd.SetBacklight(blnLCDBacklight);
                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug_WriteToLCD("Exception in toggleLCDBacklight: " + ex.Message);
            }
            finally
            {
                //blnDisableLCD = false;
                Monitor.Exit(lcdLock);
            }
        }

        private static void buttonHold(object data)
        {
            try
            {
                // tell the button release event to ignore it - we're handling it
                blnButtonHeld = true;

                if ((InterruptPort)data == buttonRed)
                {
                    // cancel the button hold timer - otherwise, this event will go off again if user keeps button pressed
                    timerButtonRed.Dispose();
                    // if both buttons are held down...
                    if (buttonGreen.Read())
                    {
                        // cancel the green button's timer - it was being held to display the timer status
                        timerButtonGreen.Dispose();
                    }
                    else
                    {
                        ToggleComPort();
                    }
                }
                else // green button
                {
                    if (buttonRed.Read())
                    {
                        // cancel the red button's timer - it was being held to display the timer status
                        timerButtonGreen.Dispose();
                    }
                    else
                    {
                        resetLCD();
                    }
                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug_WriteToLCD("Exception in toggleLCDBacklight: " + ex.Message);
            }
        }

        //HACK - workaround for problem where 4x20 LCD with I2C backpack would intermittently switch back to 2x16 mode
        private static void resetLCD()
        {
            try
            {
                Debug.Print("Resetting LCD");
                blnDisableLCD = true;
                initializeLCD();
                Debug.Print("Finished resetting LCD");
            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.Print("Exception in resetLCD: " + ex.Message);
            }
            finally
            {
                blnDisableLCD = false;
            }
        }
        #endregion

        public static string Int_ToZeroPrefixedString(int intValue, int intOutLen)
        {
            string strValue = new string('0', intOutLen) + intValue;
            return strValue.Substring(strValue.Length - intOutLen);

        }

        private static bool blnDisableLCD;

        // replaced attribute with Monitor object, to ensure other lcd methods don't run at same time
        //[MethodImpl(MethodImplOptions.Synchronized)]
        private static void Debug_WriteToLCD(string strData)
        {
            try
            {
                Debug.Print("entering Debug_WriteToLog :" + strData);
                if (blnDisableLCD)
                {
                    return;
                }

                // prevent other thread from writing to lcd at same time (mandatory for I2C LCD)
                Monitor.Enter(lcdLock);

                // calculate number of rows needed to display all of new data
                int intNumRows = (strData.Length + lcd.GetNumCols() - 1) / lcd.GetNumCols();
                if (intNumRows > lcd.GetNumRows())
                {
                    intNumRows = lcd.GetNumRows();
                }
                int intLastRow = lcd.GetNumRows() - intNumRows;
                // scroll up existing data
                for (int i = 0; i < intLastRow; i++)
                {
                    strLCDBuffer[i] = strLCDBuffer[i + intNumRows];
                }
                // save the new data to the LCD buffer - if it wraps over multiple rows, save in multiple rows
                for (int i = intLastRow; i < strLCDBuffer.Length; i++)
                {
                    strLCDBuffer[i] = strData;
                }


                for (int i = 0; i <= intLastRow; i++)
                {
                    lcd.SelectLine((byte)(i + 1), true);
                    lcd.WriteStringToLCD(strLCDBuffer[i]);

                }
            }
            catch (Exception)
            {
                Debug.Assert(false);
            }
            finally
            {
                Monitor.Exit(lcdLock);
            }
        }

        private static float fltTemperature;
        private static float fltHumidity;
        private static void getDHTReadings()
        {
            try
            {
                Debug.Print("before ReadDHT");
                int intResult = ReadDHT.Invoke(bytArray);
                Debug.Print("after ReadDHT");

                if (intResult != 0)
                {
                    Debug_WriteToLCD("ReadDHT retd " + intResult);
                    return;
                }
                // NOTE - the following would be used to extract values from DHT11
                // Debug.Print("DHT11 Temp: " + myArray[2].ToString() + "  Humidity: " + myArray[0].ToString());

                bool blnGotData = false;
                int intTotal = 0;
                for (int i = 0; i < bytArray.Length; i++)
                {
                    blnGotData = (bytArray[i] != 0);
                    if (i < 4)
                    {
                        intTotal += bytArray[i];
                    }
                    Debug.Print("index " + i + " = " + bytArray[i]);
                }
                if (blnGotData)
                {
                    if ((intTotal & 255) == bytArray[4])
                    {

#if DHT22
                        int intValues = 256 * (bytArray[2] & 0x7f) + bytArray[3];
                        if ((bytArray[2] & 0x80) != 0)
                        {
                            intValues *= -1;
                        }
                        fltTemperature = intValues;
                        fltTemperature /= 10;
#endif
#if DHT11
                        fltTemperature = bytArray[2];
#endif


                        Debug_WriteToLCD("Temp " + fltTemperature.ToString("F1"));

#if DHT22
                        intValues = 256 * bytArray[0] + bytArray[1];
                        fltHumidity = intValues;
                        fltHumidity /= 10;
#endif
#if DHT11
                        fltHumidity = bytArray[0];
#endif

                        Debug_WriteToLCD("Hum " + fltHumidity.ToString("F1"));
                    }
                    else
                    {
                        Debug_WriteToLCD("Checksum failed");
                    }
                }
                else
                {
                    Debug_WriteToLCD("No Data from DHT");
                }
            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug_WriteToLCD("Exception in getDHTReadings: " + ex.Message);
            }
        }

    }
}
