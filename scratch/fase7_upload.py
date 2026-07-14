import paramiko
import os
from stat import S_ISDIR

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

local_dir = r"E:\Programación\Antigravity\InnovaTecPOS\InnovaTecPOS\publish"
remote_dir = "/var/www/innovatecpos"

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    
    # Ensure remote directory exists
    ssh.exec_command(f"mkdir -p {remote_dir}")
    sftp = ssh.open_sftp()

    def upload_dir(local_path, remote_path):
        for item in os.listdir(local_path):
            l_path = os.path.join(local_path, item)
            r_path = f"{remote_path}/{item}"
            if os.path.isfile(l_path):
                sftp.put(l_path, r_path)
            elif os.path.isdir(l_path):
                try:
                    sftp.mkdir(r_path)
                except Exception:
                    pass
                upload_dir(l_path, r_path)

    print("Uploading publish directory...")
    upload_dir(local_dir, remote_dir)
    print("Upload complete!")
    
    # Set permissions
    ssh.exec_command(f"chown -R innovatec:www-data {remote_dir}")
    ssh.exec_command(f"chmod -R 750 {remote_dir}")
    print("Permissions set.")
    
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
