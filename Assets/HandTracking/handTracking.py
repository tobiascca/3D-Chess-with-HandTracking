import cv2
import mediapipe as mp
import socket
import json

mp_hands = mp.solutions.hands
mp_drawing = mp.solutions.drawing_utils
hands = mp_hands.Hands()

cap = cv2.VideoCapture(0)

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.connect(("127.0.0.1", 5055))

while True:
    ret, frame = cap.read()
    frame = cv2.flip(frame, 1)

    h, w, _ = frame.shape

    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = hands.process(rgb)

    data = {
        "index_x": 0,
        "index_y": 0,
        "thumb_x": 0,
        "thumb_y": 0,
        "pinch": False
    }

    if results.multi_hand_landmarks:
        for hand_landmarks in results.multi_hand_landmarks:
            mp_drawing.draw_landmarks(frame, hand_landmarks, mp_hands.HAND_CONNECTIONS)
        
        hand = results.multi_hand_landmarks[0].landmark

        index_tip = hand[8]
        thumb_tip = hand[4]

        data["index_x"] = index_tip.x
        data["index_y"] = index_tip.y
        data["thumb_x"] = thumb_tip.x
        data["thumb_y"] = thumb_tip.y

        # pinch detection (distance)
        dx = index_tip.x - thumb_tip.x
        dy = index_tip.y - thumb_tip.y

        dist = (dx*dx + dy*dy) ** 0.5

        data["pinch"] = dist < 0.08

    message = json.dumps(data) + "\n"
    sock.sendall(message.encode())

    cv2.imshow("Hand", frame)
    if cv2.waitKey(1) & 0xFF == 27:
        break

cap.release()
sock.close()
cv2.destroyAllWindows()