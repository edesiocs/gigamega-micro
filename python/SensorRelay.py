#!/usr/bin/env python
import serial, time, datetime, sys, string
import urllib
import re
import struct
import logging
from xbee import XBee
#import eeml # required for old Pachube code
import urllib2 # required for new Pachube code

CONFIGFILE = "SensorRelay.cfg"

# the following are set or overridden in SensorRelay.cfg
LOGFILENAME = "SensorRelay.log"   # log filename - if no path, must be in current directory
LOGDATA = True
USBPORT = ""  # the serial port ID that the micro is directly connected to
XBEEPORT = "" # the serial port ID the XBee is connected to
serUSB = None # USB serial port object
serXBee = None # XBee serial port object
serDisplays = [] # display serial port objects
xbee = None
BAUDRATE = 9600     # Baud rate for XBee communications
TIMEOUT = 3         # serial port timeout in seconds
SENDTIME = True
DEBUG = False       # if true, debug messages will be written to console
numSites = 0 # of data logging sites found in config file
buf = "" # buffer for reading data from serial port
logfile = None
config = { } # dictionary of config settings

logging.basicConfig(level=logging.DEBUG,
                    format='%(asctime)s %(levelname)s %(message)s',
                    filename='SensorRelayError.log')
log = logging.getLogger()

def LogMsg(message, comment=False):
    try:
        print message
        if logfile:
            logfile.seek(0, 2) # skip to end of file
            if comment:
                logfile.write("# ")
            logfile.write(time.strftime("%Y %m %d, %H:%M")+ ", " + message + "\n")
            logfile.flush()
    except:
        print "Error in LogMsg: ", message

def sendToDisplay(siteconfig):
    try:
        # build display buffer containing sensor values
        sensorValues = siteconfig["_SENSORVALUES"]
        display = ""
        if (len(sensorValues) > 0):
            for key in sensorValues:
                if key[0] != '_':
                    # round to 1 decimal place
                    data = ("%.1f" % sensorValues[key][1]).rstrip('0').rstrip('.')
                    datetime = sensorValues[key][2].strftime("%m/%d %H:%M")
                    display += siteconfig[key] + ": " + data + "- " + datetime + "\r\n"
            # strip off ending CR/LF
            display = display[0:len(display)-2]
        else:
            display = "No data!"
        # send display buffer
        sendDisplayCommand(siteconfig["_SER"], "B0", display)            

        
    except:
        log.exception('Error in sendToDisplay')
        return False

def sendToPachube(dataPoint, floatValue, siteconfig):
    try:
        if DEBUG:
            print 'in sendToPachube', dataPoint, floatValue
            
        # Note - the following code uses the V1 Pachube API and XML (EEML) data format    
        #pac = siteconfig["_EEML_OBJ"]
        # NOTE - eeml library currently requires numeric data point ID
        #pac.update([eeml.Data(int(dataPoint), floatValue)]) # unit=eeml.Unit('Volt', type='basicSI', symbol='V'))])
        #pac.put()
        
        # Note - the following code uses the V2 Pachube API and JSON data format
        #url = 'http://api.pachube.com/v2/feeds/' + siteconfig["PACHUBE_FEED_ID"] + '/datastreams/' + dataPoint + '?_method=put'
        #values = {'current_value' : floatValue}
        #data = json.dumps(values)        
        
        # Note - the following codes uses the V2 Pachube API and CSV data format
        url = 'http://api.pachube.com/v2/feeds/' + siteconfig["PACHUBE_FEED_ID"] + '/datastreams/' + dataPoint + '.csv?_method=put'
        # HACK round to 3 decimal places and drop trailing zeros : technically not needed, but makes Pachube output nicer looking
        data = ("%.3f" % floatValue).rstrip('0').rstrip('.')
        headers = {'X-PachubeApiKey': siteconfig["PACHUBE_API_KEY"]}
        if DEBUG:
            print url
            print data
            print headers
        req = urllib2.Request(url, data, headers)
        try:
            response = urllib2.urlopen(req)
        except urllib2.HTTPError, e:
            log.exception('Error code ' + e.code + ' sending to pachube, datapoint ' + dataPoint)
            return False
        except urllib2.URLError, e:
            log.exception('Reason code ' + e.reason + ' sending to pachube, datapoint ' + dataPoint)
            return False

        log.debug('Finished sending to pachube, datapoint ' + dataPoint)
        return True
    except:
        log.exception('Error sending to pachube, datapoint ' + dataPoint)
        return False

def sendToNimbits(dataPoint, floatValue, siteconfig):
    try:
        if DEBUG:
            print 'in sendToNimbits', dataPoint, str(floatValue)
        # HACK round to 3 decimal places and drop trailing zeros : technically not needed, but makes Nimbits output nicer looking
        stringValue = ("%.3f" % floatValue).rstrip('0').rstrip('.')
        #if dataPoint != "":
        url = (siteconfig["NIMBITS_SERVER"] + "/service/currentvalue?value=" +
               stringValue + "&point=" + dataPoint.replace(" ", "+") + "&email=" + siteconfig["NIMBITS_USERID"] +
               "&secret=" + siteconfig["NIMBITS_API_KEY"])
        if DEBUG:
            print url
        urllib.urlopen(url)
        log.debug('Finished sending to nimbits, url=' + url)
        return True
    except:
        log.exception('Error sending to nimbits, site ' + siteconfig["NIMBITS_SERVER"] + ', datapoint ' + dataPoint);
        return False


def processSensorSite(sensor, float_value, siteconfig):
    try:
        if DEBUG:
            print "in processSensorSite", sensor, float_value, siteconfig["_SITE"]
        if sensor in siteconfig:
            uploadInterval = int(siteconfig["UPDATE_INTERVAL"]) # default
            parts = siteconfig[sensor].split(",")
            dataPoint = parts[0]
            if len(parts) > 1:
                uploadInterval = int(parts[1]) # override default for this sensor

            # add this reading to the sensor's list
            sensorValues = siteconfig["_SENSORVALUES"]
            if sensor in sensorValues:
                if siteconfig["_TYPE"] == "D":
                    # for display, only keep track of most recent sensor reading
                    sensorValues[sensor] = [1, float_value, datetime.datetime.now()]
                else:                    
                    sensorValues[sensor][0] = sensorValues[sensor][0] + 1
                    sensorValues[sensor][1] = sensorValues[sensor][1] + float_value
                    if DEBUG:
                        print "found sensorValue", sensor, sensorValues[sensor]
            else:
                sensorValues[sensor] = [1, float_value, datetime.datetime.now()]
                if DEBUG:
                    print "adding new sensorValue", siteconfig["_SITE"], sensor, sensorValues[sensor]
            # if it's time to upload the sensor reading, do it
            if DEBUG:
                print "sensorValues:", sensorValues[sensor]
                # print "time since last update", datetime.datetime.now() - sensorValues[sensor][2]
            if siteconfig["_TYPE"] == "D":
                # the display is always updated with all sensor readings, not just the current sensor
                timeSinceLastUpload = datetime.datetime.now() - sensorValues["_LASTUPDATE"][2]
            else:                
                timeSinceLastUpload = datetime.datetime.now() - sensorValues[sensor][2]
            if DEBUG:
                print "seconds since last update: ",  timeSinceLastUpload.seconds, "upload interval", uploadInterval
            if timeSinceLastUpload.seconds >= uploadInterval:
                sent = False
                if DEBUG:
                    print "sending to site", siteconfig["_SITE"], siteconfig["_TYPE"]
                if siteconfig["_TYPE"] == "N":
                    sent = sendToNimbits(dataPoint, sensorValues[sensor][1]/ sensorValues[sensor][0], siteconfig)
                elif siteconfig["_TYPE"] == "P":
                    sent = sendToPachube(dataPoint, sensorValues[sensor][1]/ sensorValues[sensor][0], siteconfig)
                elif siteconfig["_TYPE"] == "D":
                    sendToDisplay(siteconfig);
                    sent = False # don't clear the sensor reading in the Display list
                if sent:
                    sensorValues[sensor][0] = 0
                    sensorValues[sensor][1] = 0
                    
                if siteconfig["_TYPE"] == "D":
                    # the display is always updated with all sensor readings, not just the current sensor
                    sensorValues["_LASTUPDATE"][2] = datetime.datetime.now()
                else:
                    sensorValues[sensor][2] = datetime.datetime.now() # will set "time last uploaded" even if upload failed to avoid excessive uploads
            return True
        else:
            if DEBUG:
                print "unknown sensor: " + sensor + ", site " + siteconfig["_SITE"]
            return False
    except:
        log.exception("processSensorSite for sensor " + sensor + ", site " + siteconfig["_SITE"]);
        return True


def processSensor(sensor, float_value):
    try:
        if LOGDATA:
            LogMsg(sensor + "," + str(float_value))
        sensorFound = False
        for i in range(numSites):
            siteconfig = config["_SITE" + str(i)]
            if processSensorSite(sensor, float_value, siteconfig):
                sensorFound = True
        if not sensorFound:
            log.error("Unknown sensor: " + sensor)
    except:
        log.exception("processSensor for sensor " + sensor + "," + str(float_value));

def processXbeeData(data):
    try:
        while len(data) > 0:

            # expect buffer to end with CR and LF
            pos = data.find("\r\n")

            if pos >= 0:
                line, data = data[:pos], data[pos+2:]
                if pos > 0: # if pos = 0, then line starts with /r/n, so ignore it
                    if DEBUG:
                        print "handling line ", line, ", data is ", data
                    # stuff we care about has format <sensor_id>:<value>
                    parts = line.split(':')
                    if len(parts) == 2:
                        try:
                            processSensor(parts[0], float(parts[1]))
                        except ValueError:
                            log.error("Sensor value wasn't a number: " + line)
                    else:
                        # garbage - write it to the log as a comment
                        # LogMsg("# Got unknown data: " + line)
                        if DEBUG:
                            print "got garbage: ", line

    except:
        log.exception("processXbeeData, data = " + data)

        # NOTE: the following is based on EnhancedSerial.py
MAXTRIES = 5
def readUSBData():
    try:
        global buf
        if serUSB == None:
            return

        tries = 0

        while 1:

            buf += serUSB.read(100)
            if DEBUG and len(buf) > 0:
                print "in readUSBData, buf is ", buf

            # expect buffer to end with CR and LF
            pos = buf.find("\r\n")

            if pos >= 0:
                line, buf = buf[:pos], buf[pos+2:]
                if pos > 0: # if pos = 0, then line starts with /r/n, so ignore it
                    if DEBUG:
                        print "handling line ", line, ", buf is ", buf
                    # stuff we care about has format <sensor_id>:<value>
                    parts = line.split(':')
                    if len(parts) == 2:
                        try:
                            processSensor(parts[0], float(parts[1]))
                        except ValueError:
                            log.error("Sensor value wasn't a number: " + line)
                    else:
                        # garbage - write it to the log as a comment
                        # LogMsg("# Got unknown data: " + line)
                        if DEBUG:
                            print "got garbage: ", line
                        line = ""
                    return line
            tries += 1
            if tries >= MAXTRIES:
                break
        buf = ""
        return ""
    except:
        log.exception('readUSBData, buf = ' + buf);
        return ""

def readXBeeData(frame):
    global buf
    try:
        frame_id = frame['id']
        # source address is 2-bytes binary (e.g. x00x01)
        # this also works: source_addr = struct.unpack('>h', frame['source_addr'])
        source_addr = ord(frame['source_addr'][0]) * 256 + ord(frame['source_addr'][1])
        # rssi (signal strength) is 1-byte binary
        rssi = ord(frame['rssi'])
        data = frame['rf_data']
        if DEBUG:
            print "read_frame, id=", frame_id, ", source_addr=", source_addr, ", rssi=", rssi, ", data=", data
            log.debug("read_frame, id=" + str(frame_id) + ", source_addr=" + str(source_addr) + ", rssi=" + str(rssi) + ", data=" + data)
        if frame_id == "rx":
            processXbeeData(data)
        else:
            # huh, what's this now?
            log.error("got frame id " + frame_id + " with data " + data)
    except:
        log.exception('readXBeeData');



def sendXBeeCommand(strPrefix, strCommand):
    try:
        if xbee:
            strData = strPrefix + ":" + strCommand
            # HACK - append null to data to make it look like serial data to the other end
            xbee.send("tx", dest_addr = '\x00\x01', data=strData + '\x00')
        else:
            if XBEEPORT:
                print "ERROR - xbee not set!!"
    except:
        log.exception('sendXBeeCommand: ' + strPrefix + ':' + strCommand);


def sendUSBCommand(strPrefix, strCommand):
    try:
        if serUSB:
            serUSB.write(strPrefix + ":" + strCommand + chr(0))
            serUSB.flush()
        else:
            if USBPORT:
                print "ERROR - serUSB not set!!"
    except:
        log.exception('sendUSBCommand: ' + strPrefix + ':' + strCommand);

def sendDisplayCommand(ser, strPrefix, strCommand):
    try:
        if len(strCommand) > 100:
            ser.write(strPrefix + ":" + strCommand[:100])
            time.sleep(3) # give recipient time to catch up
            ser.write(strCommand[100:] + chr(0))
        else:
            ser.write(strPrefix + ":" + strCommand + chr(0))
            #print "bytes sent to display: ",  written, " data len ", len(strCommand) + len(strPrefix) + 1
        ser.flush()

    except:
        log.exception('sendDisplayCommand: ' + strPrefix + ':' + strCommand);        


def sendDateTime():
    try:
        timeStr = time.strftime("%Y-%m-%d %H:%M")
        sendXBeeCommand("D", timeStr);
        sendUSBCommand("D", timeStr)
        for ser in serDisplays:
            sendDisplayCommand(ser, "S0", timeStr)
        print "sendDateTime: " + timeStr
    except:
        log.exception('sendDateTime');

def readDisplayData():
    for ser in serDisplays:
        data = ser.read(100)
        if data:
            print data


def mainloop():
    lastMin = -1
    while 1==1:
        try:
            readUSBData()
            readDisplayData()
            if SENDTIME:
                currTime = datetime.datetime.now()
                currMin = currTime.minute
                if lastMin != currMin:
                    lastMin = currMin
                    sendDateTime()
            time.sleep(5);
        except:
            log.exception('mainloop');

# read contents of configuration file into dictionary named config
def readConfigFile():
    global numSites
    try:
        lines = open(CONFIGFILE).readlines()
        ctr = 0
        while ctr < len(lines):
            line = lines[ctr].strip()
            ctr += 1
            if len(line) == 0 or line[0] == "#": # skip comment lines
                continue
            else:
                if line[0] == "[":  # start of sensor section
                    site = line[1:]
                    offset = line.find("]", 1)
                    if offset > 0:
                        site = line[1:offset]
                    # build list of site settings
                    settings = {}
                    settings["_SITE"] = site
                    while ctr < len(lines):
                        line = lines[ctr].strip()
                        ctr += 1
                        if line[0] == "[":
                            # start of next settings section - exit the while loop
                            ctr -= 1
                            break
                        if line[0] == "#": # skip comment lines
                            continue
                        parts = line.split("=", 1)
                        if len(parts) == 2:
                            settings[parts[0].strip()] = parts[1].strip()
                        else:
                            print "invalid configuration file line ", str(ctr), ": ", line
                    # TODO - validate that all required settings were read
                    if len(settings) > 0:
                        # dictionary of sensor values
                        # - value is list : [<# of readings since last upload>,
                        #                    <total of readings since last update>,
                        #                    <date/time of last upload>]
                        settings["_SENSORVALUES"] = { }
                        if "USBPORT" in settings:
                            try:
                                settings["_TYPE"] = "D"
                                # _LASTUPDATE isn't really a sensor type, just a placeholder for the last time the display was updated
                                settings["_SENSORVALUES"]["_LASTUPDATE"] = [0, 0, datetime.datetime.now()]
                                try:
                                    serDisplay = serial.Serial(settings["USBPORT"], BAUDRATE, timeout=TIMEOUT)
                                    serDisplay.close() # workaround for known problem when running on Windows
                                    serDisplay.open()
                                    print "Display enabled for " + settings["USBPORT"]
                                    settings["_SER"] = serDisplay
                                    serDisplays.append(serDisplay);
                                    config["_SITE" + str(numSites)] = settings
                                    numSites += 1
                                except:
                                    print "Unable to open Display port ", settings["USBPORT"]
                                    log.exception('Unable to open Display port ' + USBPORT);
                                    # keep going, but without this display
                            except:
                                print "ERROR - unable to initialize Display", settings["_SITE"], " -- site skipped"
                                log.exception("Unable to initialize Display " +  settings["_SITE"]);
                        elif "PACHUBE_FEED_ID" in settings:
                            try:
                                settings["_TYPE"] = "P"
                                #pac = eeml.Pachube(settings["PACHUBE_FEED_ID"], settings["PACHUBE_API_KEY"])
                                #settings["_EEML_OBJ"] = pac
                                print "Pachube logging to " + settings["PACHUBE_FEED_ID"] + " enabled"
                                config["_SITE" + str(numSites)] = settings
                                numSites += 1
                            except:
                                print "ERROR - unable to create object for Pachube feed in site " + site + " -- site skipped"
                                log.exception('ERROR!');
                        elif "NIMBITS_SERVER" in settings:
                                settings["_TYPE"] = "N"
                                config["_SITE" + str(numSites)] = settings
                                numSites += 1
                        else:
                            print "ERROR - invalid settings for site " + site + " -- site skipped"

                else:
                    # global entry in config file
                    parts = line.split("=", 1)
                    if len(parts) == 2:
                        config[parts[0].strip()] = parts[1].strip()
                    else:
                        print "invalid configuration file line " + str(ctr) + ": " +  line
    except:
        log.exception('readConfigFile');

def processConfig():
    global DEBUG, LOGFILENAME, USBPORT, XBEEPORT, SENDTIME, LOGDATA
    try:
        if "DEBUG" in config:
            DEBUG = bool(config["DEBUG"])
        if "LOGFILENAME" in config:
            LOGFILENAME = config["LOGFILENAME"]
        if "USBPORT" in config:
            USBPORT = config["USBPORT"]
        if "XBEEPORT" in config:
            XBEEPORT = config["XBEEPORT"]
        if "SENDTIME" in config:
            SENDTIME = config["SENDTIME"]
        if "LOGDATA" in config:
            LOGDATA = config["LOGDATA"]            

        if DEBUG:
            print "config file settings:"
            for key in config:
                print key, config[key]
    except:
        log.exception('processConfig');

readConfigFile()

processConfig()

if "-d" in sys.argv:
    DEBUG = True
    print "Debug On"

if "-nl" in sys.argv:
    LOGFILENAME = None
    print "Logging Off!"

if LOGFILENAME:
    try:
        logfile = open(LOGFILENAME, 'r+')
    except IOError:
        # didn't exist yet
        logfile = open(LOGFILENAME, 'w+')
        logfile.write("Started logging\n");
        logfile.flush()

LogMsg("SensorRelay.py version 102311 started", comment=True)

if USBPORT:
    try:
        serUSB = serial.Serial(USBPORT, BAUDRATE, timeout=TIMEOUT)
        serUSB.close() # workaround for known problem when running on Windows
        serUSB.open()
    except:
        print "Unable to open USB port ", USBPORT, ". See ya."
        log.exception('usb port ' + USBPORT);
        sys.exit(1)
    

if XBEEPORT:
    try:
        serXBee = serial.Serial(XBEEPORT, BAUDRATE, timeout=TIMEOUT)
        serXBee.close() # workaround for known problem when running on Windows
        serXBee.open()
        xbee = XBee(serXBee, callback=readXBeeData)
    except:
        print "Unable to open XBee port ", XBEEPORT, ". See ya."
        log.exception('XBee Port ' + XBEEPORT);
        sys.exit(1)

lastMin = -1
mainloop()
