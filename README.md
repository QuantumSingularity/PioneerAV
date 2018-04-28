# PioneerAV

My VSX-1123 Remote Access and Control.

I want to control it with my RaspberryPi and automatically turn it on/off and set the volume when I change the input.



# Install as Daemon
Source: http://pmcgrath.net/running-a-simple-dotnet-core-linux-daemon

- Create SystemD service file

Will run the application from the bin sub directory for now

cat > piopi.service <<EOF
[Unit]
Description=PioPi service
After=network.target

[Service]
ExecStart=/usr/bin/dotnet /home/bem/Projects/BeM_Apps/PioPi/bin/Debug/netcoreapp2.0/PioPi.dll
Restart=on-failure

[Install]
WantedBy=multi-user.target
EOF

Configure SystemD so it is aware of the new service

- Copy service file to a System location
sudo cp piopi.service /lib/systemd/system

- Reload SystemD and enable the service, so it will restart on reboots
sudo systemctl daemon-reload 
sudo systemctl enable piopi

- Start service
sudo systemctl start piopi 

- View service status
systemctl status piopi

Tail the service log

Since we are just writing to stdout the output can be examined with journalctl

sudo journalctl --unit piopi --follow

Stopping and restarting the service

- Stop service
sudo systemctl stop dnsvc 
systemctl status dnsvc 

- Restart the service
sudo systemctl start dnsvc 
systemctl status dnsvc

Cleaning up

- Ensure service is stopped
sudo systemctl stop dnsvc 

- Disable
sudo systemctl disable dnsvc 

- Remove and reload SystemD
sudo rm dnsvc.service /lib/systemd/system/dnsvc.service 
sudo systemctl daemon-reload 

- Verify SystemD is no longer aware of the service - Empty is what we want here
systemctl --type service |& grep dnsvc 


