import { Room, Client } from "@colyseus/core";
import { MyRoomState } from "./schema/MyRoomState";
import { Player } from "./schema/Player";

const NEXTBOTS_ENABLED = true;
const DEFAULT_NEXTBOT_SPAWN_POINTS = [
  { x: 6.45, y: 0, z: -2.38 },
  { x: -6.45, y: 0, z: 2.38 },
  { x: 0, y: 0, z: 7.5 },
];
const NEXTBOT_MOVE_SPEED = 9;
const NEXTBOT_STOPPING_DISTANCE = 1.2;
const NEXTBOT_INJURY_DISTANCE = 1.5;
const NEXTBOT_INJURY_COOLDOWN_MS = 1200;
const NEXTBOT_START_GRACE_MS = 3500;
const PLAYER_MIN_SPAWN_DISTANCE_FROM_NEXTBOT = 8;
const PLAYER_SPAWN_RANGE = 10;

export class MyRoom extends Room<MyRoomState> {
  maxClients = 4;
  state = new MyRoomState();
  private nextInjuryAt = 0;
  private playerSafeUntil = new Map<string, number>();
  private nextbotSpawnPoints = DEFAULT_NEXTBOT_SPAWN_POINTS;
  private activeNextbotSpawnPoint = DEFAULT_NEXTBOT_SPAWN_POINTS[0];

  onCreate(options: any) {
    this.nextbotSpawnPoints = this.resolveNextbotSpawnPoints(options);
    this.activeNextbotSpawnPoint = this.nextbotSpawnPoints[0];
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
      player.isInjured = message.isInjured;
      player.isCrouching = message.isCrouching;
      player.isWallRunning = message.isWallRunning;
      player.wallRunSide = message.wallRunSide;
      player.moveInputX = message.moveInputX;
      player.moveInputY = message.moveInputY;
      player.visualYaw = message.visualYaw;
      player.isHitReacting = message.isHitReacting;
      player.hitReactionTimeRemaining = message.hitReactionTimeRemaining;
      player.hitReactionPitch = message.hitReactionPitch;
      player.hitReactionRoll = message.hitReactionRoll;
      player.hitReactionSeed = message.hitReactionSeed;
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
          this.resetNextbotToSpawnPoint();
          const safeUntil = Date.now() + NEXTBOT_START_GRACE_MS;
          this.state.players.forEach((readyPlayer) => {
            this.playerSafeUntil.set(readyPlayer.sessionId, safeUntil);
          });
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

    const spawnPosition = this.getSafePlayerSpawnPosition();
    player.x = spawnPosition.x;
    player.y = 0;
    player.z = spawnPosition.z;
    player.isGrounded = true;
    player.isJumping = false;
    player.isInjured = false;
    player.isCrouching = false;
    player.isWallRunning = false;
    player.wallRunSide = 0;
    player.moveInputX = 0;
    player.moveInputY = 0;
    player.visualYaw = 180;
    player.isHitReacting = false;
    player.hitReactionTimeRemaining = 0;
    player.hitReactionPitch = 0;
    player.hitReactionRoll = 0;
    player.hitReactionSeed = 0;
    player.hitTriggerId = 0;
    player.hitSourceX = 0;
    player.hitSourceY = 0;
    player.hitSourceZ = 0;

    //Add player to state
    this.state.players.set(client.sessionId, player);
    this.playerSafeUntil.set(client.sessionId, Date.now() + NEXTBOT_START_GRACE_MS);
  }

  onLeave(client: Client, consented: boolean) {
    console.log(client.sessionId, "left!");

    //Remove player from state
    this.state.players.delete(client.sessionId);
    this.playerSafeUntil.delete(client.sessionId);

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
    this.activeNextbotSpawnPoint = this.pickNextbotSpawnPoint();
    this.state.nextbot.x = this.activeNextbotSpawnPoint.x;
    this.state.nextbot.y = this.activeNextbotSpawnPoint.y;
    this.state.nextbot.z = this.activeNextbotSpawnPoint.z;
    this.state.nextbot.rotationY = 0;
    this.state.nextbot.targetSessionId = "";
    this.state.nextbot.isActive = false;
  }

  private resetNextbotToSpawnPoint() {
    this.activeNextbotSpawnPoint = this.pickNextbotSpawnPoint();
    this.state.nextbot.x = this.activeNextbotSpawnPoint.x;
    this.state.nextbot.y = this.activeNextbotSpawnPoint.y;
    this.state.nextbot.z = this.activeNextbotSpawnPoint.z;
    this.state.nextbot.rotationY = 0;
    this.state.nextbot.targetSessionId = "";
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

    if (distance > NEXTBOT_INJURY_DISTANCE || Date.now() < this.nextInjuryAt || target.isInjured || target.isHitReacting) {
      return;
    }

    this.nextInjuryAt = Date.now() + NEXTBOT_INJURY_COOLDOWN_MS;
    target.hitTriggerId += 1;
    target.hitSourceX = nextbot.x;
    target.hitSourceY = nextbot.y;
    target.hitSourceZ = nextbot.z;
  }

  private findNearestChaseablePlayer(nextbotX: number, nextbotZ: number): Player | undefined {
    let closestPlayer: Player | undefined;
    let closestDistanceSqr = Number.POSITIVE_INFINITY;
    const now = Date.now();

    this.state.players.forEach((player) => {
      const safeUntil = this.playerSafeUntil.get(player.sessionId) ?? 0;
      if (safeUntil > now) {
        return;
      }

      if (player.isInjured || player.isHitReacting) {
        return;
      }

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

  private getSafePlayerSpawnPosition() {
    for (let attempt = 0; attempt < 20; attempt++) {
      const x = Math.random() * PLAYER_SPAWN_RANGE - PLAYER_SPAWN_RANGE * 0.5;
      const z = Math.random() * PLAYER_SPAWN_RANGE - PLAYER_SPAWN_RANGE * 0.5;
      const dx = x - this.activeNextbotSpawnPoint.x;
      const dz = z - this.activeNextbotSpawnPoint.z;
      if (Math.hypot(dx, dz) >= PLAYER_MIN_SPAWN_DISTANCE_FROM_NEXTBOT) {
        return { x, z };
      }
    }

    return {
      x: -this.activeNextbotSpawnPoint.x,
      z: -this.activeNextbotSpawnPoint.z,
    };
  }

  private pickNextbotSpawnPoint() {
    const index = Math.floor(Math.random() * this.nextbotSpawnPoints.length);
    return this.nextbotSpawnPoints[index];
  }

  private resolveNextbotSpawnPoints(options: any) {
    const candidatePoints = options?.nextbotSpawnPoints;
    if (!Array.isArray(candidatePoints) || candidatePoints.length === 0) {
      return DEFAULT_NEXTBOT_SPAWN_POINTS;
    }

    const parsedPoints = candidatePoints
      .map((point) => {
        const x = Number(point?.x);
        const y = Number(point?.y);
        const z = Number(point?.z);
        if (!Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) {
          return undefined;
        }

        return { x, y, z };
      })
      .filter((point): point is { x: number; y: number; z: number } => point !== undefined);

    if (parsedPoints.length === 0) {
      return DEFAULT_NEXTBOT_SPAWN_POINTS;
    }

    return parsedPoints;
  }

}
