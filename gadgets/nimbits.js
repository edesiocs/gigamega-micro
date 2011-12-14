// Last updated: Dec 5 2011
 
   // Get userprefs
   var prefs = new gadgets.Prefs();
  	
	// global settings from user prefs:
	var serverName;
	var dataPoints1; // array
	var dataPoints2; // array
	var dataPoints3; // array
	var userEmail;
	var displayStatus;

	// global variables 
	var status1;
	var status2 = "";
	var status3 = "";	
	var currentValueURL;
	var colours1; 
	var colours2; 
	var colours3; 
	// NOTE: the following colours are assigned in sequence to each Data Point
	var colourList = ["FF0000", "00FF00", "0000FF", "FF4500", "00C78C", "7F00FF", "8A360F", "FF00AA"];
	var colourIndex = 0;
	
	function init()
	{

		getGlobalSettings();
		if ((serverName == '') || (dataPoints1.length == 0))
		{
			var errorMsg = "Server and Data Point(s) required.  Please enter the fields in the Edit Settings dialog.";
			var element = document.getElementById('content_div');
			element.innerHTML = '<span class="error">' + errorMsg + '</span>'; 
		} else
		{
			if (displayStatus)
			{
				requestStatus1();
			} else
			{
				displayChart();
			}
		}
	}
	
	// NOTE - the following code is taken from the Google Gadgets Developer documentation
	function makeCachedRequest(url, callback, params, refreshInterval) 
	{
	  // replace spaces with plus signs, since this will be embedded in a URL request
	  url = url.replace(/\ /g, "+");	
	  console.log("Nimbits Gadget: status request html is " + url);
	  
	  var ts = new Date().getTime();
	  var sep = "?";
	  if (refreshInterval && refreshInterval > 0) {
		ts = Math.floor(ts / (refreshInterval * 1000));
	  }
	  if (url.indexOf("?") > -1) {
		sep = "&";
	  }	  
	  
	  url = [ url, sep, "nocache=", ts ].join("");

	  gadgets.io.makeRequest(url, callback, params);
	}
	
	function requestStatus1()
	{
		var url = currentValueURL + "point=" + dataPoints1[0] + "&email=" + userEmail + "&format=json";
		var params = {};
        params[gadgets.io.RequestParameters.CONTENT_TYPE] = gadgets.io.ContentType.JSON;
		// refresh data if longer than 5 minutes since last request
		makeCachedRequest(url, getResponse1, params, 300);
	}
	
	// from StackOverflow Wiki: http://stackoverflow.org/wiki/JavaScript_string_trim
	function trim(stringToTrim) 
	{
		return stringToTrim.replace(/^\s+|\s+$/g,"");
	}	
	
   // NOTE - the following isNumber function is taken from StackOverflow: http://stackoverflow.com/questions/18082/validate-numbers-in-javascript-isnumeric/1830844#1830844
    function isNumber(n) 
    {
        return !isNaN(parseFloat(n)) && isFinite(n);
    }
	
	// NOTE: parm consists of 3 properties:
	//  data: JSON data, or null if something went wrong
	// error: null, or HTTP error if something went wrong
	// text: JSON data as string, or possibly an error message if something went wrong
	function buildStatusString(obj)
	{
		try {
			if (obj.data == null || typeof obj.data == "undefined")
			{
				// something went wrong
				if (typeof obj.text == "undefined" || obj.text == null || obj.text == "")
				{
					return obj.error;
				} else
				{
					return obj.text;
				}
			}
			jsondata = obj.data;
			
			var alldata = "";
			for (var key in jsondata) {
				var value = jsondata[key];
				alldata += key + ":" + value + ", ";
			 }

			 var date1;

			// 12/4/11 - detect and convert date and time in Unix Epoch format
			if (isNumber(jsondata['timestamp']))
			{
				// assume it is milliseconds in Unix Epoch time
				date1 = new Date(jsondata['timestamp']);
			} else
			{
				 // DAW - 8/17/11 - changed handling of timestamp property: format was changed by GAE or GG API to add blank after time (e.g. "2011-08-17T13:58:05 +0000")
				//  The following will work if the delimiters change, provided that the order of the date fields remains the same
				var matches = jsondata['timestamp'].match(/\d+/g); // use regular expression to extract the numbers
				// NOTE that the Date constructor expects month to be 0-11
				date1 = new Date(Date.UTC(matches[0], matches[1] - 1, matches[2], matches[3], matches[4], matches[5]));
			}

			 // retrieve values as local time
			 //var dateAndTime = date1.getMonth() + "/" + date1.getDate() + " " + date1.getHours() + ":" + date1.getMinutes();
			 var monthNames = [ "January", "February", "March", "April", "May", "June",
				"July", "August", "September", "October", "November", "December" ];
			 var dateAndTime = monthNames[date1.getMonth()].substr(0,3) + " " + date1.getDate() + "  " + date1.toLocaleTimeString();
	 
			 var value1 = jsondata['d'];
			 value1 = Math.round(100 * value1) / 100;

			 return dateAndTime + ' -- ' + value1;
		 } catch (ex)
		 {		
			return "Status unknown";
		 }
	}
	
	function getResponse1(obj)
	{

		console.log("getResponse1 errors: " + obj.errors);
		console.log("getResponse1 text:" + obj.text);
		console.log("getResponse1 data:" + obj.data);

		
		//var jsondata = obj.data;
		
		 status1 = dataPoints1[0] + ":  " + buildStatusString(obj);
		 if (dataPoints2.length > 0)
		 {
			requestStatus2()
		 } else
		 {
			displayChart();
		}
	}
	
	function requestStatus2()
	{

		var url = currentValueURL + "point=" + dataPoints2[0] + "&email=" + userEmail + "&format=json";

		var params = {};
        params[gadgets.io.RequestParameters.CONTENT_TYPE] = gadgets.io.ContentType.JSON;
		// refresh data if longer than 5 minutes since last request
		makeCachedRequest(url, getResponse2, params, 300);
	}
	
	function getResponse2(obj)
	{

		 //var jsondata = obj.data;
		 status2 = dataPoints2[0] + ":  " + buildStatusString(obj);
		 if (dataPoints3.length > 0)
		 {
			requestStatus3()
		 } else
		 {
			displayChart();
		 }
	}
	
	function requestStatus3()
	{

		var url = currentValueURL + "point=" + dataPoints3[0] + "&email=" + userEmail + "&format=json";
		var params = {};
        params[gadgets.io.RequestParameters.CONTENT_TYPE] = gadgets.io.ContentType.JSON;
		// refresh data if longer than 5 minutes since last request
		makeCachedRequest(url, getResponse3, params, 300);
	}
	
	function getResponse3(obj)
	{
		//var jsondata = obj.data;
		status3 = dataPoints3[0] + ":  " + buildStatusString(obj);
		displayChart();
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
   
   function getGlobalSettings()
   {

		// get the user prefs
		serverName = prefs.getString("serverName");
		dataPoints1 = prefs.getArray("dataPoints1");
		userEmail = prefs.getString("userEmail");
		dataPoints2 = "";
		if (prefs.getBool("displayGraph2"))
		{
			dataPoints2 = prefs.getArray("dataPoints2");
		}
		dataPoints3 = "";
		if (prefs.getBool("displayGraph3"))
		{
			dataPoints3 = prefs.getArray("dataPoints3");
		}
		
		// assign colours to data points
		colours1 = assignColours(dataPoints1);
		colours2 = assignColours(dataPoints2);
		colours3 = assignColours(dataPoints3);
		
		currentValueURL = "http://" + serverName + "/service/currentvalue?";
		displayStatus = prefs.getBool("displayStatus");
   }
   
   function assignColours(dataPointArray)
   {
		var colourString = "";
		for (var i = 0; i < dataPointArray.length; i++)
		{
			colourString += colourList[colourIndex++] + ",";
			if (colourIndex >= colourList.length)
			{
				colourIndex = 0; // loop around
			}
		}
		if (colourString.length > 0)
		{
			// remove last comma
			colourString = colourString.substr(0, colourString.length - 1);
		}
		return colourString;
   }
   
   function displayChart()
   {
		console.log('Nimbits Gadget in displayChart, view: ' + gadgets.views.getCurrentView().getName() );
		var element = document.getElementById('content_div');
		
		// get the user prefs
		var timeSpan = prefs.getInt("timeSpan");
		var graphHeight = prefs.getInt("graphHeight");
		
		if ((serverName == '') || (dataPoints1.length == 0) || (timeSpan <= 0))
		{
			var errorMsg = "Server, Data Point(s) and Time Span required.  Please enter the fields in the Edit Settings dialog.";
			element.innerHTML = '<span class="error">' + errorMsg + '</span>'; 
			return;
		}
		
		if (graphHeight < 0)
		{
			var errorMsg = "Invalid setting.  Please check the values in the Edit Settings dialog.";
			element.innerHTML = '<span class="error">' + errorMsg + '</span>'; 
			return;
		}
		
        // 12/5/11 - replace hard-coded graph width with user setting
		//var graphWidth = 320; // in normal gadget mode, 320 is the recommended width
        var graphWidth = prefs.getInt("graphWidth");
        
        if (!isNumber(graphWidth) || (graphWidth <= 0))
        {
            // if not set, use old default for backwards compatibility
            graphWidth = 320;
        }
		// in canvas mode, double the graph size
		if (gadgets.views.getCurrentView().getName() == "CANVAS")
		{
			graphHeight *= 2;
			graphWidth *= 2;
		}
		
		var displayLegend = prefs.getBool("displayLegend");
		var displayGrid = prefs.getString("gridStyle");
		var spanType = prefs.getString("spanType");
		
		gadgets.window.setTitle('Nimbits - ' + serverName);

		var html = "";
		for (var i = 0; i < 3; i++)
		{
			var pointsParm;
			var colourParm;
			if (i == 0)
			{
				if (displayStatus && status1 != "")
				{
					html += '<span class="dp_header">' + status1 + '</span><br>';
				}
				pointsParm = getDataPoints(dataPoints1);
				colourParm = colours1;
			} else if (i == 1)
			{
				if (displayStatus && status2 != "")
				{
					html += '<br><span class="dp_header">' + status2 + '</span><br>';
				}
				pointsParm = getDataPoints(dataPoints2);
				colourParm = colours2;
			} else
			{
				if (displayStatus && status3 != "")
				{
					html += '<br><span class="dp_header">' + status3 + '</span><br>';
				}
				pointsParm = getDataPoints(dataPoints3);
				colourParm = colours3;
			}
			if ( pointsParm == "" || pointsParm == null)
			{
				continue; // skip this chart #
			}
			
			// chdl parm is pipe delimited, not comma delimited
			var legendParm = pointsParm.replace(/\,/g, "|");
			
			html += '<img src=http://' + serverName + '/service/chartapi?'
			  + 'points=' + pointsParm;   
			html += '&email=' + userEmail; 
			// 12/3/12 - autoscale no longer works - use Google Charts API equivalent
			//html +=  '&autoscale=true
			html += '&chds=a'
			html += '&cht=lc&chco=' + colourParm + '&chxt=y&chf=bg,s,EFEFEF';
			html += '&chs=' + graphWidth + 'x' + graphHeight;
			if (spanType == "Hours")
			{
				html += "&st=-" + timeSpan + "h&et=now";
			} else
			{
				// # of readings
				html += "&count=" + timeSpan;
			}
			
			 if (displayLegend)
			{
				// display it at top listing all data points
				html += '&chdl=' + legendParm + '&chdlp=t';
			}
			var gridStyle = "&chg=";
			if (displayGrid == "Vertical")
			{
			   gridStyle += "10,0,1,0";	   
			} else if (displayGrid == "Horiz")
			{
				gridStyle += "0,10,1,0";
			} else if (displayGrid == "Both")
			{
				gridStyle += "10,10,1,0";
			} else 
			{
				// no grid
				gridStyle = "";
			}
			
			html += gridStyle;
			 html += '>';
		}
		console.log("Nimbits Gadget: chart html is " + html);
		element.innerHTML = html;
        
        // 12/5/11 - support gadget height setting
        if (gadgets.views.getCurrentView().getName() !== "CANVAS")
        {
            var gadgetHeight = prefs.getInt("gadgetHeight");
            if (isNumber(gadgetHeight))
            {
                gadgets.window.adjustHeight(gadgetHeight);
            } else
            {
                gadgets.window.adjustHeight(200);
            }
        }
   }
   
   // Following is from http://alvinabad.wordpress.com/2009/03/05/firebug-consolelogger/ -- prevents errors if no Javascript console
   // if console is not defined, e.g., Firebug console is not enabled or Non-Firefox browser
	if (typeof console == 'undefined') {
		var console = {};
		console.log = function(msg) {
			return;
		};
	}

   
   gadgets.util.registerOnLoadHandler(init)
