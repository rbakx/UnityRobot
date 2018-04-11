from Vision import VisionDetector

visionDetector = VisionDetector(2)

while(True):
	markerAngle,markerPosition,blobSize,blobPosition = visionDetector.detect()
	print(markerAngle,markerPosition,blobSize,blobPosition)
