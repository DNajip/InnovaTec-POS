import paramiko

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

commands = [
    # Download libldap-2.5-0 from Jammy repo
    "wget http://archive.ubuntu.com/ubuntu/pool/main/o/openldap/libldap-2.5-0_2.5.20+dfsg-0ubuntu0.22.04.1_amd64.deb",
    "sudo dpkg -i libldap-2.5-0_2.5.20+dfsg-0ubuntu0.22.04.1_amd64.deb",
    "sudo apt --fix-broken install -y",
    "sudo systemctl start mssql-server",
    "systemctl is-active mssql-server"
]

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    for cmd in commands:
        stdin, stdout, stderr = ssh.exec_command(cmd)
        exit_status = stdout.channel.recv_exit_status()
        print(stdout.read().decode('utf-8'))
        if exit_status != 0:
            print(stderr.read().decode('utf-8'))
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
