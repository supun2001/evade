import { Room, Client } from "@colyseus/core";
import { MyRoomState } from "./schema/MyRoomState";
import { Player } from "./schema/Player";

const NEXTBOTS_ENABLED = false;
const NEXTBOT_SPAWN_X = 6.45;
const NEXTBOT_SPAWN_Y = 0;
const NEXTBOT_SPAWN_Z = -2.38;
const NEXTBOT_MOVE_SPEED = 9;
const NEXTBOT_STOPPING_DISTANCE = 1.2;
const NEXTBOT_INJURY_DISTANCE = 1.5;
const NEXTBOT_INJURY_COOLDOWN_MS = 1200;

export class MyRoom extends Room<MyRoomState> {
  maxClients = 4;
  state = new MyRoomState();
  private nextInjuryAt = 0;

  onCreate(options: any) {
    this.initializeNextbot();

    //Room ID
    this.roomId = Math.floor(1000 + Math.random() * 9000).toString();
    console.log("Room created!", options, "ID:", this.roomId);

    //Handle player movement
    this.onMessage("playerUpdate", (client, message) => {
      const player = this.state.players.get(client.sessionId);
      if (!player) return;

      // Camera is informational, so keep it in sync every tick.
      player.cameraRotationX = message.cameraRotationX;
      player.cameraRotationY = message.cameraRotationY;
      player.timestamp = Date.now();

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
    const nextbot = this.state.nextbot;
    if (!NEXTBOTS_ENABLED) {
      nextbot.isActive = false;
      nextbot.targetSessionId = "";
      return;
    }

    nextbot.isActive = this.state.isGameStarted && this.state.players.size > 0;

    if (!nextbot.isActive) {
      nextbot.targetSessionId = "";
      return;
    }

    const target = this.findNearestChaseablePlayer(nextbot.x, nextbot.z);
    if (!target) {
      nextbot.targetSessionId = "";
      return;
    }

    nextbot.targetSessionId = target.sessionId;

    const deltaSeconds = deltaTime / 1000;
    this.moveNextbotTowardsPlayer(target, deltaSeconds);
    this.tryInjurePlayer(target);
  }

  private initializeNextbot() {
    this.state.nextbot.x = NEXTBOT_SPAWN_X;
    this.state.nextbot.y = NEXTBOT_SPAWN_Y;
    this.state.nextbot.z = NEXTBOT_SPAWN_Z;
    this.state.nextbot.rotationY = 0;
    this.state.nextbot.targetSessionId = "";
    this.state.nextbot.isActive = false;
  }

  private moveNextbotTowardsPlayer(target: Player, deltaSeconds: number) {
    const nextbot = this.state.nextbot;
    const dx = target.x - nextbot.x;
    const dz = target.z - nextbot.z;
    const distance = Math.hypot(dx, dz);

    if (distance <= 0.0001) {
      return;
    }

    nextbot.rotationY = Math.atan2(dx, dz) * (180 / Math.PI);

    if (distance <= NEXTBOT_STOPPING_DISTANCE) {
      return;
    }

    const moveDistance = Math.min(distance - NEXTBOT_STOPPING_DISTANCE, NEXTBOT_MOVE_SPEED * deltaSeconds);
    nextbot.x += (dx / distance) * moveDistance;
    nextbot.z += (dz / distance) * moveDistance;
  }

  private tryInjurePlayer(target: Player) {
    const nextbot = this.state.nextbot;
    const dx = target.x - nextbot.x;
    const dz = target.z - nextbot.z;
    const distance = Math.hypot(dx, dz);

    if (distance > NEXTBOT_INJURY_DISTANCE || Date.now() < this.nextInjuryAt) {
      return;
    }

    this.nextInjuryAt = Date.now() + NEXTBOT_INJURY_COOLDOWN_MS;
  }

  private findNearestChaseablePlayer(nextbotX: number, nextbotZ: number): Player | undefined {
    let closestPlayer: Player | undefined;
    let closestDistanceSqr = Number.POSITIVE_INFINITY;

    this.state.players.forEach((player) => {
      const dx = player.x - nextbotX;
      const dz = player.z - nextbotZ;
      const distanceSqr = dx * dx + dz * dz;
      if (distanceSqr < closestDistanceSqr) {
        closestDistanceSqr = distanceSqr;
        closestPlayer = player;
      }
    });

    return closestPlayer;
  }

}
