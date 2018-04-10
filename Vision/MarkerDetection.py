import numpy as np
import cv2
import math

class MarkerDetector(object):

	def __init__(self, cameraId):
		self.cap = cv2.VideoCapture(cameraId)
		self.dictionary = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_6X6_1000)
 
	def markerDetect(self):
		try:
			# Capture frame-by-frame
			ret, frame = self.cap.read()
			if ret == True:
				# Our operations on the frame come here
				gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

				res = cv2.aruco.detectMarkers(gray,self.dictionary)
				#print(res[0],res[1],len(res[2]))
				rotation,location = -1,(-1,-1)

				if len(res[0]) > 0:
					cv2.aruco.drawDetectedMarkers(gray,res[0],res[1])
					topLeft     = res[0][0][0][0]
					topRight    = res[0][0][0][1]
					bottomRight = res[0][0][0][2]
					bottomLeft  = res[0][0][0][3]
					xAxis = [[(topLeft[0] + bottomLeft[0]) / 2,(topLeft[1] + bottomLeft[1]) / 2],[(topRight[0] + bottomRight[0]) / 2,(topRight[1] + bottomRight[1]) / 2]]
					rotation = math.degrees(math.atan((xAxis[1][1] - xAxis[0][1]) / (xAxis[1][0] - xAxis[0][0])))
					if xAxis[0][0] > xAxis[1][0]:
			    			if xAxis[0][1] > xAxis[1][1]:
			        			rotation = rotation - 180
			    			else:
			       				rotation = rotation + 180
					location = (topLeft[0] + topRight[0] + bottomRight[0] + bottomLeft[0]) / 4,(topLeft[1] + topRight[1] + bottomRight[1] + bottomLeft[1]) / 4

					# Detect and show the pose of the markers.
					#cameraMatrix = np.array([[1.,0.,0.],[0.,1.,0.],[0.,0.,1.]])
					#distCoeffs = np.array([0.,0.,0.,0.])
					#rvec, tvec, _ = cv2.aruco.estimatePoseSingleMarkers(res[0],0.05,cameraMatrix,distCoeffs)
					#gray = cv2.aruco.drawAxis(gray, cameraMatrix, distCoeffs, rvec, tvec, 100)
					#print(rvec)
				# Display the resulting frame
				cv2.imshow('frame',gray)
				cv2.waitKey(1)
			else:
				# Restart capturing
				cap.release()
				cv2.destroyAllWindows()
				cap = cv2.VideoCapture(2)
			return rotation,location
		except:
			print("An unexpected error occurred")
			raise

		# When everything done, release the capture
		cap.release()
		cv2.destroyAllWindows()