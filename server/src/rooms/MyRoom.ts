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

type SpawnPoint = { x: number; y: number; z: number };
type PredictedTargetPosition = { x: number; z: number; distance: number };
type ScoredTarget = {
  player: Player;
  score: number;
  predicted: PredictedTargetPosition;
  eligible: boolean;
};

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
    const now = Date.now();
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

    const target = this.getCurrentTarget();
    if (!target) {
      nextbot.targetSessionId = "";
      return;
    }

    nextbot.targetSessionId = target.sessionId;
    const predictedTarget = this.getPredictedTargetPosition(target);
    const deltaSeconds = deltaTime / 1000;
    this.moveNextbotTowardsPosition(predictedTarget, deltaSeconds);
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
        this.clearNextbotTargetingState();
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
        this.clearNextbotTargetingState();
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

  private clearNextbotTargetingState() {
    this.currentTargetSessionId = "";
    this.targetLockedUntil = 0;
    this.pendingSwitchSessionId = "";
    this.pendingSwitchStartedAt = 0;
    this.currentTargetLostSince = 0;
    this.state.nextbot.targetSessionId = "";
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

}
