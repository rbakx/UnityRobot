#!/usr/local/bin/python
import cv2
import numpy as np


cap = cv2.VideoCapture(0)
print "The video size =", cap.get(3),"x", cap.get(4)

template = cv2.imread('Template.png',cv2.IMREAD_GRAYSCALE)
w, h = template.shape[::-1]
print 'size template = ', w, h

methods = [cv2.TM_CCOEFF, cv2.TM_CCOEFF_NORMED, cv2.TM_CCORR,
           cv2.TM_CCORR_NORMED, cv2.TM_SQDIFF, cv2.TM_SQDIFF_NORMED]
method = cv2.TM_SQDIFF_NORMED
while(True):
    ret, frame = cap.read()
    i= cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    res = cv2.matchTemplate(i,template,method)
    cv2.imshow('frame',i)
    min_val, max_val, min_loc, max_loc = cv2.minMaxLoc(res)
    # If the method is TM_SQDIFF or TM_SQDIFF_NORMED, take minimum
    if method in [cv2.TM_SQDIFF, cv2.TM_SQDIFF_NORMED]:
        top_left = min_loc
    else:
        top_left = max_loc
    bottom_right = (top_left[0] + w, top_left[1] + h)
    cv2.rectangle(i, top_left, bottom_right, (50, 0, 130), 2)
    cv2.imshow('output',i)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break
cap.release()
cv2.destroyAllWindows()
