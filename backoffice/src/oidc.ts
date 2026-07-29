import { UserManager, WebStorageStateStore } from 'oidc-client-ts'

// OIDC configuration for the backoffice SPA.
//
// This is a PUBLIC client using Authorization Code Flow with PKCE (S256). The
// client id (`backoffice-web`) is configured in `.docker/keycloak/realm-export.json`
// with `pkce.code.challenge.method = S256` and `directAccessGrantsEnabled = false`,
// so no client secret is ever involved - the only credential exchanged at the token
// endpoint is the PKCE code_verifier, which never leaves the browser.
//
// Configuration values come from Vite env vars (see .env), so the same build can
// run on the host (http://localhost:8080 Keycloak) or in Docker (override VITE_OIDC_AUTHORITY).
const clientId = import.meta.env.VITE_OIDC_CLIENT_ID || 'backoffice-web'
const authority = import.meta.env.VITE_OIDC_AUTHORITY || 'http://localhost:8080/realms/company'
const redirectUri =
  import.meta.env.VITE_OIDC_REDIRECT_URI || `${window.location.origin}/`

const userManagerConfig = {
  authority,
  client_id: clientId,
  redirect_uri: redirectUri,
  post_logout_redirect_uri: redirectUri,
  response_type: 'code', // Authorization Code Flow
  scope: 'openid profile email', // offline_access can be added here to get refresh tokens
  loadUserInfo: true,
  // PKCE is enabled by default by oidc-client-ts, which generates `code_verifier`
  // and `code_challenge` with S256. Nothing more is needed.
  automaticSilentRenew: true,
  // Store OIDC state in localStorage so it survives a refresh, which matters for
  // the redirect back from Keycloak and for silent renew.
  userStore: new WebStorageStateStore({
    store: window.localStorage,
    prefix: 'backoffice.oidc.',
  }),
  // Do NOT set `metadata` here - providing only `{ issuer }` makes oidc-client-ts
  // treat that object as the COMPLETE metadata response and skip the discovery
  // document fetch, which breaks the authorize redirect. Leaving `metadata`
  // unset lets the library fetch the full discovery doc from `authority`
  // (http://localhost:8080/realms/company/.well-known/openid-configuration),
  // which Keycloak serves correctly.
  extraQueryParams: {},
}

export const userManager = new UserManager(userManagerConfig)

// Helpful for debugging from the browser console.
if (import.meta.env.DEV) {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  ;(window as any).userManager = userManager
}

export const oidcSettings = userManagerConfig
