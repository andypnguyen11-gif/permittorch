#!/usr/bin/env node
// Idempotent Firebase test-user provisioning for the E2E suite and the dev seeder.
//
//   pnpm e2e:create-users            (from the repo root; reads ../.env automatically)
//
// Creates (or finds, and re-syncs the password of) the three accounts the seeder and the
// specs expect, then prints their UIDs for .env: SUPERADMIN_FIREBASE_UID,
// E2E_ENTITLED_FIREBASE_UID, E2E_UNENTITLED_FIREBASE_UID. Re-run the API seeder afterwards with
// SEED_E2E_IDENTITIES=true in .env
// (`dotnet run --project apps/api/PermitTorch.Api.csproj --no-launch-profile -- seed`).
import { existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { cert, initializeApp } from "firebase-admin/app";
import { getAuth } from "firebase-admin/auth";

const rootEnv = fileURLToPath(new URL("../../.env", import.meta.url));
if (existsSync(rootEnv)) process.loadEnvFile(rootEnv); // shell values win

const projectId = process.env.FIREBASE_PROJECT_ID;
const clientEmail = process.env.FIREBASE_CLIENT_EMAIL;
const privateKey = (process.env.FIREBASE_PRIVATE_KEY ?? "").replace(/\\n/g, "\n");
const password = process.env.E2E_USER_PASSWORD;

const missing = Object.entries({
  FIREBASE_PROJECT_ID: projectId, FIREBASE_CLIENT_EMAIL: clientEmail,
  FIREBASE_PRIVATE_KEY: privateKey, E2E_USER_PASSWORD: password,
}).filter(([, v]) => !v).map(([k]) => k);
if (missing.length) {
  console.error(`Missing ${missing.join(" / ")} (set them in .env).`);
  process.exit(1);
}
if (password.length < 8) {
  console.error("E2E_USER_PASSWORD must be at least 8 characters (the app's sign-up rule).");
  process.exit(1);
}

const users = [
  { label: "SUPERADMIN", email: process.env.SUPERADMIN_EMAIL || "e2e-admin@permittorch.dev" },
  { label: "E2E_ENTITLED", email: process.env.E2E_ENTITLED_EMAIL || "e2e-entitled@permittorch.dev" },
  { label: "E2E_UNENTITLED", email: process.env.E2E_UNENTITLED_EMAIL || "e2e-unentitled@permittorch.dev" },
];

const isAuthNotInitialized = (err) =>
  err?.code === "auth/configuration-not-found" || /CONFIGURATION_NOT_FOUND/.test(String(err?.message ?? err));

initializeApp({ credential: cert({ projectId, clientEmail, privateKey }) });
const auth = getAuth();

try {
  const lines = [];
  for (const { label, email } of users) {
    let user;
    try {
      user = await auth.getUserByEmail(email);
      // Keep existing accounts usable with the current E2E_USER_PASSWORD.
      user = await auth.updateUser(user.uid, { password, emailVerified: true, disabled: false });
      console.error(`found   ${email}`);
    } catch (err) {
      if (err?.code !== "auth/user-not-found") throw err;
      user = await auth.createUser({ email, password, emailVerified: true });
      console.error(`created ${email}`);
    }
    lines.push(`${label}_FIREBASE_UID=${user.uid}`);
  }
  console.log(lines.join("\n"));
} catch (err) {
  if (isAuthNotInitialized(err)) {
    console.error(
      `Firebase Authentication is not initialized for project "${projectId}" (CONFIGURATION_NOT_FOUND).\n` +
      "Enable Email/Password in the Firebase console first: " +
      `https://console.firebase.google.com/project/${projectId}/authentication/providers ` +
      '("Get started" → Sign-in method → Email/Password → Enable), then re-run this script.',
    );
  } else {
    console.error(`Failed to provision E2E users: ${err?.code ?? ""} ${err?.message ?? err}`);
  }
  process.exit(1);
}
