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
const NEXTBOT_STOPPING_DISTANCE = 0.7;
const NEXTBOT_INJURY_DISTANCE = 0.95;
const NEXTBOT_INJURY_COOLDOWN_MS = 1200;
const NEXTBOT_START_GRACE_MS = 3500;
const NEXTBOT_SCAN_INTERVAL_MS = 250;
const NEXTBOT_TARGET_LOCK_MS = 1400;
const NEXTBOT_SWITCH_SCORE_THRESHOLD = 20;
const NEXTBOT_SWITCH_CONFIRM_MS = 300;
const NEXTBOT_UNREACHABLE_TIMEOUT_MS = 1800;
const NEXTBOT_PREDICTION_TIME = 0.28;
const NEXTBOT_MAX_CHASE_RANGE = 70;
const NEXTBOT_MAX_VERTICAL_DELTA = 8;
const NEXTBOT_STALE_TARGET_TIMEOUT_MS = 1500;
const NEXTBOT_DISTANCE_SCORE_BASE = 120;
const NEXTBOT_VISIBLE_PROXY_RANGE = 18;
const NEXTBOT_VISIBLE_PROXY_BONUS = 15;
const NEXTBOT_FRONT_BONUS = 10;
const NEXTBOT_CURRENT_TARGET_BONUS = 30;
const NEXTBOT_RECENT_REACHABLE_BONUS = 15;
const NEXTBOT_RECENT_REACHABLE_MS = 1500;
const NEXTBOT_INTERCEPT_BONUS_MAX = 20;
const NEXTBOT_FRONT_ANGLE_THRESHOLD = 85;
const PLAYER_MIN_SPAWN_DISTANCE_FROM_NEXTBOT = 8;
const PLAYER_SPAWN_RANGE = 10;
const PLAYER_REVIVE_DISTANCE = 6;
const PLAYER_REVIVE_SYNC_GRACE_MS = 1000;
const DEFAULT_INTERMISSION_DURATION_MS = 30_000;
const DEFAULT_ROUND_DURATION_MS = 180_000;
const PLAYER_UPDATE_X = 0;
const PLAYER_UPDATE_Y = 1;
const PLAYER_UPDATE_Z = 2;
const PLAYER_UPDATE_ROTATION_Y = 3;
const PLAYER_UPDATE_VELOCITY_X = 4;
const PLAYER_UPDATE_VELOCITY_Y = 5;
const PLAYER_UPDATE_VELOCITY_Z = 6;
const PLAYER_UPDATE_ANIM_INPUT_X = 7;
const PLAYER_UPDATE_ANIM_INPUT_Y = 8;
const PLAYER_UPDATE_IS_GROUNDED = 9;
const PLAYER_UPDATE_IS_JUMPING = 10;
const PLAYER_UPDATE_IS_INJURED = 11;
const PLAYER_UPDATE_IS_CROUCHING = 12;
const PLAYER_UPDATE_IS_WALL_RUNNING = 13;
const PLAYER_UPDATE_WALL_RUN_SIDE = 14;
const PLAYER_UPDATE_MOVE_INPUT_X = 15;
const PLAYER_UPDATE_MOVE_INPUT_Y = 16;
const PLAYER_UPDATE_VISUAL_YAW = 17;
const PLAYER_UPDATE_CAMERA_ROTATION_X = 18;
const PLAYER_UPDATE_CAMERA_ROTATION_Y = 19;
const PLAYER_UPDATE_IS_HIT_REACTING = 20;
const PLAYER_UPDATE_HIT_REACTION_TIME_REMAINING = 21;
const PLAYER_UPDATE_HIT_REACTION_PITCH = 22;
const PLAYER_UPDATE_HIT_REACTION_ROLL = 23;
const PLAYER_UPDATE_HIT_REACTION_SEED = 24;

type SpawnPoint = { x: number; y: number; z: number };
type PredictedTargetPosition = { x: number; z: number; distance: number };
type RoundPhase = "waiting" | "intermission" | "round";
type ScoredTarget = {
  player: Player;
  score: number;
  predicted: PredictedTargetPosition;
  eligible: boolean;
};
type PlayerUpdateMessage = Record<string, unknown> | number[];
type PlayerRoundStats = {
  bestTimeMs: number;
  currentLifeStartMs: number | null;
  downedCount: number;
  revivesDone: number;
  joinOrder: number;
  displayName: string;
};
type RoundPhaseMessage = {
  phase: RoundPhase;
  roundIndex: number;
  timeRemainingMs: number;
  roundDurationMs: number;
  intermissionDurationMs: number;
};
type RoundAnnouncementMessage = {
  title: string;
  subtitle: string;
  durationSeconds: number;
};
type RoundResultEntry = {
  sessionId: string;
  displayName: string;
  bestTimeMs: number;
  downedCount: number;
  revivesDone: number;
  joinOrder: number;
  rank: number;
};
type RoundResultsMessage = {
  roundIndex: number;
  roundDurationMs: number;
  entries: RoundResultEntry[];
};

function readPlayerUpdateNumber(message: PlayerUpdateMessage, index: number, key: string): number {
  if (Array.isArray(message)) {
    const value = message[index];
    return typeof value === "number" ? value : 0;
  }

  const value = message[key];
  return typeof value === "number" ? value : 0;
}

function readPlayerUpdateBoolean(message: PlayerUpdateMessage, index: number, key: string): boolean {
  if (Array.isArray(message)) {
    return readPlayerUpdateNumber(message, index, key) !== 0;
  }

  const value = message[key];
  return value === true || value === 1;
}

export class MyRoom extends Room<MyRoomState> {
  maxClients = 4;
  state = new MyRoomState();
  private nextInjuryAt = 0;
  private playerSafeUntil = new Map<string, number>();
  private nextbotSpawnPoints = DEFAULT_NEXTBOT_SPAWN_POINTS;
  private activeNextbotSpawnPoint = DEFAULT_NEXTBOT_SPAWN_POINTS[0];
  private nextTargetScanAt = 0;
  private currentTargetSessionId = "";
  private targetLockedUntil = 0;
  private pendingSwitchSessionId = "";
  private pendingSwitchStartedAt = 0;
  private currentTargetLostSince = 0;
  private recentReachableUntil = new Map<string, number>();
  private lastKnownTargetPosition?: SpawnPoint;
  private playerRevivedUntil = new Map<string, number>();
  private currentPhase: RoundPhase = "waiting";
  private phaseEndsAt = 0;
  private roundIndex = 0;
  private roundStats = new Map<string, PlayerRoundStats>();
  private nextJoinOrder = 1;
  private hasStartedMatchFlow = false;
  private latestRoundResultsJson = "";
  private intermissionDurationMs = DEFAULT_INTERMISSION_DURATION_MS;
  private roundDurationMs = DEFAULT_ROUND_DURATION_MS;

  onCreate(options: any) {
    this.nextbotSpawnPoints = this.resolveNextbotSpawnPoints(options);
    this.intermissionDurationMs = this.resolvePositiveDurationMs(options?.intermissionDurationMs, DEFAULT_INTERMISSION_DURATION_MS);
    this.roundDurationMs = this.resolvePositiveDurationMs(options?.roundDurationMs, DEFAULT_ROUND_DURATION_MS);
    this.activeNextbotSpawnPoint = this.nextbotSpawnPoints[0];
    this.initializeNextbot();

    //Room ID
    this.roomId = Math.floor(1000 + Math.random() * 9000).toString();
    console.log("Room created!", options, "ID:", this.roomId);

    //Handle player movement
    this.onMessage("playerUpdate", (client, message) => {
      const player = this.state.players.get(client.sessionId);
      if (!player) return;
      const now = Date.now();
      const revivedUntil = this.playerRevivedUntil.get(client.sessionId) ?? 0;
      const ignoreStaleInjuredState = revivedUntil > now;
      const playerUpdate = message as PlayerUpdateMessage;
      const wasInjured = player.isInjured;

      // Camera is informational, so keep it in sync every tick.
      player.cameraRotationX = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_CAMERA_ROTATION_X, "cameraRotationX");
      player.cameraRotationY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_CAMERA_ROTATION_Y, "cameraRotationY");
      player.timestamp = now;

      // Position & Rotation
      player.x = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_X, "x");
      player.y = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_Y, "y");
      player.z = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_Z, "z");
      player.rotationY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_ROTATION_Y, "rotationY");

      // Velocity
      player.velocityX = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_VELOCITY_X, "velocityX");
      player.velocityY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_VELOCITY_Y, "velocityY");
      player.velocityZ = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_VELOCITY_Z, "velocityZ");

      // Animation States
      player.animInputX = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_ANIM_INPUT_X, "animInputX");
      player.animInputY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_ANIM_INPUT_Y, "animInputY");
      player.isGrounded = readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_GROUNDED, "isGrounded");
      player.isJumping = readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_JUMPING, "isJumping");
      player.isInjured = ignoreStaleInjuredState ? false : readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_INJURED, "isInjured");
      player.isCrouching = readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_CROUCHING, "isCrouching");
      player.isWallRunning = readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_WALL_RUNNING, "isWallRunning");
      player.wallRunSide = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_WALL_RUN_SIDE, "wallRunSide");
      player.moveInputX = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_MOVE_INPUT_X, "moveInputX");
      player.moveInputY = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_MOVE_INPUT_Y, "moveInputY");
      player.visualYaw = readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_VISUAL_YAW, "visualYaw");
      player.isHitReacting = ignoreStaleInjuredState ? false : readPlayerUpdateBoolean(playerUpdate, PLAYER_UPDATE_IS_HIT_REACTING, "isHitReacting");
      player.hitReactionTimeRemaining = ignoreStaleInjuredState ? 0 : readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_HIT_REACTION_TIME_REMAINING, "hitReactionTimeRemaining");
      player.hitReactionPitch = ignoreStaleInjuredState ? 0 : readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_HIT_REACTION_PITCH, "hitReactionPitch");
      player.hitReactionRoll = ignoreStaleInjuredState ? 0 : readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_HIT_REACTION_ROLL, "hitReactionRoll");
      player.hitReactionSeed = ignoreStaleInjuredState ? 0 : readPlayerUpdateNumber(playerUpdate, PLAYER_UPDATE_HIT_REACTION_SEED, "hitReactionSeed");

      if (player.isBeingCarried) {
        player.velocityX = 0;
        player.velocityY = 0;
        player.velocityZ = 0;
        player.animInputX = 0;
        player.animInputY = 0;
        player.moveInputX = 0;
        player.moveInputY = 0;
        player.isJumping = false;
        player.isCrouching = false;
        player.isWallRunning = false;
        player.wallRunSide = 0;
      }

      if (!wasInjured && player.isInjured) {
        this.recordPlayerDowned(client.sessionId, now);
      }
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

        this.tryStartRoundLoop();
      }
    });

    this.onMessage("revivePlayer", (client, message) => {
      const reviver = this.state.players.get(client.sessionId);
      const targetSessionId = typeof message?.targetSessionId === "string" ? message.targetSessionId : "";
      const target = this.state.players.get(targetSessionId);

      if (!reviver || !target || target.sessionId === client.sessionId) {
        return;
      }

      if (reviver.isInjured || reviver.isHitReacting || !target.isInjured) {
        return;
      }

      const distance = Math.hypot(target.x - reviver.x, target.z - reviver.z);
      if (distance > PLAYER_REVIVE_DISTANCE) {
        return;
      }

      this.clearCarryStateForPlayer(target.sessionId);
      target.isInjured = false;
      target.isHitReacting = false;
      target.hitReactionTimeRemaining = 0;
      target.hitReactionPitch = 0;
      target.hitReactionRoll = 0;
      target.hitReactionSeed = 0;
      this.playerRevivedUntil.set(targetSessionId, Date.now() + PLAYER_REVIVE_SYNC_GRACE_MS);
      this.recordPlayerRevived(targetSessionId, Date.now());
      const reviverStats = this.roundStats.get(client.sessionId);
      if (reviverStats != null && this.currentPhase === "round") {
        reviverStats.revivesDone += 1;
      }

      const targetClient = this.clients.find((roomClient) => roomClient.sessionId === targetSessionId);
      targetClient?.send("playerRevived", "revived");
    });

    this.onMessage("carryPlayer", (client, message) => {
      const carrier = this.state.players.get(client.sessionId);
      const targetSessionId = typeof message?.targetSessionId === "string" ? message.targetSessionId : "";
      const target = this.state.players.get(targetSessionId);

      if (!carrier || !target || carrier.sessionId === target.sessionId) {
        return;
      }

      if (carrier.isInjured || carrier.isHitReacting || target.isHitReacting || !target.isInjured) {
        return;
      }

      const distance = Math.hypot(target.x - carrier.x, target.z - carrier.z);
      if (distance > PLAYER_REVIVE_DISTANCE) {
        return;
      }

      if (carrier.isCarrying && carrier.carriedPlayerSessionId === targetSessionId) {
        this.clearCarryStateForPlayer(carrier.sessionId);
        return;
      }

      if (carrier.isCarrying || carrier.isBeingCarried || target.isCarrying || target.isBeingCarried) {
        return;
      }

      carrier.isCarrying = true;
      carrier.carriedPlayerSessionId = target.sessionId;
      target.isBeingCarried = true;
      target.carrierSessionId = carrier.sessionId;
      target.isHitReacting = false;
      target.hitReactionTimeRemaining = 0;
      target.hitReactionPitch = 0;
      target.hitReactionRoll = 0;
      target.hitReactionSeed = 0;
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

    // Late joiners are automatically ready after the room flow has started.
    if (this.currentPhase !== "waiting" || this.hasStartedMatchFlow) {
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
    player.isCarrying = false;
    player.isBeingCarried = false;
    player.carriedPlayerSessionId = "";
    player.carrierSessionId = "";

    //Add player to state
    this.state.players.set(client.sessionId, player);
    this.playerSafeUntil.set(client.sessionId, Date.now() + NEXTBOT_START_GRACE_MS);
    this.roundStats.set(client.sessionId, {
      bestTimeMs: 0,
      currentLifeStartMs: this.currentPhase === "round" ? Date.now() : null,
      downedCount: 0,
      revivesDone: 0,
      joinOrder: this.nextJoinOrder,
      displayName: `Player ${this.nextJoinOrder}`,
    });
    this.nextJoinOrder += 1;

    this.sendRoundPhaseToClient(client);
    if (this.latestRoundResultsJson.length > 0 && this.currentPhase === "intermission") {
      client.send("roundResults", this.latestRoundResultsJson);
    }

    this.tryStartRoundLoop();
  }

  onLeave(client: Client, consented: boolean) {
    console.log(client.sessionId, "left!");

    //Remove player from state
    this.clearCarryStateForPlayer(client.sessionId);
    this.state.players.delete(client.sessionId);
    this.playerSafeUntil.delete(client.sessionId);
    this.playerRevivedUntil.delete(client.sessionId);
    this.roundStats.delete(client.sessionId);

    // If no players left, reset game state
    if (this.state.players.size === 0) {
      this.state.isGameStarted = false;
      this.currentPhase = "waiting";
      this.phaseEndsAt = 0;
      this.roundIndex = 0;
      this.hasStartedMatchFlow = false;
      this.latestRoundResultsJson = "";
      this.clearNextbotTargetingState();
    }

    // Unlock the room for late commers 
    this.unlock();
  }

  onDispose() {
    console.log("room", this.roomId, "disposing...");
  }

  private clearCarryStateForPlayer(sessionId: string) {
    const player = this.state.players.get(sessionId);
    if (!player) {
      return;
    }

    if (player.isCarrying && player.carriedPlayerSessionId) {
      const carriedPlayer = this.state.players.get(player.carriedPlayerSessionId);
      if (carriedPlayer) {
        carriedPlayer.isBeingCarried = false;
        carriedPlayer.carrierSessionId = "";
      }
    }

    if (player.isBeingCarried && player.carrierSessionId) {
      const carrier = this.state.players.get(player.carrierSessionId);
      if (carrier) {
        carrier.isCarrying = false;
        carrier.carriedPlayerSessionId = "";
      }
    }

    player.isCarrying = false;
    player.isBeingCarried = false;
    player.carriedPlayerSessionId = "";
    player.carrierSessionId = "";
  }

  update(deltaTime: number) {
    const nextbot = this.state.nextbot;
    const now = Date.now();
    this.updateRoundFlow(now);
    if (!NEXTBOTS_ENABLED) {
      nextbot.isActive = false;
      this.clearNextbotTargetingState();
      return;
    }

    nextbot.isActive = this.state.isGameStarted && this.state.players.size > 0;

    if (!nextbot.isActive) {
      this.clearNextbotTargetingState();
      return;
    }

    if (now >= this.nextTargetScanAt || !this.canKeepCurrentTarget(now)) {
      this.nextTargetScanAt = now + NEXTBOT_SCAN_INTERVAL_MS;
      this.evaluateNextbotTargetSelection(now);
    }

    const deltaSeconds = deltaTime / 1000;
    const target = this.getCurrentTarget();
    if (!target) {
      if (this.lastKnownTargetPosition != null) {
        this.moveNextbotTowardsSearchPosition(deltaSeconds);
      } else {
        nextbot.targetSessionId = "";
      }

      return;
    }

    this.rememberTargetPosition(target);
    nextbot.targetSessionId = target.sessionId;
    const predictedTarget = this.getPredictedTargetPosition(target);
    this.moveNextbotTowardsPosition(predictedTarget, deltaSeconds);
    this.tryInjurePlayer(target);
  }

  private moveNextbotTowardsSearchPosition(deltaSeconds: number) {
    const nextbot = this.state.nextbot;
    if (this.lastKnownTargetPosition == null) {
      nextbot.targetSessionId = "";
      return;
    }

    this.moveNextbotTowardsPosition({
      x: this.lastKnownTargetPosition.x,
      z: this.lastKnownTargetPosition.z,
      distance: Math.hypot(
        this.lastKnownTargetPosition.x - this.state.nextbot.x,
        this.lastKnownTargetPosition.z - this.state.nextbot.z),
    }, deltaSeconds);
    nextbot.targetSessionId = "";
  }

  private initializeNextbot() {
    this.activeNextbotSpawnPoint = this.pickNextbotSpawnPoint();
    this.state.nextbot.x = this.activeNextbotSpawnPoint.x;
    this.state.nextbot.y = this.activeNextbotSpawnPoint.y;
    this.state.nextbot.z = this.activeNextbotSpawnPoint.z;
    this.state.nextbot.rotationY = 0;
    this.state.nextbot.targetSessionId = "";
    this.state.nextbot.isActive = false;
    this.clearNextbotTargetingState();
  }

  private resetNextbotToSpawnPoint() {
    this.activeNextbotSpawnPoint = this.pickNextbotSpawnPoint();
    this.state.nextbot.x = this.activeNextbotSpawnPoint.x;
    this.state.nextbot.y = this.activeNextbotSpawnPoint.y;
    this.state.nextbot.z = this.activeNextbotSpawnPoint.z;
    this.state.nextbot.rotationY = 0;
    this.state.nextbot.targetSessionId = "";
    this.clearNextbotTargetingState();
  }

  private moveNextbotTowardsPosition(target: PredictedTargetPosition, deltaSeconds: number) {
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
    this.recordPlayerDowned(target.sessionId, Date.now());
    target.hitTriggerId += 1;
    target.hitSourceX = nextbot.x;
    target.hitSourceY = nextbot.y;
    target.hitSourceZ = nextbot.z;
  }

  private evaluateNextbotTargetSelection(now: number) {
    const nextbot = this.state.nextbot;
    const currentTarget = this.getCurrentTarget();
    const currentCanContinue = this.canKeepCurrentTarget(now);
    const currentScoredTarget = currentTarget != null
      ? this.buildScoredTarget(currentTarget, now, true)
      : undefined;
    const bestCandidate = this.findBestScoredTarget(now, currentTarget?.sessionId ?? "");

    if (currentTarget == null) {
      if (bestCandidate != null) {
        this.assignCurrentTarget(bestCandidate.player.sessionId, now);
      } else {
        this.clearCurrentTargetSelection();
      }
      nextbot.targetSessionId = this.currentTargetSessionId;
      return;
    }

    if (currentCanContinue && now < this.targetLockedUntil) {
      nextbot.targetSessionId = currentTarget.sessionId;
      this.pendingSwitchSessionId = "";
      this.pendingSwitchStartedAt = 0;
      return;
    }

    if (!currentCanContinue) {
      if (bestCandidate != null) {
        this.assignCurrentTarget(bestCandidate.player.sessionId, now);
      } else {
        this.clearCurrentTargetSelection();
      }
      nextbot.targetSessionId = this.currentTargetSessionId;
      return;
    }

    if (bestCandidate == null || currentScoredTarget == null) {
      nextbot.targetSessionId = currentTarget.sessionId;
      return;
    }

    if (bestCandidate.player.sessionId === currentTarget.sessionId) {
      this.pendingSwitchSessionId = "";
      this.pendingSwitchStartedAt = 0;
      nextbot.targetSessionId = currentTarget.sessionId;
      return;
    }

    const currentScore = currentScoredTarget.score;
    const newScore = bestCandidate.score;
    if (newScore <= currentScore + NEXTBOT_SWITCH_SCORE_THRESHOLD) {
      this.pendingSwitchSessionId = "";
      this.pendingSwitchStartedAt = 0;
      nextbot.targetSessionId = currentTarget.sessionId;
      return;
    }

    if (this.pendingSwitchSessionId !== bestCandidate.player.sessionId) {
      this.pendingSwitchSessionId = bestCandidate.player.sessionId;
      this.pendingSwitchStartedAt = now;
      nextbot.targetSessionId = currentTarget.sessionId;
      return;
    }

    if (now - this.pendingSwitchStartedAt >= NEXTBOT_SWITCH_CONFIRM_MS) {
      this.assignCurrentTarget(bestCandidate.player.sessionId, now);
    }

    nextbot.targetSessionId = this.currentTargetSessionId;
  }

  private findBestScoredTarget(now: number, currentTargetSessionId: string): ScoredTarget | undefined {
    let bestTarget: ScoredTarget | undefined;

    this.state.players.forEach((player) => {
      const scoredTarget = this.buildScoredTarget(player, now, player.sessionId === currentTargetSessionId);
      if (scoredTarget == null || !scoredTarget.eligible) {
        return;
      }

      if (bestTarget == null || scoredTarget.score > bestTarget.score) {
        bestTarget = scoredTarget;
      }
    });

    return bestTarget;
  }

  private buildScoredTarget(player: Player, now: number, isCurrentTarget: boolean): ScoredTarget | undefined {
    if (!this.isScoreEligibleTarget(player, now)) {
      return undefined;
    }

    const predicted = this.getPredictedTargetPosition(player);
    const nextbot = this.state.nextbot;
    const distanceScore = Math.max(0, NEXTBOT_DISTANCE_SCORE_BASE - predicted.distance);
    const visibilityProxyBonus = predicted.distance <= NEXTBOT_VISIBLE_PROXY_RANGE ? NEXTBOT_VISIBLE_PROXY_BONUS : 0;
    const facingBonus = this.isTargetInFront(predicted) ? NEXTBOT_FRONT_BONUS : 0;
    const currentTargetBonus = isCurrentTarget ? NEXTBOT_CURRENT_TARGET_BONUS : 0;
    const reachableBonus = (this.recentReachableUntil.get(player.sessionId) ?? 0) > now ? NEXTBOT_RECENT_REACHABLE_BONUS : 0;
    const interceptBonus = this.getInterceptBonus(player, predicted);

    this.recentReachableUntil.set(player.sessionId, now + NEXTBOT_RECENT_REACHABLE_MS);

    return {
      player,
      predicted,
      eligible: true,
      score: distanceScore
        + visibilityProxyBonus
        + facingBonus
        + currentTargetBonus
        + reachableBonus
        + interceptBonus,
    };
  }

  private getPredictedTargetPosition(player: Player): PredictedTargetPosition {
    const nextbot = this.state.nextbot;
    const predictedX = player.x + player.velocityX * NEXTBOT_PREDICTION_TIME;
    const predictedZ = player.z + player.velocityZ * NEXTBOT_PREDICTION_TIME;
    const dx = predictedX - nextbot.x;
    const dz = predictedZ - nextbot.z;

    return {
      x: predictedX,
      z: predictedZ,
      distance: Math.hypot(dx, dz),
    };
  }

  private isScoreEligibleTarget(player: Player, now: number) {
    const safeUntil = this.playerSafeUntil.get(player.sessionId) ?? 0;
    if (safeUntil > now) {
      return false;
    }

    if (player.isInjured || player.isHitReacting) {
      return false;
    }

    if (Math.abs(player.y - this.state.nextbot.y) > NEXTBOT_MAX_VERTICAL_DELTA) {
      return false;
    }

    if (now - player.timestamp > NEXTBOT_STALE_TARGET_TIMEOUT_MS) {
      return false;
    }

    const predicted = this.getPredictedTargetPosition(player);
    if (predicted.distance > NEXTBOT_MAX_CHASE_RANGE) {
      return false;
    }

    return true;
  }

  private canKeepCurrentTarget(now: number) {
    const currentTarget = this.getCurrentTarget();
    if (!currentTarget) {
      this.currentTargetLostSince = 0;
      return false;
    }

    const safeUntil = this.playerSafeUntil.get(currentTarget.sessionId) ?? 0;
    if (safeUntil > now || currentTarget.isInjured || currentTarget.isHitReacting) {
      this.currentTargetLostSince = 0;
      return false;
    }

    const verticalOkay = Math.abs(currentTarget.y - this.state.nextbot.y) <= NEXTBOT_MAX_VERTICAL_DELTA * 1.5;
    const predicted = this.getPredictedTargetPosition(currentTarget);
    const withinExtendedRange = predicted.distance <= NEXTBOT_MAX_CHASE_RANGE * 1.2;
    const freshEnough = now - currentTarget.timestamp <= NEXTBOT_STALE_TARGET_TIMEOUT_MS + NEXTBOT_UNREACHABLE_TIMEOUT_MS;

    if (verticalOkay && withinExtendedRange && freshEnough && this.isScoreEligibleTarget(currentTarget, now)) {
      this.currentTargetLostSince = 0;
      return true;
    }

    if (verticalOkay && withinExtendedRange && freshEnough) {
      if (this.currentTargetLostSince <= 0) {
        this.currentTargetLostSince = now;
      }

      return now - this.currentTargetLostSince <= NEXTBOT_UNREACHABLE_TIMEOUT_MS;
    }

    this.currentTargetLostSince = 0;
    return false;
  }

  private isTargetInFront(predicted: PredictedTargetPosition) {
    const nextbot = this.state.nextbot;
    const dx = predicted.x - nextbot.x;
    const dz = predicted.z - nextbot.z;
    const targetYaw = Math.atan2(dx, dz) * (180 / Math.PI);
    const yawDelta = Math.abs(this.deltaAngle(nextbot.rotationY, targetYaw));
    return yawDelta <= NEXTBOT_FRONT_ANGLE_THRESHOLD;
  }

  private getInterceptBonus(player: Player, predicted: PredictedTargetPosition) {
    const speed = Math.hypot(player.velocityX, player.velocityZ);
    if (speed <= 0.1) {
      return 0;
    }

    const toPredictedX = predicted.x - this.state.nextbot.x;
    const toPredictedZ = predicted.z - this.state.nextbot.z;
    const toPredictedDistance = Math.hypot(toPredictedX, toPredictedZ);
    if (toPredictedDistance <= 0.0001) {
      return 0;
    }

    const playerDirectionX = player.velocityX / speed;
    const playerDirectionZ = player.velocityZ / speed;
    const towardBotDot = (playerDirectionX * (-toPredictedX / toPredictedDistance))
      + (playerDirectionZ * (-toPredictedZ / toPredictedDistance));

    if (towardBotDot <= 0) {
      return 0;
    }

    return towardBotDot * NEXTBOT_INTERCEPT_BONUS_MAX;
  }

  private deltaAngle(current: number, target: number) {
    let delta = (target - current) % 360;
    if (delta > 180) {
      delta -= 360;
    } else if (delta < -180) {
      delta += 360;
    }

    return delta;
  }

  private assignCurrentTarget(sessionId: string, now: number) {
    this.currentTargetSessionId = sessionId;
    this.targetLockedUntil = now + NEXTBOT_TARGET_LOCK_MS;
    this.pendingSwitchSessionId = "";
    this.pendingSwitchStartedAt = 0;
    this.currentTargetLostSince = 0;
    this.state.nextbot.targetSessionId = sessionId;
  }

  private getCurrentTarget() {
    if (!this.currentTargetSessionId) {
      return undefined;
    }

    return this.state.players.get(this.currentTargetSessionId);
  }

  private clearCurrentTargetSelection() {
    this.currentTargetSessionId = "";
    this.targetLockedUntil = 0;
    this.pendingSwitchSessionId = "";
    this.pendingSwitchStartedAt = 0;
    this.currentTargetLostSince = 0;
    this.state.nextbot.targetSessionId = "";
  }

  private clearNextbotTargetingState() {
    this.clearCurrentTargetSelection();
    this.lastKnownTargetPosition = undefined;
  }

  private rememberTargetPosition(target: Player) {
    this.lastKnownTargetPosition = {
      x: target.x,
      y: this.state.nextbot.y,
      z: target.z,
    };
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

  private pickNextbotSpawnPoint(): SpawnPoint {
    const index = Math.floor(Math.random() * this.nextbotSpawnPoints.length);
    return this.nextbotSpawnPoints[index];
  }

  private resolveNextbotSpawnPoints(options: any): SpawnPoint[] {
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

  private tryStartRoundLoop() {
    if (this.currentPhase !== "waiting" || this.state.players.size === 0) {
      return;
    }

    let allReady = true;
    this.state.players.forEach((player) => {
      if (!player.isReady) {
        allReady = false;
      }
    });

    if (!allReady) {
      return;
    }

    if (!this.hasStartedMatchFlow) {
      this.hasStartedMatchFlow = true;
      this.broadcast("startGame");
    }

    this.beginIntermission(Date.now());
  }

  private updateRoundFlow(now: number) {
    if (this.state.players.size === 0) {
      return;
    }

    if (this.currentPhase === "intermission" && now >= this.phaseEndsAt) {
      this.beginRound(now);
      return;
    }

    if (this.currentPhase === "round" && now >= this.phaseEndsAt) {
      this.endRound(now);
    }
  }

  private beginIntermission(now: number) {
    this.currentPhase = "intermission";
    this.phaseEndsAt = now + this.intermissionDurationMs;
    this.state.isGameStarted = false;
    this.resetNextbotToSpawnPoint();
    this.resetPlayersForIntermission(now);
    this.broadcastRoundPhase();
  }

  private beginRound(now: number) {
    this.currentPhase = "round";
    this.roundIndex += 1;
    this.phaseEndsAt = now + this.roundDurationMs;
    this.state.isGameStarted = true;
    this.latestRoundResultsJson = "";
    this.resetNextbotToSpawnPoint();
    this.resetPlayersForRoundStart(now);
    this.broadcastRoundPhase();
    this.broadcastRoundAnnouncement({
      title: "ROUND STARTED",
      subtitle: "SURVIVE FOR 3 MINUTES",
      durationSeconds: 3,
    });
  }

  private endRound(now: number) {
    const results = this.buildRoundResults(now);
    this.beginIntermission(now);
    this.latestRoundResultsJson = JSON.stringify(results);
    this.broadcast("roundResults", this.latestRoundResultsJson);
  }

  private resetPlayersForIntermission(now: number) {
    this.state.players.forEach((player) => {
      this.clearCarryStateForPlayer(player.sessionId);
      player.isInjured = false;
      player.isHitReacting = false;
      player.hitReactionTimeRemaining = 0;
      player.hitReactionPitch = 0;
      player.hitReactionRoll = 0;
      player.hitReactionSeed = 0;
      player.hitTriggerId = 0;
      player.hitSourceX = 0;
      player.hitSourceY = 0;
      player.hitSourceZ = 0;
      this.playerSafeUntil.set(player.sessionId, now + this.intermissionDurationMs + NEXTBOT_START_GRACE_MS);
      this.playerRevivedUntil.set(player.sessionId, now + PLAYER_REVIVE_SYNC_GRACE_MS);
    });

    this.broadcast("roundPlayerReset", "reset");
  }

  private resetPlayersForRoundStart(now: number) {
    const safeUntil = now + NEXTBOT_START_GRACE_MS;
    this.state.players.forEach((player) => {
      const stats = this.roundStats.get(player.sessionId);
      if (stats != null) {
        stats.bestTimeMs = 0;
        stats.currentLifeStartMs = now;
        stats.downedCount = 0;
        stats.revivesDone = 0;
      }

      this.clearCarryStateForPlayer(player.sessionId);
      player.isInjured = false;
      player.isHitReacting = false;
      player.hitReactionTimeRemaining = 0;
      player.hitReactionPitch = 0;
      player.hitReactionRoll = 0;
      player.hitReactionSeed = 0;
      player.hitTriggerId = 0;
      player.hitSourceX = 0;
      player.hitSourceY = 0;
      player.hitSourceZ = 0;
      player.timestamp = now;
      this.playerSafeUntil.set(player.sessionId, safeUntil);
      this.playerRevivedUntil.set(player.sessionId, now + PLAYER_REVIVE_SYNC_GRACE_MS);
    });

    this.broadcast("roundPlayerReset", "reset");
  }

  private recordPlayerDowned(sessionId: string, now: number) {
    if (this.currentPhase !== "round") {
      return;
    }

    const stats = this.roundStats.get(sessionId);
    if (stats == null || stats.currentLifeStartMs == null) {
      return;
    }

    const runTime = Math.max(0, now - stats.currentLifeStartMs);
    stats.bestTimeMs = Math.max(stats.bestTimeMs, runTime);
    stats.downedCount += 1;
    stats.currentLifeStartMs = null;
  }

  private recordPlayerRevived(sessionId: string, now: number) {
    if (this.currentPhase !== "round") {
      return;
    }

    const stats = this.roundStats.get(sessionId);
    if (stats == null) {
      return;
    }

    stats.currentLifeStartMs = now;
  }

  private buildRoundResults(now: number): RoundResultsMessage {
    const entries: RoundResultEntry[] = [];

    this.state.players.forEach((player) => {
      const stats = this.roundStats.get(player.sessionId);
      if (stats == null) {
        return;
      }

      if (stats.currentLifeStartMs != null) {
        const runTime = Math.max(0, now - stats.currentLifeStartMs);
        stats.bestTimeMs = Math.max(stats.bestTimeMs, runTime);
      }

      entries.push({
        sessionId: player.sessionId,
        displayName: stats.displayName,
        bestTimeMs: stats.bestTimeMs,
        downedCount: stats.downedCount,
        revivesDone: stats.revivesDone,
        joinOrder: stats.joinOrder,
        rank: 0,
      });
    });

    entries.sort((a, b) => {
      if (a.bestTimeMs !== b.bestTimeMs) {
        return b.bestTimeMs - a.bestTimeMs;
      }

      if (a.downedCount !== b.downedCount) {
        return a.downedCount - b.downedCount;
      }

      if (a.revivesDone !== b.revivesDone) {
        return b.revivesDone - a.revivesDone;
      }

      return a.joinOrder - b.joinOrder;
    });

    for (let i = 0; i < entries.length; i++) {
      const previous = i > 0 ? entries[i - 1] : undefined;
      const current = entries[i];
      if (previous != null
        && previous.bestTimeMs === current.bestTimeMs
        && previous.downedCount === current.downedCount
        && previous.revivesDone === current.revivesDone) {
        current.rank = previous.rank;
      } else {
        current.rank = i + 1;
      }
    }

    return {
      roundIndex: this.roundIndex,
      roundDurationMs: this.roundDurationMs,
      entries,
    };
  }

  private createRoundPhaseMessage(now: number): RoundPhaseMessage {
    return {
      phase: this.currentPhase,
      roundIndex: this.roundIndex,
      timeRemainingMs: this.phaseEndsAt > 0 ? Math.max(0, this.phaseEndsAt - now) : 0,
      roundDurationMs: this.roundDurationMs,
      intermissionDurationMs: this.intermissionDurationMs,
    };
  }

  private broadcastRoundPhase() {
    this.broadcast("roundPhase", JSON.stringify(this.createRoundPhaseMessage(Date.now())));
  }

  private sendRoundPhaseToClient(client: Client) {
    client.send("roundPhase", JSON.stringify(this.createRoundPhaseMessage(Date.now())));
  }

  private broadcastRoundAnnouncement(message: RoundAnnouncementMessage) {
    this.broadcast("roundAnnouncement", JSON.stringify(message));
  }

  private resolvePositiveDurationMs(candidate: unknown, fallback: number) {
    const value = Number(candidate);
    if (!Number.isFinite(value)) {
      return fallback;
    }

    return Math.max(1000, Math.round(value));
  }

}
