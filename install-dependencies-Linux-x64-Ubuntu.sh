# Below is tested with Ubuntu v22 on a Linux x64 system
# Based on https://tecadmin.net/setup-selenium-chromedriver-on-ubuntu/ and https://www.geeksforgeeks.org/how-to-install-selenium-tools-on-linux/

sudo apt update

# netstat
sudo apt install net-tools

# Xvfb (X virtual framebuffer) is an in-memory display server for a UNIX-like operating system (e.g., Linux). 
# It implements the X11 display server protocol without any display
sudo apt install -y unzip xvfb libxi6 libgconf-2-4

# Java OpenJDK
sudo apt install default-jdk

# Google Chrome (it will give some obsolete/deprecated warnings)
sudo curl -sS -o - https://dl-ssl.google.com/linux/linux_signing_key.pub | apt-key add 
sudo bash -c "echo 'deb [arch=amd64] http://dl.google.com/linux/chrome/deb/ stable main' >> /etc/apt/sources.list.d/google-chrome.list" 
sudo apt -y update 
sudo apt -y install google-chrome-stable=116.0.5845.96-1

# ChromeDriver
# Find out which Chrome version you have, since it must match this!
google-chrome --version

# I have hardcoded it to v116 (Linux x64 architecture)
wget https://edgedl.me.gvt1.com/edgedl/chrome/chrome-for-testing/116.0.5845.96/linux64/chromedriver-linux64.zip
unzip chromedriver_linux64.zip
