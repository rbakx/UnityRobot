#file:server.py
import time
from JsonSocket import Server
from Vision import VisionDetector

host = 'LOCALHOST'
port = 5000

data = {
	"videoSize" : [-1,-1],
    "bot1" : [1000,-1,-1],
    "ball" : [1000,-1,-1]
}

server = Server(host, port)
visionDetector = VisionDetector(2)
data["videoSize"] = [visionDetector.videoSize[0],visionDetector.videoSize[1]]

print('waiting for a connection')
server.accept()

while True:
	try:
		markerAngle,markerPosition,blobSize,blobPosition = visionDetector.detect()
		data["bot1"] = [markerAngle, markerPosition[0], markerPosition[1]]
		data["ball"] = [blobSize, blobPosition[0], blobPosition[1]]
		print (data)
		server.send(data)
		time.sleep(0.1);
	except:
		print('waiting for a connection')
		server.accept()

server.close()