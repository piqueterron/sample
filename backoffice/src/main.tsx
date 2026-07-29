import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { AuthProvider } from 'react-oidc-context'
import { userManager } from './oidc'
import App from './App.tsx'
import './styles.css'

const rootElement = document.getElementById('root')
if (!rootElement) {
  throw new Error('Root element #root not found in index.html')
}

createRoot(rootElement).render(
  <StrictMode>
    <AuthProvider
      userManager={userManager}
      // Clean the code/state query params from the URL after a successful redirect
      // back from Keycloak, so a refresh doesn't try to replay the callback.
      onSigninCallback={() => {
        window.history.replaceState({}, document.title, window.location.pathname)
      }}
    >
      <App />
    </AuthProvider>
  </StrictMode>,
)
