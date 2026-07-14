import paramiko
import time

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

bash_script = """#!/bin/bash
exec > /tmp/fase5.log 2>&1
echo "Stopping SQL Server..."
systemctl stop mssql-server
killall -9 sqlservr || true
sleep 3

echo "Setting SA Password..."
export MSSQL_SA_PASSWORD='InnoV@t3c2026!POS'
/opt/mssql/bin/mssql-conf set-sa-password
sleep 3

echo "Starting SQL Server..."
systemctl start mssql-server
sleep 10

echo "Checking if SQL Server is up..."
systemctl status mssql-server

echo "Running SQL scripts..."
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -Q 'CREATE DATABASE InnovaTecBD;'
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -d InnovaTecBD -i /tmp/InnovaTecBD.sql
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -d InnovaTecBD -i /tmp/Insersion_Limpia.sql
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -Q "USE InnovaTecBD; IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'innovatec_app') BEGIN CREATE LOGIN innovatec_app WITH PASSWORD = 'App$ecur3P@ss2026!'; END; IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'innovatec_app') BEGIN CREATE USER innovatec_app FOR LOGIN innovatec_app; END; ALTER ROLE db_datareader ADD MEMBER innovatec_app; ALTER ROLE db_datawriter ADD MEMBER innovatec_app; GRANT EXECUTE TO innovatec_app;"
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U innovatec_app -P 'App$ecur3P@ss2026!' -No -d InnovaTecBD -Q "SELECT COUNT(*) FROM ADM.CONFIGURACION"
echo "Done!"
"""

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    
    # Write bash script
    sftp = ssh.open_sftp()
    with sftp.file('/tmp/fase5_runner.sh', 'w') as f:
        f.write(bash_script)
    sftp.close()
    
    print("Executing Phase 5 bash script in background...")
    # nohup prevents paramiko from blocking
    ssh.exec_command("nohup bash /tmp/fase5_runner.sh > /dev/null 2>&1 &")
    print("Command triggered.")
    
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
