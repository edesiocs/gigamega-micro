#!/usr/bin/env python
import serial, time, datetime, sys
from xbee import xbee
import eeml # DAW - 2/7/11 - for Pachube
#import twitter
import tweepy
import sensorhistory
import urllib


DEBUG = False
NOTWIT = False
NOPACH = False
NONIMBIT = False
NIMBITS_ID = ""
PACHUBE_ID = ""

# use App Engine? or log file? comment out next line if appengine
LOGFILENAME = "tweetawatt.csv"   # where we will store our flatfile data

if not LOGFILENAME:
    import appengineauth

# for graphing stuff
GRAPHIT = False         # whether we will graph data
if GRAPHIT:
    import wx
    import numpy as np
    import matplotlib
    matplotlib.use('WXAgg') # do this before importing pylab
    from pylab import *


SERIALPORT = "/dev/ttyUSB0"    # the com/serial port the XBee is connected to
BAUDRATE = 9600      # the baud rate we talk to the xbee
CURRENTSENSE = 4       # which XBee ADC has current draw data
VOLTSENSE = 0          # which XBee ADC has mains voltage data
MAINSVPP = 170 * 2     # +-170V is what 120Vrms ends up being (= 120*2sqrt(2))
vrefcalibration = [492,  # Calibration for sensor #0
                   507,  # Calibration for sensor #1
                   489,  # Calibration for sensor #2
                   492,  # Calibration for sensor #3
                   501,  # Calibration for sensor #4
                   493]  # etc... approx ((2.4v * (10Ko/14.7Ko)) / 3
CURRENTNORM = 15.5  # conversion to amperes from ADC
NUMWATTDATASAMPLES = 1800 # how many samples to watch in the plot window, 1 hr @ 2s samples

# DAW - 2/7/11 - Pachube settings:
API_KEY = 'OOlNyaWHokwm-0r02QjXYOO7DEFq1n-eJtmWNFjUWwU'
API_URL = 18751
pac = eeml.Pachube(API_URL, API_KEY)

CONSUMER_KEY = 'ig7OdEqykZrbhy23RJr8sA'
CONSUMER_SECRET = '1Zf3G2Buh85VEjFZcqgQIdi9ym8Qs8w0jCc0eHX28A8'
ACCESS_KEY = '49628472-BgchQsqyTV2Ca2qj8AtqUjYjkOKXYUP260iQp2Min'
ACCESS_SECRET = 'X8lq7GcihSb6lU2QryGt5jkhg2C38WnUviAoAYAg'

auth = tweepy.OAuthHandler(CONSUMER_KEY, CONSUMER_SECRET)
auth.set_access_token(ACCESS_KEY, ACCESS_SECRET)
api = tweepy.API(auth)

# DAW - 8/7/10 - determine when the day has changed
currday = datetime.datetime.now().day

# def TwitterIt(u, p, message):
def TwitterIt(message):
    if NOTWIT:
        return
    try:
        api.update_status(message)
        #print "%s just posted: %s" % (status.user.name, status.text)
        print "twittered: ", message
    except UnicodeDecodeError:
        print "Your message could not be encoded.  Perhaps it contains non-ASCII characters? "
        print "Try explicitly specifying the encoding with the  it with the --encoding flag"
    except:
        print "Couldn't connect, check network, username and password!"
        LogMsg("Twitter failed!")

# DAW - 8/14/10 - write a message to the .csv file (for debugging only!)
def LogMsg(message):
    if logfile:
        logfile.seek(0, 2) # 2 == SEEK_END. ie, go to the end of the file
        logfile.write("# " + message + "\n")
        logfile.flush()


# open our datalogging file
logfile = None
try:
    # NOTE - the "+" means open for writing: "r+" opens for appending data, "w+" overwrites any existing data
    logfile = open(LOGFILENAME, 'r+')
except IOError:
    # didn't exist yet
    logfile = open(LOGFILENAME, 'w+')
    logfile.write("#Date, time, sensornum, avgWatts\n");
    logfile.flush()


# Command Line Parms:
#  -d --> debug mode
#  -nimbits xxxx --> Data Point ID to be sent to Nimbits (default - no Nimbit logging)
#  -pachube xxxx --> Data Point ID to be sent to Pachube (default - no Pachube logging)
#  -calib nnn --> calibration baseline for sensor
#  -notwit --> disable twittering

if "-notwit" in sys.argv:
    NOTWIT = True
    print "Twittering disabled"

if "-d" in sys.argv:
    DEBUG = True
    print "DEBUGGING!!!!!"
    TwitterIt("Up and running with OAuth...")

if "-nimbits" in sys.argv:
    index = sys.argv.index("-nimbits")
    NIMBITS_ID = sys.argv[index+1]
    print "Nimbits ID is " + NIMBITS_ID
else:
    print "Nimbit Logging disabled"

if "-pachube" in sys.argv:
    index = sys.argv.index("-pachube")
    PACHUBE_ID = sys.argv[index+1]
    print "Pachube ID is " + PACHUBE_ID
else:
    print "Pachube Logging disabled"

if "-calib" in sys.argv:
    index = sys.argv.index("-calib")
    vrefcalibration[1] = int(sys.argv[index+1])
    print "calibration is " + str(vrefcalibration[1])

# open up the FTDI serial port to get data transmitted to xbee
ser = serial.Serial(SERIALPORT, BAUDRATE)
ser.open()

if GRAPHIT:
    # Create an animated graph
    fig = plt.figure()
    # with three subplots: line voltage/current, watts and watthr
    wattusage = fig.add_subplot(211)
    mainswatch = fig.add_subplot(212)

    # data that we keep track of, the average watt usage as sent in
    avgwattdata = [0] * NUMWATTDATASAMPLES # zero out all the data to start
    avgwattdataidx = 0 # which point in the array we're entering new data

    # The watt subplot
    watt_t = np.arange(0, len(avgwattdata), 1)
    wattusageline, = wattusage.plot(watt_t, avgwattdata)
    wattusage.set_ylabel('Watts')
    wattusage.set_ylim(0, 500)

    # the mains voltage and current level subplot
    mains_t = np.arange(0, 18, 1)
    voltagewatchline, = mainswatch.plot(mains_t, [0] * 18, color='blue')
    mainswatch.set_ylabel('Volts (blue)')
    mainswatch.set_xlabel('Sample #')
    mainswatch.set_ylim(-200, 200)
    # make a second axies for amp data
    mainsampwatcher = mainswatch.twinx()
    ampwatchline, = mainsampwatcher.plot(mains_t, [0] * 18, color='green')
    mainsampwatcher.set_ylabel('Amps (green)')
    mainsampwatcher.set_ylim(-15, 15)

    # and a legend for both of them
    #legend((voltagewatchline, ampwatchline), ('volts', 'amps'))


# a simple timer for twitter, makes sure we don't twitter more than once a day
twittertimer = 0

sensorhistories = sensorhistory.SensorHistories(logfile, DEBUG)
print sensorhistories

# the 'main loop' runs once a second or so
def update_graph(idleevent):
    global avgwattdataidx, sensorhistories, twittertimer, DEBUG, currday

    # DAW - 5/18/11 - wrap the whole xbee packet inspection code in an exception handler
    try:

        # grab one packet from the xbee, or timeout
        packet = xbee.find_packet(ser)
        if not packet:
            return        # we timedout

        xb = xbee(packet)             # parse the packet
        #print xb.address_16
        if DEBUG:       # for debugging sometimes we only want one
            print xb

        # DAW - 5/18/11 - prevent exception that was caused by invalid xbee packet address
        if xb.address_16 < 0 or xb.address_16 > len(vrefcalibration):
            LogMsg("ERROR - invalid packet address: " + xb.address_16)
            return

        # we'll only store n-1 samples since the first one is usually messed up
        voltagedata = [-1] * (len(xb.analog_samples) - 1)
        ampdata = [-1] * (len(xb.analog_samples ) -1)
        # grab 1 thru n of the ADC readings, referencing the ADC constants
        # and store them in nice little arrays
        for i in range(len(voltagedata)):
            voltagedata[i] = xb.analog_samples[i+1][VOLTSENSE]
            ampdata[i] = xb.analog_samples[i+1][CURRENTSENSE]

        if DEBUG:
            print "ampdata: "+str(ampdata)
            print "voltdata: "+str(voltagedata)
        # get max and min voltage and normalize the curve to '0'
        # to make the graph 'AC coupled' / signed
        min_v = 1024     # XBee ADC is 10 bits, so max value is 1023
        max_v = 0
        for i in range(len(voltagedata)):
            if (min_v > voltagedata[i]):
                min_v = voltagedata[i]
            if (max_v < voltagedata[i]):
                max_v = voltagedata[i]

        # figure out the 'average' of the max and min readings
        avgv = (max_v + min_v) / 2
        # also calculate the peak to peak measurements
        vpp =  max_v-min_v

        for i in range(len(voltagedata)):
            #remove 'dc bias', which we call the average read
            voltagedata[i] -= avgv
            # We know that the mains voltage is 120Vrms = +-170Vpp
            voltagedata[i] = (voltagedata[i] * MAINSVPP) / vpp

        # normalize current readings to amperes
        for i in range(len(ampdata)):
            # VREF is the hardcoded 'DC bias' value, its
            # about 492 but would be nice if we could somehow
            # get this data once in a while maybe using xbeeAPI
            if vrefcalibration[xb.address_16]:
                ampdata[i] -= vrefcalibration[xb.address_16]
            else:
                ampdata[i] -= vrefcalibration[0]
            # the CURRENTNORM is our normalizing constant
            # that converts the ADC reading to Amperes
            ampdata[i] /= CURRENTNORM

        #print "Voltage, in volts: ", voltagedata
        #print "Current, in amps:  ", ampdata

        # calculate instant. watts, by multiplying V*I for each sample point
        wattdata = [0] * len(voltagedata)
        for i in range(len(wattdata)):
            wattdata[i] = voltagedata[i] * ampdata[i]

        # sum up the current drawn over one 1/60hz cycle
        avgamp = 0

        # DAW - 2/26/11 - check for less than 17 samples - would sometimes cause index out of range exception
        if (len(ampdata) < 17):
            return

        # 16.6 samples per second, one cycle = ~17 samples
        # close enough for govt work :(

        for i in range(17):
            avgamp += abs(ampdata[i])
        avgamp /= 17.0

        # DAW - average of all 18 amp readings
        avgamp2 = 0
        for i in range(len(ampdata)):
            avgamp2 += abs(ampdata[i])
        avgamp2 /= len(ampdata)

        # DAW - average of all 18 volt readings
        avgvolt = 0
        for i in range(len(voltagedata)):
            avgvolt += abs(voltagedata[i])
        avgvolt /= len(voltagedata)

        # sum up power drawn over one 1/60hz cycle
        avgwatt = 0
        # DAW - 2/26/11 - check for less than 17 samples - would sometimes cause index out of range exception
        if len(wattdata) < 17:
            return

        # 16.6 samples per second, one cycle = ~17 samples
        for i in range(17):
            avgwatt += abs(wattdata[i])
        avgwatt /= 17.0

        # DAW - average of all 18 watt readings
        avgwatt2 = 0
        for i in range(len(wattdata)):
            avgwatt2 += abs(wattdata[i])
        avgwatt2 /= len(wattdata)
    except:
        LogMsg("Error in packet handling, ignoring")
        return

    # Print out our most recent measurements
    print str(xb.address_16)+"\tCurrent draw, in amperes: "+str(avgamp)
    print "\tWatt draw, in VA: "+str(avgwatt)
    # DAW print out extra data
    print "\t RSSI: " + str(xb.rssi) + " Volt: " + str(avgvolt) + " Amps: " + str(avgamp2) + " Watts: " + str(avgwatt2)

    if (avgamp > 13):
        return            # hmm, bad data

    if GRAPHIT:
        # Add the current watt usage to our graph history
        avgwattdata[avgwattdataidx] = avgwatt
        avgwattdataidx += 1
        if (avgwattdataidx >= len(avgwattdata)):
            # If we're running out of space, shift the first 10% out
            tenpercent = int(len(avgwattdata)*0.1)
            for i in range(len(avgwattdata) - tenpercent):
                avgwattdata[i] = avgwattdata[i+tenpercent]
            for i in range(len(avgwattdata) - tenpercent, len(avgwattdata)):
                avgwattdata[i] = 0
            avgwattdataidx = len(avgwattdata) - tenpercent

    # retreive the history for this sensor
    sensorhistory = sensorhistories.find(xb.address_16)
    #print sensorhistory

    # add up the delta-watthr used since last reading
    # Figure out how many watt hours were used since last reading
    elapsedseconds = time.time() - sensorhistory.lasttime
    dwatthr = (avgwatt * elapsedseconds) / (60.0 * 60.0)  # 60 seconds in 60 minutes = 1 hr
    sensorhistory.lasttime = time.time()
    print "\t\tWh used in last ",elapsedseconds," seconds: ",dwatthr
    sensorhistory.addwatthr(dwatthr)

    if DEBUG:
        print "sensorhistory.avgwattover5min is " + str(sensorhistory.avgwattover5min())
        print time.strftime("%Y %m %d, %H:%M")+", "+str(sensorhistory.sensornum)+", "+str(sensorhistory.avgwattover5min())+"\n"

    # Determine the minute of the hour (ie 6:42 -> '42')
    currminute = (int(time.time())/60) % 10
    # Figure out if its been five minutes since our last save
    if (((time.time() - sensorhistory.fiveminutetimer) >= 60.0)
        and (currminute % 5 == 0)
        ):
        # Print out debug data, Wh used in last 5 minutes
        avgwattsused = sensorhistory.avgwattover5min()
        print time.strftime("%Y %m %d, %H:%M")+", "+str(sensorhistory.sensornum)+", "+str(sensorhistory.avgwattover5min())+"\n"

        # Lets log it! Seek to the end of our log file
        if logfile:
            logfile.seek(0, 2) # 2 == SEEK_END. ie, go to the end of the file
            logfile.write(time.strftime("%Y %m %d, %H:%M")+", "+
                          str(sensorhistory.sensornum)+", "+
                          str(sensorhistory.avgwattover5min())+"\n")
            logfile.flush()

        # Or, send it to the app engine
        if not LOGFILENAME:
            appengineauth.sendreport(xb.address_16, avgwattsused)

        # DAW - 1/18/11 - log avg watts used to nimbits
        # avgwattstosend = "%.2f" % avgwattsused
        if NIMBITS_ID:
            try:
                urllib.urlopen("http://gigamega-beta.appspot.com/service/currentvalue?value=" +
                    str(avgwattsused) + "&point=" + NIMBITS_ID + "&email=gigamegawatts@gmail.com&secret=e90d9c41-6529-4cf8-b248-92335c577cc7")
            except IOError:
                print 'Error sending to nimbits'

        # DAW - 2/7/11 - log avg watts used to Pachube
        if PACHUBE_ID:
            try:
                pac.update([eeml.Data(int(PACHUBE_ID), avgwattsused, unit=eeml.Watt())]) # unit=eeml.Unit('Volt', type='basicSI', symbol='V'))])
                pac.put()
            except:
                print 'Error sending to Pachube'

        # DAW - 8/7/10 - moved here from below - otherwise, reset5mintimer can cause watts to be 0 when tweet occurs
        # We're going to twitter at midnight, 8am and 4pm

        # Determine the hour of the day (ie 6:42 -> '6')
        currhour = datetime.datetime.now().hour
        # twitter every 8 hours
        # DAW - if debugging, twitter (actually just log to file) every minute
        #if ((((time.time() - twittertimer) >= 3660.0) and (currhour % 8 == 0)) or (DEBUG and ((time.time() - twittertimer) >= 60.0))):
        # DAW - twitter every 4 hours
        if (((time.time() - twittertimer) >= 3660.0) and (currhour % 4 == 0)):
            print "twittertime!"
            twittertimer = time.time();
            if not LOGFILENAME:
                message = appengineauth.gettweetreport()
            else:
                # sum up all the sensors' data
                wattsused = 0
                whused = 0
                if DEBUG:
                    print "num sensor histories", len(sensorhistories.sensorhistories)
                newday = False
                thisday = datetime.datetime.now().day
                if currday != thisday:
                    newday = True
                    if DEBUG:
                        message = "New day, clearing dayswatthr " + str(currday) + " to " + str(thisday)
                        LogMsg(message)
                    currday = thisday
                for history in sensorhistories.sensorhistories:
                    if DEBUG:
                        print "in twitter loop, found history", history.sensornum, history.cumulative5mwatthr
                    wattsused += history.avgwattover5min()
                    whused += history.dayswatthr
                    # DAW - 8/7/10 - clear day's count when day changes
                    if newday:
                        history.dayswatthr = 0

                message = "Currently using "+str(int(wattsused))+" Watts, "+str(int(whused))+" Wh today so far #tweetawatt"

                # DAW - 8/7/10 - if it's midnight, clear the day's count

                # write something ourselves
            if message:
                print message
                # DAW - if debugging, write to CSV file for debug purposes rather than twittering
                if DEBUG:
                    LogMsg(message)
                else:
                    #TwitterIt(twitterusername, twitterpassword, message)
                    TwitterIt(message)

        # Reset our 5 minute timer
        sensorhistory.reset5mintimer()

    if GRAPHIT:
        # Redraw our pretty picture
        fig.canvas.draw_idle()
        # Update with latest data
        wattusageline.set_ydata(avgwattdata)
        voltagewatchline.set_ydata(voltagedata)
        ampwatchline.set_ydata(ampdata)
        # Update our graphing range so that we always see all the data
        maxamp = max(ampdata)
        minamp = min(ampdata)
        maxamp = max(maxamp, -minamp)
        mainsampwatcher.set_ylim(maxamp * -1.2, maxamp * 1.2)
        wattusage.set_ylim(0, max(avgwattdata) * 1.2)

if GRAPHIT:
    timer = wx.Timer(wx.GetApp(), -1)
    timer.Start(500)        # run an in every 'n' milli-seconds
    wx.GetApp().Bind(wx.EVT_TIMER, update_graph)
    plt.show()
else:
    while True:
        update_graph(None)

