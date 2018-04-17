# Vision module providing vision data.
# It is capable of detecting the position and orientation of Aruco markers. 
# It is also capable of detecting the position of a ball.
import numpy as np
import cv2
import math

class VisionDetector(object):

	def __init__(self, cameraId):
		self.MinBlobSize = 15
		self.MaxBlobSize = 80
		self.cap = cv2.VideoCapture(cameraId)
		self.dictionary = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_6X6_1000)
		self.videoSize = self.cap.get(3), self.cap.get(4)
 
	def detect(self):
		try:
			# Capture frame-by-frame
			ret, frame = self.cap.read()
			if ret == True:
				# Our operations on the frame come here
				img_gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

				res = cv2.aruco.detectMarkers(img_gray,self.dictionary)
				#print(res[0],res[1],len(res[2]))
				# Start with illegal value, used for checking if marker data is valid.
				markerAngle,markerPosition = 1000,(0,0)

				if len(res[0]) > 0:
					cv2.aruco.drawDetectedMarkers(img_gray,res[0],res[1])
					topLeft     = res[0][0][0][0]
					topRight    = res[0][0][0][1]
					bottomRight = res[0][0][0][2]
					bottomLeft  = res[0][0][0][3]
					xAxis = [[(topLeft[0] + bottomLeft[0]) / 2,(topLeft[1] + bottomLeft[1]) / 2],[(topRight[0] + bottomRight[0]) / 2,(topRight[1] + bottomRight[1]) / 2]]
					markerAngle = math.degrees(math.atan((xAxis[1][1] - xAxis[0][1]) / (xAxis[1][0] - xAxis[0][0])))
					if xAxis[0][0] > xAxis[1][0]:
			    			if xAxis[0][1] > xAxis[1][1]:
			        			markerAngle = markerAngle - 180
			    			else:
			       				markerAngle = markerAngle + 180
					markerPosition = (topLeft[0] + topRight[0] + bottomRight[0] + bottomLeft[0]) / 4,(topLeft[1] + topRight[1] + bottomRight[1] + bottomLeft[1]) / 4

					# Detect and show the pose of the markers.
					#cameraMatrix = np.array([[1.,0.,0.],[0.,1.,0.],[0.,0.,1.]])
					#distCoeffs = np.array([0.,0.,0.,0.])
					#rvec, tvec, _ = cv2.aruco.estimatePoseSingleMarkers(res[0],0.05,cameraMatrix,distCoeffs)
					#img_gray = cv2.aruco.drawAxis(img_gray, cameraMatrix, distCoeffs, rvec, tvec, 100)
					#print(rvec)


				#Start blob detection
				params = cv2.SimpleBlobDetector_Params()

				# Change thresholds
				# The default value of params.thresholdStep (10?) seems to work well.
				# To speed processing up, increase to 20 or more.
				#params.thresholdStep = 10
				params.minThreshold = 20
				params.maxThreshold = 200

				# Filter by Area.
				# This prevents that many small blobs (one pixel) will be detected.
				# In addition tt is observed that in that case invalid keypoint coordinates are produced: nan (not a number).
				# When filterByArea is set to True with a minArea > 0 this problem does not occur.
				params.filterByArea = True
				params.minArea = 100
				params.maxArea = 10000
				# Filter by Circularity
				params.filterByCircularity = True
				params.minCircularity = 0.80
				# Filter by Convexity
				params.filterByConvexity = False
				params.minConvexity = 0.87
				# Filter by Inertia
				params.filterByInertia = False
				params.minInertiaRatio = 0.01
				#Filter by distance between blobs
				#params.minDistBetweenBlobs = 100
				# Detect blobs.
				detector = cv2.SimpleBlobDetector_create(params)
				keypoints = detector.detect(img_gray)
				print("nof keypoints", len(keypoints))

				# Start with illegal value, used for checking if blob data is valid.
				blobSize, blobPosition = 1000,(0,0)
				for keyPoint in keypoints:
					s = keyPoint.size
					if s > self.MinBlobSize and s < self.MaxBlobSize:
						blobSize, blobPosition = s,(keyPoint.pt[0],keyPoint.pt[1])
				img_gray_with_keypoints = cv2.drawKeypoints(img_gray, keypoints, np.array([]), (0, 255, 0), cv2.DRAW_MATCHES_FLAGS_DRAW_RICH_KEYPOINTS)
				# End blob detection

				# Display the resulting frame
				cv2.imshow('frame',img_gray_with_keypoints)
				cv2.waitKey(1)
			else:
				# Restart capturing
				cap.release()
				cv2.destroyAllWindows()
				cap = cv2.VideoCapture(2)
			return markerAngle,markerPosition,blobSize,blobPosition
		except:
			print("An unexpected error occurred")
			raise

		# When everything done, release the capture
		cap.release()
		cv2.destroyAllWindows()