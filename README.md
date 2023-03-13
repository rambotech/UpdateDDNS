# UpdateDDNS

A simple and straight-forward dotnet core console app, which will update any DDNS 
service which supports the RA-API specification here:

https://help.dyn.com/remote-access-api/

It can run on Windows, Linux (including ARM platforms like a Raspberry Pi) and MacOS, 
and is designed to be launched from a cron job.  There is no GUI: it uses a simple json 
configuration file which, by default, resides in the user's home folder.  It was intended 
for use on a computer operating within a home network. As such, it does not support 
multiple IP addresses.

To create the configuration file, simply run BOG.UpdateDDNS with no parameters the 
first time.  It will create the configuration file with three DDNS services using sample
entries. Only one service is updated per execution, with that service specified as a
command-line parameter.

```
[
  {
    "Name": "GoogleDomains",
    "Url": "https://user:password@domains.google.com/nic/update?hostname=host1.mydomain.com&myip={IP}",
    "Domain": "host1.mydomain.com"
  },
  {
    "Name": "DuckDns",
    "Url": "https://www.duckdns.org/update?domains=host01&token=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee&ip={IP}",
    "Domain": "host01.duckdns.org"
  },
  {
    "Name": "DynDns",
    "Url": "https://user:updater-client-key@members.dyndns.org/v3/update?hostname=myhost&myip={IP}",
    "Domain": "myhost.dyndns.org"
  }
]
```

To setup the configurations:

- Ensure the Name property is unique among all the DDNS services, and the name is not 
longer than 20 characters.
- The Domain property needs the full host name to resolve, so it can look up the
current registered IP.
- The Url property represents the call to the DDNS service RA-API endpoint, complete 
with credentials needed for updating the domain's IP address.  Note that each service
can have a different URL format for its call. The URL also has a placeholder \{IP\}, where 
the current IP address will be injected by the application: do not change this value.

## Command Line Parameters

### -s *name*, --service *name*

- *(mandatory)* Specifies which of the defined DDNS services in the configuration is 
to be executed (e.g. GoogleDomains, DuckDns, DynDns ... or whatever you are using).

### -p *folder*, --path *folder*

- *(optional)* Overrides the default location for the config (json) and log files for
the application.  NOTE: The only environment value recognized here is $HOME, for the 
user's home directory (for Windows, Linux and Mac).  On Windows, $HOME equates to 
c:\users\\*username*
- By default, the files are stored in the current logged-in user's home
folder.  *This override is mostly used for cron on a Linux platform, to ensure that 
local and cron launches (root user) use the same directory.*

### Starting 

BOG.UpdateDDNS --service GoogleDomains

When the application is executed, the application loads the configuration file from the
default directory or the directory specified in the path parameter.  It then locates the
configuration for the service, and does the following:

- Calls the google checkip endpoint of Google domains to acquire the current IP used
for the WAN address.
- Compares the IP value returned by the query to the last recorded IP address in use (stored)
in the configuration for the service.
- If it has changed, it calls the dynamic DNS service to update the public IP address (i.e.
it excutes the URL in its configuration with the IP address in the {IP} placeholder).

The application will also append to a log file, which has the same root name 
and folder as the json configuration file but uses the extension "log".

Also, the current and previous IP addresses are recorded in additional properties of the 
configuration file when the application is complete.

### Exit Codes:
 
- 0 == Success
- 1 == Invalid Parameters
- 2 == Error/Failure

### Cron and recommended deployment on Linux (including Raspberry Pi)

To make the proper build, open a console window and execute the following command with the current directory set to where the file UpdateDDNS.csproj resides.  The
resulting deployment file set for will be in publish\linux-arm (for the Raspberry Pi), or publish\linux-x64 for 64-bit Linux systems.

```
for %f in (linux-arm linux-x64) do dotnet publish -c release -o publish\%f -r %f
```

I recommend deployment as follows under the linux account's $HOME folder:
- create a folder named *config*, andd copy the file BOG.UpdateDDNS.json to this folder, and set the DDNS service parameters
- create a folder named *apps*
- create a folder named *updateddns* under the apps folder, and copy the deployment file set to that folder.

Use *sudo crontab -e* to add the following lines (note: subsititute pi for the account name if different).

```
# Dynamic DNS updating (every 15 minutes)
1,16,31,46 * * * * /home/pi/apps/updateddns/BOG.UpdateDDNS --service GoogleDomains --path /home/pi/config
2,17,32,47 * * * * /home/pi/apps/updateddns/BOG.UpdateDDNS --service DuckDns --path /home/pi/config
```

*Note: UpdateDDNS does a soft update. It will always first check the current public IP address for the dynamic dns host name, and only update
the DDNS service if a difference is detected.  The 15-minute interval will almost always be only a check.*

***

That's it.  See your dynamic DNS provider's documentation for the specific format of the url 
(if not one of the three services listed in the configuration example here).
