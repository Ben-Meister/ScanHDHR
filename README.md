# ScanHDHR
Author: Ben Meister (opensource at benmeister dot com)

## Description
This program will physically scan the input of a SilconDust HDHomeRun tuner. Similar to a signal meter, it produces a CSV of the signal strength present in the (6 MHz) channel slot, the SNR of the signal if present, and a list of all MPEG TV programs present in the carrier on that channel slot if known. Additionally, a "quickscan" mode is provided for automated performance monitoring, and output may be consumed by other programs such as PRTG.

For devices such as the SiliconDust HDHR Prime, we are able to obtain the raw signal strength (dBmV) and SNR (dB) values and these are provided in the CSV. For antenna devices such as the HDHomeRun CONNECT, these values are not available and we revert to the percentage values provided by the device (which are still useful for identifying spectrum holes etc.)

*IMPORTANT:* The SiliconDust tuner is *not* a calibrated meter. I have provided calibration offsets that work for my two HDHR Primes when calibrating it against a Trilithic DSP860i. However, you will want to calibrate your tuner against a known good meter if you would like to use it to measure correct absolute dBmV values. An uncalibrated readout is still useful for relative readings (comparing historical to current conditions for example).

*NB:* The program currently uses the calibration offsets only in the quickscan mode, and only for the outputs written to the quickscan log files and single reading file. All other readings including the readings on-screen in all modes are raw and will require you to adjust them. (TODO: This could be added as a feature.)

## Usage
Output of scanhdhr.exe when run without parameters:
```plaintext
scanhdhr 1.0.0.0
Ben Meister

ERROR(S):
  Required option 'c, configfile' is missing.

  -c, --configfile     Required. Path to the JSON config file.

  -q, --quick          (Default: false) Perform a quick scan (default: full)

  -p, --printconfig    (Default: false) Print config parameters to the screen (as JSON)

  --writesample        (Default: false) Write a sample config file to the location provided.

  --help               Display this help screen.

  --version            Display version information.
```

Before you begin, you'll need to:
* Install the SiliconDust HDHomeRun software
* Write your config file. You can obtain a sample config by running `scanhdhr.exe --writesample -c sample.json` . A JSON editor such as [JSONedit](https://tomeko.net/software/JSONedit/) makes it easy to edit the file.
* Create the folder to store your CSV scan logs (logpath)

Here's a sample config:

```json
{
	"IP" : "192.168.3.5",
	"HDHRExePath" : "C:\\Program Files\\Silicondust\\HDHomeRun\\hdhomerun_config.exe",
	"tuner" : 1,
	"antenna" : false,
	"quickscan" : false,
	"logpath" : "C:\\scanlogs",
	"quickscansingleresultfilename" : "c:\\inetpub\\wwwroot\\qsresult.txt",
	"minchannel" : 2,
	"maxchannel" : 157,
	"channeltunedelay" : 3000,
	"offsets" : {
		"2" : 5.0,
		"6" : 2.5,
		"98" : 2.9,
		"11" : 0.9,
		"37" : 3.2,
		"72" : 4.2,
		"87" : 4.2,
		"107" : 4.4,
		"123" : 4.4,
		"157" : 4.4
	},
	"qschannelstoscan" : [ 123, 157, 98, 11, 37, 72, 87, 107 ]
}
```

Here's a description of the parameters:
```csharp
// The IP address of the HDHR unit
public string IP;
// The path to hdhomerun_config.exe
public string HDHRExePath;
// The number of the tuner to use
public int tuner;
// True if this is an Antenna HDHR; otherwise False (Cable)
public bool antenna;
// True if quickscan mode; otherwise False (fullscan mode)
public bool quickscan;
// The directory in which to write the output log files
public string logpath;
// For quickscan mode, the file name in which to write the single quickscan result (for PRTG)
public string quickscansingleresultfilename;
// For full scan mode, the starting channel
public int minchannel;
// For full scan mode, the ending channel
public int maxchannel;
// The delay to wait after tuning a channel before reading the signal, in msec
public int channeltunedelay;
// For quickscan mode, offsets to apply to the read channel signal strength values
public Dictionary<int, double> offsets = new Dictionary<int, double>();
// For quickscan mode, which channels to scan
public List<int> qschannelstoscan = new List<int>();
```
