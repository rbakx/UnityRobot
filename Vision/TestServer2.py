#file:server.py
import time
from JsonSocket import Server

host = 'LOCALHOST'
port = 5000
data = {
    'name': 'Patrick Jane',
    'age': 45,
    'children': ['Susie', 'Mike', 'Philip']
}

server = Server(host, port)
#server.accept()

while True:
    #data = server.recv()
    try:
    	server.send(data)
    	time.sleep(0.01);
    except:
    	print('waiting for a connection')
    	server.accept()

server.close()