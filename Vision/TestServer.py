#file:server.py
import time
from JsonSocket import Server

host = 'LOCALHOST'
port = 5000

data = {
     "bot1" : {
         "x" : "20",
         "y" : "30",
         "r" : "42"
         },
     "bot2" : {
         "x" : "50",
         "y" : "60",
         "r" : "72"
         }
 }
data = {
     "bot1" : [20,30,42],
     "bot2" : [50,60,72],
 }

server = Server(host, port)
#server.accept()

while True:
    #data = server.recv()
    try:
    	server.send(data)
    	time.sleep(0.1);
    except:
    	print('waiting for a connection')
    	server.accept()

server.close()