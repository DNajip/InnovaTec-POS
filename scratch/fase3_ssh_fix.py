import paramiko

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

commands = [
    # Mover la llave al directorio trusted
    "sudo cp /usr/share/keyrings/microsoft-prod.gpg /etc/apt/trusted.gpg.d/",
    # Instalar SQL
    "sudo apt update",
    "sudo apt install -y mssql-server",
    # Configurar
    "MSSQL_PID='Express' ACCEPT_EULA='Y' MSSQL_SA_PASSWORD='InnoV@t3c2026!POS' /opt/mssql/bin/mssql-conf -n setup",
    # Tools
    "ACCEPT_EULA=Y apt install -y mssql-tools18 unixodbc-dev",
    "echo 'export PATH=\"$PATH:/opt/mssql-tools18/bin\"' >> ~/.bashrc",
    # Localhost only
    "sudo /opt/mssql/bin/mssql-conf set network.ipaddress 127.0.0.1",
    "sudo systemctl restart mssql-server",
    "systemctl is-active mssql-server"
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
