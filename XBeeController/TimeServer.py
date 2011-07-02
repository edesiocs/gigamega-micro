#!/usr/bin/env python
import serial, time, datetime, sys, string

LOGFILENAME = "controller.txt"   # where we will store our flatfile data
SERIALPORT = "/dev/ttyUSB0"    # the com/serial port the XBee is connected to
BAUDRATE = 9600      # the baud rate we talk to the xbee
TIMEOUT = 3
DEBUG = False

# open our datalogging file
logfile = None

def LogMsg(message):
    print message
    if logfile:
        logfile.seek(0, 2) # skip to end of file
        logfile.write(time.strftime("%Y %m %d, %H:%M")+ ", " + message + "\n")
        logfile.flush()

def sendCommand(strPrefix, strCommand):
    if ser:
        ser.write(strPrefix + ":" + strCommand + chr(0))
        ser.flush()

def sendDateTime():
    timeStr = time.strftime("%Y-%m-%d %H:%M")
    sendCommand("D", timeStr);
    print "sendDateTime: " + timeStr

def displayTime():
    lastMin = -1
    while 1==1:
        try:
            currTime = datetime.datetime.now()
            if DEBUG:
                print "in displayTime, currTime = " + currTime.strftime("%Y-%m-%d %H:%M:%S")
            currMin = currTime.minute
            if lastMin != currMin:
                lastMin = currMin
                sendDateTime()
            time.sleep(15);
        except Exception:
            LogMsg("Error in displayTime")
            print sys.exc_info()[0]

if "-d" in sys.argv:
    DEBUG = True
    print "Debug On"

if "-nl" in sys.argv:
    LOGFILENAME = None
    print "Logging Off!"

if "-port" in sys.argv:
    index = sys.argv.index("-port")
    SERIALPORT = sys.argv[index+1]
    print "Serial port is " + SERIALPORT

if LOGFILENAME:
    try:
        logfile = open(LOGFILENAME, 'r+')
    except IOError:
        # didn't exist yet
        logfile = open(LOGFILENAME, 'w+')
        logfile.write("Started logging\n");
        logfile.flush()

LogMsg("TimeServer.py version 060411 started")

try:
    ser = serial.Serial(SERIALPORT, BAUDRATE, timeout=TIMEOUT)
    ser.open()
except:
     sys.exit("No serial port found on " + SERIALPORT + ". See ya.")

lastMin = -1
displayTime()

