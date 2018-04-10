#file:server.py
import time
from JsonSocket import Server
from MarkerDetection import MarkerDetector

host = 'LOCALHOST'
port = 5000

data = {
     "bot1" : [20,30,42],
     "bot2" : [50,60,72],
}
data["bot1"] = [77,88,99]

server = Server(host, port)
markerDetector = MarkerDetector(2)

print('waiting for a connection')
server.accept()

while True:
	try:
		rotation,location = markerDetector.markerDetect()
		data["bot1"] = [rotation, location[0], location[1]]
		server.send(data)
		time.sleep(0.1);
	except:
		print('waiting for a connection')
		server.accept()

server.close()