import paramiko
import sys

sys.stdout.reconfigure(encoding='utf-8')

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

systemd_file = """[Unit]
Description=InnovaTecPOS .NET Web Application
After=network.target mssql-server.service

[Service]
WorkingDirectory=/var/www/innovatecpos
ExecStart=/usr/bin/dotnet /var/www/innovatecpos/InnovaTecPOS.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=innovatecpos
User=innovatec
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000

[Install]
WantedBy=multi-user.target
"""

bash_script = f"""#!/bin/bash
set -e
cat << 'EOF' > /etc/systemd/system/innovatec.service
{systemd_file}
EOF

systemctl daemon-reload
systemctl enable innovatec
systemctl restart innovatec
systemctl status innovatec --no-pager
"""

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    sftp = ssh.open_sftp()
    with sftp.file('/tmp/fase11.sh', 'w') as f:
        f.write(bash_script)
    sftp.close()
    
    print("Executing Phase 11 (Systemd Configuration)...")
    stdin, stdout, stderr = ssh.exec_command("bash /tmp/fase11.sh")
    print(stdout.read().decode('utf-8', errors='replace'))
    err = stderr.read().decode('utf-8', errors='replace')
    if err:
        print("STDERR:")
        print(err)
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
