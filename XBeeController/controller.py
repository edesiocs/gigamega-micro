#!/usr/bin/env python
import serial, time, datetime, sys, string
# from enhancedserial import EnhancedSerial
from threading import Thread
import pywapi  # weather API
# import simplejson, urllib # for twitter with basic authentication
import tweepy
import traceback
import re  # for regular expressions
import Sun, math # for sunrise and sunset calculation

# TODO
# - calc start time based on sunset if not specified on command line
#   - if specified on command line, then use it and don't recalculate based on sunset
# - improve exception handling so that exception doesn't kill thread

LOGFILENAME = "controller.csv"   # where we will store our flatfile data
ERRORFILENAME = "error_controller.txt"
TRACEFILENAME = "trace_controller.txt"

SERIALPORT = "/dev/ttyUSB0"    # the com/serial port the XBee is connected to
BAUDRATE = 9600      # the baud rate we talk to the xbee
TIMEOUT = 3
CITY="'toronto,canada'"   # used by Weather API

startHour = 18
startMinute = 0
endHour = 23
endMinute = 0
lightState = False

terminate = "N"
buf = ""

TWITTER_URL_PUBLIC = 'https://twitter.com/statuses/public_timeline.json'

# TWITTER OAUTH values (for gigamegawatts)
CONSUMER_KEY = 'ig7OdEqykZrbhy23RJr8sA'
CONSUMER_SECRET = '1Zf3G2Buh85VEjFZcqgQIdi9ym8Qs8w0jCc0eHX28A8'
ACCESS_KEY = '46975740-ce1PPh64M5MZ0qwX12eTsxkyGwZ6LVXskmjzEorA'
ACCESS_SECRET = 'b4nW9rUZ07vJqEn93p4IjroFnt14zRj2GoBQsxqXo'

# number of seconds between sending tweet over XBee
TWEET_DURATION = 30
# number of seconds between requesting new tweets from Twitter
TWITTER_FREQ = 180
since_friend = 0
since_mentions = 0
since_dm = 0
since_public = 0
maxtweets = 2
currenttweet = 0

# open our datalogging file
logfile = None
try:
    logfile = open(LOGFILENAME, 'r+')
except IOError:
    # didn't exist yet
    logfile = open(LOGFILENAME, 'w+')
    logfile.write("Started logging\n");
    logfile.flush()

# open our error file

errorfile = None
try:
    errorfile = open(ERRORFILENAME, 'r+')
except IOError:
    # didn't exist yet
    errorfile = open(ERRORFILENAME, 'w+')
    errorfile.write("Error log for controller.py\n");
    errorfile.flush()

# open our trace file
tracefile = None
try:
    tracefile = open(TRACEFILENAME, 'r+')
except IOError:
    # didn't exist yet
    tracefile = open(TRACEFILENAME, 'w+')
    tracefile.write("Started tracing\n");
    tracefile.flush()

def LogMsg(message):
    if logfile:
        logfile.seek(0, 2) # 2 == SEEK_END. ie, go to the end of the file
        logfile.write(time.strftime("%Y %m %d, %H:%M")+ ", " + message + "\n")
        logfile.flush()

def TraceMsg(message):
    if tracefile:
        tracefile.seek(0, 2) # 2 == SEEK_END. ie, go to the end of the file
        tracefile.write(time.strftime("%Y %m %d, %H:%M")+ ", " + message + "\n")
        tracefile.flush()

def sendData(type, index, msg):
    global DEBUG
    if ser:
        # Arduino handles asc 0 as end of message
        # NOTE - without the "utf-8" encoding, I got a type conversion error (expected str, got Unicode)
        if index > 0:
            ser.write(type + str(index) + ":" + msg.encode('utf-8') + chr(0))
        else:
            ser.write(type + ":" + msg.encode('utf-8') + chr(0))
        ser.flush()
        # DAW - 12/1/10 - enable thisif DEBUG:
        print time.strftime("%H:%M:%S"), "sending", type, index, msg


def backlight(nBrightness):
    sendcmd(chr(0xfe) + chr(80) + chr(nBrightness))

g_google_result = {}
g_google_cnt = 0

def fetchWeatherGoogle():
    global g_google_result
    try:
        tmp = pywapi.get_weather_from_google(CITY)
        if DEBUG:
            print "Google read: %s" % tmp
        g_google_result = tmp
    # don't ignore user Ctrl-C or out of memory
    except KeyboardInterrupt, MemoryError:
        raise
    except Exception:
        traceback.print_exc(file=errorfile)
##    except Exception, e:
##        errmsg = "Error in fetchWeatherGoogle: " + e.message + " -- args: "
##        for arg in e.args:
##            errmsg += arg + "; "
##        print errmsg
##        LogMsg(errmsg)

# DAW - version that doesn't send down 80 character buffers - caused Arduino to hang?
def weatherDisplayGoogle_UNBUFFERED(displayFormat):
    # format of report:
    # line1: day + date
    # line2: time
    # line3: sun/cloudy/rainy + temp
    # line4: wind and wind chill

    global g_google_result

    if  displayFormat == 1:
        timeLine1 = time.strftime("%a %b %d") # use lower case codes to display abbreviations
        timeLine2 = time.strftime("%I:%M:%S %p") # removed - replaced leading zero in hour with blank
        #if timeLine2[0]=="0":  timeLine2 = " "+timeLine2[1:]
        timeLine1 = timeLine1.ljust(20," ")
        timeLine2 = timeLine2.ljust(20," ")
        current = g_google_result['current_conditions']
        outlook = current['condition']
        #curTemp = current['temp_f'] + "F"
        curTemp = current['temp_c'] + "C"
        windDesc = current['wind_condition']
        humidity = current['humidity']
        humidity = humidity.ljust(20, " ")
        weather1 = outlook.ljust(14," ")[0:14] + curTemp.rjust(6," ")[0:6]
        weather2 = windDesc.ljust(20," ")[0:20]
        # DAW - 11/6/10 - add humidity
        report = timeLine1[0:20]+timeLine2[0:20]+weather1[0:20]+weather2[0:20] # + humidity[0:20]
        # report = timeLine1 + "\n" + timeLine2 + "\n" + weather1 + "\n" + weather2 + "\n"
    else:
        report = ""
        forecasts = g_google_result['forecasts']
        for forecast in forecasts:
            dow = forecast['day_of_week']
            tempLo = forecast['low']
            tempHi = forecast['high']
            # DAW - 8/7/10 - calculate Celcius
            tempLoC = (float(tempLo) - 32.0) * 5.0 / 9.0
            tempHiC = (float(tempHi) - 32.0) * 5.0 / 9.0
            outlook = forecast['condition']
            # DAW - 8/7/10 - display Celcius
            #fLine = ("%3s %sF %sF %s          " % (dow,tempHi,tempLo,outlook))[0:20]
            #fLine = ("%3s %sC %sC %s          " % (dow,str(int(tempHiC)),str(int(tempLoC)),outlook))
            # DAW - 11/6/10 - put conditions on separate line if it won't fit
            fLine = ("%3s %sC %sC" % (dow,str(int(tempHiC)),str(int(tempLoC))))
            if len(fLine) + len(outlook) >= 20:
                # put outlook on separate line
                # report = report + '{0:20}'.format(fLine) + '{0:20}'.format(outlook)
                report = report + fLine.ljust(20, " ")[0:20] + outlook.ljust(20, " ")[0:20]
            else:
                report = report + (fLine + " " + outlook).ljust(20, " ")[0:20]

            # DAW - 8/22/10 - moved 20 char truncation here, just in case
            # Arduino recognizes \n as end of that row
            #report = report + fLine[0:20]
            # report = report + fLine + "\n"
        # DAW - 8/22/10 - truncate display at 80 chars, in case that was causing twatch to lockup
        # DAW - 11/6/10 - removed 80 char limit, since XBeeLCD can scroll vertically now
        #report = report [0:80]
    sendData("W", displayFormat, report)

def weatherDisplayGoogle(displayFormat):
    # format of report:
    # line1: day + date
    # line2: time
    # line3: sun/cloudy/rainy + temp
    # line4: wind and wind chill

    global g_google_result

    if  displayFormat == 1:
        timeLine1 = time.strftime("%a %b %d") # use lower case codes to display abbreviations
        timeLine2 = time.strftime("%I:%M:%S %p") # removed - replaced leading zero in hour with blank
        #if timeLine2[0]=="0":  timeLine2 = " "+timeLine2[1:]
        timeLine1 = timeLine1.ljust(20," ")
        timeLine2 = timeLine2.ljust(20," ")
        current = g_google_result['current_conditions']
        outlook = current['condition']
        #curTemp = current['temp_f'] + "F"
        curTemp = current['temp_c'] + "C"
        windDesc = current['wind_condition']
        humidity = current['humidity']
        weather1 = outlook.ljust(14," ")[0:14] + curTemp.rjust(6," ")[0:6]
        weather2 = windDesc.ljust(20," ")[0:20]
        # Arduino recognizes \n as end of that row
        report = timeLine1[0:20]+timeLine2[0:20]+weather1[0:20]+weather2[0:20]
        # report = timeLine1 + "\n" + timeLine2 + "\n" + weather1 + "\n" + weather2 + "\n"
    else:
        report = ""
        forecasts = g_google_result['forecasts']
        for forecast in forecasts:
            dow = forecast['day_of_week']
            tempLo = forecast['low']
            tempHi = forecast['high']
            # DAW - 8/7/10 - calculate Celcius
            tempLoC = (float(tempLo) - 32.0) * 5.0 / 9.0
            tempHiC = (float(tempHi) - 32.0) * 5.0 / 9.0
            outlook = forecast['condition']
            # DAW - 8/7/10 - display Celcius
            #fLine = ("%3s %sF %sF %s          " % (dow,tempHi,tempLo,outlook))[0:20]
            fLine = ("%3s %sC %sC %s          " % (dow,str(int(tempHiC)),str(int(tempLoC)),outlook))
            # DAW - 8/22/10 - moved 20 char truncation here, just in case
            # Arduino recognizes \n as end of that row
            report = report + fLine[0:20]
            # report = report + fLine + "\n"
        # DAW - 8/22/10 - truncate display at 80 chars, in case that was causing twatch to lockup
        report = report [0:80]
    sendData("W", displayFormat, report)

def displayWeather():
    while 1==1:
        fetchWeatherGoogle()
        weatherDisplayGoogle(1)
        time.sleep(5) # give Arduino time to catch up
        weatherDisplayGoogle(2)
        time.sleep(900) # will update the weather again in 15 minutes


# DAW - taken from EnhancedSerial.py
def readline(maxtries):
    """maxsize is ignored, timeout in seconds is the max time that is way for a complete line"""
    global buf, DEBUG
    if ser == None:
        return

    # buf = ""; # DAW - 11/13/10 - why so much garbage in what we read?
    tries = 0

    while 1:
        buf += ser.read(100)

        pos = buf.find('\n')


        if pos > 0:
            line, buf = buf[:pos+1], buf[pos+1:]
            # stuff we care about has a colon in 2nd char
            if len(line) > 1:
                if line[1] == ":":
                    # good stuff - write it to the log
                    LogMsg(line)
                    if DEBUG:
                        print "got data ", line, " buf=", buf

                else:
                    # garbage - write it to the log as a comment
                    #LogMsg("# Got: " + line)
                    if DEBUG:
                        print "got garbage: ", line
                    line = ""
            pos = buf.find('\n')
            if pos == 0:
                buf = ""
            return line
        tries += 1
        if tries >= maxtries:
            break
    buf = ""
    return ""


try:
    ser = serial.Serial(SERIALPORT, BAUDRATE, timeout=TIMEOUT)
    ser.open()
except:
    print "NO SERIAL PORT FOUND!!!!!!!"

# send current date and time to the serial port
# DAW - 5/23/11 - obsolete: replaced with version below for Netduino
def sendDateTime_OLD():
    global lightState
    suffix = "-"
    if lightState:
        suffix = "+"

    timeStr = time.strftime("%m/%d %H:%M") + suffix
    sendCommand("D", timeStr);

    #if ser:
        #ser.write(timeStr + chr(0))
        #ser.flush()
    if DEBUG:
        print "sendDateTime: " + timeStr
		
def sendDateTime():
    timeStr = time.strftime("%Y-%m-%d %H:%M")
    sendCommand("D", timeStr);
    if DEBUG:
        print "sendDateTime: " + timeStr		

def sendCommand(strPrefix, strCommand):
    if ser:
        ser.write(strPrefix + ":" + strCommand + chr(0))
        ser.flush()
    LogMsg("sent command " + strPrefix + ": " + strCommand)

def checkTime():
    global currTime, startTime, endTime, lightState
    if currTime >= startTime:
        startTime = startTime + datetime.timedelta(days = 1)
        if DEBUG:
            print "new startTime is " + startTime.strftime("%Y %m %d, %H:%M")
        print "Sent ON command at " + currTime.strftime("%Y %m %d, %H:%M")
        TraceMsg("Sent ON command")
        lightState = True
        sendCommand("C", "ON")

    if currTime >= endTime:
        endTime = endTime + datetime.timedelta(days = 1)
        if DEBUG:
            print "new endTime is " + endTime.strftime("%Y %m %d, %H:%M")
        print "Sent OFF command at " + currTime.strftime("%Y %m %d, %H:%M")
        TraceMsg("Sent OFF command")
        lightState = False
        sendCommand("C", "OFF")


def displayTime():
    global currTime, startTime, endTime, DEBUG, lastMin, lastDay
    while 1==1:
        data = readline(4)
        if data is None:
            data = ""
        if DEBUG:
            print "main loop: ", data
        currTime = datetime.datetime.now()
        currMin = currTime.minute
        if lastMin != currMin:
            if DEBUG:
               print "time changed to ", currMin
            lastMin = currMin
            checkTime()
            sendDateTime()
        # DAW - 11/20/10 - if new day, get the sunrise and sunset time
        if currTime.day != lastDay:
            lastDay = currTime.day
            getSunriseSunset()
        time.sleep(15); # DAW  - 11/13/10 - changed from 30 - XBeeLCD is sending data now

def getSunriseSunset():
    global startTime, startHour, startMinute
    currTime = datetime.datetime.now()
    times = mySun.sunRiseSet(currTime.year, currTime.month, currTime.day, -79.38, 43.65)

    # convert UTC to local time - this approach only works if python knows local TZ, and local TZ is behind UTC
    hourdiff = datetime.datetime.utcnow().hour - datetime.datetime.now().hour
    if hourdiff < 0:
        hourdiff = hourdiff + 24
    startHour = math.floor(times[1]) - hourdiff
    startMin = math.floor(60*(times[1] - math.floor(times[1])))
    startTime = startTime.replace(hour = startHour, minute = startMin)
    if DEBUG:
        print "Sunset is at ", startTime
    TraceMsg("Sunset is at " + startTime.strftime("%H:%M"))
    # lights on 15 minutes before sundown
    startTime = startTime + datetime.timedelta(minutes = -15)
    startHour = startTime.hour
    startMin = startTime.minute
    if DEBUG:
        print "New start time is ", startTime
    timeStr = time.strftime("%H:%M")
    sendCommand("S", timeStr);



tweetbuffer = 1
numtweetbuffers = 2

def send_tweet_UNBUFFERED(result):
    global tweetbuffer
    if DEBUG:
        print "entered send_tweet"
    if result.user.lang == 'en':
        # strip out http URLs
        msg = result.user.name + ':' + re.sub("\s*http\:\S*", "", result.text)
        # DAW - 11/13/10 - send the whole message - let the Arduino control how to display it
        #msg1 = msg[:80]
        if DEBUG:
            print "msg ", result.id_str, " ", result.created_at.strftime("%Y %m %d, %H:%M:%S"), " ", msg
        sendData("T", tweetbuffer, msg)
        if tweetbuffer == numtweetbuffers:
            tweetbuffer = 1
        else:
            tweetbuffer = tweetbuffer + 1
        time.sleep(5) # give Arduino time to catch up

##        if len(msg) > 80:
##            msg1 = msg[80:160]
##            #print "msg1 is ", msg1
##            sendData("T", 2, msg1)
##            time.sleep(5) # give Arduino time to catch up

def send_tweet(result):
    global maxtweets, currenttweet
    #TraceMsg("In send_tweet: " + result.text)
    if result.user.lang == 'en':
        # strip out http URLs
        msg = result.user.name + ':' + re.sub("\s*http\:\S*", "", result.text)
        msg1 = msg[:80]
        #currenttweet = currenttweet + 1
        #if currenttweet > maxtweets:
            #currenttweet = 1
        sendData("T", 1, msg1)
        time.sleep(5) # give Arduino time to catch up
        if len(msg) > 80:
            msg1 = msg[80:160]
            ##print "msg1 is ", msg1
            #currenttweet = currenttweet + 1
            #if currenttweet > maxtweets:
                #currenttweet = 1
            sendData("T", 2, msg1)
            time.sleep(5) # give Arduino time to catch up


#Thread to deal with twitter friend timeline
def twitter():
    auth = tweepy.OAuthHandler(CONSUMER_KEY, CONSUMER_SECRET)
    auth.set_access_token(ACCESS_KEY, ACCESS_SECRET)
    api = tweepy.API(auth)
    since_home = 1
    while 1==1:
        try:
            next_time = int(time.time() + TWITTER_FREQ) # 3 minutes from now
            results = api.home_timeline(since_home)
            if DEBUG:
                print "new tweets: ", len(results)
            # DAW - 11/20/10 if there are no new tweets, get the 5 most recent ones
            if len(results) == 0:
                results = api.home_timeline(count = 5)
                if DEBUG:
                    print "getting last 5 tweets, got ", len(results)
            #TraceMsg("in twitter, num tweets " + str(len(results)))
            for result in reversed(results):
                next_tweet_time = int(time.time()) + TWEET_DURATION
                since_home = result.id
                send_tweet(result)
                if (result.id > since_home):
                    since_home = result.id
                curr_time = int(time.time())
                if (curr_time < next_tweet_time):
                    time.sleep(next_tweet_time - curr_time)
            curr_time = int(time.time())
            # if it is less than 3 minutes since the last time we checked, wait
            if (curr_time < next_time):
                time.sleep(next_time - curr_time)
        # don't ignore user Ctrl-C or out of memory
        except KeyboardInterrupt, MemoryError:
            raise
        except Exception:
            TraceMsg("Exception in twitter")
            traceback.print_exc(file=errorfile)

DEBUG = False

# if (sys.argv and len(sys.argv) > 1):
#    if sys.argv[1] == "-d":
#        DEBUG = True

print "Controller.py version 031211"

firstArg = True
startTimeArg = False # uninitialized
for arg in sys.argv:
    if firstArg:
        firstArg = False
    elif arg == "-d":
        DEBUG = True
    elif arg.startswith("-s"):
        parts = string.split(arg[2:], ":")
        startHour = int(parts[0])
        startMinute = int(parts[1])
        startTimeArg = True
    elif arg.startswith("-e"):
        parts = string.split(arg[2:], ":")
        endHour = int(parts[0])
        endMinute = int(parts[1])
    else:
        sys.exit("Invalid parm " + arg + ". Format is controller [-sHH:MM] [-eHH:MM] -d")

mySun = Sun.Sun()

currTime = datetime.datetime.now()
startTime = currTime.replace(hour=startHour, minute=startMinute, second=0)
if startTimeArg == False:
    getSunriseSunset() # will be set to 15 minutes before sunset

endTime = currTime.replace(hour=endHour, minute=endMinute, second=0)
if ((endHour < startHour) or (endHour == startHour and endMinute < startMinute)):
    endTime = endTime + datetime.timedelta(days = 1)

print "Start time is " +  startTime.strftime("%Y %m %d, %H:%M")
print "End time is " + endTime.strftime("%Y %m %d, %H:%M")
print "Debug is " + str(DEBUG)

lastMin = datetime.datetime.now().minute
lastDay = 0


Thread(target=displayWeather).start()
Thread(target=displayTime).start()
Thread(target=twitter).start()
