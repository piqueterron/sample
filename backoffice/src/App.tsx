import { useAuth } from 'react-oidc-context'
import { useEffect, useState } from 'react'
import { callApi } from './api'
import './styles.css'

export default function App() {
  const auth = useAuth()
  const [apiResult, setApiResult] = useState<string>('')
  const [apiError, setApiError] = useState<string>('')

  // react-oidc-context v3 does not auto-start the redirect. Kicking it off when
  // the user is unauthenticated and no callback is in progress gives the same
  // behavior as the old `autoSignin` prop.
  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.error && !auth.activeNavigator) {
      void auth.signinRedirect()
    }
  }, [auth])

  // react-oidc-context loads the user asynchronously after the redirect callback.
  if (auth.isLoading) {
    return <Loading />
  }

  // If the redirect from Keycloak errored, show the error so we can diagnose it.
  if (auth.error) {
    return (
      <ErrorView
        title="Authentication error"
        message={auth.error.message}
        onRetry={() => auth.signinRedirect()}
      />
    )
  }

  // While the redirect is being triggered we show the sign-in screen briefly.
  if (!auth.user || !auth.isAuthenticated) {
    return <SignIn onSignIn={() => auth.signinRedirect()} />
  }

  const user = auth.user

  return (
    <div className="container">
      <header className="header">
        <div>
          <h1 className="brand">Backoffice</h1>
          <p className="muted">React 19 + Keycloak (Authorization Code + PKCE)</p>
        </div>
        <div className="user-pill">
          <span className="user-name">{user.profile.preferred_username ?? user.profile.sub}</span>
          <button className="btn btn-ghost" onClick={() => auth.signoutRedirect()}>
            Sign out
          </button>
        </div>
      </header>

      <main>
        <section className="card">
          <h2>Token (decoded)</h2>
          <p className="muted">Access token issued by Keycloak for the <code>backoffice-web</code> client.</p>
          <pre className="token-box">{safeDecode(user.access_token) ?? user.access_token}</pre>
          <pre className="meta-box">{formatClaims(user.profile)}</pre>
        </section>

        <section className="card">
          <h2>Protected API call</h2>
          <p className="muted">
            Calls <code>GET /api/users</code> (proxied to the backend Sample API) with the access
            token as a Bearer header. The <code>admin</code> policy is enforced server-side.
          </p>
          <button
            className="btn btn-primary"
            onClick={() =>
              callApi({
                accessToken: user.access_token,
                onResult: setApiResult,
                onError: setApiError,
              })
            }
          >
            Call GET /users
          </button>
          {apiResult && (
            <pre className="result-box result-ok">
              200 OK{apiResult ? `\n${apiResult}` : ''}
            </pre>
          )}
          {apiError && <pre className="result-box result-err">{apiError}</pre>}
        </section>
      </main>

      <footer className="muted footer">
        .env values · authority: {import.meta.env.VITE_OIDC_AUTHORITY} · client: {import.meta.env.VITE_OIDC_CLIENT_ID}
      </footer>
    </div>
  )
}

function Loading() {
  return (
    <div className="container">
      <div className="spinner" />
      <p className="muted">Loading session…</p>
    </div>
  )
}

function SignIn({ onSignIn }: { onSignIn: () => void }) {
  return (
    <div className="container">
      <div className="card center">
        <h1 className="brand">Backoffice</h1>
        <p className="muted">Sign in with your Keycloak account to continue.</p>
        <button className="btn btn-primary btn-lg" onClick={onSignIn}>
          Sign in with Keycloak
        </button>
        <p className="muted small">
          Uses Authorization Code flow with PKCE (S256). Public client, no client secret.
        </p>
      </div>
    </div>
  )
}

function ErrorView({ title, message, onRetry }: { title: string; message: string; onRetry: () => void }) {
  return (
    <div className="container">
      <div className="card">
        <h2>{title}</h2>
        <pre className="result-box result-err">{message}</pre>
        <button className="btn btn-primary" onClick={onRetry}>
          Retry sign-in
        </button>
      </div>
    </div>
  )
}

function safeDecode(token: string | undefined): string | undefined {
  if (!token) return undefined
  try {
    const [, payload] = token.split('.')
    if (!payload) return undefined
    // base64url -> base64
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
    // Handle UTF-8 characters inside the JSON payload.
    const decoded = decodeURIComponent(escape(json))
    return JSON.stringify(JSON.parse(decoded), null, 2)
  } catch {
    return undefined
  }
}

function formatClaims(profile: Record<string, unknown>): string {
  return JSON.stringify(profile, null, 2)
}
