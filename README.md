# UpdateDDNS

A simple and straight-forward dotnet core console app, which will update any DDNS 
service which supports updating with the RA-API.  See the specification here:

https://help.dyn.com/remote-access-api/

It can run on Windows, Linux (including ARM platforms like a Raspberry Pi) and MacOS, 
and is designed to be launched from a cron job.  There is no GUI: it uses a simple json 
configuration file which resides in the user's home folder.  It was intended for use on 
a home computer operating within the network domain: as such, it does not support 
multiple IP addresses.

To create the configuration file, simply run BOG.UpdateDDNS with no parameters the 
first time.  It will create the configuration file with three DDNS services using sample
entries.  In most cases, only one is needed.

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
    "Domain": "host1.mydomain.com"
  },
  {
    "Name": "DynDns",
    "Url": "https://user:updater-client-key@members.dyndns.org/v3/update?hostname=myhost&myip={IP}",
    "Domain": "myhost.dyndns.org"
  }
]
```

To setup the configurations:

- Ensure the Name property is unique among all the DDNS services, and the name is not longer than 20 characters.
- The Domain property needs the full host name to resolve, so it can look up the current registered IP.
- The Url property represents the call to the DDNS service RA-API endpoint, complete with credentials needed for updating the domain's IP address.  Note: only one address is supported.  It also contains a placeholder {IP}, where the current IP address appears in the Url.

When the application is executed, the only parameter specified is the name property value
for the DNS service in the configuraton (e.g. GoogleDomains, DuckDns, DynDns ... or whatever 
you are using).  The application will also append to a log file, which has the same root name 
and folder as the json configuration file but uses the extension "log".

Also, the current and previous IP addresses are recorded in additional properties of the 
configuration file when the application is complete.

That's it.  See your dynamic DNS provider's documentation for the format of the url.

