import paramiko
import sys

sys.stdout.reconfigure(encoding='utf-8')

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

bash_script = """#!/bin/bash
set -e
echo "Installing Certbot..."
apt install -y certbot python3-certbot-nginx

echo "Requesting SSL Certificate..."
certbot --nginx -d innovatecpos.com -d www.innovatecpos.com --non-interactive --agree-tos -m darenoportapineda@gmail.com --redirect

echo "Checking Nginx status..."
systemctl restart nginx
systemctl status nginx --no-pager
"""

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    sftp = ssh.open_sftp()
    with sftp.file('/tmp/fase10.sh', 'w') as f:
        f.write(bash_script)
    sftp.close()
    
    print("Executing Phase 10 (SSL Configuration)...")
    stdin, stdout, stderr = ssh.exec_command("bash /tmp/fase10.sh")
    print(stdout.read().decode('utf-8', errors='replace'))
    err = stderr.read().decode('utf-8', errors='replace')
    if err:
        print("STDERR:")
        print(err)
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
