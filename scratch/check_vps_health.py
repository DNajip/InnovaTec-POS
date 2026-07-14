import paramiko

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect('31.97.147.42', username='root', password='TuPassword123!')

stdin, stdout, stderr = client.exec_command('df -h /')
print('Disk Usage:')
print(stdout.read().decode())

stdin, stdout, stderr = client.exec_command('systemctl status innovatecpos | head -n 5')
print('Service Status:')
print(stdout.read().decode())

client.close()
