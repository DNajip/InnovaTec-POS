import paramiko

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect('31.97.147.42', username='root', password='admin_password_here')

stdin, stdout, stderr = client.exec_command('systemctl status innovatecpos --no-pager')
print('--- STATUS ---')
print(stdout.read().decode())
print(stderr.read().decode())

stdin, stdout, stderr = client.exec_command('journalctl -u innovatecpos -n 20 --no-pager')
print('--- LOGS ---')
print(stdout.read().decode())

client.close()
