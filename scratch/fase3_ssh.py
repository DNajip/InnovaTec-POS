import paramiko

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

commands = [
    # 4.1 Importar llaves GPG
    "curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | sudo gpg --dearmor --yes -o /usr/share/keyrings/microsoft-prod.gpg",
    # 4.2 Registrar repo de SQL Server 2022
    "curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/mssql-server-2022.list | sudo tee /etc/apt/sources.list.d/mssql-server-2022.list",
    "sudo apt update",
    # 4.3 Instalar SQL Server
    "sudo apt install -y mssql-server",
    # 4.4 Configurar SQL Server automáticamente (Express)
    "MSSQL_PID='Express' ACCEPT_EULA='Y' MSSQL_SA_PASSWORD='InnoV@t3c2026!POS' /opt/mssql/bin/mssql-conf -n setup",
    # 4.6 Instalar Herramientas de SQL
    "curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/prod.list | sudo tee /etc/apt/sources.list.d/mssql-release.list",
    "sudo apt update",
    "ACCEPT_EULA=Y apt install -y mssql-tools18 unixodbc-dev",
    # 4.8 Configurar para acceso local
    "sudo /opt/mssql/bin/mssql-conf set network.ipaddress 127.0.0.1",
    "sudo systemctl restart mssql-server",
    # Verificar status
    "systemctl is-active mssql-server"
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
