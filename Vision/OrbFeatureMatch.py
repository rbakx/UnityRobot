#!/usr/local/bin/python
import numpy as np
import cv2
from matplotlib import pyplot as plt
cv2.ocl.setUseOpenCL(False) # Known issue, otherwise orb.compute does not work.

cap = cv2.VideoCapture(0)
print "The video size =", cap.get(3),"x", cap.get(4)

img1 = cv2.imread('Template.png',0)          # queryImage

# Initiate SIFT detector
orb = cv2.ORB_create()

kp1, des1 = orb.detectAndCompute(img1,None)


while(True):
    ret, frame = cap.read()
    img2 = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    kp2, des2 = orb.detectAndCompute(img2,None)
    # create BFMatcher object
    bf = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=True)
    # Match descriptors.
    matches = bf.match(des1,des2)
    # Sort them in the order of their distance.
    matches = sorted(matches, key = lambda x:x.distance)
    # Draw first 10 matches.
    img3 = cv2.drawMatches(img1,kp1,img2,kp2,matches[:1], img1, flags=2)
    cv2.imshow('output',img3)

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break
cap.release()
cv2.destroyAllWindows()
