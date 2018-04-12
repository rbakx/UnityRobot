from Vision import VisionDetector

visionDetector = VisionDetector(1)

while(True):
	markerAngle,markerPosition,blobSize,blobPosition = visionDetector.detect()
	print(markerAngle,markerPosition,blobSize,blobPosition)
