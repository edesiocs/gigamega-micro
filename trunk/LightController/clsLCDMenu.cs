using System;
using System.Threading;
using System.IO.Ports;
using System.Text;
using System.Collections;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace LightController
{
    public class clsLCDMenu
    {
        private ArrayList lstMenu = new ArrayList();
        private int intMenuCount;
        private ILCD lcd;
        private InterruptPort buttonUp;
        private InterruptPort buttonDown; 
        private Timer timerButtonUp;
        private Timer timerButtonDown;
        // indicates that "held button" action was handled, so ignore the button release
        private bool blnIgnoreButtonUpRelease; 
        private bool blnIgnoreButtonDownRelease;
        // indicates that first click of button occurred, waiting for double-click
        private bool blnButtonUpClicked;
        private bool blnButtonDownClicked;
        private Int16 intUpRepeatCount;
        private Int16 intDownRepeatCount;
        const int BUTTON_DELAY = 1500;
        const int DOUBLECLICK_DELAY = 400;
        const int BUTTONREPEAT_DELAY = 500;
        const int TURBOREPEAT_DELAY = 200; // when button held down for 4 or more repeats

        private byte currentRow; // current cursor row (1-based)
        private int intCurrentMenuIndex; // menu item displayed on current row
        private byte lastRow; // last screen row that contains a menu item
        private ArrayList lstMenuStack = new ArrayList();
        private MenuCallback menuGetCaption; // function in main class to provide formatted strings to menu
        private MenuGetValue menuGetValue; // function in main class to get current value, min value, max value and increment
        private MenuSetValue menuSetValue; // method in main class to get new value

        // values used in Up/Down menus
        private bool blnInUpDownMenu;
        private Int16 intCurrentValue;
        private Int16 intMinValue;
        private Int16 intMaxValue;
        private byte increment;
        private string strMenuHeader;
        private string strMenuFormat;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strItems">Array of menu items.  Must be 4 strings per menu item: 1) menu ID,
        /// 2) caption, 3) action</param>
        public clsLCDMenu(string[] strItems, ILCD lcd, InterruptPort buttonUp, InterruptPort buttonDown, 
            MenuCallback getCaption, MenuGetValue getValue, MenuSetValue setValue)
        {
           
            int intNumItems = strItems.Length;

            if (intNumItems % 3 != 00)
            {
                throw new ApplicationException("Invalid menu array - size must be multiple of 4");
            }

            int intMenuIndex = 0; // index of item within its menu (0-based)
            for (int i = 0; i < intNumItems; i = i + 3)
            {
                int intNextMenu = -1;
                int intPrevMenu = -1;
                if (i >= 3 && strItems[i] == strItems[i - 3])
                {
                    // this isn't the first item in this menu - point to the previous item
                    intPrevMenu = intMenuIndex - 1; 
                }
                if ((i + 3) < intNumItems && strItems[i] == strItems[i + 3])
                {
                    // this isn't the last item in this menu - point to the next item
                    intNextMenu = intMenuIndex + 1; 
                }
                lstMenu.Add(new menuItem((byte)(i + 1), strItems[i], strItems[i+1], intNextMenu, intPrevMenu, strItems[i+2]));
                intMenuIndex += 1;
            }
            intMenuCount = lstMenu.Count;
            this.lcd = lcd;
            this.buttonUp = buttonUp;
            this.buttonDown = buttonDown;
            this.buttonUp.OnInterrupt +=new NativeEventHandler(buttonUp_OnInterrupt);
            this.buttonDown.OnInterrupt +=new NativeEventHandler(buttonDown_OnInterrupt);
            lcd.SetCursorOn(true); // set block cursor
            this.menuGetCaption = getCaption;
            this.menuGetValue = getValue;
            this.menuSetValue = setValue;

            // display menu 1 - the Main Menu
            displayMenu("1");
        }

        void buttonDown_OnInterrupt(uint port, uint state, DateTime time)
        {
            try
            {
                Debug.Print("in buttonDown_OnInterrupt, port is " + port + " state is " + state);
                //blnRelayState = !blnRelayState;
                //relay.Write(blnRelayState);

                if (state == 1)
                {
                    // start the button hold timer
                    intDownRepeatCount = 0;
                    timerButtonDown = new Timer(new TimerCallback(buttonHold), "Down", BUTTON_DELAY, BUTTON_DELAY);

                }
                else
                {
                    // cancel the button hold timer
                    if (timerButtonDown == null)
                    {
                        Debug.Print("null timerButtonDown in buttonDown_OnInterrupt " + blnIgnoreButtonDownRelease);
                        // shouldn't happen, but ignore it if it does
                    }
                    else
                    {
                        Debug.Print("disposing of timerButtonDown");
                        timerButtonDown.Dispose();
                        timerButtonDown = null;
                    }

                    if (blnIgnoreButtonDownRelease)
                    {
                        // button action already handled, so ignoring the release
                        blnIgnoreButtonDownRelease = false;
                        Debug.Print("ignoring button down release - it was held");
                        return;
                    }                                        
                    else if (blnButtonDownClicked)
                    {
                        // double-click of up button means go forward
                        blnButtonDownClicked = false;
                        doIt(((menuItem)lstMenu[intCurrentMenuIndex]).StrAction);
                        return;
                    }

                    // start the double-click timer
                    blnButtonDownClicked = true;
                    Debug.Print("starting timerButtonDown for buttonclick");
                    timerButtonDown = new Timer(new TimerCallback(buttonClick), "Down", DOUBLECLICK_DELAY, -1);

                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in clsLCDMenu.buttonDown: " + ex.Message);
            }
        }

        void buttonUp_OnInterrupt(uint port, uint state, DateTime time)
        {
            try
            {
                Debug.Print("in buttonUp_OnInterrupt, port is " + port + " state is " + state);

                if (state == 1)
                {
                    if (!blnButtonUpClicked) // if button was already clicked once, then we are waiting for double-click
                    {
                        // start the button hold timer
                        intUpRepeatCount = 0;
                        timerButtonUp = new Timer(new TimerCallback(buttonHold), "Up", BUTTON_DELAY, BUTTON_DELAY);
                    }

                }
                else
                {
                    // cancel the button hold timer
                    if (timerButtonUp == null)
                    {
                        Debug.Print("null timerButtonUp in buttonUp_OnInterrupt " + blnIgnoreButtonUpRelease);
                        // shouldn't happen, but ignore it if it does
                    }
                    else
                    {
                        timerButtonUp.Dispose();
                        timerButtonUp = null;
                    }
                    if (blnIgnoreButtonUpRelease)
                    {
                        // button action already handled, so ignore the release
                        blnIgnoreButtonUpRelease = false;
                        Debug.Print("ignoring up button release - it was held");
                        return;
                    }
                    else if (blnButtonUpClicked)
                    {
                        // double-click of up button means go back
                        blnButtonUpClicked = false;
                        goBack();
                        return;
                    }

                    // start the double-click timer
                    blnButtonUpClicked = true;
                    timerButtonUp = new Timer(new TimerCallback(buttonClick), "Up", DOUBLECLICK_DELAY, -1);
                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in clsLCDMenu.buttonUp: " + ex.Message);
            }
        }



        private void displayMenu(string strMenuID)
        {
            // find menu
            for (int i = 0; i < intMenuCount; i++)
            {
                 if (((menuItem)lstMenu[i]).StrMenuID == strMenuID)
                 {
                     intCurrentMenuIndex = i; // this will be the current row
                     displayMenu(i, intCurrentMenuIndex);
                     return;
                 }
                
            }
            throw new ApplicationException("Invalid menu ID " + strMenuID);
        }

        private void displayMenu(int intMenu, int intCursorIndex)
        {

            lcd.ClearScreen();
            byte row = 1;
            byte cursorRow = 1;

            do
            {
                lcd.SelectLine(row, false);
                lastRow = row;
                string strMenuText = menuGetCaption(((menuItem)lstMenu[intMenu]).StrCaption);
                lcd.WriteStringToLCD(strMenuText);
                if (intMenu == intCursorIndex)
                {
                    // this will be the cursor row
                    cursorRow = row;
                    intCurrentMenuIndex = intMenu;
                }
                row += 1;
                intMenu = ((menuItem)lstMenu[intMenu]).IntNextMenu;
            } while (row <= lcd.GetNumRows() && intMenu < intMenuCount && (intMenu != -1));

            lcd.SetCursorPos(cursorRow, 1);
            currentRow = cursorRow;
        }

        private void buttonHold(object data)
        {
            try
            {
                Debug.Print("in buttonhold");

                if ((string)data == "Down")
                {

                    // cancel the button hold timer 
                    if (timerButtonDown == null)
                    {
                        Debug.Print("null timerButtonDown in buttonHold");
                        return; // double-click must have occurred, so ignore this event
                    }
                    else
                    {
                        timerButtonDown.Dispose();
                        timerButtonDown = null;
                    }
                    // tell the button release event to ignore it - we're handling it
                    blnIgnoreButtonDownRelease = true;
                    
                    //doIt(((menuItem)lstMenu[intCurrentMenuIndex]).StrAction);

                    // when button is held, repeat this event every x milliseconds
                    goDown();
                    if (!buttonDown.Read())
                    {
                        // not being held down anymore
                        return;
                    }
                    intDownRepeatCount += 1;
                    int delay = BUTTONREPEAT_DELAY;
                    if (intDownRepeatCount >= 4)
                        delay = TURBOREPEAT_DELAY;
                    timerButtonDown = new Timer(new TimerCallback(buttonHold), "Down", delay, -1);
                }
                else
                {
                    // cancel the button hold timer - otherwise, this event will go off again if user keeps button pressed
                    if (timerButtonUp == null)
                    {
                        Debug.Print("null timerButtonUp in buttonHold");
                        return; // double-click must have occurred, so ignore this event
                    }
                    else
                    {
                        timerButtonUp.Dispose();
                        timerButtonUp = null;
                    }

                    // tell the button release event to ignore it - we're handling it
                    blnIgnoreButtonUpRelease = true;
                    // when button is held, repeat this event every x milliseconds
                    goUp();
                    if (!buttonUp.Read())
                    {
                        return;
                    }
                    intUpRepeatCount += 1;
                    int delay = BUTTONREPEAT_DELAY;
                    if (intUpRepeatCount >= 4)
                        delay = TURBOREPEAT_DELAY;
                    timerButtonUp = new Timer(new TimerCallback(buttonHold), "Up", delay, -1);
                }
            }
            catch (Exception ex)
            {
                // catch and release
                Debug.Assert(false);
                Debug.Print("Exception in clsLCDMenu.ButtonHold: " + ex.Message);
            }
        }

        // timer expired after first click, so handle as single click
        private void buttonClick(object data)
        {
            Debug.Print("in buttonClick");
            if ((string)data == "Down")
            {
                // cancel the button click timer 
                if (timerButtonDown == null)
                {
                    // oops, someone already cancelled it, so we should ignore it
                    return;
                }
                timerButtonDown.Dispose();
                timerButtonDown = null;
                blnButtonDownClicked = false; // no longer waiting for double-click
                goDown();
            }
            else
            {
                if (timerButtonUp == null)
                {
                    // oops, someone already cancelled it, so we should ignore it
                    return;
                }
                timerButtonUp.Dispose();
                timerButtonUp = null;
                blnButtonUpClicked = false; // no longer waiting for double-click
                goUp();
            }
        }

        private void displayUpDownMenu(bool blnDisplayHeader)
        {
            if (blnDisplayHeader)
            {
                lcd.SelectLine(1, true);
                lcd.WriteStringToLCD(strMenuHeader);
            }
            lcd.SelectLine(2, true);
            lcd.WriteStringToLCD("  " + intCurrentValue);
        }

        private void goDown()
        {
            if (blnInUpDownMenu)
            {
                if (intCurrentValue == intMaxValue)
                {
                    intCurrentValue = intMinValue;
                } else
                {
                    intCurrentValue++;
                }
                displayUpDownMenu(false);
                return;
            }

            if (((menuItem)lstMenu[intCurrentMenuIndex]).IntNextMenu != -1)
            {
                if (currentRow < lcd.GetNumRows())
                {
                    // go down a row 
                    currentRow += 1;
                    lcd.SetCursorPos(currentRow, 1);
                    intCurrentMenuIndex = ((menuItem)lstMenu[intCurrentMenuIndex]).IntNextMenu;
                }
                else
                {
                    // scroll down a row - current menu should be top line of new menu, cursor on next menu item
                    //displayMenu(((menuItem)lstMenu[intCurrentMenuIndex]).IntNextMenu);
                    displayMenu(intCurrentMenuIndex, ((menuItem)lstMenu[intCurrentMenuIndex]).IntNextMenu);
                }
            }
            else
            {
                beep();
            }
        }

        private void goUp()
        {
            if (blnInUpDownMenu)
            {
                if (intCurrentValue == intMinValue)
                {
                    intCurrentValue = intMaxValue;
                } else
                {
                    intCurrentValue--;
                }
                displayUpDownMenu(false);
                return;
            }

            if (((menuItem)lstMenu[intCurrentMenuIndex]).IntPrevMenu != -1)
            {
                if (currentRow > 1)
                {
                    // go up a row 
                    currentRow -= 1;
                    lcd.SetCursorPos(currentRow, 1);
                    intCurrentMenuIndex = ((menuItem)lstMenu[intCurrentMenuIndex]).IntPrevMenu;
                }
                else
                {
                    // scroll up a row -- prev menu item should be 1st row, and cursor should be on that item
                    displayMenu(((menuItem)lstMenu[intCurrentMenuIndex]).IntPrevMenu, ((menuItem)lstMenu[intCurrentMenuIndex]).IntPrevMenu);                    
                }
            }
            else
            {
                beep();
            }
        }

        private void doIt(string strAction)
        {
            if (blnInUpDownMenu)
            {
                blnInUpDownMenu = false;
                menuSetValue(strMenuFormat, intCurrentValue);
                // go back to prior menu
                goBack();
                return;
            }
            if (strAction[0] == '_')
            {
                // strAction is an action to perform
                if (menuGetValue(strAction, out intCurrentValue, out intMinValue, out intMaxValue, out increment))
                {
                    blnInUpDownMenu = true;
                    strMenuHeader = menuGetCaption(((menuItem)lstMenu[intCurrentMenuIndex]).StrCaption);
                    strMenuFormat = strAction;
                    // strAction is the ID of a submenu
                    lstMenuStack.Add(((menuItem)lstMenu[intCurrentMenuIndex]).StrMenuID);
                    displayUpDownMenu(true);

                } else
                {
                // this action doesn't have its own menu, so just do it
                    menuSetValue(strAction, 0); // 2nd parm is ignored for this type of menu
                    //TODO - would be nice to confirm that something happened, but for now just redisplay menu
                    displayMenu(intCurrentMenuIndex, currentRow);
                }
                return;
            }
            else
            {
                // strAction is the ID of a submenu
                lstMenuStack.Add(((menuItem)lstMenu[intCurrentMenuIndex]).StrMenuID);
                displayMenu(strAction);
            }
        }

        private void goBack()
        {
            if (blnInUpDownMenu)
            {
                blnInUpDownMenu = false;
                // user cancelled, so just go back to the prior menu
            }

            if (lstMenuStack.Count > 0)
            {
                string strMenu = (string)lstMenuStack[lstMenuStack.Count - 1];
                lstMenuStack.RemoveAt(lstMenuStack.Count - 1);
                displayMenu(strMenu);
            }
            else
            {
                //  - exit the menu
                buttonUp.OnInterrupt -= buttonUp_OnInterrupt;
                buttonDown.OnInterrupt -= buttonDown_OnInterrupt;
                // tell the main class that the menu has been exited
                menuSetValue("BYE", 0);
                
            }
        }

        private void beep()
        {
            //TODO 
        }

        private struct menuItem
        {
            public menuItem(byte id, string strMenuID, string strCaption, int intNextMenu, int intPrevMenu, string strAction)
            {
                this.id = id;
                this.StrMenuID = strMenuID;
                this.StrCaption = strCaption;
                this.IntNextMenu = intNextMenu;
                this.IntPrevMenu = intPrevMenu;
                this.StrAction = strAction;
            }
            public string StrCaption;
            public byte id;
            public string StrMenuID;
            public int IntNextMenu;
            public int IntPrevMenu;
            public string StrAction;
        }
    }


    
}
