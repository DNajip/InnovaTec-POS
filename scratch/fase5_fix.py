import paramiko

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

commands = [
    # Install mssql-tools18
    "ACCEPT_EULA=Y apt install -y mssql-tools18 unixodbc-dev",
    
    # Executing Phase 5 using explicit path
    "/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -Q 'CREATE DATABASE InnovaTecBD;'",
    "/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -d InnovaTecBD -i /tmp/InnovaTecBD.sql",
    "/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -d InnovaTecBD -i /tmp/Insersion_Limpia.sql",
    "/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -Q \"USE InnovaTecBD; CREATE LOGIN innovatec_app WITH PASSWORD = 'App$ecur3P@ss2026!'; CREATE USER innovatec_app FOR LOGIN innovatec_app; ALTER ROLE db_datareader ADD MEMBER innovatec_app; ALTER ROLE db_datawriter ADD MEMBER innovatec_app; GRANT EXECUTE TO innovatec_app;\"",
    "/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U innovatec_app -P 'App$ecur3P@ss2026!' -No -d InnovaTecBD -Q \"SELECT COUNT(*) FROM ADM.CONFIGURACION\""
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
