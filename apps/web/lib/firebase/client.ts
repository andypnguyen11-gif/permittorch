// FINAL FORM (WS0). Do not edit in any workstream.
// Browser-side Firebase app used by /login, /signup, the account menu and useApiToken().
"use client";

import { getApp, getApps, initializeApp } from "firebase/app";
import { getAuth } from "firebase/auth";

const firebaseConfig = {
  // A placeholder keeps module evaluation from throwing (getAuth throws
  // auth/invalid-api-key on an empty string) during CI/builds without
  // Firebase env configured. Real network calls still fail loudly with a
  // clear Firebase error until the real env is set.
  apiKey: process.env.NEXT_PUBLIC_FIREBASE_API_KEY || "missing-firebase-api-key",
  authDomain: process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN,
  projectId: process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID,
  appId: process.env.NEXT_PUBLIC_FIREBASE_APP_ID,
};

export const firebaseApp = getApps().length ? getApp() : initializeApp(firebaseConfig);
export const firebaseAuth = getAuth(firebaseApp);
