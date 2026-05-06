const fs = require("fs");
const path = require("path");

function loadDotEnv(envPath) {
  if (!fs.existsSync(envPath)) {
    return {};
  }

  const content = fs.readFileSync(envPath, "utf8");
  const result = {};

  for (const rawLine of content.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) {
      continue;
    }

    const separatorIndex = line.indexOf("=");
    if (separatorIndex <= 0) {
      continue;
    }

    const key = line.slice(0, separatorIndex).trim();
    let value = line.slice(separatorIndex + 1).trim();

    if ((value.startsWith("\"") && value.endsWith("\"")) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }

    result[key] = value;
  }

  return result;
}

const envFromFile = loadDotEnv(path.join(__dirname, ".env"));
const sharedEnv = {
  PORT: "2567",
  ...envFromFile,
};

module.exports = {
  apps: [{
    name: "colyseus-app",
    script: "build/index.js",
    time: true,
    watch: false,
    instances: 1,
    exec_mode: "fork",
    wait_ready: true,
    env: {
      NODE_ENV: "development",
      ...sharedEnv,
    },
    env_production: {
      NODE_ENV: "production",
      ...sharedEnv,
    }
  }],
};
