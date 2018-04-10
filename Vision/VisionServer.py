#file:server.py
import time
from JsonSocket import Server
from MarkerDetection import MarkerDetector

host = 'LOCALHOST'
port = 5000

data = {
	"videoSize" : [-1,-1],
    "bot1" : [-1,-1,-1]
}

server = Server(host, port)
markerDetector = MarkerDetector(0)
data["videoSize"] = [markerDetector.videoSize[0],markerDetector.videoSize[1]]

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