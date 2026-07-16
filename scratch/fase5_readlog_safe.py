import paramiko
import sys

# Change console encoding to utf-8 just in case
sys.stdout.reconfigure(encoding='utf-8')

host = "31.97.147.42"
port = 22
username = "root"
password = "Najippineda2002#"

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    ssh.connect(host, port, username, password, timeout=10)
    stdin, stdout, stderr = ssh.exec_command("cat /tmp/fase5.log")
    text = stdout.read().decode('utf-8', errors='replace')
    print(text)
except Exception as e:
    print(f"Error: {e}")
finally:
    ssh.close()
