import paramiko
import sys

sys.stdout.reconfigure(encoding='utf-8')

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

bash_script = r"""#!/bin/bash
set -e

# Replace 'Connection keep-alive;' with 'Connection "upgrade";'
sed -i 's/proxy_set_header Connection keep-alive;/proxy_set_header Connection "upgrade";/g' /etc/nginx/sites-available/innovatecpos

nginx -t
systemctl reload nginx
"""

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    sftp = ssh.open_sftp()
    with sftp.file('/tmp/fase8_fix_ws.sh', 'w') as f:
        f.write(bash_script)
    sftp.close()
    
    stdin, stdout, stderr = ssh.exec_command("bash /tmp/fase8_fix_ws.sh")
    print(stdout.read().decode('utf-8', errors='replace'))
    err = stderr.read().decode('utf-8', errors='replace')
    if err:
        print("STDERR:")
        print(err)
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
