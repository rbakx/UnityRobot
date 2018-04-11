#!/usr/local/bin/python
import cv2
import numpy as np


cap = cv2.VideoCapture(0)
print("The video size =", cap.get(3),"x", cap.get(4))

template = cv2.imread('Template.png',cv2.IMREAD_GRAYSCALE)
w, h = template.shape[::-1]
print('size template = ', w, h)

methods = [cv2.TM_CCOEFF, cv2.TM_CCOEFF_NORMED, cv2.TM_CCORR,
           cv2.TM_CCORR_NORMED, cv2.TM_SQDIFF, cv2.TM_SQDIFF_NORMED]
method = cv2.TM_SQDIFF_NORMED
while(True):
    ret, frame = cap.read()
    img_gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

    # Setup SimpleBlobDetector parameters.
    params = cv2.SimpleBlobDetector_Params()

    # Change thresholds
    # The default value of params.thresholdStep (10?) seems to work well.
    # To speed processing up, increase to 20 or more.
    #params.thresholdStep = 20
    params.minThreshold = 20
    params.maxThreshold = 200

    # Filter by Area.
    # This prevents that many small blobs (one pixel) will be detected.
    # In addition tt is observed that in that case invalid keypoint coordinates are produced: nan (not a number).
    # When filterByArea is set to True with a minArea > 0 this problem does not occur.
    params.filterByArea = True
    params.minArea = 100
    params.maxArea = 100000
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
    img_gray_with_keypoints = cv2.drawKeypoints(img_gray, keypoints, np.array([]), (0, 255, 0), cv2.DRAW_MATCHES_FLAGS_DRAW_RICH_KEYPOINTS)

    # Show keypoints
    cv2.imshow('blobs',img_gray_with_keypoints)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break
cap.release()
cv2.destroyAllWindows()
