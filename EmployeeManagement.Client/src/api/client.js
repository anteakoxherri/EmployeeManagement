export async function apiRequest(path, { token, onUnauthorized, ...options } = {}) {
  let response
  try {
    response = await fetch(`/api${path}`, {
      ...options,
      headers: {
        ...(options.body && !(options.body instanceof FormData) ? { 'Content-Type': 'application/json' } : {}),
        ...options.headers,
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    })
  } catch (error) {
    if (error.name === 'AbortError') throw error
    throw new Error('Cannot connect to the API. Check that the backend is running.', { cause: error })
  }
  // Token-i i refuzuar mbyll sesionin; 403 tregon mungese lejeje.
  if (response.status === 401) {
    onUnauthorized?.()
    throw new Error(token ? 'Your session is invalid or expired. Please sign in again.' : 'Incorrect email or password.')
  }
  if (response.status === 403) throw new Error('You do not have permission to access this resource.')
  if (!response.ok) {
    // API kthen errors si liste ose si objekt nga Data Annotations.
    let details
    try { details = await response.json() } catch { /* Pergjigjja mund te mos jete JSON. */ }
    const errors = details?.errors
    const messages = Array.isArray(errors) ? errors : errors && typeof errors === 'object' ? Object.values(errors).flat() : []
    const fallback = {
      400: path === '/auth/login' ? 'Check your email and password.' : 'Check the submitted information and try again.',
      404: 'The requested information could not be found.',
      409: 'Your information changed elsewhere. Reload the page and try again.',
      413: 'The image must be no larger than 2 MB.',
    }
    const message = messages.filter((item) => typeof item === 'string').join(' ') ||
      (typeof details?.message === 'string' ? details.message : '') ||
      fallback[response.status] || 'The API could not complete the request. Please try again.'
    throw new Error(message)
  }
  if (response.status === 204) return null
  return response.json()
}
