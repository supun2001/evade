import { randomBytes, createHash } from "crypto";
import { MongoClient, Collection } from "mongodb";

const DEFAULT_MONGODB_URI = "mongodb+srv://supunhasankauk23034_db_user:nyx3FW0uOdazgzLh@cluster0.xbyqos9.mongodb.net/";
const DATABASE_NAME = process.env.MONGODB_DB_NAME || "evade";
const USERS_COLLECTION_NAME = "users";

const DEFAULT_MONEY = 100;
const DEFAULT_OWNED_SKINS = [0];
const DEFAULT_EQUIPPED_SKIN = 0;

const SKIN_PRICES = [0, 75];

export interface AccountDocument {
  username: string;
  passwordHash: string;
  authToken: string;
  money: number;
  ownedSkinIndices: number[];
  equippedSkinIndex: number;
  createdAt: Date;
  updatedAt: Date;
}

export interface PublicAccount {
  username: string;
  money: number;
  ownedSkinIndices: number[];
  equippedSkinIndex: number;
}

let mongoClientPromise: Promise<MongoClient> | null = null;

function getMongoClient(): Promise<MongoClient> {
  if (!mongoClientPromise) {
    const uri = process.env.MONGODB_URI || DEFAULT_MONGODB_URI;
    const client = new MongoClient(uri);
    mongoClientPromise = client.connect();
  }

  return mongoClientPromise;
}

async function getUsersCollection(): Promise<Collection<AccountDocument>> {
  const client = await getMongoClient();
  return client.db(DATABASE_NAME).collection<AccountDocument>(USERS_COLLECTION_NAME);
}

function normalizeUsername(username: string): string {
  return (username || "").trim().toLowerCase();
}

function hashPassword(password: string): string {
  return createHash("sha256").update(password).digest("hex");
}

function createToken(): string {
  return randomBytes(24).toString("hex");
}

function sanitizeOwnedSkinIndices(indices: number[] | undefined): number[] {
  const normalized = new Set<number>(DEFAULT_OWNED_SKINS);

  if (Array.isArray(indices)) {
    for (const index of indices) {
      if (Number.isInteger(index) && index >= 0 && index < SKIN_PRICES.length) {
        normalized.add(index);
      }
    }
  }

  return [...normalized].sort((a, b) => a - b);
}

function sanitizeEquippedSkinIndex(index: number, ownedSkinIndices: number[]): number {
  if (Number.isInteger(index) && ownedSkinIndices.includes(index)) {
    return index;
  }

  return DEFAULT_EQUIPPED_SKIN;
}

function toPublicAccount(account: AccountDocument): PublicAccount {
  const ownedSkinIndices = sanitizeOwnedSkinIndices(account.ownedSkinIndices);

  return {
    username: account.username,
    money: Math.max(0, Number.isFinite(account.money) ? account.money : DEFAULT_MONEY),
    ownedSkinIndices,
    equippedSkinIndex: sanitizeEquippedSkinIndex(account.equippedSkinIndex, ownedSkinIndices),
  };
}

export async function ensureAccountIndexes() {
  const users = await getUsersCollection();
  await users.createIndex({ username: 1 }, { unique: true });
  await users.createIndex({ authToken: 1 }, { unique: true, sparse: true });
}

export async function registerAccount(username: string, password: string): Promise<{ account: PublicAccount; token: string; }> {
  const normalizedUsername = normalizeUsername(username);
  const trimmedPassword = (password || "").trim();
  if (normalizedUsername.length < 3) {
    throw new Error("Username must be at least 3 characters.");
  }

  if (trimmedPassword.length < 4) {
    throw new Error("Password must be at least 4 characters.");
  }

  const users = await getUsersCollection();
  const existingAccount = await users.findOne({ username: normalizedUsername });
  if (existingAccount) {
    throw new Error("That username is already taken.");
  }

  const now = new Date();
  const authToken = createToken();
  const document: AccountDocument = {
    username: normalizedUsername,
    passwordHash: hashPassword(trimmedPassword),
    authToken,
    money: DEFAULT_MONEY,
    ownedSkinIndices: [...DEFAULT_OWNED_SKINS],
    equippedSkinIndex: DEFAULT_EQUIPPED_SKIN,
    createdAt: now,
    updatedAt: now,
  };

  await users.insertOne(document);
  return {
    account: toPublicAccount(document),
    token: authToken,
  };
}

export async function loginAccount(username: string, password: string): Promise<{ account: PublicAccount; token: string; }> {
  const normalizedUsername = normalizeUsername(username);
  const users = await getUsersCollection();
  const account = await users.findOne({ username: normalizedUsername });

  if (!account || account.passwordHash !== hashPassword((password || "").trim())) {
    throw new Error("Invalid username or password.");
  }

  const authToken = createToken();
  const updatedAt = new Date();

  await users.updateOne(
    { username: normalizedUsername },
    {
      $set: {
        authToken,
        updatedAt,
      },
    }
  );

  return {
    account: toPublicAccount({
      ...account,
      authToken,
      updatedAt,
    }),
    token: authToken,
  };
}

export async function getAccountByToken(token: string): Promise<PublicAccount | null> {
  const trimmedToken = (token || "").trim();
  if (!trimmedToken) {
    return null;
  }

  const users = await getUsersCollection();
  const account = await users.findOne({ authToken: trimmedToken });
  return account ? toPublicAccount(account) : null;
}

export async function purchaseSkin(token: string, skinIndex: number): Promise<PublicAccount> {
  const trimmedToken = (token || "").trim();
  if (!trimmedToken) {
    throw new Error("Missing auth token.");
  }

  if (!Number.isInteger(skinIndex) || skinIndex < 0 || skinIndex >= SKIN_PRICES.length) {
    throw new Error("Unknown skin.");
  }

  const users = await getUsersCollection();
  const account = await users.findOne({ authToken: trimmedToken });
  if (!account) {
    throw new Error("Account not found.");
  }

  const publicAccount = toPublicAccount(account);
  if (publicAccount.ownedSkinIndices.includes(skinIndex)) {
    return publicAccount;
  }

  const price = SKIN_PRICES[skinIndex];
  if (publicAccount.money < price) {
    throw new Error("Not enough money.");
  }

  const nextOwnedSkinIndices = sanitizeOwnedSkinIndices([...publicAccount.ownedSkinIndices, skinIndex]);
  const nextMoney = Math.max(0, publicAccount.money - price);
  const updatedAt = new Date();

  await users.updateOne(
    { authToken: trimmedToken },
    {
      $set: {
        money: nextMoney,
        ownedSkinIndices: nextOwnedSkinIndices,
        equippedSkinIndex: skinIndex,
        updatedAt,
      },
    }
  );

  return {
    username: publicAccount.username,
    money: nextMoney,
    ownedSkinIndices: nextOwnedSkinIndices,
    equippedSkinIndex: skinIndex,
  };
}

export async function equipSkin(token: string, skinIndex: number): Promise<PublicAccount> {
  const trimmedToken = (token || "").trim();
  if (!trimmedToken) {
    throw new Error("Missing auth token.");
  }

  if (!Number.isInteger(skinIndex) || skinIndex < 0 || skinIndex >= SKIN_PRICES.length) {
    throw new Error("Unknown skin.");
  }

  const users = await getUsersCollection();
  const account = await users.findOne({ authToken: trimmedToken });
  if (!account) {
    throw new Error("Account not found.");
  }

  const publicAccount = toPublicAccount(account);
  if (!publicAccount.ownedSkinIndices.includes(skinIndex)) {
    throw new Error("Skin not owned.");
  }

  const updatedAt = new Date();
  await users.updateOne(
    { authToken: trimmedToken },
    {
      $set: {
        equippedSkinIndex: skinIndex,
        updatedAt,
      },
    }
  );

  return {
    ...publicAccount,
    equippedSkinIndex: skinIndex,
  };
}
