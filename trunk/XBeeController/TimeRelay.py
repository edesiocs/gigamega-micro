#!/usr/bin/env python
import serial, time, datetime, sys, string
import urllib

LOGFILENAME = "controller.txt"   # where we will store our flatfile data
SERIALPORT = "/dev/ttyUSB0"    # the com/serial port the XBee is connected to
BAUDRATE = 9600      # the baud rate we talk to the xbee
TIMEOUT = 3
DEBUG = False
NIMBITS_SERVER = "http://gigamega-beta.appspot.com"
NIMBITS_USERID = "&email=gigamegawatts@gmail.com&secret=e90d9c41-6529-4cf8-b248-92335c577cc7"

# TO DO: replace hardcoded dictionary with one loaded from command line
dataPoints = { 'L1' : 'Light1', 'T1' : 'Temperature1', 'H1' : 'Humidity1'}

# open our datalogging file
logfile = None

def LogMsg(message):
    print message
    if logfile:
        logfile.seek(0, 2) # skip to end of file
        logfile.write(time.strftime("%Y %m %d, %H:%M")+ ", " + message + "\n")
        logfile.flush()

def sensorToDataPoint(sensor):
   dataPoint = dataPoints.get(sensor, sensor)
   if dataPoint == sensor:
        print 'sensor not found!', sensor
   return dataPoint

def sendToNimbits(sensor, value):
    if NIMBITS_USERID:
        dataPoint = sensorToDataPoint(sensor)
        try:
            if DEBUG:
                print "sending to Nimbits", NIMBITS_SERVER, value,  dataPoint
            urllib.urlopen(NIMBITS_SERVER + "/service/currentvalue?value=" +
                value + "&point=" + dataPoint + NIMBITS_USERID)
        except IOError:
            print 'Error sending to nimbits'

# DAW - based on EnhancedSerial.py
MAXTRIES = 5
def readData():
    global buf, DEBUG
    if ser == None:
        return

    tries = 0

    while 1:
        buf += ser.read(100)

        pos = buf.find('\n')


        if pos > 0:
            line, buf = buf[:pos+1], buf[pos+1:]
            if DEBUG:
                print "got data ", line
            # stuff we care about has format <sensor_id>:<value>
            parts = line.split(':')
            if len(parts) == 2:
                sendToNimbits(parts[0], parts[1])                
            else:
                # garbage - write it to the log as a comment
                LogMsg("# Got unknown data: " + line)
                if DEBUG:
                    print "got garbage: ", line
                line = ""
            pos = buf.find('\n')
            if pos == 0:
                buf = ""
            return line
        tries += 1
        if tries >= MAXTRIES:
            break
    buf = ""
    return ""

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
            readData()
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

LogMsg("TimeRelay.py version 070211 started")

try:
    ser = serial.Serial(SERIALPORT, BAUDRATE, timeout=TIMEOUT)
    ser.open()
except:
     sys.exit("No serial port found on " + SERIALPORT + ". See ya.")

lastMin = -1
displayTime()

