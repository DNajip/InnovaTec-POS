import paramiko

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

nginx_conf = """server {
    listen 80;
    server_name innovatecpos.com www.innovatecpos.com srv1693117.hstgr.cloud;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
"""

bash_script = f"""#!/bin/bash
set -e
apt install -y nginx
cat << 'EOF' > /etc/nginx/sites-available/innovatecpos
{nginx_conf}
EOF
ln -sf /etc/nginx/sites-available/innovatecpos /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl restart nginx
"""

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    sftp = ssh.open_sftp()
    with sftp.file('/tmp/fase8.sh', 'w') as f:
        f.write(bash_script)
    sftp.close()
    
    print("Executing Phase 8 (Nginx configuration)...")
    stdin, stdout, stderr = ssh.exec_command("bash /tmp/fase8.sh")
    exit_status = stdout.channel.recv_exit_status()
    print(stdout.read().decode('utf-8'))
    if exit_status != 0:
        print(stderr.read().decode('utf-8'))
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
