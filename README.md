# Multiplayer-3D-Chess-with-HandTracking

A multiplayer chess game controlled by hand tracking. Players move pieces by pointing with their thumb and pinching to select and place them.

---

## Requirements

### Python
- Python 3.x
- OpenCV: `pip install opencv-python`
- MediaPipe: `pip install mediapipe`

### Unity
- Unity 2021.3 or later
- Mirror Networking package
- Edgegap KCP Transport package

### Hardware
- Webcam (built-in or external)
- Two PCs with a network connection for multiplayer

---

## How to Run

### 1. Start Unity
Open the project in Unity and press **Play**.

### 2. Start Hand Tracking
In a terminal, run:
```bash
python handTracking.py
```
> Always start Unity **before** running the Python script. Unity opens the TCP listener on port 5055 first, then Python connects to it.

### 3. Connect Players

**Host (White pieces):**
1. Click **Host** in the main menu
2. Wait for a session ID to appear — it is copied to your clipboard automatically
3. Share the session ID with the other player

**Client (Black pieces):**
1. Click **Join** in the main menu
2. Paste the session ID you received
3. Click **Connect**

The game starts automatically when both players are connected.

---

## How to Play

| Action | How |
|---|---|
| Move cursor | Point with your **thumb** |
| Select a piece | **Pinch** over it |
| Place a piece | **Pinch** on a highlighted square |
| Quit | Press `Escape` |

After selecting a piece, valid moves are highlighted on the board. Pinch on a highlighted square to move there.

---

## Multiplayer Notes

- Each PC runs its **own** `handTracking.py` independently
- The game uses **Edgegap** as a relay so players don't need to forward ports
- White is always the first player to connect (the host)
- Black is the second player to connect (the client)
- Turns are enforced server-side — you cannot move on your opponent's turn

---

## Troubleshooting

**Python can't connect to Unity**
Make sure Unity is running and you pressed Play before starting the Python script.

**Hand tracking not responding**
Check that your webcam is detected by opening the Python script output window. Press `Esc` in the Python window to quit hand tracking.

**Second player can't connect**
Make sure both PCs are on the same network and the session ID was entered correctly.

**Cursor jumping or jittery**
Ensure good lighting for the webcam. The pinch threshold can be adjusted in `handTracking.py` by changing the `dist < 0.12` value.
