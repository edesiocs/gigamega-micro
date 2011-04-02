//#define MATRIX_ORBITAL
//#define SPARKFUN
#define MICRO_LIQUID_CRYSTAL
using System;
using System.Threading;
using System.IO.Ports;
using System.Text;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using MicroLiquidCrystal;

namespace LightController
{
    public delegate void CommandEventHandler(object sender, CommandArgs e);
    public delegate string MenuCallback(string s);
    public delegate bool MenuGetValue(string menuType, out Int16 start, out Int16 min, out Int16 max, out byte increment);
    public delegate void MenuSetValue(string menuType, Int16 value);

    public class Program
    {

        //TODO ---------- Replace the following with your local longitude and latitude and timezone offset from UTC --------------
        // settings for Toronto Canada
        const double MY_LONGITUDE = -79.38;
        const double MY_LATITUDE = 43.65;
        const int UTC_OFFSET = -5;
        const bool USE_DAYLIGHT_SAVINGS = false;

        static ILCD lcd = null;
        static clsUART uart = null;
        static InterruptPort buttonRed;
        static InterruptPort buttonGreen;
        //static PWM ledGreen;
        //static PWM ledRed;
        static PWM buzzer;
        static AnalogInput pot;
        static AnalogInput temperature;
        static OutputPort relay;
        static bool blnRelayState;
        //static Timer timerRedLED;
        //static Timer timerGreenLED;
        static Timer timerTemperature;
        static Timer timerPot;
        static Timer timerButtonRed;
        static Timer timerButtonGreen;
        static Timer timerTime;
        static bool blnButtonHeld;
        static bool blnFahr;
        static ExtendedTimer tmrLightOn;
        static ExtendedTimer tmrLightOff;
        static DateTime datNextOn;
        static DateTime datNextOff;
        static string strLightOn = "+";
        static string strLightOff = "-";
        static bool blnShowTimers;
        static bool blnShowCountDown;

        static bool blnDateSet;
        static bool blnTimeSet;
        static bool blnLightOnAtSunset = true; // on by default, but set off if timer set manually
        static string strSunset;
        static DateTime datSunset;
        static DateTime datSunrise;

        static SunCalculator sunCalc;

        static byte bytBrightness = 0;
        static string strTemperature = "";
        static bool blnBacklight = true;
        static bool blnManualBrightness;

        const int BUTTON_DELAY = 2000;

        static string[] strMenuItems = new string[] { "1", "Date `yy`-`MM`-`dd`", "1-1",
                                              "1", "Time `HH`:`mm`", "2-1",
                                              "1", "Timers", "3-1",
                                              "1", "Settings", "4-1",
                                              "1-1", "Year `yy`", "_UD-`yy`",
                                              "1-1", "Month `MM`", "_UD-`MM`",
                                              "1-1", "Day `dd`", "_UD-`dd`",
                                              "2-1", "Hour `HH`", "_UD-`HH`",
                                              "2-1", "Min `mm`", "_UD-`mm`",
                                              "3-1", "Light On `HH:mm+`", "3-2",
                                              "3-1", "Light Off `HH:mm-`", "3-3",
                                              "3-2", "Manual `HH:mm+`", "3-2-1",
                                              "3-2", "Sunset `HH:mm_`", "_SUNSET",
                                              "3-3", "Manual `HH:mm-`", "3-3-1",
                                              "3-3", "Sunrise `HH:mm*`", "_SUNRISE",
                                              "3-2-1", "Hour `HH+`", "_UD-`HH+`",
                                              "3-2-1", "Min `mm+`", "_UD-`mm+`",
                                              "3-3-1", "Hour `HH-`", "_UD-`HH-`",
                                              "3-3-1", "Min `mm-`", "_UD-`mm-`",
                                              "4-1", "Temp Fmt `TD`", "4-1-1",
                                              "4-1", "Backlight `LB`", "_BACKLITE",
                                              "4-1", "Brightness `BR`", "_UD-`BR`",
                                              "4-1-1", "Celcius", "_CELCIUS",
                                              "4-1-1", "Fahrenheit", "_FAHR"
                                        };
        private static clsLCDMenu menu;

        public static void Main()
        {

            initializePins();
            // update temperature every 30 seconds
            timerTemperature = new Timer(new TimerCallback(displayTemperature), null, 0, 30000);
            // check potentiometer every 5 seconds
            timerPot = new Timer(new TimerCallback(setLCDBrightness), null, 0, 5000);
            // update time every second
            timerTime = new Timer(new TimerCallback(displayTime), null, 1000, 1000);

            if (lcd != null)
            {
                lcd.ClearScreen();
                lcd.SetAutowrap(true);
                lcd.SelectLine(1, true);
                lcd.WriteStringToLCD("Hello");
                Thread.Sleep(2000);
            }

            Thread.Sleep(Timeout.Infinite);

        }

        private static void initializePins()
        {
            // initialize - will be set to actual values based on which LCD is being used
            string strUARTPort = "";
            Cpu.Pin relayPin = Pins.GPIO_NONE;

            try
            {
#if MATRIX_ORBITAL
                lcd = new clsLCD_MO(SerialPorts.COM2, 9600, 2, 16);
                strUARTPort = SerialPorts.COM1;
                relayPin = Pins.GPIO_PIN_D13;
#else
#if SPARKFUN
                lcd = new clsLCD_SF(SerialPorts.COM2, 9600, 4, 20);
                strUARTPort = SerialPorts.COM1;
                relayPin = Pins.GPIO_PIN_D13;
#else
#if MICRO_LIQUID_CRYSTAL
                lcd = new clsLCD_MLC(Pins.GPIO_PIN_D4, Pins.GPIO_PIN_D13, Pins.GPIO_PIN_D5,
                    Pins.GPIO_PIN_D9, Pins.GPIO_PIN_D10, Pins.GPIO_PIN_D11, Pins.GPIO_PIN_D12, 2, 8);
                strUARTPort = SerialPorts.COM2;
                relayPin = Pins.GPIO_PIN_D0;
#endif
#endif
#endif
            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.Print("No LCD for you! " + ex.Message);
            }
            try
            {

                uart = new clsUART(strUARTPort, 9600, 50, true);
                uart.Command += new CommandEventHandler(uart_Command);
            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.Print("No UART for you! " + ex.Message);
            }
            try
            {

                buttonGreen = new InterruptPort(Pins.GPIO_PIN_D8, true, SecretLabs.NETMF.Hardware.Netduino.ResistorModes.Disabled,
                    SecretLabs.NETMF.Hardware.Netduino.InterruptModes.InterruptEdgeBoth);
                buttonRed = new InterruptPort(Pins.GPIO_PIN_D7, true, SecretLabs.NETMF.Hardware.Netduino.ResistorModes.Disabled,
                    SecretLabs.NETMF.Hardware.Netduino.InterruptModes.InterruptEdgeBoth);

                buttonGreen.OnInterrupt += new NativeEventHandler(buttonGreen_OnInterrupt);
                buttonRed.OnInterrupt += new NativeEventHandler(buttonRed_OnInterrupt);

                pot = new AnalogInput(Pins.GPIO_PIN_A0);
                temperature = new AnalogInput(Pins.GPIO_PIN_A2);
                relay = new OutputPort(relayPin, false);
                buzzer = new PWM(Pins.GPIO_PIN_D6);
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in initializePins: " + ex.Message);
            }
        }

        static void uart_Command(object sender, CommandArgs e)
        {
            try
            {
                Debug.Print("in uart_Command: " + e.StrCommand);
                char command = e.StrCommand[0];
                if (command == 'B')
                {
                    if (lcd != null)
                    {
                        bytBrightness = byte.Parse(e.StrCommand.Substring(2));

                        lcd.SetBrightness(bytBrightness);
                        blnManualBrightness = true; // ignore pot setting
                    }
                    e.blnHandled = true;
                }
                else if (command == 'F')
                {
                    if (e.StrCommand[2] == 'F')
                    {
                        blnFahr = true;
                    }
                    else
                    {
                        blnFahr = false;
                    }
                    // update Temperature right away so user knows it worked
                    displayTemperature(null);
                    e.blnHandled = true;
                }
                // backlight L:ON|OFF
                else if (command == 'L')
                {
                    if (e.StrCommand.Substring(2).ToUpper() == "ON")
                    {
                        blnBacklight = true;
                        lcd.SetBacklight(true);
                    }
                    else
                    {
                        blnBacklight = false;
                        lcd.SetBacklight(false);
                    }
                    e.blnHandled = true;
                // turn light on +:hh:mm
                }
                else if (command == '+')
                {
                    e.blnHandled = setLightTime(e.StrCommand.Substring(2), true);
                    // if Light On time manually set, then always use it, not sunset
                    blnLightOnAtSunset = false; 
                }
                // turn light on +:hh:mm               
                else if (command == '-')
                {
                    e.blnHandled = setLightTime(e.StrCommand.Substring(2), false);
                }
                else if (command == 'D')
                {
                    blnDateSet = true;
                    // set default light timer if date and time have been set for the first time
                    setDefaultTimers(); 
                }
                else if (command == 'T')
                {
                    blnTimeSet = true;
                    // set default light timer if date and time have been set for the first time
                    setDefaultTimers(); 
                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in uart_Command: " + ex.Message);
            }
        }

        private static void setLCDBrightness(object state)
        {
            try
            {
                if (blnManualBrightness)
                {
                    // if brightness set manually, then ignore pot setting
                    return;
                }

                float fltPot = pot.Read();

                //Debug.Print("pot reading is " + fltPot);

                // NOTE - following should be used if analog ref is 2.5V
                //fltPot *= 2;
                // NOTE - following should be used if analog ref is 3.3V
                fltPot = fltPot * 5f / 3.3f;

                if (fltPot > 1024)
                {
                    fltPot = 1024;
                }
                bytBrightness = (byte)((255f * fltPot) / 1024f);


                if (lcd != null)
                {


                    lcd.SetBrightness(bytBrightness);
 
                }

            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in setLCDBrightness: " + ex.Message);
            }
        }

        private static void calcTemperature()
        {
            int intTemp = temperature.Read();

            // The following algorithm is from the Grove datasheet
            //- results in 34C reading with 2.5V signal, but 22 reading with 5V
            double resistance = (double)(1023 - intTemp) * 10000 / intTemp;
            int B = 3975;
            double degrees = 1 / (exMath.Log(resistance / 10000) / B + 1 / 298.15) - 273.15;
            string strFormat = "C";
            if (blnFahr)
            {
                degrees = degrees * 9 / 5 + 32;
                strFormat = "F";
            }

            // the following algorithm from the Seeed forum - same problem: 94F reading when converted to 2.5V signal, but 22.8 at 5V signal
            //float tempInF = map(intTemp, 250, 700, 343, 1110); // analog pin reads 250-700, corresponds to 34.3F to 111.0F
            //tempInF /= 10; // divide by 10; map() uses integers
            //float tempInC = (tempInF - 32) * 5 / 9;

            Debug.Print("temperature is " + intTemp + " analog, " + degrees + strFormat);
            // round to 1 decimal place
            strTemperature = degrees.ToString("F1") + strFormat;
        }

        private static void displayTemperature(object state)
        {
            try
            {
                calcTemperature();

                if (lcd != null  && !blnShowTimers)
                {
                    lcd.SelectLine(2, true);

                    string strLine = strTemperature;
                    // if sunset time is set, and LCD has enough cols, display sunset
                    if (lcd.GetNumCols() >= 16 && strSunset != null)
                    {
                        strLine += "  Sun " + strSunset;
                    }
                    lcd.WriteStringToLCD(strLine);
                }


            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in displayTemperature: " + ex.Message);
            }

        }

        static void buttonRed_OnInterrupt(uint port, uint state, DateTime time)
        {
            Debug.Print("in buttonRed interrupt, port is " + port + " state is " + state);
            //blnRelayState = !blnRelayState;
            //relay.Write(blnRelayState);

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
                launchMenu();

                // display timers, or hide them if they are currently displayed
                //blnShowTimers = !blnShowTimers;
                //if (!blnShowTimers)
                //{
                //    // if switching back to time and temperature, display temp now rather than waiting for next timer
                //    displayTemperature(null);
                //}

            }

        }

        static void buttonGreen_OnInterrupt(uint port, uint state, DateTime time)
        {
            Debug.Print("in buttonGreen interrupt, port is " + port + " state is " + state);

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

                // display countdown to next light change, or switch back to time-of-day
                blnShowCountDown = !blnShowCountDown;

            }

        }



        private static void buttonHold(object data)
        {
            Debug.Print("in buttonhold");
            // tell the button release event to ignore it - we're handling it
            blnButtonHeld = true;

            if ((InterruptPort)data == buttonRed)
            {
                // cancel the button hold timer - otherwise, this event will go off again if user keeps button pressed
                timerButtonRed.Dispose();
                // if both buttons are held down, display the light timers
                if (buttonGreen.Read())
                {
                    // cancel the green button's timer - it was being held to display the timer status
                    timerButtonGreen.Dispose();
                    clearTimers();
                }
                else
                {
                    ToggleComPort();
                }
            }
            else
            {
                if (buttonRed.Read())
                {
                    // cancel the red button's timer - it was being held to display the timer status
                    timerButtonGreen.Dispose();
                    clearTimers();
                }
                else
                {
                    ToggleRelay();
                }
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
                            if (lcd != null && !blnShowTimers)
                            {
                                lcd.SelectLine(2, true);
                                lcd.WriteStringToLCD("COM Down");
                            }
                            beep(false);

                        }
                    }
                    else
                    {

                        if (uart.ReopenPort())
                        {
                            if (lcd != null && !blnShowTimers)
                            {
                                lcd.SelectLine(2, true);
                                lcd.WriteStringToLCD("COM Up");
                            }
                            beep(true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in ToggleComPort: " + ex.Message);
            }
        }

        private static void SetLightState(object state)
        {
            blnRelayState = (bool)state;
            Debug.Print("relay is now " + blnRelayState);
            relay.Write(blnRelayState);
            if (blnRelayState)
            {
                datNextOn = datNextOn.AddDays(1);
                // shouldn't happen, but if we are out of sync with the date-of-day, skip forward
                while (datNextOn < DateTime.Now)
                {
                    datNextOn = datNextOn.AddDays(1);
                }
                if (blnLightOnAtSunset)
                {                  
                    // set light to turn on 15 minutes before sunset tomorrow
                    calculateSunriseAndSunset(DateTime.Now.AddDays(1));
                    DateTime datLightOn = datSunset.AddMinutes(-15);
                    setLightOnTimer(datLightOn);
                }
            }
            else
            {
                datNextOff = datNextOff.AddDays(1);
                // shouldn't happen, but if we are out of sync with the date-of-day, skip forward
                while (datNextOff < DateTime.Now)
                {
                    datNextOff = datNextOff.AddDays(1);
                }
            }
        }

        private static void ToggleRelay()
        {
            blnRelayState = !blnRelayState;
            Debug.Print("relay is now " + blnRelayState);
            relay.Write(blnRelayState);
        }

        private static void displayTime(object data)
        {
            if (lcd != null)
            {
                if (blnShowTimers)
                {
                    displayTimers();
                } else
                {
                    if (blnShowCountDown)
                    {
                        if (blnRelayState)
                        {
                            // relay is on, display time to next light-off
                            string strCountdown;
                            
                            if (datNextOff == DateTime.MinValue)
                            {
                                strCountdown = "Not set";
                            } else
                            {
                                TimeSpan timeTo = datNextOff.Subtract(DateTime.Now);
                                //HACK - truncate fractional seconds by assuming hh:mm:ss format
                                strCountdown = timeTo.ToString().Substring(0, 8);
                            } 
                            lcd.SelectLine(1, true);
                            lcd.WriteStringToLCD(strCountdown);
                        }
                        else
                        {
                            // relay is off, display time to next light-off
                            string strCountdown;
                            if (datNextOn == DateTime.MinValue)
                            {
                                strCountdown = "Not set";
                            } else
                            {
                                TimeSpan timeTo = datNextOn.Subtract(DateTime.Now);
                                //HACK - truncate fractional seconds by assuming hh:mm:ss format
                                strCountdown = timeTo.ToString().Substring(0, 8);
                            }
                            lcd.SelectLine(1, true);
                            lcd.WriteStringToLCD(strCountdown);
                        }
                    }
                    else
                    {
                        // display the current time
                        lcd.SelectLine(1, true);
                        if (lcd.GetNumCols() < 16)
                        {
                            lcd.WriteStringToLCD(DateTime.Now.TimeOfDay.ToString());
                        }
                        else
                        {
                            lcd.WriteStringToLCD(DateTime.Now.ToString("MMM dd  HH:mm:ss"));
                        }
                    }
                }
            }
        }

        private static void beep(bool blnUp)
        {
            float fltFreq = 80;
            int intDuration = 500;
            if (!blnUp)
            {
                fltFreq = 30;
                intDuration = 1000;
            }

            uint period = (uint)(1000000 / fltFreq);
            buzzer.SetPulse(period, period / 2);
            Thread.Sleep(intDuration);
            buzzer.SetDutyCycle(0);
        }

        private static void setLightOnTimer(DateTime datAlarm)
        {
            if (tmrLightOn != null)
            {
                tmrLightOn.Dispose();
            }
            Debug.Print("setting light on timer to " + datAlarm);
            strLightOn = "+ " + datAlarm.ToString("HH:mm");
            // set the timer to go off at this date, and repeat forever if we aren't recalculating based on sunset
            // NOTE - MSDN says period can be set to TimeoutInfinite, but TimeSpan(-1) causes out of range exception
            TimeSpan tsRepeat = TimeSpan.MaxValue;
            if (!blnLightOnAtSunset)
            {
                // repeat timer at same time every day
                tsRepeat = new TimeSpan(1, 0, 0, 0);
            }
            tmrLightOn = new ExtendedTimer(new TimerCallback(SetLightState), true, datAlarm, tsRepeat);
            datNextOn = datAlarm;


        }

        private static void setLightOffTimer(DateTime datAlarm)
        {
            if (tmrLightOff != null)
            {
                tmrLightOff.Dispose();
            }
            Debug.Print("setting light off timer to " + datAlarm);
            strLightOff = "- " + datAlarm.ToString("HH:mm");
            // set the timer to go off at this date, and every 24 hours after that
            tmrLightOff = new ExtendedTimer(new TimerCallback(SetLightState), false, datAlarm, new TimeSpan(1, 0, 0, 0));
            datNextOff = datAlarm;

        }

        private static bool setLightTime(string strTime, bool blnState)
        {
            try
            {
                string[] strTimeParts = strTime.Split(new char[] { ':', '-', ' ' });
                int intHour = Int16.Parse(strTimeParts[0]);
                int intMinute = Int16.Parse(strTimeParts[1]);
                DateTime datAlarm = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, intHour, intMinute, 0);
                // wow, we got this far, so hopefully the string was correct.  If the time has already passed, change to tomorrow
                if (datAlarm <= DateTime.Now)
                {
                    datAlarm = datAlarm.AddDays(1);
                }
                
                if (blnState)
                {
                    setLightOnTimer(datAlarm);
                }
                else
                {
                    setLightOffTimer(datAlarm);
                }
                return true;
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in setLightTime: " + ex.Message);
                return false;
            }
        }

        // display the on and off times for the light relay
        static void displayTimers()
        {
            if (lcd != null)
            {
                // turn off the time of day timer, or it will overwrite the display
                //timerTime.Dispose();
                lcd.SelectLine(1, true);
                lcd.WriteStringToLCD(strLightOn);
                lcd.SelectLine(2, true);
                lcd.WriteStringToLCD(strLightOff);
                // // make sure they stay up for a couple of secs before the time is displayed
                //Thread.Sleep(2000);
                //// turn on the time timer again
                //timerTime = new Timer(new TimerCallback(displayTime), null, 1000, 1000);
            }
            else
            {
                beep(false);
            }
        }

        static void clearTimers()
        {
            if (tmrLightOff != null)
            {
                tmrLightOff.Dispose();
            }
            if (tmrLightOn != null)
            {
                tmrLightOn.Dispose();
            }
            // make sure that LCD tells the user that the timers are turned off
            strLightOn = "+";
            strLightOff = "-";
            datNextOn = DateTime.MinValue;
            datNextOff = DateTime.MinValue;
            // give audible confirmation, so user can let go of buttons
            beep(true);
        }

        static void calculateSunriseAndSunset(DateTime date)
        {
            sunCalc = new SunCalculator(MY_LONGITUDE, MY_LATITUDE, UTC_OFFSET * 15, USE_DAYLIGHT_SAVINGS);
            datSunset = sunCalc.CalculateSunSet(date);
            datSunrise = sunCalc.CalculateSunRise(date);
            Debug.Print("sunrise is " + datSunrise + " sunset is " + datSunset);
            strSunset = datSunset.ToString("HH:mm");
            
        }

        static void setDefaultTimers()
        {
            if (blnTimeSet && blnDateSet && blnLightOnAtSunset)
            {
                // set light to turn on 15 minutes before sunset
                calculateSunriseAndSunset(DateTime.Now);
                DateTime datLightOn = datSunset.AddMinutes(-15);
                setLightTime(datLightOn.ToString("HH:mm"), true);
                // set light to turn off at 11 pm
                setLightTime("23:00", false);
            }
        }

        /// <summary>
        /// Replaces keywords in menu caption with actual value.  Keywords are embedded in strCaption parm, delimited
        /// with back-quote (`).  e.g. "Date `yy`-`MM`-`dd`" returns "Date 11-02-13" on Feb 13 2011.
        /// </summary>
        /// <param name="strCaption"></param>
        /// <returns></returns>
        public static string getStringForMenu(string strCaption)
        {
            try
            {
                string[] strKeywords = strCaption.Split(new char[] { '`' });
                if (strKeywords.Length == 1)
                {
                    return strCaption; // no keyword delimiters, so nothing to change
                }
                // first array index will be part before the first "'", every other index after 
                //   that is part between closing ` and next ` -- ignore those parts
                for (int i = 1; i < strKeywords.Length; i+=2)
                {
                    // first handle keywords unrelated to date and time

                    // TD = temperature in degrees
                    if (strKeywords[i] == "TD")
                    {
                        strKeywords[i] = strTemperature;
                        continue; // continue with next keyword in caption
                    }
                    // BR = LCD Brightness (0-255)
                    if (strKeywords[i] == "BR")
                    {
                        strKeywords[i] = bytBrightness.ToString();
                        continue; // continue with next keyword in caption
                    }
                    // LB = LCD Backlight (on or off)
                    if (strKeywords[i] == "LB")
                    {
                        if (blnBacklight)
                        {
                            strKeywords[i] = "On";
                        }
                        else
                        {
                            strKeywords[i] = "Off";
                        }
                        continue;
                    }
                    DateTime date = DateTime.Now;
                    string strKeyword = "";
                    char suffix = strKeywords[i][strKeywords[i].Length - 1];
                    bool blnNoSuffix = false;
                    if (suffix == '+')
                    {
                        // suffix of '+' indicates Light On time (e.g. "HH+" for hour the light turns on)
                        date = datNextOn; 

                    } else if (suffix == '-')
                    {
                        // suffix of '-' indicates Light Off time (e.g. "mm-" for minute the light turns off)
                        date = datNextOff;                         
                    } else if (suffix == '*')
                    {
                        // suffix of '*' indicates sunrise
                        if (datSunrise == DateTime.MinValue)
                        {
                            // try calculating sunrise
                            calculateSunriseAndSunset(DateTime.Today);
                        }
                        date = datSunrise;
                    } else if (suffix == '_')
                    {
                        // suffix of '_' indicates sunset
                        if (datSunset == DateTime.MinValue)
                        {
                            // try calculating sunset
                            calculateSunriseAndSunset(DateTime.Today);
                        }
                        date = datSunset;
                    } else
                    {
                        // whole thing better be a date or time format string for the current date and time
                        strKeyword = strKeywords[i]; 
                        blnNoSuffix = true;
                    }
                    if (!blnNoSuffix)
                    {
                        // strip off suffix, leaving format string
                        strKeyword = strKeywords[i].Substring(0, strKeywords[i].Length - 1);
                    }
                    // what's left in the keyword string better be a date or time format string, or an exception will be thrown
                    try
                    {
                        // replace the keyword with the desired value
                        strKeywords[i] = date.ToString(strKeyword);
                    }
                    catch (Exception ex)
                    {
                        Debug.Assert(false);
                        Debug.Print("getStringForMenu: bad format string: " + strKeywords[i] + " -- " + ex);
                        // leave strKeywords unchanged
                    }

                }
                // put the caption back together, without the delimiters
                return String.Concat(strKeywords);

            }
            catch (Exception ex)
            {
                //TODO - are these asserts safe for Release version?
                Debug.Assert(false);
                Debug.Print("Exception in getStringForMenu: " + ex);
                return strCaption;
            }
        }

        public static bool getValuesForMenu(string menuCode, out Int16 start, out Int16 min, out Int16 max, out byte increment)
        {
            
            if (menuCode.Substring(0, 4) != "_UD-")
            {
                // not a menu type that requires initialization
                start = -1;
                min = -1;
                max = -1;
                increment = 0;
                return false;
            }

            // the rest of menu code is the value we want to initialize
            string strFormatCode = menuCode.Substring(4);
            start = Int16.Parse(getStringForMenu(strFormatCode));
            increment = 1; // unless otherwise stated below
            if (strFormatCode.IndexOf("HH") >= 0)
            {
                min = 0;
                max = 23;                
            }
            else if (strFormatCode.IndexOf("mm") >= 0)
            {
                min = 0;
                max = 59;
            } else if (strFormatCode.IndexOf("yy") >= 0)
            {
                min = 0;
                max = 99;
            }
            else if (strFormatCode.IndexOf("MM") >= 0)
            {
                min = 1;
                max = 12;
            }
            else if (strFormatCode.IndexOf("dd") >= 0)
            {
                // day range depends on what month it is - for now, that has to be current month
                min = 1;
                // the # of days in the current month is the difference between today and the same day of the next month
                max = (Int16)DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
            }
            else if (strFormatCode.IndexOf("BR") >= 0)
            {
                // LCD brightness
                min = 0;
                max = 255;
            } else
            {
                // ror roh, don't know this one, so anything goes
                Debug.Print("unknown format code in getValuesForMenu: " + strFormatCode);
                min = Int16.MinValue;
                max = Int16.MaxValue;
            }
            return true;
            
        }

        static void setValueFromMenu(string menuCode, Int16 intValue)
        {

            if (menuCode == "BYE")
            {
                // menu has been closed, so hook up our events again

                // update temperature every 30 seconds
                timerTemperature = new Timer(new TimerCallback(displayTemperature), null, 0, 30000);
                // update time every second
                timerTime = new Timer(new TimerCallback(displayTime), null, 1000, 1000);

                // tell the program to ignore the next button release event, since its still the "exit menu" click
                blnButtonHeld = true; 
                buttonGreen.OnInterrupt += new NativeEventHandler(buttonGreen_OnInterrupt);
                buttonRed.OnInterrupt += new NativeEventHandler(buttonRed_OnInterrupt);
            }
            else if (menuCode == "_SUNSET")
            {
                if (datSunset == DateTime.MinValue)
                {
                    // try calculating sunset
                    calculateSunriseAndSunset(DateTime.Now);                    
                }
                // set light to turn on 15 minutes before sunset
                DateTime datLightOn = datSunset.AddMinutes(-15);
                setLightTime(datLightOn.ToString("HH:mm"), true);
            }
            else if (menuCode == "_SUNRISE")
            {
                if (datSunrise == DateTime.MinValue)
                {
                    // try calculating sunset
                    calculateSunriseAndSunset(DateTime.Now);
                }
                // set light to turn off 15 minutes after sunrise
                DateTime datLightOff = datSunrise.AddMinutes(15);
                setLightTime(datLightOff.ToString("HH:mm"), false);
            }
            else if (menuCode == "_CELCIUS")
            {
                // display temperature in celcius
                blnFahr = false;
                // recalc temperature so that it is displayed in right format in menu
                calcTemperature();
            }
            else if (menuCode == "_FAHR")
            {
                blnFahr = true;
                // recalc temperature so that it is displayed in right format in menu
                calcTemperature();
            }
            else if (menuCode == "_BACKLITE")
            {
                // toggle backlight
                blnBacklight = !blnBacklight;
                lcd.SetBacklight(blnBacklight);
            } else
            {
                string[] strParts = menuCode.Split(new char[] { '`' });
                // first part is probably UD_, but we actually only care about the format code
                DateTime date = DateTime.Now;
                string strTime;
                switch (strParts[1])
                {
                    case "yy":
                        Utility.SetLocalTime(new DateTime(2000 + intValue, date.Month, date.Day, date.Hour, date.Minute, date.Second));
                        break;
                    case "MM":
                        Utility.SetLocalTime(new DateTime(date.Year, intValue, date.Day, date.Hour, date.Minute, date.Second));
                        break;
                    case "dd":
                        Utility.SetLocalTime(new DateTime(date.Year, date.Month, intValue, date.Hour, date.Minute, date.Second));
                        break;
                    case "HH":
                        Utility.SetLocalTime(new DateTime(date.Year, date.Month, date.Day, intValue, date.Minute, date.Second));
                        break;
                    case "mm":
                        Utility.SetLocalTime(new DateTime(date.Year, date.Month, date.Day, date.Hour, intValue, date.Second));
                        break;
                    case "HH+":
                        strTime = Int_ToZeroPrefixedString(intValue, 2) + datNextOn.ToString(":mm");
                        setLightTime(strTime, true);
                        break;
                    case "mm+":
                        strTime = datNextOn.ToString("HH:") + Int_ToZeroPrefixedString(intValue, 2);
                        setLightTime(strTime, true);
                        break;
                    case "HH-":
                        strTime = Int_ToZeroPrefixedString(intValue, 2) + datNextOff.ToString(":mm");
                        setLightTime(strTime, false);
                        break;
                    case "mm-":
                        strTime = datNextOff.ToString("HH:") + Int_ToZeroPrefixedString(intValue, 2);
                        setLightTime(strTime, false);
                        break;
                    case "BR":
                        // LCD brightness
                        bytBrightness = (byte)intValue;
                        lcd.SetBrightness(bytBrightness);
                        blnManualBrightness = true; // ignore pot setting
                        break;
                    default:
                        Debug.Print("unknown format code in setValueForMenu: " + menuCode);
                        break;
                }
            }
            
        }

        static void launchMenu()
        {
            // turn off the timers that update the display
            timerTemperature.Dispose();
            timerTime.Dispose();

            //unhook the button event handlers
            buttonRed.OnInterrupt -= buttonRed_OnInterrupt;
            buttonGreen.OnInterrupt -= buttonGreen_OnInterrupt;

            // buttonGreen = up, buttonRed = down
            menu = new clsLCDMenu(strMenuItems, lcd, buttonGreen, buttonRed, getStringForMenu, getValuesForMenu, setValueFromMenu);
        }

        static string Int_ToZeroPrefixedString(int intValue, int intOutLen)
        {
            string strValue = new string('0', intOutLen) + intValue;
            return strValue.Substring(strValue.Length - intOutLen);

        }

    }
}
