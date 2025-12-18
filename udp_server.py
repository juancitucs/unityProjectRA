import socket
import json
import random
import string

# Dictionary to store rooms and their clients
rooms = {}
# Dictionary to map client addresses to their room
client_to_room = {}


def generate_room_code(length=5):
    """Generate a random, unique room code."""
    while True:
        code = "".join(random.choices(string.ascii_uppercase + string.digits, k=length))
        if code not in rooms:
            return code


def server_program():
    host = "0.0.0.0"  # Listen on all available interfaces
    port = 5000

    server_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    server_socket.bind((host, port))
    print(f"UDP Server listening on {host}:{port}")

    while True:
        try:
            data, addr = server_socket.recvfrom(1024)
            if not data:
                continue

            message = json.loads(data.decode())
            action = message.get("action")

            if action == "create_room":
                room_code = generate_room_code()
                rooms[room_code] = {"clients": {addr}}
                client_to_room[addr] = room_code
                print(f"Room {room_code} created by {addr}")
                response = {"action": "room_created", "room_code": room_code}
                server_socket.sendto(json.dumps(response).encode(), addr)

            elif action == "join_room":
                room_code = message.get("room_code")
                if room_code in rooms:
                    rooms[room_code]["clients"].add(addr)
                    client_to_room[addr] = room_code
                    print(f"Client {addr} joined room {room_code}")
                    response = {"action": "joined_room", "room_code": room_code}
                    server_socket.sendto(json.dumps(response).encode(), addr)
                else:
                    response = {"action": "error", "message": "Room not found"}
                    server_socket.sendto(json.dumps(response).encode(), addr)

            elif action == "send_transform":
                if addr in client_to_room:
                    room_code = client_to_room[addr]
                    if room_code in rooms:
                        # Broadcast the transform to other clients in the room
                        for client_addr in rooms[room_code]["clients"]:
                            if client_addr != addr:
                                server_socket.sendto(data, client_addr)
                else:
                    # Handle cases where a client sends data without being in a room
                    print(f"Received transform from {addr} which is not in any room.")

        except json.JSONDecodeError:
            print(f"Received non-JSON data from {addr}")
        except Exception as e:
            print(f"An error occurred: {e}")
            # Clean up if a client disconnects
            if addr in client_to_room:
                room_code = client_to_room[addr]
                if room_code in rooms and addr in rooms[room_code]["clients"]:
                    rooms[room_code]["clients"].remove(addr)
                    if not rooms[room_code]["clients"]:
                        del rooms[room_code]  # Delete room if empty
                        print(f"Room {room_code} is empty and has been deleted.")
                del client_to_room[addr]
                print(f"Client {addr} disconnected and was removed.")


if __name__ == "__main__":
    server_program()
