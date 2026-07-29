interface CallApiArgs {
  accessToken: string
  onResult: (text: string) => void
  onError: (text: string) => void
}

// Calls the protected /users endpoint via the Vite dev proxy (/api -> http://localhost:5157).
// In Docker (preview/production build), the request must instead go to the API container
// directly - see the Dockerfile and the docker-compose override for the runtime API URL.
export async function callApi({ accessToken, onResult, onError }: CallApiArgs): Promise<void> {
  const baseUrl = import.meta.env.DEV ? '/api' : (import.meta.env.VITE_API_BASE_URL || '/api')

  try {
    const response = await fetch(`${baseUrl}/users`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${accessToken}`,
        Accept: 'application/json',
      },
      // Same-origin via the Vite proxy in dev; in container we use $baseUrl so it
      // works without CORS preflight if configured.
    })

    const text = await response.text()

    if (response.ok) {
      const trimmed = text ? `\n${text}` : ''
      onResult(`${response.status} ${response.statusText}${trimmed}`)
    } else {
      onError(`${response.status} ${response.statusText}${text ? `\n${text}` : ''}`)
    }
  } catch (err) {
    onError(err instanceof Error ? err.message : String(err))
  }
}
