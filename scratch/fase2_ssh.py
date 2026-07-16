import paramiko
import sys
import time

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

commands = [
    "apt update && DEBIAN_FRONTEND=noninteractive apt upgrade -y",
    "timedatectl set-timezone America/Managua",
    "timedatectl",
    "id -u innovatec &>/dev/null || adduser --disabled-password --gecos '' innovatec",
    "echo 'innovatec:Innovatec!2026' | chpasswd",
    "usermod -aG sudo innovatec",
    "apt install -y curl wget git unzip software-properties-common apt-transport-https nano ufw"
]

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    print(f"Connecting to {host}...")
    ssh.connect(host, port, username, password, timeout=10)
    print("Connected successfully!")
    
    for cmd in commands:
        print(f"\nExecuting: {cmd}")
        stdin, stdout, stderr = ssh.exec_command(cmd)
        
        # Wait for the command to finish
        exit_status = stdout.channel.recv_exit_status()
        
        print("STDOUT:")
        print(stdout.read().decode('utf-8'))
        print("STDERR:")
        print(stderr.read().decode('utf-8'))
        print(f"Exit status: {exit_status}")

except Exception as e:
    print(f"An error occurred: {e}")
finally:
    ssh.close()
