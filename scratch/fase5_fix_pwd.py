import paramiko

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

bash_script = r"""#!/bin/bash
exec > /tmp/fase5_fix.log 2>&1
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -No -Q "ALTER LOGIN innovatec_app WITH PASSWORD = 'App\$ecur3P@ss2026!';"
/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U innovatec_app -P 'App$ecur3P@ss2026!' -No -d InnovaTecBD -Q "SELECT COUNT(*) FROM ADM.CONFIGURACION"
"""

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    sftp = ssh.open_sftp()
    with sftp.file('/tmp/fase5_fix_pwd.sh', 'w') as f:
        f.write(bash_script)
    sftp.close()
    
    stdin, stdout, stderr = ssh.exec_command("bash /tmp/fase5_fix_pwd.sh && cat /tmp/fase5_fix.log")
    print(stdout.read().decode('utf-8'))
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
