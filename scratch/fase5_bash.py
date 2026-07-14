import paramiko

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

bash_script = """#!/bin/bash
set -e
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -Q 'CREATE DATABASE InnovaTecBD;'
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -d InnovaTecBD -i /tmp/InnovaTecBD.sql
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -d InnovaTecBD -i /tmp/Insersion_Limpia.sql
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -Q "USE InnovaTecBD; CREATE LOGIN innovatec_app WITH PASSWORD = 'App$ecur3P@ss2026!'; CREATE USER innovatec_app FOR LOGIN innovatec_app; ALTER ROLE db_datareader ADD MEMBER innovatec_app; ALTER ROLE db_datawriter ADD MEMBER innovatec_app; GRANT EXECUTE TO innovatec_app;"
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U innovatec_app -P 'App$ecur3P@ss2026!' -No -d InnovaTecBD -Q "SELECT COUNT(*) FROM ADM.CONFIGURACION"
"""

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    
    # Write bash script
    sftp = ssh.open_sftp()
    with sftp.file('/tmp/fase5.sh', 'w') as f:
        f.write(bash_script)
    sftp.close()
    
    # Execute bash script
    print("Executing Phase 5 bash script...")
    stdin, stdout, stderr = ssh.exec_command("bash /tmp/fase5.sh")
    exit_status = stdout.channel.recv_exit_status()
    print(stdout.read().decode('utf-8'))
    if exit_status != 0:
        print(stderr.read().decode('utf-8'))
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
