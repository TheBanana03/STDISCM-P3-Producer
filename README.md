## STDISCM-P3 Producer Implementation
- The program requires its partner that can be found in the following github: https://github.com/Retraxi/STDISCM-P3
- Set the channel to http://localhost:5001 for testing within the same machine
- Otherwise, if using a VM like VirtualBox with Bridged Adapter
  - find the IP address of the host through the 'ipconfig' command
  - for the developers the Ipv4 address used was that of the Wi-Fi adapter
- Depending on what happens an inbound rule in firewall might need to be made at the port 5001 to allow for any
  traffic to come into it.

# Config
- The config has 3 entries
  - Producer Threads
    - the number of threads that will be created by the consumer
  - Consumer Threads
    - the number of threads that the producer will have
    - coincides with how many folders will be used
  - Queue Length
    - the maximum amount of data the consumer buffer can hold

# Format for Videos to Upload
- The UploadedVideos folder will be used as the base
- Create a folder following zero-index naming (e.g. 0, 1, 2 as folder names)
- Under each folder you can store multiple video files that will be sent to the consumer
- The number of consumer threads decides how many of the folders will be processed and have
  its files sent over
