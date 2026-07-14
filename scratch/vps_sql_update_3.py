import paramiko

sql_script = """
USE InnovaTecBD;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ARCHIVADO' AND Object_ID = Object_ID(N'INV.PRODUCTOS'))
BEGIN
    ALTER TABLE INV.PRODUCTOS ADD ARCHIVADO BIT NOT NULL DEFAULT 0;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'FECHA_DESACTIVACION' AND Object_ID = Object_ID(N'INV.PRODUCTOS'))
BEGIN
    ALTER TABLE INV.PRODUCTOS ADD FECHA_DESACTIVACION DATETIME2 NULL;
END
GO
"""

with open('scratch/update_procs.sql', 'r') as f:
    sql_script += f.read()

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect('31.97.147.42', 22, 'root', 'Najippineda2002#')
sftp = ssh.open_sftp()
with sftp.file('/tmp/update.sql', 'w') as f:
    f.write(sql_script)
sftp.close()

stdin, stdout, stderr = ssh.exec_command('/opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U SA -P "InnoV@t3c2026!POS" -i /tmp/update.sql -C')
print(stdout.read().decode())
print(stderr.read().decode())
ssh.exec_command('systemctl restart innovatec')
ssh.close()
