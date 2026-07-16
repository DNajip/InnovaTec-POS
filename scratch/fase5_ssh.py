import paramiko
import os

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

local_schema = r"E:\Programación\Antigravity\InnovaTecPOS\Documentos\InnovaTecBD.sql"
local_data = r"E:\Programación\Antigravity\InnovaTecPOS\Documentos\Insersion_Limpia.sql"

remote_schema = "/tmp/InnovaTecBD.sql"
remote_data = "/tmp/Insersion_Limpia.sql"

sql_commands = [
    # Crear Base de datos
    "sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -Q 'CREATE DATABASE InnovaTecBD;'",
    # Ejecutar Schema
    "sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -d InnovaTecBD -i /tmp/InnovaTecBD.sql",
    # Ejecutar Datos Limpios
    "sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -d InnovaTecBD -i /tmp/Insersion_Limpia.sql",
    # Crear usuario de aplicacion
    "sqlcmd -S 127.0.0.1 -U SA -P 'InnoV@t3c2026!POS' -Q \"USE InnovaTecBD; CREATE LOGIN innovatec_app WITH PASSWORD = 'App$ecur3P@ss2026!'; CREATE USER innovatec_app FOR LOGIN innovatec_app; ALTER ROLE db_datareader ADD MEMBER innovatec_app; ALTER ROLE db_datawriter ADD MEMBER innovatec_app; GRANT EXECUTE TO innovatec_app;\"",
    # Verificar usuario
    "sqlcmd -S 127.0.0.1 -U innovatec_app -P 'App$ecur3P@ss2026!' -d InnovaTecBD -Q \"SELECT COUNT(*) FROM ADM.CONFIGURACION\""
]

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    print("Connecting...")
    ssh.connect(host, port, username, password, timeout=10)
    
    # SFTP Transfer
    print("Transferring SQL files...")
    sftp = ssh.open_sftp()
    sftp.put(local_schema, remote_schema)
    sftp.put(local_data, remote_data)
    sftp.close()
    print("Transfer complete.")
    
    # Executing SQL Commands
    for cmd in sql_commands:
        print(f"\nExecuting: {cmd}")
        stdin, stdout, stderr = ssh.exec_command(cmd)
        exit_status = stdout.channel.recv_exit_status()
        print(stdout.read().decode('utf-8'))
        if exit_status != 0:
            print(stderr.read().decode('utf-8'))

except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
