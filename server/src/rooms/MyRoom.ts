import { Room, Client } from "@colyseus/core";
import { MyRoomState } from "./schema/MyRoomState";
import { Player } from "./schema/Player";

export class MyRoom extends Room<MyRoomState> {
  maxClients = 4;
  state = new MyRoomState();

  onCreate(options: any) {

    //Room ID
    this.roomId = Math.floor(1000 + Math.random() * 9000).toString();
    console.log("Room created!", options, "ID:", this.roomId);

    //Handle player movement
    this.onMessage("playerUpdate", (client, message) => {
      const player = this.state.players.get(client.sessionId);
      if (!player) return;

      // Position & Rotation
      player.x = message.x;
      player.y = message.y;
      player.z = message.z;
      player.rotationY = message.rotationY;

      // Velocity
      player.velocityX = message.velocityX;
      player.velocityY = message.velocityY;
      player.velocityZ = message.velocityZ;

      // Animation States
      player.animInputX = message.animInputX;
      player.animInputY = message.animInputY;
      player.isGrounded = message.isGrounded;
      player.isJumping = message.isJumping;

      // Camera
      player.cameraRotationX = message.cameraRotationX;
      player.cameraRotationY = message.cameraRotationY;

      player.timestamp = Date.now();
    });

    this.onMessage("playerReady", (client, isReady) => {
      const player = this.state.players.get(client.sessionId);
      if (player) {
        player.isReady = isReady;

        // Check if all players are ready
        let allReady = true;
        this.state.players.forEach((p) => {
          if (!p.isReady) allReady = false;
        });

        if (allReady && this.state.players.size > 0) {
          this.state.isGameStarted = true;
          this.broadcast("startGame");
          // this.lock(); // Removed lock to allow late joiners
        }
      }
    });

    //Set update rate (60 times per second)
    this.setSimulationInterval((deltaTime) =>
      this.update(deltaTime), 1000 / 60
    );

    this.onMessage("setSkin", (client, skinIndex) => {
      const player = this.state.players.get(client.sessionId);
      if (player) {
        player.skinIndex = skinIndex;
      }
    });
  }

  onJoin(client: Client, options: any) {
    console.log(client.sessionId, "joined!");

    //Create a new player
    const player = new Player();
    player.sessionId = client.sessionId;

    // Late joiners are automatically ready if game already started
    if (this.state.isGameStarted) {
      player.isReady = true;
    }

    //Spawn at random position
    player.x = Math.random() * 10 - 5;
    player.y = 0;
    player.z = Math.random() * 10 - 5;

    //Add player to state
    this.state.players.set(client.sessionId, player);
  }

  onLeave(client: Client, consented: boolean) {
    console.log(client.sessionId, "left!");

    //Remove player from state
    this.state.players.delete(client.sessionId);

    // If no players left, reset game state
    if (this.state.players.size === 0) {
      this.state.isGameStarted = false;
    }

    // Unlock the room for late commers 
    this.unlock();
  }

  onDispose() {
    console.log("room", this.roomId, "disposing...");
  }

  update(deltaTime: number) {
    //server-side game logics
  }

}
