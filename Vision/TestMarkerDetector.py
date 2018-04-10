from MarkerDetection import MarkerDetector

markerDetector = MarkerDetector(2)

while(True):
	rotation,location = markerDetector.markerDetect()
	print(rotation,location)
	print (location[0])
