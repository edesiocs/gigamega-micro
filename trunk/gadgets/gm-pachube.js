    // Get userprefs
    var prefs = new gadgets.Prefs();

    // global settings from user prefs:
    var feedID;             // numeric Pachube Feed ID
    var datastreamsSel;     // array of datastream IDs
    var displayStatus;      // display status line: boolean
    var timeSpan;           // numeric timespan
    var spanType;           // Hours, Days, Weeks or Months
    var detailedGrid;       // display Gridlines: boolean
    var axisLabels;         // display labels on axes: boolean
    var graphHeight;        // height in pixels
    var graphWidth;         // width in pixels
    var timeZone;           // offset from UTC, in hours
	var autoSizeGadget;		// automatically size gadget to fit contents: boolean 
    var gadgetHeight;       // manual override for gadget height (ignored if autoSizeGadget is true)

    // global variables 
    
    var urlPrefix;          // first part of URL for all Rest APIs
    var urlSuffix;          // last part of URL for all Rest APIs
    var errorMsg = "";
    var datastreams; 		// Javascript object containing JSON data for each datastream in the feed
	var datastreamsArray;   // array of datastream IDs

    // Feed settings
    var feedTitle;
    var feedStatus;             // Status: Live or Frozen
    var feedDesc;
    var MAX_GRAPH_SIZE = 300000;  // Pachube's maximum graph size, in pixels
    
	
    function init()
    {
        getGlobalSettings();		
        verifySettings();
        if (errorMsg !== "")
        {
            displayErrorMsg(errorMsg + ' Please correct the values in the Edit 		Settings dialog.'); 
        } else
        {
            initialize();
            if ((datastreamsSel.length === 0) || displayStatus)
            {
                getDatastreamStatus();
            }  else
            {
                datastreamsArray = datastreamsSel;
                displayGraphs();
            }
        }
    }
    
    function displayErrorMsg(errorMsg)
    {
        var element = document.getElementById('content_div');
        element.innerHTML = '<span class="error">' + errorMsg + '</span>'; 
    }
    
    // NOTE - the following code is taken from the Google Gadgets Developer documentation
    function makeCachedRequest(url, callback, params, refreshInterval) 
    {
      // replace spaces with plus signs, since this will be embedded in a URL request
      url = url.replace(/\ /g, "+");	
      console.log("Pachube Gadget: status request html is " + url);
      
      var ts;
      ts = new Date().getTime();
      var sep;
      sep ="?";
      if (refreshInterval && refreshInterval > 0) {
        ts = Math.floor(ts / (refreshInterval * 1000));
      }
      if (url.indexOf("?") > -1) {
        sep = "&";
      }	  
      
      url = [ url, sep, "nocache=", ts ].join("");

      gadgets.io.makeRequest(url, callback, params);
    }
    
    // get list of datastreams and their status info in the feed
    function getDatastreamStatus()
    {
        var url = urlPrefix + "feeds/" + feedID + ".json" + "?" + urlSuffix;
        var params = {};
        params[gadgets.io.RequestParameters.CONTENT_TYPE] = gadgets.io.ContentType.JSON;
		// NOTE - this API key has READ access only
        params[gadgets.io.RequestParameters.HEADERS] = {
            "X-PachubeApiKey": "nBGUIT63Onke5-sfOoARTvKvkwl45b85wjz5PKDIZkc"
        };
        // refresh data if longer than 5 minutes since last request
        makeCachedRequest(url, getDatastreamsResponse, params, 300);
    }

//    function getDatastreamStatus_Old() {
//        var url = "http://www.pachube.com/feeds/37080.json";
//        _IG_FetchContent(url, getDatastreamsResponse, { refreshInterval: (60 * 5) });
//    }
    
    function getDatastreamsResponse(obj)
    {
        console.log("getDatastreamsResponse errors: " + obj.errors);
        console.log("getDatastreamsResponse text:" + obj.text);
        console.log("getDatastreamsResponse data:" + obj.data);
        
        jsonData = checkResponse(obj);
        if (jsonData === "")
        {
            if (datastreamsSel.length > 0)
            {
                // couldn't get status, but display graphs for the specified data streams
				datastreamsArray = datastreamsSel;
                displayGraphs();
            }
        }
        var datastreamList, datastream;
        datastreamList = jsonData.datastreams;
        feedTitle = jsonData.title;
        feedStatus = jsonData.status;
        feedDesc = jsonData.description;
        datastreamsArray = new Array();
        datastreams = {};
        var dsCounter = 0;
        
        for (i = 0; i < datastreamList.length; i++) 
        {
			// include this datastream if it was selected by the user, or if the user left the datastream selection box empty
			if ((datastreamsSel.length === 0) || (datastreamsSel.indexOf(datastreamList[i].id) > -1))
			{
				datastreams[datastreamList[i].id] = datastreamList[i];
				datastreamsArray[dsCounter] = datastreamList[i].id;
				dsCounter += 1;
			}
        }
                
        displayGraphs();
    }
    
    function stringIsEmpty(s)
    {
        if (s)
        {
            if (trim(s) !== "")
            {
                return false;
            }
        }
        return true;
    }
    
    // returns data part of reponse, or "" if error
    function checkResponse(obj)
    {
        try {
            if (obj.data === null || typeof obj.data === "undefined")
            {
                // something went wrong
                if (typeof obj.text === "undefined" || obj.text === null || obj.text === "")
                {
                    displayErrorMsg(obj.error);
                    return "";
                } else
                {
                    displayErrorMsg(obj.text);
                    return "";
                }
            }
            return obj.data;
        } catch (ex)
        {		
            displayErrorMsg("Unknown error");
            return "";
        }
    }
    
    // from StackOverflow Wiki: http://stackoverflow.org/wiki/JavaScript_string_trim
    function trim(stringToTrim) 
    {
        return stringToTrim.replace(/^\s+|\s+$/g,"");
    }	
    
    // NOTE: parm consists of 3 properties:
    //  data: JSON data, or null if something went wrong
    // error: null, or HTTP error if something went wrong
    // text: JSON data as string, or possibly an error message if something went wrong
    function buildStatusString(datastreamObj)
    {
        try {
            var value, timestamp;
            if (datastreamObj == null)
            {
                return "Status unknown";
            }
            value = datastreamObj.current_value;
            timestamp = datastreamObj.at;

            // use regular expression to extract the numbers, usual format is: yyyy-mm-ddThh:mm:ss
            var matches = timestamp.match(/\d+/g); 
            var date1;
            if (!stringIsEmpty(timeZone))
            {
                // time is local
                // NOTE that the Date constructor expects month to be 0-11
                date1 = new Date(matches[0], matches[1] - 1, matches[2], matches[3], matches[4], matches[5]);
            } else
            {
                // time is UTC
                // NOTE that the Date constructor expects month to be 0-11
                date1 = new Date(Date.UTC(matches[0], matches[1] - 1, matches[2], matches[3], matches[4], matches[5]));
            }

             // retrieve values as local time
             //var dateAndTime = date1.getMonth() + "/" + date1.getDate() + " " + date1.getHours() + ":" + date1.getMinutes();
             var monthNames = [ "January", "February", "March", "April", "May", "June",
                "July", "August", "September", "October", "November", "December" ];
             var dateAndTime = monthNames[date1.getMonth()].substr(0,3) + " " + date1.getDate() + "  " + date1.toLocaleTimeString();
     
             // round value to 2 decimal places
             value = Math.round(100 * value) / 100;

             return datastreamObj.id + ": " + value + " at " + dateAndTime;
         } catch (ex)
         {		
            return "Status unknown";
         }
    }
    
   
    function getDataPoints(dataPointArray)
    {

        var pointsParm = '';
        for (var i = 0; i < dataPointArray.length; i++)
        {
            // replace spaces with plus signs, since this will be embedded in a URL request
            pointsParm += dataPointArray[i].replace(/\ /g, "+") + ',';		
        }
        // remove last comma
        pointsParm = pointsParm.substr(0, pointsParm.length - 1);

        return pointsParm;
    }

    // get settings from Preferences dialog
    function getGlobalSettings()
    {

        // get the user prefs
        feedID = prefs.getString("feedID");
        datastreamsSel = prefs.getArray("datastreams");
        timeSpan = prefs.getInt("timeSpan");
        spanType = prefs.getString("spanType");
        detailedGrid = prefs.getBool("detailedGrid");
        axisLabels = prefs.getBool("axisLabels");
        graphHeight = prefs.getInt("graphHeight");
        graphWidth = prefs.getInt("graphWidth");
        displayStatus = prefs.getBool("displayStatus");
        timeZone = prefs.getString("timeZone");
        // Disabled - only worked when the graph PNG images arrived in time
		//autoSizeGadget = prefs.getBool("autoSizeGadget");        
        gadgetHeight = prefs.getInt("gadgetHeight");
    }

    // verify that settings in Prefs dialog are valid
    function verifySettings()
    {
        errorMsg = "";
        if ((feedID == '') || (timeSpan == ''))
        {
            errorMsg = "Feed ID and timespan are required.";
        } else if (graphHeight <= 0 || graphWidth <= 0)
        {
            errorMsg = "Invalid graph height or width";
        } else if  (graphHeight * graphWidth > 300000)
        {
            errorMsg = "Maximum graph size is 300,000 pixels (e.g. 300 by 1000)";
        }
    }
	

    function initialize()
    {
        urlPrefix = "http://api.pachube.com/v2/"
        urlSuffix = "";
		if (timeZone === "None")
		{
			timeZone = "";
		}
        if (!stringIsEmpty(timeZone))
        {


			// replace And with & (Google's Gadget API hates ampersands)
			timeZone = timeZone.replace(" and ", " & ");
			// use escape function to encode spaces and ampersands in URL
            urlSuffix = "timezone=" + escape(timeZone);
        }
    }
   
   // NOTE - the following isNumber function is taken from StackOverflow: http://stackoverflow.com/questions/18082/validate-numbers-in-javascript-isnumeric/1830844#1830844
    function isNumber(n) 
    {
        return !isNaN(parseFloat(n)) && isFinite(n);
    }
   
   function displayGraphs()
   {
        console.log('Pachube Gadget in displayGraphs, view: ' + gadgets.views.getCurrentView().getName() );
        var element = document.getElementById('content_div');
      
        var header = "Pachube - ";
        if (stringIsEmpty(feedTitle))
        {
            header += "Feed " + feedID;
        } else
        {
            header += feedTitle;
        }
        if (!stringIsEmpty(feedStatus))
        {
            header += " (" + feedStatus + ")";
        }
        
        var defaultWidth = 320; // in normal gadget mode, 320 is the recommended width
        // in canvas mode, double the graph size
        if (gadgets.views.getCurrentView().getName() === "CANVAS")
        {
            graphHeight *= 2;
            graphWidth *= 2;
            defaultWidth *= 2;            
        }
        
        if (graphHeight * graphWidth > MAX_GRAPH_SIZE)
        {
            // set to maximum size that will fit within Pachube's limit
            if (graphWidth > defaultWidth)
            {
                graphWidth = defaultWidth;
            }
            graphHeight = Math.floor(MAX_GRAPH_SIZE / graphWidth);
        }        
        
        
        gadgets.window.setTitle(header);

        var html = "";
        var url = urlPrefix + "feeds/" + feedID + "/datastreams/"
        for (var i = 0; i < datastreamsArray.length; i++)
        {
            // put status above graph is requested
            if (displayStatus && datastreamsArray[i] in datastreams)
            {
                var status = buildStatusString(datastreams[datastreamsArray[i]]);
                html += '<br><span class="dp_header">' + status + '</span><br>';
            }

            html += "<img src=" + url + datastreamsArray[i] + ".png?";   
            html += "width=" + graphWidth + "&height=" + graphHeight;
            html += "&duration=" + timeSpan + spanType;
            if (!displayStatus)
            {
                html += "&title=" + datastreamsArray[i]; 
            }
            if (detailedGrid)
            {   
                html += "&detailed_grid=true";
            }
            if (axisLabels)
            {
                html += "&show_axis_labels=true";
            }
            // to override default colour, comment out the next line
            //html += "&color=0000FF" // e.g. blue 
			if (!stringIsEmpty(urlSuffix))
			{
				html += "&" + urlSuffix
			}
			html += '>';
        }
        console.log("Pachube Gadget: chart html is " + html);
        element.innerHTML = html;
		if (autoSizeGadget || gadgets.views.getCurrentView().getName() === "CANVAS")
		{
            windowSize = gadgets.window.getViewportDimensions();
            console.log("Pachube gadget, height is " + windowSize.height);
			gadgets.window.adjustHeight(windowSize.height);
		} else
		{
			// set back to default height
            if (isNumber(gadgetHeight))
            {
                gadgets.window.adjustHeight(gadgetHeight);
            } else
            {
                gadgets.window.adjustHeight(200);
            }
		}
   }
   
   	// add Array.indexOf method if it isn't implemented by the browser
	//  from: http://stackoverflow.com/questions/1181575/javascript-determine-whether-an-array-contains-a-value
	if (!Array.prototype.indexOf) {
		Array.prototype.indexOf = function(needle) {
			for(var i = 0; i < this.length; i++) {
				if(this[i] === needle) {
					return i;
				}
			}
			return -1;
		};
	}
   
   // add null console object if it isn't implemented by the browser
   //  from http://alvinabad.wordpress.com/2009/03/05/firebug-consolelogger/ -- prevents errors if no Javascript console
    if (typeof console == 'undefined') {
        var console = {};
        console.log = function(msg) {
            return;
        };
    }

   
   gadgets.util.registerOnLoadHandler(init)
