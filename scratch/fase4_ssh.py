import paramiko

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

commands = [
    "wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh",
    "chmod +x /tmp/dotnet-install.sh",
    "/tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet",
    "ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet",
    "dotnet --version"
]

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    for cmd in commands:
        print(f"Executing: {cmd}")
        stdin, stdout, stderr = ssh.exec_command(cmd)
        exit_status = stdout.channel.recv_exit_status()
        print(stdout.read().decode('utf-8'))
        if exit_status != 0:
            print(stderr.read().decode('utf-8'))
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
