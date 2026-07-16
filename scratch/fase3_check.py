import paramiko

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

commands = [
    "journalctl -u mssql-server.service -n 50 --no-pager"
]

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    for cmd in commands:
        stdin, stdout, stderr = ssh.exec_command(cmd)
        print(stdout.read().decode('utf-8'))
        print(stderr.read().decode('utf-8'))
except Exception as e:
    pass
finally:
    ssh.close()
